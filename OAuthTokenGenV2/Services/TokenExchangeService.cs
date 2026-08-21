using System.Text.Json;
using OAuthTokenGenV2.Models;

namespace OAuthTokenGenV2.Services;

/// <summary>
/// Exchanges an OAuth authorization code for an access token
/// by calling POST /oauth/token on the GitLab instance.
/// </summary>
public class TokenExchangeService : IDisposable
{
    private readonly HttpClient _httpClient;

    public TokenExchangeService()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Exchanges the authorization code for an access token.
    /// </summary>
    /// <param name="config">The OAuth configuration with GitLab domain, client ID, secret, and redirect URI.</param>
    /// <param name="authorizationCode">The code received from the OAuth callback.</param>
    public async Task<TokenResult> ExchangeCodeForTokenAsync(OAuthConfig config, string authorizationCode)
    {
        try
        {
            var tokenUrl = config.BuildTokenUrl();

            var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = config.ApplicationId,
                ["client_secret"] = config.ApplicationSecret,
                ["code"] = authorizationCode,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = config.RedirectUri
            });

            using var response = await _httpClient.PostAsync(tokenUrl, requestBody);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new TokenResult
                {
                    Success = false,
                    ErrorMessage = $"Token exchange failed (HTTP {(int)response.StatusCode}): {responseBody}"
                };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new TokenResult
                {
                    Success = false,
                    ErrorMessage = "No access_token found in the token response."
                };
            }

            var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";
            var scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() : "api";
            var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 0;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            return new TokenResult
            {
                Success = true,
                AccessToken = accessToken,
                TokenType = tokenType ?? "Bearer",
                Scope = scope ?? "api",
                ExpiresIn = expiresIn,
                RefreshToken = refreshToken ?? string.Empty
            };
        }
        catch (HttpRequestException ex)
        {
            return new TokenResult
            {
                Success = false,
                ErrorMessage = $"Network error during token exchange: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new TokenResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error during token exchange: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Refreshes an expired access token using the refresh token.
    /// This avoids requiring the user to re-authorize in the browser.
    /// </summary>
    /// <param name="config">The OAuth configuration with GitLab domain, client ID, and secret.</param>
    /// <param name="refreshToken">The refresh token from the original token response.</param>
    public async Task<TokenResult> RefreshAccessTokenAsync(OAuthConfig config, string refreshToken)
    {
        try
        {
            var tokenUrl = config.BuildTokenUrl();

            var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = config.ApplicationId,
                ["client_secret"] = config.ApplicationSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["redirect_uri"] = config.RedirectUri
            });

            using var response = await _httpClient.PostAsync(tokenUrl, requestBody);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new TokenResult
                {
                    Success = false,
                    ErrorMessage = $"Token refresh failed (HTTP {(int)response.StatusCode}): {responseBody}"
                };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new TokenResult
                {
                    Success = false,
                    ErrorMessage = "No access_token found in the refresh response."
                };
            }

            var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";
            var scope = root.TryGetProperty("scope", out var sc) ? sc.GetString() : "api";
            var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 0;
            var newRefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            return new TokenResult
            {
                Success = true,
                AccessToken = accessToken,
                TokenType = tokenType ?? "Bearer",
                Scope = scope ?? "api",
                ExpiresIn = expiresIn,
                RefreshToken = newRefreshToken ?? refreshToken // GitLab may return a new refresh token
            };
        }
        catch (HttpRequestException ex)
        {
            return new TokenResult
            {
                Success = false,
                ErrorMessage = $"Network error during token refresh: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new TokenResult
            {
                Success = false,
                ErrorMessage = $"Unexpected error during token refresh: {ex.Message}"
            };
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
