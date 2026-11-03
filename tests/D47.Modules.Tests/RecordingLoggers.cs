using Microsoft.Extensions.Logging;

namespace D47.Modules.Tests;

/// <summary>Keeps every warning and above, with its exception, for a failure message.</summary>
internal sealed class RecordingLoggers : ILoggerFactory, ILogger
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return [.. _lines];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => this;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        lock (_lines)
        {
            _lines.Add($"{logLevel}: {formatter(state, exception)} {exception}");
        }
    }
}
