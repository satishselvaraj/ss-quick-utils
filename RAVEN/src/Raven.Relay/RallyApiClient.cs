using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Raven.Core.Configuration;
using Raven.Core.Models.Rally;

namespace Raven.Relay;

/// <summary>
/// Low-level HTTP client for the Rally Web Services API (WSAPI v2.0).
/// Handles authentication, pagination, and JSON response parsing.
/// </summary>
public sealed class RallyApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<RallyApiClient> _logger;
    private readonly string _baseUrl;

    public RallyApiClient(RallyConfig config, ILogger<RallyApiClient> logger)
    {
        _logger = logger;
        _baseUrl = config.Url.TrimEnd('/') + "/slm/webservice/v2.0";

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("zsessionid", config.ApiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Queries Rally for user stories assigned to the current user in the current iteration.
    /// </summary>
    public async Task<List<RallyWorkItem>> GetMyStoriesAsync(
        string project, string? iteration = null, CancellationToken ct = default)
    {
        var query = "(Owner.UserName = \"/currentuser\")";
        if (!string.IsNullOrEmpty(iteration))
        {
            query = $"((Owner.UserName = \"/currentuser\") AND (Iteration.Name = \"{iteration}\"))";
        }

        return await QueryWorkItemsAsync("hierarchicalrequirement", query, project, RallyWorkItemType.UserStory, ct);
    }

    /// <summary>
    /// Queries Rally for defects assigned to the current user.
    /// </summary>
    public async Task<List<RallyWorkItem>> GetMyDefectsAsync(
        string project, string? iteration = null, CancellationToken ct = default)
    {
        var query = "(Owner.UserName = \"/currentuser\")";
        if (!string.IsNullOrEmpty(iteration))
        {
            query = $"((Owner.UserName = \"/currentuser\") AND (Iteration.Name = \"{iteration}\"))";
        }

        return await QueryWorkItemsAsync("defect", query, project, RallyWorkItemType.Defect, ct);
    }

    /// <summary>
    /// Gets the current iteration for a project.
    /// </summary>
    public async Task<RallyIteration?> GetCurrentIterationAsync(string project, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var query = $"((StartDate <= \"{today}\") AND (EndDate >= \"{today}\"))";
        var url = $"{_baseUrl}/iteration?query={Uri.EscapeDataString(query)}" +
                  $"&project={Uri.EscapeDataString(project)}" +
                  "&fetch=ObjectID,Name,StartDate,EndDate,State,PlannedVelocity" +
                  "&pagesize=1";

        _logger.LogDebug("Rally API: GET {Url}", url);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        var results = json?["QueryResult"]?["Results"]?.AsArray();

        if (results is null || results.Count == 0)
            return null;

        var item = results[0]!;
        return new RallyIteration
        {
            ObjectId = item["ObjectID"]!.GetValue<long>(),
            Name = item["Name"]?.GetValue<string>() ?? "",
            StartDate = ParseDate(item["StartDate"]),
            EndDate = ParseDate(item["EndDate"]),
            State = item["State"]?.GetValue<string>() ?? "",
            PlannedVelocity = item["PlannedVelocity"]?.GetValue<double>()
        };
    }

    /// <summary>
    /// Gets a single work item by its FormattedID (e.g., US12345 or DE54321).
    /// </summary>
    public async Task<RallyWorkItem?> GetWorkItemByIdAsync(string formattedId, CancellationToken ct = default)
    {
        var type = formattedId.StartsWith("DE", StringComparison.OrdinalIgnoreCase)
            ? "defect"
            : "hierarchicalrequirement";

        var itemType = formattedId.StartsWith("DE", StringComparison.OrdinalIgnoreCase)
            ? RallyWorkItemType.Defect
            : RallyWorkItemType.UserStory;

        var query = $"(FormattedID = \"{formattedId}\")";
        var items = await QueryWorkItemsAsync(type, query, null, itemType, ct);
        return items.FirstOrDefault();
    }

    /// <summary>
    /// Updates the ScheduleState of a work item.
    /// </summary>
    public async Task<bool> UpdateStateAsync(string workItemRef, string newState, CancellationToken ct = default)
    {
        return await UpdateFieldAsync(workItemRef, "ScheduleState", newState, ct);
    }

    /// <summary>
    /// Appends text to the Notes field of a work item.
    /// </summary>
    public async Task<bool> UpdateNotesAsync(string workItemRef, string notes, CancellationToken ct = default)
    {
        return await UpdateFieldAsync(workItemRef, "Notes", notes, ct);
    }

    /// <summary>
    /// Updates the TaskActualTotal on a work item.
    /// </summary>
    public async Task<bool> UpdateActualsAsync(string workItemRef, double actuals, CancellationToken ct = default)
    {
        var url = workItemRef;
        var type = workItemRef.Contains("/defect/") ? "Defect" : "HierarchicalRequirement";

        var payload = new JsonObject
        {
            [type] = new JsonObject
            {
                ["TaskActualTotal"] = actuals
            }
        };

        _logger.LogDebug("Rally API: POST {Url} - Update actuals to {Actuals}", url, actuals);

        var response = await _http.PostAsJsonAsync(url, payload, ct);
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> UpdateFieldAsync(string workItemRef, string field, string value, CancellationToken ct)
    {
        var type = workItemRef.Contains("/defect/") ? "Defect" : "HierarchicalRequirement";

        var payload = new JsonObject
        {
            [type] = new JsonObject
            {
                [field] = value
            }
        };

        _logger.LogDebug("Rally API: POST {Url} - {Field} = {Value}", workItemRef, field, value);

        var response = await _http.PostAsJsonAsync(workItemRef, payload, ct);
        return response.IsSuccessStatusCode;
    }

    private async Task<List<RallyWorkItem>> QueryWorkItemsAsync(
        string type, string query, string? project, RallyWorkItemType itemType, CancellationToken ct)
    {
        var fetch = "FormattedID,ObjectID,Name,Description,ScheduleState,PlanEstimate," +
                    "TaskEstimateTotal,TaskActualTotal,TaskRemainingTotal,Owner,Project,Iteration,Notes,Tags";

        var url = $"{_baseUrl}/{type}?query={Uri.EscapeDataString(query)}&fetch={fetch}&pagesize=200";

        if (!string.IsNullOrEmpty(project))
        {
            url += $"&project={Uri.EscapeDataString(project)}";
        }

        _logger.LogDebug("Rally API: GET {Url}", url);

        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(ct);
        var results = json?["QueryResult"]?["Results"]?.AsArray();

        if (results is null)
            return [];

        return results
            .Where(r => r is not null)
            .Select(r => ParseWorkItem(r!, itemType))
            .ToList();
    }

    private static RallyWorkItem ParseWorkItem(JsonNode node, RallyWorkItemType type)
    {
        return new RallyWorkItem
        {
            FormattedId = node["FormattedID"]?.GetValue<string>() ?? "",
            ObjectId = node["ObjectID"]?.GetValue<long>() ?? 0,
            Name = node["Name"]?.GetValue<string>() ?? "",
            Description = node["Description"]?.GetValue<string>() ?? "",
            Type = type,
            ScheduleState = node["ScheduleState"]?.GetValue<string>() ?? "",
            PlanEstimate = node["PlanEstimate"]?.GetValue<double>(),
            TaskEstimateTotal = node["TaskEstimateTotal"]?.GetValue<double>(),
            TaskActualTotal = node["TaskActualTotal"]?.GetValue<double>(),
            TaskRemainingTotal = node["TaskRemainingTotal"]?.GetValue<double>(),
            Owner = node["Owner"]?["_refObjectName"]?.GetValue<string>(),
            Project = node["Project"]?["_refObjectName"]?.GetValue<string>(),
            Iteration = node["Iteration"]?["_refObjectName"]?.GetValue<string>(),
            Notes = node["Notes"]?.GetValue<string>() ?? "",
            Ref = node["_ref"]?.GetValue<string>() ?? "",
            Tags = ParseTags(node["Tags"])
        };
    }

    private static List<string> ParseTags(JsonNode? tagsNode)
    {
        var tags = tagsNode?["_tagsNameArray"]?.AsArray();
        if (tags is null) return [];
        return tags
            .Where(t => t is not null)
            .Select(t => t!["Name"]?.GetValue<string>() ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
    }

    private static DateTime? ParseDate(JsonNode? node)
    {
        var str = node?.GetValue<string>();
        return DateTime.TryParse(str, out var dt) ? dt : null;
    }

    public void Dispose() => _http.Dispose();
}
