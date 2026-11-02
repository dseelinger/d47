using Microsoft.Extensions.Logging;

namespace D47.Vr.Tests;

/// <summary>Appends each formatted line to the list it is given.</summary>
internal sealed class RecordingLogger<T>(List<string> lines) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        lines.Add(formatter(state, exception));
}
