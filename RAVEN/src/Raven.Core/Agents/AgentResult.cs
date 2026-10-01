namespace Raven.Core.Agents;

/// <summary>
/// Standard result wrapper for all agent operations.
/// </summary>
public sealed class AgentResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public string AgentName { get; init; } = string.Empty;

    public static AgentResult<T> Ok(T data, string agentName) => new()
    {
        Success = true,
        Data = data,
        AgentName = agentName
    };

    public static AgentResult<T> Fail(string error, string agentName) => new()
    {
        Success = false,
        Error = error,
        AgentName = agentName
    };
}
