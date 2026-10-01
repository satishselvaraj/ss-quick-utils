namespace Raven.Core.Agents;

/// <summary>
/// Base interface for all RAVEN agents.
/// Each agent is a specialized unit that performs a specific category of work.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Agent identifier (e.g., "relay", "scout", "forge").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of the agent's purpose.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Validates that the agent's required configuration is present.
    /// </summary>
    Task<AgentHealthCheck> CheckHealthAsync(CancellationToken ct = default);
}

public sealed record AgentHealthCheck(
    string AgentName,
    bool IsHealthy,
    string? Message = null);
