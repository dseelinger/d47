using System.Text.Json;
using D47.Core.CaptainsLog;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Storage;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.CaptainsLog;

/// <summary>"Captain's log" alone holds the next utterance as the entry, until it arrives or two minutes pass.</summary>
public class ABareCaptainsLogHoldsTheNextUtteranceTests
{
    private const string Commander = "F100";

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private sealed class SteppedClock : ITurnClock
    {
        public TimeSpan Now { get; set; }

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask;

        public CancellationTokenSource CreateTimeout(TimeSpan duration, CancellationToken linkedTo) =>
            CancellationTokenSource.CreateLinkedTokenSource(linkedTo);

        public void Restart(CancellationTokenSource timeout, TimeSpan duration)
        {
        }
    }

    private sealed class Bench
    {
        public Bench()
        {
            Store = new CaptainsLogStore(
                Path.Combine(@"C:\d47-test", "data", "captains-log.json"), new MemoryFileSystem(), NullLogger<CaptainsLogStore>.Instance);

            var state = new CommanderGameState(new CommanderIdentity(Commander, "Kestrel Vane"));
            var registry = CapabilityRegistry.Build(
            [
                CaptainsLogCapability.Create(Store, () => Commander, () => state, () => new GameStatus(), () => Now),
            ]);

            Model = FakeLlmProvider.Answering("From the model.");

            Loop = new TurnLoop(
                registry,
                new KeywordRouter(registry),
                new LlmAvailabilityState(providerConfigured: true),
                new SpendTracker(),
                PriceTable.Default,
                NullLogger<TurnLoop>.Instance,
                Model,
                clock: Clock);

            Loop.Retry = RetryPolicy.Default with { Attempts = 1 };
            Loop.ShipAiNames = () => ["D47"];
        }

        public SteppedClock Clock { get; } = new();

        public CaptainsLogStore Store { get; }

        public FakeLlmProvider Model { get; }

        public TurnLoop Loop { get; }

        public async Task<string> SaidAsync(string input)
        {
            var text = new System.Text.StringBuilder();

            await foreach (var turnEvent in Loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
            {
                if (turnEvent is TurnEvent.TextDelta delta)
                {
                    text.Append(delta.Text);
                }
            }

            return text.ToString();
        }
    }

    [Fact]
    public async Task TheNextUtteranceIsSavedWordForWord()
    {
        var bench = new Bench();

        Assert.Equal("Go ahead.", await bench.SaidAsync("captain's log"));
        Assert.Equal("Logged.", await bench.SaidAsync("the beacon is offline"));

        Assert.Equal("the beacon is offline", Assert.Single(bench.Store.For(Commander)).Text);
        Assert.Equal(0, bench.Model.CallCount);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("Never mind.")]
    public async Task CancellingSavesNothing(string cancel)
    {
        var bench = new Bench();

        await bench.SaidAsync("log entry");

        Assert.Equal("Not logged.", await bench.SaidAsync(cancel));
        Assert.Empty(bench.Store.For(Commander));
    }

    [Fact]
    public async Task OnlyOneUtteranceIsHeld()
    {
        var bench = new Bench();

        await bench.SaidAsync("captain's log");
        await bench.SaidAsync("fuel is low");
        await bench.SaidAsync("what is my fuel");

        Assert.Single(bench.Store.For(Commander));
        Assert.Equal(1, bench.Model.CallCount);
    }

    [Fact]
    public async Task AnAddressedLineIsSavedAsTextAndNotActedOn()
    {
        var bench = new Bench();

        await bench.SaidAsync("captain's log");
        await bench.SaidAsync("D47, request docking");

        Assert.Equal("D47, request docking", Assert.Single(bench.Store.For(Commander)).Text);
        Assert.Equal(0, bench.Model.CallCount);
    }

    [Fact]
    public async Task AfterTwoMinutesTheNextUtteranceIsRoutedNormally()
    {
        var bench = new Bench();

        await bench.SaidAsync("captain's log");
        bench.Clock.Now = TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1);

        Assert.Equal("From the model.", await bench.SaidAsync("the beacon is offline"));
        Assert.Empty(bench.Store.For(Commander));
    }

    [Fact]
    public async Task TheHeldWordsNeverReachTheModelsHistory()
    {
        var bench = new Bench();

        await bench.SaidAsync("captain's log");
        await bench.SaidAsync("the vault code is nine nine four");
        await bench.SaidAsync("what did I just say");

        var carried = string.Join(
            ' ',
            bench.Model.LastRequest!.Prompt.History.SelectMany(message => message.Content).OfType<ConversationContent.Text>().Select(part => part.Value));

        Assert.DoesNotContain("vault code", carried, StringComparison.Ordinal);
    }
}
