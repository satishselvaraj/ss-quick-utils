using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OAuthTokenGenExt.Services
{
    /// <summary>
    /// Handles OAuth token exchange and refresh with the GitLab instance.
    /// All calls go directly to the GitLab instance; no intermediary server.
    /// </summary>
    public class OAuthTokenService
    {
        private static readonly HttpClient HttpClient = new();

        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public int ExpiresIn { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public DateTime? CreatedAt { get; private set; }
        public string? UserName { get; private set; }
        public string? UserLogin { get; private set; }

        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
        public bool HasToken => !string.IsNullOrEmpty(AccessToken);
        public bool CanRefresh => !string.IsNullOrEmpty(RefreshToken);

        public TimeSpan? TimeUntilExpiry => ExpiresAt.HasValue
            ? (ExpiresAt.Value > DateTime.UtcNow ? ExpiresAt.Value - DateTime.UtcNow : TimeSpan.Zero)
            : null;

        /// <summary>
        /// Exchange an authorization code for an access token.
        /// </summary>
        public async Task<(bool success, string error)> ExchangeCodeAsync(
            string gitlabUrl, string clientId, string clientSecret, string code, string redirectUri)
        {
            try
            {
                var tokenUrl = gitlabUrl.TrimEnd('/') + "/oauth/token";
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                    ["redirect_uri"] = redirectUri
                });

                var response = await HttpClient.PostAsync(tokenUrl, content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return (false, $"HTTP {(int)response.StatusCode}: {json}");

                return ParseTokenResponse(json, gitlabUrl);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Refresh the access token using the stored refresh token.
        /// </summary>
        public async Task<(bool success, string error)> RefreshAsync(
            string gitlabUrl, string clientId, string clientSecret, string redirectUri)
        {
            if (string.IsNullOrEmpty(RefreshToken))
                return (false, "No refresh token available.");

            try
            {
                var tokenUrl = gitlabUrl.TrimEnd('/') + "/oauth/token";
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["refresh_token"] = RefreshToken,
                    ["grant_type"] = "refresh_token",
                    ["redirect_uri"] = redirectUri
                });

                var response = await HttpClient.PostAsync(tokenUrl, content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return (false, $"HTTP {(int)response.StatusCode}: {json}");

                return ParseTokenResponse(json, gitlabUrl);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Validate the token by calling GET /api/v4/user.
        /// </summary>
        public async Task<bool> ValidateAsync(string gitlabUrl)
        {
            if (string.IsNullOrEmpty(AccessToken)) return false;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, gitlabUrl.TrimEnd('/') + "/api/v4/user");
                request.Headers.Add("Authorization", $"Bearer {AccessToken}");

                var response = await HttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return false;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                UserName = root.TryGetProperty("name", out var n) ? n.GetString() : null;
                UserLogin = root.TryGetProperty("username", out var u) ? u.GetString() : null;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private (bool success, string error) ParseTokenResponse(string json, string gitlabUrl)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("access_token", out var at))
                    return (false, "No access_token in response.");

                AccessToken = at.GetString();
                RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : RefreshToken;
                ExpiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 7200;
                CreatedAt = DateTime.UtcNow;
                ExpiresAt = CreatedAt.Value.AddSeconds(ExpiresIn);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to parse response: {ex.Message}");
            }
        }

        /// <summary>
        /// Extract the authorization code from a redirect URL or raw code string.
        /// </summary>
        public static string ExtractCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            try
            {
                var uri = new Uri(input);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var code = query["code"];
                if (!string.IsNullOrEmpty(code)) return code;
            }
            catch { /* not a URL, treat as raw code */ }

            return input.Trim();
        }

        /// <summary>
        /// Build the GitLab authorization URL.
        /// </summary>
        public static string BuildAuthorizeUrl(string gitlabUrl, string clientId, string redirectUri, string scope, string state)
        {
            return gitlabUrl.TrimEnd('/') + "/oauth/authorize"
                + $"?client_id={Uri.EscapeDataString(clientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + "&response_type=code"
                + $"&scope={Uri.EscapeDataString(scope)}"
                + $"&state={Uri.EscapeDataString(state)}";
        }
    }
}
