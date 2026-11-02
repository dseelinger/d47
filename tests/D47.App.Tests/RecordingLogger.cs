using Microsoft.Extensions.Logging;

namespace D47.App.Tests;

/// <summary>Keeps each formatted line, followed by its exception when there is one.</summary>
internal class RecordingLogger : ILogger
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

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_lines)
        {
            _lines.Add(formatter(state, exception) + exception);
        }
    }
}

internal sealed class RecordingLogger<T> : RecordingLogger, ILogger<T>;
