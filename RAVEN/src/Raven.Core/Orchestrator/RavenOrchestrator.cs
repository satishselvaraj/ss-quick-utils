using Microsoft.Extensions.Logging;
using Raven.Core.Agents;
using Raven.Core.Models.Rally;
using Raven.Core.Workflow;

namespace Raven.Core.Orchestrator;

/// <summary>
/// RAVEN Core Orchestrator - coordinates agents through workflows.
/// This is the central engine that routes work through the agent pipeline.
/// </summary>
public sealed class RavenOrchestrator
{
    private readonly Dictionary<string, IAgent> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<RavenOrchestrator> _logger;

    public RavenOrchestrator(ILogger<RavenOrchestrator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers an agent with the orchestrator.
    /// </summary>
    public void RegisterAgent(IAgent agent)
    {
        _agents[agent.Name] = agent;
        _logger.LogDebug("Registered agent: {Name} - {Description}", agent.Name, agent.Description);
    }

    /// <summary>
    /// Gets a registered agent by name.
    /// </summary>
    public T? GetAgent<T>(string name) where T : class, IAgent
    {
        return _agents.TryGetValue(name, out var agent) ? agent as T : null;
    }

    /// <summary>
    /// Runs health checks on all registered agents.
    /// </summary>
    public async Task<List<AgentHealthCheck>> CheckAllAgentsAsync(CancellationToken ct = default)
    {
        var results = new List<AgentHealthCheck>();

        foreach (var agent in _agents.Values)
        {
            var health = await agent.CheckHealthAsync(ct);
            results.Add(health);
        }

        return results;
    }

    /// <summary>
    /// Lists all registered agents.
    /// </summary>
    public IReadOnlyList<IAgent> ListAgents() => _agents.Values.ToList().AsReadOnly();

    /// <summary>
    /// Creates a new workflow context for processing a work item.
    /// </summary>
    public WorkflowContext CreateContext(RallyWorkItem? workItem = null)
    {
        var ctx = new WorkflowContext { WorkItem = workItem };
        _logger.LogInformation("Created workflow context{ForItem}",
            workItem is not null ? $" for {workItem.FormattedId}" : "");
        return ctx;
    }
}
