using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Raven.Core.Configuration;

namespace Raven.Core.Auth;

/// <summary>
/// GitLab OAuth 2.0 Authorization Code flow for RAVEN.
/// Uses clientId/clientSecret from ~/.gitconfig to obtain a Bearer token.
/// No PAT required. Tokens are cached in ~/.raven/token.json and auto-refreshed.
///
/// Flow:
///   1. Start loopback HttpListener on 127.0.0.1:{random port}
///   2. Open browser to GitLab /oauth/authorize
///   3. User authorizes, GitLab redirects to loopback with ?code=...
///   4. Exchange code for access_token + refresh_token via POST /oauth/token
///   5. Cache tokens in ~/.raven/token.json
///   6. All GitLab API calls use Authorization: Bearer {access_token}
///   7. Auto-refresh when token expires
/// </summary>
public sealed class GitLabOAuthService : IDisposable
{
    private static readonly HttpClient Http = new();
    private static readonly string TokenCachePath = Path.Combine(
        ConfigManager.HomePath, "token.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _gitlabUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private TokenCache? _cache;

    public string? AccessToken => _cache?.AccessToken;
    public bool HasToken => !string.IsNullOrEmpty(_cache?.AccessToken);
    public bool IsExpired => _cache?.ExpiresAt is not null && DateTime.UtcNow >= _cache.ExpiresAt;
    public bool CanRefresh => !string.IsNullOrEmpty(_cache?.RefreshToken);

    public GitLabOAuthService(GitLabConfig config)
    {
        _gitlabUrl = config.Url.TrimEnd('/');
        _clientId = config.ClientId;
        _clientSecret = config.ClientSecret;
    }

    /// <summary>
    /// Ensures a valid access token is available.
    /// Loads from cache, refreshes if expired, or runs full OAuth flow if needed.
    /// </summary>
    public async Task<(bool Success, string? Error)> EnsureTokenAsync(CancellationToken ct = default)
    {
        // 1. Try loading cached token
        await LoadCacheAsync(ct);

        if (HasToken && !IsExpired)
        {
            // Validate the token is still good
            if (await ValidateTokenAsync(ct))
                return (true, null);
        }

        // 2. Try refresh
        if (CanRefresh)
        {
            var refreshResult = await RefreshTokenAsync(ct);
            if (refreshResult.Success)
                return (true, null);
        }

        // 3. Full OAuth authorization flow
        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
        {
            return (false, "GitLab OAuth credentials (clientId/clientSecret) not found in ~/.gitconfig");
        }

        return await AuthorizeAsync(ct);
    }

    /// <summary>
    /// Runs the full OAuth Authorization Code flow with loopback redirect.
    /// Opens the user's browser for authorization.
    /// </summary>
    public async Task<(bool Success, string? Error)> AuthorizeAsync(CancellationToken ct = default)
    {
        HttpListener? listener = null;
        try
        {
            // Find a free port
            var tcpListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            var redirectUri = $"http://127.0.0.1:{port}/callback";
            var state = Guid.NewGuid().ToString("N");

            // Start listener
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            // Open browser
            var authorizeUrl = $"{_gitlabUrl}/oauth/authorize" +
                $"?client_id={Uri.EscapeDataString(_clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&response_type=code" +
                "&scope=api" +
                $"&state={Uri.EscapeDataString(state)}";

            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

            // Wait for callback (120 second timeout)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(120));

            var code = await WaitForCallbackAsync(listener, state, timeoutCts.Token);

            // Exchange code for token
            return await ExchangeCodeAsync(code, redirectUri, ct);
        }
        catch (OperationCanceledException)
        {
            return (false, "Authorization timed out. Please try again.");
        }
        catch (Exception ex)
        {
            return (false, $"Authorization failed: {ex.Message}");
        }
        finally
        {
            try { listener?.Stop(); } catch { /* best effort */ }
            (listener as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Exchanges an authorization code for access + refresh tokens.
    /// </summary>
    private async Task<(bool Success, string? Error)> ExchangeCodeAsync(
        string code, string redirectUri, CancellationToken ct)
    {
        var tokenUrl = $"{_gitlabUrl}/oauth/token";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        });

        var response = await Http.PostAsync(tokenUrl, content, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return (false, $"Token exchange failed: HTTP {(int)response.StatusCode}: {json}");

        return ParseAndCacheTokenResponse(json);
    }

    /// <summary>
    /// Refreshes the access token using the stored refresh token.
    /// </summary>
    public async Task<(bool Success, string? Error)> RefreshTokenAsync(CancellationToken ct = default)
    {
        if (!CanRefresh)
            return (false, "No refresh token available");

        var tokenUrl = $"{_gitlabUrl}/oauth/token";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = _cache!.RefreshToken!,
            ["grant_type"] = "refresh_token",
            ["redirect_uri"] = "http://127.0.0.1/callback"
        });

        try
        {
            var response = await Http.PostAsync(tokenUrl, content, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return (false, $"Token refresh failed: HTTP {(int)response.StatusCode}");

            return ParseAndCacheTokenResponse(json);
        }
        catch (Exception ex)
        {
            return (false, $"Token refresh failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates the current token by calling GET /api/v4/user.
    /// </summary>
    public async Task<bool> ValidateTokenAsync(CancellationToken ct = default)
    {
        if (!HasToken) return false;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_gitlabUrl}/api/v4/user");
            request.Headers.Add("Authorization", $"Bearer {_cache!.AccessToken}");

            var response = await Http.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Configures an HttpClient with the Bearer token for GitLab API calls.
    /// </summary>
    public void ConfigureHttpClient(HttpClient client)
    {
        if (HasToken)
        {
            // Remove any existing auth headers
            client.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_cache!.AccessToken}");
        }
    }

    private (bool Success, string? Error) ParseAndCacheTokenResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var at))
                return (false, "No access_token in response");

            _cache = new TokenCache
            {
                AccessToken = at.GetString()!,
                RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : _cache?.RefreshToken,
                ExpiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 7200,
                CreatedAt = DateTime.UtcNow
            };
            _cache.ExpiresAt = _cache.CreatedAt.Value.AddSeconds(_cache.ExpiresIn);

            // Persist to disk
            SaveCacheSync();

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to parse token response: {ex.Message}");
        }
    }

