namespace OAuthTokenGenV2.Models;

/// <summary>
/// Represents the result of an OAuth token extraction from a redirect URL.
/// </summary>
public class TokenResult
{
    public bool Success { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The estimated expiration time based on ExpiresIn seconds.
    /// </summary>
    public DateTime? ExpiresAt => ExpiresIn > 0 ? CreatedAt.AddSeconds(ExpiresIn) : null;

    public override string ToString()
    {
        if (!Success)
            return $"[Error] {ErrorMessage}";

        return $"Access Token : {AccessToken}\n" +
               $"Token Type   : {TokenType}\n" +
               $"Scope        : {Scope}\n" +
               $"Expires In   : {ExpiresIn} seconds\n" +
               $"Expires At   : {ExpiresAt:yyyy-MM-dd HH:mm:ss UTC}";
    }
}
