using System.Text.Json;
using System.Text.Json.Serialization;

namespace Raven.Core.Configuration;

/// <summary>
/// Root configuration for RAVEN, stored at ~/.raven/config.json
/// </summary>
public sealed class RavenConfig
{
    [JsonPropertyName("rally")]
    public RallyConfig Rally { get; set; } = new();

    [JsonPropertyName("gitlab")]
    public GitLabConfig GitLab { get; set; } = new();

    [JsonPropertyName("preferences")]
    public PreferencesConfig Preferences { get; set; } = new();
}

public sealed class RallyConfig
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "https://rally1.rallydev.com";

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("workspace")]
    public string Workspace { get; set; } = string.Empty;

    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether credentials were loaded from ~/.gitconfig.
    /// </summary>
    [JsonIgnore]
    public bool LoadedFromGitConfig { get; set; }
}

public sealed class GitLabConfig
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "https://trgl.gitlab-dedicated.com";

    [JsonPropertyName("personalAccessToken")]
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// OAuth Application ID (client_id) read from ~/.gitconfig gitLabDevClientId.
    /// Used for Duo authentication flow.
    /// </summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth Application Secret (client_secret) read from ~/.gitconfig gitLabDevClientSecret.
    /// Used for Duo authentication flow.
    /// </summary>
    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = string.Empty;

    [JsonPropertyName("defaultGroup")]
    public string DefaultGroup { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether credentials were loaded from ~/.gitconfig.
    /// </summary>
    [JsonIgnore]
    public bool LoadedFromGitConfig { get; set; }
}

public sealed class PreferencesConfig
{
    [JsonPropertyName("defaultBranch")]
    public string DefaultBranch { get; set; } = "main";

    [JsonPropertyName("autoUpdateRally")]
    public bool AutoUpdateRally { get; set; } = true;

    [JsonPropertyName("branchPrefix")]
    public BranchPrefixConfig BranchPrefix { get; set; } = new();
}

public sealed class BranchPrefixConfig
{
    [JsonPropertyName("feature")]
    public string Feature { get; set; } = "feature";

    [JsonPropertyName("bugfix")]
    public string Bugfix { get; set; } = "bugfix";
}
