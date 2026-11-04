using Microsoft.Extensions.Logging;

namespace D47.Replay;

/// <summary>A warning or worse: the message template only, so no journal text reaches the report.</summary>
internal sealed record LoggedProblem(LogLevel Level, string Category, string Template, string? Exception);

/// <summary>Keeps every warning or worse that any logger it creates is given.</summary>
internal sealed class CollectingLoggerFactory : ILoggerFactory
{
    public List<LoggedProblem> Problems { get; } = [];

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, Problems);

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(string category, List<LoggedProblem> problems) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var template = state is IReadOnlyList<KeyValuePair<string, object?>> values
                && values.FirstOrDefault(pair => pair.Key == "{OriginalFormat}").Value is string format
                    ? format
                    : "(no template)";

            problems.Add(new LoggedProblem(logLevel, category, template, exception?.GetType().Name));
        }
    }
}
