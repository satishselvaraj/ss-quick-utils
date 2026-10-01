using System.Text.Json.Serialization;

namespace Raven.Core.Models.GitLab;

/// <summary>
/// Represents a GitLab branch.
/// </summary>
public sealed class GitLabBranch
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("merged")]
    public bool Merged { get; set; }

    [JsonPropertyName("protected")]
    public bool Protected { get; set; }

    [JsonPropertyName("web_url")]
    public string? WebUrl { get; set; }
}
