using D47.Core.CaptainsLog;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.CaptainsLog;

/// <summary>A captain's log entry is not written to d47's log, in the correction line or in the Listener's predicate.</summary>
public class ALoggedEntryKeepsItsWordsOutOfTheLogTests
{
    private const string Commander = "F100";

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private sealed class CapturingLogger : ILogger<TurnLoop>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }

    private static (TurnLoop Loop, CapturingLogger Log) Build()
    {
        var store = new CaptainsLogStore(
            Path.Combine(@"C:\d47-test", "data", "captains-log.json"), new MemoryFileSystem(), NullLogger<CaptainsLogStore>.Instance);
        var state = new CommanderGameState(new CommanderIdentity(Commander, "Kestrel Vane"));
        var registry = CapabilityRegistry.Build(
        [
            CaptainsLogCapability.Create(store, () => Commander, () => state, () => new GameStatus(), () => Now),
        ]);
        var log = new CapturingLogger();

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            log,
            FakeLlmProvider.Answering("From the model."),
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        loop.Heard = text => text.Replace("beacon", "buoy", StringComparison.Ordinal);

        return (loop, log);
    }

    private static async Task SaidAsync(TurnLoop loop, string input)
    {
        await foreach (var _ in loop.RunAsync(input, InputSource.Spoken, TestContext.Current.CancellationToken))
        {
        }
    }

    [Fact]
    public async Task APhraseLedEntryIsNotInTheCorrectionLine()
    {
        var (loop, log) = Build();

        await SaidAsync(loop, "captain's log the beacon is offline");

        Assert.Contains(log.Lines, line => line.Contains("captain's log entry"));
        Assert.DoesNotContain(log.Lines, line => line.Contains("beacon") || line.Contains("buoy"));
    }

    [Fact]
    public async Task AHeldEntryIsNotInTheCorrectionLine()
    {
        var (loop, log) = Build();

        await SaidAsync(loop, "captain's log");
        await SaidAsync(loop, "the beacon is offline");

        Assert.Contains(log.Lines, line => line.Contains("captain's log entry"));
        Assert.DoesNotContain(log.Lines, line => line.Contains("beacon") || line.Contains("buoy"));
    }

    [Fact]
    public async Task AnyOtherUtteranceIsStillInTheCorrectionLine()
    {
        var (loop, log) = Build();

        await SaidAsync(loop, "request docking at the beacon");

        Assert.Contains(log.Lines, line => line.Contains("request docking at the buoy"));
    }

    [Fact]
    public async Task OnlyAnEntryIsWithheldFromTheListener()
    {
        var (loop, _) = Build();

        Assert.True(loop.IsLogEntry("captain's log the beacon is offline"));
        Assert.False(loop.IsLogEntry("request docking"));
        Assert.False(loop.IsLogEntry("the beacon is offline"));

        await SaidAsync(loop, "captain's log");

        Assert.True(loop.IsLogEntry("request docking"));
    }
}
