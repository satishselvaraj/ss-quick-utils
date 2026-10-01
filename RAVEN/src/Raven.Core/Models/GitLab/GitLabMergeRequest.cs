using System.Text.Json.Serialization;

namespace Raven.Core.Models.GitLab;

/// <summary>
/// Represents a GitLab merge request.
/// </summary>
public sealed class GitLabMergeRequest
{
    [JsonPropertyName("iid")]
    public int Iid { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("source_branch")]
    public string SourceBranch { get; set; } = string.Empty;

    [JsonPropertyName("target_branch")]
    public string TargetBranch { get; set; } = string.Empty;

    [JsonPropertyName("web_url")]
    public string WebUrl { get; set; } = string.Empty;

    [JsonPropertyName("merge_status")]
    public string? MergeStatus { get; set; }

    [JsonPropertyName("author")]
    public GitLabUser? Author { get; set; }
}

public sealed class GitLabUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
