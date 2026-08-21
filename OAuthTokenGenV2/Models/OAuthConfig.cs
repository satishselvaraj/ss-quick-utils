namespace OAuthTokenGenV2.Models;

/// <summary>
/// Configuration for the GitLab OAuth application.
/// </summary>
public class OAuthConfig
{
    public string GitLabDomain { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string ApplicationSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost";
    public string Scope { get; set; } = "api";

    /// <summary>
    /// Builds the OAuth authorization URL using the Authorization Code flow.
    /// Uses response_type=code (not token) because many GitLab instances
    /// disable the implicit grant flow for security reasons.
    /// </summary>
    public string BuildAuthorizeUrl()
    {
        var baseUrl = GitLabDomain.TrimEnd('/');
        return $"{baseUrl}/oauth/authorize" +
               $"?client_id={Uri.EscapeDataString(ApplicationId)}" +
               $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
               $"&response_type=code" +
               $"&scope={Uri.EscapeDataString(Scope)}";
    }

    /// <summary>
    /// Builds the token exchange URL for POST /oauth/token.
    /// </summary>
    public string BuildTokenUrl() => $"{GitLabDomain.TrimEnd('/')}/oauth/token";

    /// <summary>
    /// Validates that all required fields are populated.
    /// </summary>
    public (bool IsValid, string ErrorMessage) Validate()
    {
        if (string.IsNullOrWhiteSpace(GitLabDomain))
            return (false, "GitLab domain is required.");

        if (!Uri.TryCreate(GitLabDomain, UriKind.Absolute, out _))
            return (false, "GitLab domain must be a valid URL (e.g., https://gitlab.com).");

        if (string.IsNullOrWhiteSpace(ApplicationId))
            return (false, "Application ID (client_id) is required.");

        if (string.IsNullOrWhiteSpace(ApplicationSecret))
            return (false, "Application Secret is required for the Authorization Code flow.");

        return (true, string.Empty);
    }
}
