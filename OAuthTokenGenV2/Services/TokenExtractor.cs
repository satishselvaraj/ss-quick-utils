namespace OAuthTokenGenV2.Services;

/// <summary>
/// Extracts the authorization code from the OAuth redirect URL.
/// With the Authorization Code flow, GitLab redirects to:
///   http://localhost/?code=AUTHORIZATION_CODE
/// This service parses the query string to extract the code.
/// </summary>
public static class TokenExtractor
{
    /// <summary>
    /// Extracts the authorization code from a redirect URL's ?code= query parameter.
    /// </summary>
    /// <param name="redirectUrl">The full URL from the browser address bar after authorization.</param>
    /// <returns>The authorization code, or null if not found.</returns>
    public static string? ExtractCodeFromRedirectUrl(string redirectUrl)
    {
        if (string.IsNullOrWhiteSpace(redirectUrl))
            return null;

        try
        {
            var uri = new Uri(redirectUrl);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            return query["code"];
        }
        catch
        {
            return null;
        }
    }
}
