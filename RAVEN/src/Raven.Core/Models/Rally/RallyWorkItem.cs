using System.Text.Json.Serialization;

namespace Raven.Core.Models.Rally;

/// <summary>
/// Represents a Rally work item (User Story or Defect).
/// </summary>
public sealed class RallyWorkItem
{
    [JsonPropertyName("formattedID")]
    public string FormattedId { get; set; } = string.Empty;

    [JsonPropertyName("objectID")]
    public long ObjectId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public RallyWorkItemType Type { get; set; }

    [JsonPropertyName("scheduleState")]
    public string ScheduleState { get; set; } = string.Empty;

    [JsonPropertyName("planEstimate")]
    public double? PlanEstimate { get; set; }

    [JsonPropertyName("taskEstimateTotal")]
    public double? TaskEstimateTotal { get; set; }

    [JsonPropertyName("taskActualTotal")]
    public double? TaskActualTotal { get; set; }

    [JsonPropertyName("taskRemainingTotal")]
    public double? TaskRemainingTotal { get; set; }

    [JsonPropertyName("owner")]
    public string? Owner { get; set; }

    [JsonPropertyName("project")]
    public string? Project { get; set; }

    [JsonPropertyName("iteration")]
    public string? Iteration { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("ref")]
    public string Ref { get; set; } = string.Empty;

    /// <summary>
    /// Derived: application name parsed from project or tags.
    /// </summary>
    [JsonPropertyName("appName")]
    public string? AppName { get; set; }
}

public enum RallyWorkItemType
{
    UserStory,
    Defect,
    Task
}
