using System.Text.Json;
using System.Text.Json.Serialization;

namespace OAuthTokenGenV2.Services;

/// <summary>
/// Saves and loads OAuth token data to a local JSON file so that
/// the refresh token can be used to renew expired access tokens
/// without requiring the user to re-authorize in the browser.
/// 
/// The token file is stored alongside the executable.
/// </summary>
public static class TokenStorageService
{
    private const string TokenFileName = "gitlab_oauth_token.json";

    /// <summary>
    /// Gets the full path to the token storage file.
    /// Stored next to the executable (or in the current directory).
    /// </summary>
    private static string GetTokenFilePath()
    {
        var exeDir = AppContext.BaseDirectory;
        return Path.Combine(exeDir, TokenFileName);
    }

    /// <summary>
    /// Saves the token data and configuration to a local JSON file.
    /// </summary>
    public static void SaveToken(StoredTokenData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, StoredTokenJsonContext.Default.StoredTokenData);
            File.WriteAllText(GetTokenFilePath(), json);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Warning: Could not save token locally: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Loads previously saved token data from the local JSON file.
    /// Returns null if no saved token exists or the file is invalid.
    /// </summary>
    public static StoredTokenData? LoadToken()
    {
        try
        {
            var filePath = GetTokenFilePath();
            if (!File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(json, StoredTokenJsonContext.Default.StoredTokenData);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a saved token file exists.
    /// </summary>
    public static bool HasSavedToken() => File.Exists(GetTokenFilePath());

    /// <summary>
    /// Deletes the saved token file.
    /// </summary>
    public static void DeleteToken()
    {
        try
        {
            var filePath = GetTokenFilePath();
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch { /* ignore */ }
    }
}

/// <summary>
/// Data structure for persisting token and configuration to disk.
/// </summary>
public class StoredTokenData
{
    public string GitLabDomain { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string ApplicationSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Whether the access token has expired based on the stored creation time and expiry.
    /// </summary>
    public bool IsExpired => ExpiresIn > 0 && DateTime.UtcNow > CreatedAtUtc.AddSeconds(ExpiresIn);

    /// <summary>
    /// Whether a refresh token is available for renewal.
    /// </summary>
    public bool CanRefresh => !string.IsNullOrEmpty(RefreshToken);
}

/// <summary>
/// Source-generated JSON serializer context for trim-safe serialization.
/// Eliminates IL2026 warnings when PublishTrimmed is enabled.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StoredTokenData))]
internal partial class StoredTokenJsonContext : JsonSerializerContext
{
}
