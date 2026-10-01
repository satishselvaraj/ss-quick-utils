using System.Text.RegularExpressions;

namespace Raven.Core.Configuration;

/// <summary>
/// Reads credentials from the user's ~/.gitconfig file.
/// 
/// Supports two credential sections:
/// 
/// [credential "https://trgl.gitlab-dedicated.com"]
///     gitLabDevClientId = ...
///     gitLabDevClientSecret = ...
///     gitLabAuthModes = browser
///     provider = gitlab
/// 
/// [credential "https://rally1.rallydev.com"]
///     rallyApiKey = _xxxxxxxxxxxxxxxx
///     provider = rally
/// 
/// This follows the same pattern used by the OAuthTokenGenExt extension,
/// keeping all credentials in a single, familiar location.
/// </summary>
public static class GitConfigService
{
    private static readonly string GitConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig");

    /// <summary>
    /// Reads GitLab OAuth credentials from ~/.gitconfig.
    /// Returns the first [credential "https://..."] section that contains gitLabDevClientId.
    /// </summary>
    public static GitLabGitConfigCredentials? ReadGitLabCredentials()
    {
        if (!File.Exists(GitConfigPath))
            return null;

        var content = File.ReadAllText(GitConfigPath);

        // Match [credential "https://some-url"] followed by indented key=value lines
        var sectionPattern = new Regex(
            @"\[credential\s+""(https?://[^""]+)""\]\s*\n((?:\s+\w+\s*=\s*[^\n]*\n)*)",
            RegexOptions.IgnoreCase);

        foreach (Match section in sectionPattern.Matches(content))
        {
            var url = section.Groups[1].Value.Trim();
            var body = section.Groups[2].Value;

            var clientId = ExtractValue(body, "gitLabDevClientId");
            var clientSecret = ExtractValue(body, "gitLabDevClientSecret");
            var provider = ExtractValue(body, "provider");

            // Only match GitLab credential sections (has clientId or provider=gitlab)
            if (!string.IsNullOrEmpty(clientId) ||
                "gitlab".Equals(provider, StringComparison.OrdinalIgnoreCase))
            {
                // Normalize URL
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;

                return new GitLabGitConfigCredentials(
                    Url: url.TrimEnd('/'),
                    ClientId: clientId,
                    ClientSecret: clientSecret);
            }
        }

        return null;
    }

    /// <summary>
    /// Reads Rally API key from ~/.gitconfig.
    /// Looks for a [credential "https://rally..."] section with rallyApiKey.
    /// </summary>
    public static RallyGitConfigCredentials? ReadRallyCredentials()
    {
        if (!File.Exists(GitConfigPath))
            return null;

        var content = File.ReadAllText(GitConfigPath);

        var sectionPattern = new Regex(
            @"\[credential\s+""(https?://[^""]+)""\]\s*\n((?:\s+\w+\s*=\s*[^\n]*\n)*)",
            RegexOptions.IgnoreCase);

        foreach (Match section in sectionPattern.Matches(content))
        {
            var url = section.Groups[1].Value.Trim();
            var body = section.Groups[2].Value;

            var apiKey = ExtractValue(body, "rallyApiKey");
            var provider = ExtractValue(body, "provider");
            var workspace = ExtractValue(body, "rallyWorkspace");
            var project = ExtractValue(body, "rallyProject");

            // Match Rally credential sections
            if (!string.IsNullOrEmpty(apiKey) ||
                "rally".Equals(provider, StringComparison.OrdinalIgnoreCase))
            {
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;

                return new RallyGitConfigCredentials(
                    Url: url.TrimEnd('/'),
                    ApiKey: apiKey,
                    Workspace: workspace,
                    Project: project);
            }
        }

        return null;
    }

    /// <summary>
    /// Reads all RAVEN-relevant credentials from ~/.gitconfig in one pass.
    /// </summary>
    public static GitConfigCredentials ReadAll()
    {
        return new GitConfigCredentials(
            GitLab: ReadGitLabCredentials(),
            Rally: ReadRallyCredentials());
    }

    /// <summary>
    /// Checks whether ~/.gitconfig exists and contains any RAVEN-relevant credentials.
    /// </summary>
    public static bool HasCredentials()
    {
        var creds = ReadAll();
        return creds.GitLab is not null || creds.Rally is not null;
    }

    /// <summary>
    /// Gets the path to the .gitconfig file.
    /// </summary>
    public static string ConfigPath => GitConfigPath;

    private static string? ExtractValue(string sectionBody, string key)
    {
        var pattern = new Regex(
            $@"^\s*{Regex.Escape(key)}\s*=\s*(.+)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        var match = pattern.Match(sectionBody);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}

/// <summary>
/// GitLab credentials read from ~/.gitconfig [credential "https://..."] section.
/// </summary>
public sealed record GitLabGitConfigCredentials(
    string Url,
    string? ClientId,
    string? ClientSecret);

/// <summary>
/// Rally credentials read from ~/.gitconfig [credential "https://rally..."] section.
/// </summary>
public sealed record RallyGitConfigCredentials(
    string Url,
    string? ApiKey,
    string? Workspace,
    string? Project);

/// <summary>
/// All RAVEN-relevant credentials from ~/.gitconfig.
/// </summary>
public sealed record GitConfigCredentials(
    GitLabGitConfigCredentials? GitLab,
    RallyGitConfigCredentials? Rally);