    private async Task LoadCacheAsync(CancellationToken ct)
    {
        if (_cache is not null) return;

        if (!File.Exists(TokenCachePath)) return;

        try
        {
            await using var stream = File.OpenRead(TokenCachePath);
            _cache = await JsonSerializer.DeserializeAsync<TokenCache>(stream, JsonOptions, ct);
        }
        catch
        {
            _cache = null;
        }
    }

    private void SaveCacheSync()
    {
        try
        {
            ConfigManager.EnsureHomeDirectory();
            var json = JsonSerializer.Serialize(_cache, JsonOptions);
            File.WriteAllText(TokenCachePath, json);
        }
        catch { /* best effort */ }
    }

    private static async Task<string> WaitForCallbackAsync(
        HttpListener listener, string expectedState, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var contextTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(contextTask, Task.Delay(-1, ct));
            if (completed != contextTask)
                throw new OperationCanceledException();

            var context = await contextTask;
            var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? "");
            var code = query["code"];
            var state = query["state"];
            var error = query["error"];

            string html;

            if (!string.IsNullOrEmpty(error))
            {
                html = BuildPage("Authorization Failed", $"Error: {error}", false);
                SendResponse(context.Response, html);
                throw new Exception($"OAuth error: {error} - {query["error_description"]}");
            }

            if (!string.IsNullOrEmpty(code))
            {
                if (state != expectedState)
                {
                    html = BuildPage("Security Error", "State mismatch. Possible CSRF attack.", false);
                    SendResponse(context.Response, html);
                    throw new Exception("OAuth state mismatch");
                }

                html = BuildPage("Authorization Successful!",
                    "RAVEN has been authorized. You can close this tab.", true);
                SendResponse(context.Response, html);
                return code;
            }

            html = BuildPage("Waiting...", "Waiting for GitLab redirect...", false);
            SendResponse(context.Response, html);
        }

        throw new OperationCanceledException();
    }

    private static void SendResponse(HttpListenerResponse response, string html)
    {
        try
        {
            var buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
        catch { /* best effort */ }
    }

    private static string BuildPage(string title, string message, bool success)
    {
        var color = success ? "#108548" : "#dd2b0e";
        var icon = success ? "&#10004;" : "&#9888;";
        return "<!DOCTYPE html><html><head><title>" + title + "</title>" +
            "<style>body{font-family:'Segoe UI',sans-serif;background:#1e1e1e;color:#d4d4d4;" +
            "display:flex;justify-content:center;align-items:center;min-height:100vh;margin:0}" +
            ".c{background:#252526;border:1px solid #3f3f46;border-radius:12px;padding:40px;" +
            "text-align:center;max-width:500px}h1{color:" + color + ";font-size:1.4rem}" +
            ".i{font-size:3rem;margin-bottom:16px}p{color:#9d9d9d}</style></head>" +
            "<body><div class='c'><div class='i'>" + icon + "</div>" +
            "<h1>" + title + "</h1><p>" + message + "</p></div></body></html>";
    }

    public void Dispose() { }

    /// <summary>
    /// Token cache persisted to ~/.raven/token.json
    /// </summary>
    private sealed class TokenCache
    {
        public string AccessToken { get; set; } = "";
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
