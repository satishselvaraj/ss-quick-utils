using System.Text.Json;

namespace Raven.Core.Configuration;

/// <summary>
/// Manages RAVEN configuration stored in ~/.raven/config.json.
/// No database - all state is file-based in the user's home directory.
/// </summary>
public sealed class ConfigManager
{
    private static readonly string RavenHome = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".raven");

    private static readonly string ConfigPath = Path.Combine(RavenHome, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Gets the path to the .raven directory.
    /// </summary>
    public static string HomePath => RavenHome;

    /// <summary>
    /// Ensures the ~/.raven directory exists.
    /// </summary>
    public static void EnsureHomeDirectory()
    {
        if (!Directory.Exists(RavenHome))
        {
            Directory.CreateDirectory(RavenHome);
        }
    }

    /// <summary>
    /// Loads configuration from ~/.raven/config.json, then overlays credentials
    /// from ~/.gitconfig. The .gitconfig values take precedence for secrets
    /// (API keys, OAuth credentials) so they stay in one familiar location.
    /// </summary>
    public static async Task<RavenConfig> LoadAsync(CancellationToken ct = default)
    {
        RavenConfig config;

        if (File.Exists(ConfigPath))
        {
            await using var stream = File.OpenRead(ConfigPath);
            config = await JsonSerializer.DeserializeAsync<RavenConfig>(stream, JsonOptions, ct)
                     ?? new RavenConfig();
        }
        else
        {
            config = new RavenConfig();
        }

        // Overlay credentials from ~/.gitconfig (secrets live there, not in config.json)
        MergeGitConfigCredentials(config);

        return config;
    }

    /// <summary>
    /// Reads ~/.gitconfig and merges credentials into the config.
    /// .gitconfig values override config.json for secrets.
    /// Non-secret settings (workspace, project, defaultGroup) from config.json are preserved.
    /// </summary>
    private static void MergeGitConfigCredentials(RavenConfig config)
    {
        var gitCreds = GitConfigService.ReadAll();

        // GitLab: URL, OAuth clientId/clientSecret from .gitconfig
        if (gitCreds.GitLab is not null)
        {
            config.GitLab.Url = gitCreds.GitLab.Url;
            config.GitLab.LoadedFromGitConfig = true;

            if (!string.IsNullOrEmpty(gitCreds.GitLab.ClientId))
                config.GitLab.ClientId = gitCreds.GitLab.ClientId;

            if (!string.IsNullOrEmpty(gitCreds.GitLab.ClientSecret))
                config.GitLab.ClientSecret = gitCreds.GitLab.ClientSecret;
        }

        // Rally: URL, API key, workspace, project from .gitconfig
        if (gitCreds.Rally is not null)
        {
            config.Rally.Url = gitCreds.Rally.Url;
            config.Rally.LoadedFromGitConfig = true;

            if (!string.IsNullOrEmpty(gitCreds.Rally.ApiKey))
                config.Rally.ApiKey = gitCreds.Rally.ApiKey;

            // .gitconfig workspace/project override config.json if present
            if (!string.IsNullOrEmpty(gitCreds.Rally.Workspace))
                config.Rally.Workspace = gitCreds.Rally.Workspace;

            if (!string.IsNullOrEmpty(gitCreds.Rally.Project))
                config.Rally.Project = gitCreds.Rally.Project;
        }
    }

    /// <summary>
    /// Saves configuration to ~/.raven/config.json.
    /// </summary>
    public static async Task SaveAsync(RavenConfig config, CancellationToken ct = default)
    {
        EnsureHomeDirectory();
        await using var stream = File.Create(ConfigPath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, ct);
    }

    /// <summary>
    /// Checks whether RAVEN has been initialized (config file exists).
    /// </summary>
    public static bool IsInitialized() => File.Exists(ConfigPath);

    /// <summary>
    /// Gets a path within the .raven directory for caching/state files.
    /// </summary>
    public static string GetCachePath(string filename)
    {
        var cachePath = Path.Combine(RavenHome, "cache");
        if (!Directory.Exists(cachePath))
        {
            Directory.CreateDirectory(cachePath);
        }
        return Path.Combine(cachePath, filename);
    }
}
