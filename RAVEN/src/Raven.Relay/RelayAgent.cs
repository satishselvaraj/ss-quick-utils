using Microsoft.Extensions.Logging;
using Raven.Core.Agents;
using Raven.Core.Configuration;
using Raven.Core.Logging;
using Raven.Core.Models.Rally;

namespace Raven.Relay;

/// <summary>
/// RAVEN Relay Agent - Rally integration engine.
/// Retrieves stories, defects, sprint info and updates Rally state/notes/actuals.
/// </summary>
public sealed class RelayAgent : IAgent, IDisposable
{
    public string Name => "relay";
    public string Description => "Rally integration engine - retrieves and updates work items";

    private readonly RallyApiClient _client;
    private readonly RallyConfig _config;
    private readonly ILogger<RelayAgent> _logger;

    public RelayAgent(RallyConfig config, ILogger<RelayAgent> logger)
    {
        _config = config;
        _logger = logger;
        _client = new RallyApiClient(config, logger.CreateChildLogger<RallyApiClient>());
    }

    public async Task<AgentHealthCheck> CheckHealthAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            return new AgentHealthCheck(Name, false, "Rally API key not configured. Run: raven init");
        }

        try
        {
            var iteration = await _client.GetCurrentIterationAsync(_config.Project, ct);
            var msg = iteration is not null
                ? $"Connected. Current sprint: {iteration.Name}"
                : "Connected. No active sprint found.";
            return new AgentHealthCheck(Name, true, msg);
        }
        catch (Exception ex)
        {
            return new AgentHealthCheck(Name, false, $"Rally connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all stories and defects assigned to the current user in the active sprint.
    /// </summary>
    public async Task<AgentResult<List<RallyWorkItem>>> GetMyWorkAsync(CancellationToken ct = default)
    {
        try
        {
            var iteration = await _client.GetCurrentIterationAsync(_config.Project, ct);
            var iterationName = iteration?.Name;

            var stories = await _client.GetMyStoriesAsync(_config.Project, iterationName, ct);
            var defects = await _client.GetMyDefectsAsync(_config.Project, iterationName, ct);

            var all = stories.Concat(defects).OrderBy(w => w.FormattedId).ToList();

            _logger.LogInformation("Relay: Found {Count} work items ({Stories} stories, {Defects} defects)",
                all.Count, stories.Count, defects.Count);

            return AgentResult<List<RallyWorkItem>>.Ok(all, Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Relay: Failed to retrieve work items");
            return AgentResult<List<RallyWorkItem>>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Gets a specific work item by FormattedID (e.g., US12345, DE54321).
    /// </summary>
    public async Task<AgentResult<RallyWorkItem>> GetWorkItemAsync(string formattedId, CancellationToken ct = default)
    {
        try
        {
            var item = await _client.GetWorkItemByIdAsync(formattedId, ct);
            if (item is null)
                return AgentResult<RallyWorkItem>.Fail($"Work item {formattedId} not found", Name);

            return AgentResult<RallyWorkItem>.Ok(item, Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Relay: Failed to get work item {Id}", formattedId);
            return AgentResult<RallyWorkItem>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Gets the current sprint information.
    /// </summary>
    public async Task<AgentResult<RallyIteration>> GetCurrentSprintAsync(CancellationToken ct = default)
    {
        try
        {
            var iteration = await _client.GetCurrentIterationAsync(_config.Project, ct);
            if (iteration is null)
                return AgentResult<RallyIteration>.Fail("No active sprint found", Name);

            return AgentResult<RallyIteration>.Ok(iteration, Name);
        }
        catch (Exception ex)
        {
            return AgentResult<RallyIteration>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Updates the state of a work item in Rally.
    /// </summary>
    public async Task<AgentResult<bool>> UpdateStateAsync(
        RallyWorkItem workItem, string newState, CancellationToken ct = default)
    {
        try
        {
            var success = await _client.UpdateStateAsync(workItem.Ref, newState, ct);
            if (success)
            {
                _logger.LogInformation("Relay: Updated {Id} state to {State}", workItem.FormattedId, newState);
            }
            return success
                ? AgentResult<bool>.Ok(true, Name)
                : AgentResult<bool>.Fail($"Failed to update state for {workItem.FormattedId}", Name);
        }
        catch (Exception ex)
        {
            return AgentResult<bool>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Appends notes to a work item in Rally.
    /// </summary>
    public async Task<AgentResult<bool>> AppendNotesAsync(
        RallyWorkItem workItem, string notes, CancellationToken ct = default)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC");
            var formattedNotes = $"{workItem.Notes}<br/><b>[RAVEN {timestamp}]</b> {notes}";

            var success = await _client.UpdateNotesAsync(workItem.Ref, formattedNotes, ct);
            return success
                ? AgentResult<bool>.Ok(true, Name)
                : AgentResult<bool>.Fail($"Failed to update notes for {workItem.FormattedId}", Name);
        }
        catch (Exception ex)
        {
            return AgentResult<bool>.Fail(ex.Message, Name);
        }
    }

    /// <summary>
    /// Updates the actuals on a work item.
    /// </summary>
    public async Task<AgentResult<bool>> UpdateActualsAsync(
        RallyWorkItem workItem, double hours, CancellationToken ct = default)
    {
        try
        {
            var success = await _client.UpdateActualsAsync(workItem.Ref, hours, ct);
            return success
                ? AgentResult<bool>.Ok(true, Name)
                : AgentResult<bool>.Fail($"Failed to update actuals for {workItem.FormattedId}", Name);
        }
        catch (Exception ex)
        {
            return AgentResult<bool>.Fail(ex.Message, Name);
        }
    }

    public void Dispose() => _client.Dispose();
}


