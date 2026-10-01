using System.Text.Json.Serialization;

namespace Raven.Core.Models.GitLab;

/// <summary>
/// Represents a file or directory in a GitLab repository tree.
/// </summary>
public sealed class GitLabTreeItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    public bool IsDirectory => Type == "tree";
    public bool IsFile => Type == "blob";
}
