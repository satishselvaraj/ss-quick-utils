using System.Text.Json.Serialization;

namespace Raven.Core.Models.Rally;

/// <summary>
/// Represents a Rally iteration (sprint).
/// </summary>
public sealed class RallyIteration
{
    [JsonPropertyName("objectID")]
    public long ObjectId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("plannedVelocity")]
    public double? PlannedVelocity { get; set; }
}
