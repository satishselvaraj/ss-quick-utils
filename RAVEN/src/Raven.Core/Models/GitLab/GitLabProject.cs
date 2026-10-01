using System.Text.Json.Serialization;

namespace Raven.Core.Models.GitLab;

/// <summary>
/// Represents a GitLab project (repository).
/// </summary>
public sealed class GitLabProject
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_with_namespace")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("path_with_namespace")]
    public string PathWithNamespace { get; set; } = string.Empty;

    [JsonPropertyName("web_url")]
    public string WebUrl { get; set; } = string.Empty;

    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; set; } = "main";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("topics")]
    public List<string> Topics { get; set; } = [];
}
