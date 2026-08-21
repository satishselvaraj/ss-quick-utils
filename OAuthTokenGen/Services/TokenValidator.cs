using System.Net.Http.Headers;
using System.Text.Json;

namespace OAuthTokenGen.Services;

/// <summary>
/// Validates an OAuth access token by calling the GitLab API.
/// </summary>
public class TokenValidator : IDisposable
{
    private readonly HttpClient _httpClient;

    public TokenValidator()
    {
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Validates the token by calling GET /api/v4/user on the GitLab instance.
    /// Returns the authenticated user's username and name if valid.
    /// </summary>
    /// <param name="gitlabDomain">The GitLab instance URL (e.g., https://gitlab.com).</param>
    /// <param name="accessToken">The OAuth access token to validate.</param>
    public async Task<(bool IsValid, string Message)> ValidateTokenAsync(
        string gitlabDomain, string accessToken)
    {
        try
        {
            var baseUrl = gitlabDomain.TrimEnd('/');
            var requestUrl = $"{baseUrl}/api/v4/user";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return (false, $"Token validation failed (HTTP {(int)response.StatusCode}): {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var username = root.TryGetProperty("username", out var u) ? u.GetString() : "unknown";
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : "unknown";
            var email = root.TryGetProperty("email", out var e) ? e.GetString() : "unknown";

            return (true,
                $"Token is valid!\n" +
                $"  Authenticated as : {name} (@{username})\n" +
                $"  Email            : {email}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error during validation: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Unexpected error during validation: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
