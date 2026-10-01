using Microsoft.Extensions.Logging;

namespace Raven.Core.Logging;

/// <summary>
/// Shared logger extension for creating typed child loggers.
/// Used by agents that need to pass loggers to their internal API clients.
/// </summary>
public static class LoggerExtensions
{
    public static ILogger<T> CreateChildLogger<T>(this ILogger logger)
    {
        return new WrappedLogger<T>(logger);
    }

    private sealed class WrappedLogger<T>(ILogger inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
