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

/// <summary>"Captain's log, …" is read by the grammar and kept word for word, with no model call.</summary>
public class SayCaptainsLogAndTheEntryIsKeptTests
{
    private const string Commander = "F100";

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private sealed class Bench
    {
        public Bench(bool flying = true)
        {
            Store = new CaptainsLogStore(
                Path.Combine(@"C:\d47-test", "data", "captains-log.json"), Files, NullLogger<CaptainsLogStore>.Instance);

            var state = new CommanderGameState(new CommanderIdentity(Commander, "Kestrel Vane"));
            state.Apply(Event("""{"event":"Location","StarSystem":"Blae","SystemAddress":1,"Body":"Blae 7 a","BodyID":12,"BodyType":"Planet"}"""));
            state.Apply(Event("""{"event":"Loadout","Ship":"anaconda","ShipName":"Wanderer","ShipIdent":"WN-01","ShipID":4,"Modules":[]}"""));

            var status = new GameStatus { BodyName = "Blae 7 a", Latitude = 12.5, Longitude = -40.25 };

            var registry = CapabilityRegistry.Build(
            [
                CaptainsLogCapability.Create(Store, () => flying ? Commander : string.Empty, () => state, () => status, () => Now),
            ]);

            Registry = registry;
            Model = FakeLlmProvider.Answering("From the model.");

            Loop = new TurnLoop(
                registry,
                new KeywordRouter(registry),
                new LlmAvailabilityState(providerConfigured: true),
                new SpendTracker(),
                PriceTable.Default,
                NullLogger<TurnLoop>.Instance,
                Model,
                clock: new InstantClock());

            Loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        }

        public MemoryFileSystem Files { get; } = new();

        public CaptainsLogStore Store { get; }

        public CapabilityRegistry Registry { get; }

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

    private static JournalEvent Event(string json)
    {
        var root = JsonDocument.Parse(json).RootElement;
        return new JournalEvent(Now, root.GetProperty("event").GetString()!, root);
    }

    [Fact]
    public async Task TheEntryIsKeptAsSaidWithWhereAndWhatWasFlown()
    {
        var bench = new Bench();

        var said = await bench.SaidAsync("Captain's log, the Guardian site is two kilometres north of the landing pad");

        var entry = Assert.Single(bench.Store.For(Commander));
        Assert.Equal("the Guardian site is two kilometres north of the landing pad", entry.Text);
        Assert.Equal(Now, entry.At);
        Assert.Equal("Blae", entry.System);
        Assert.Equal("Blae 7 a", entry.Body);
        Assert.Null(entry.Station);
        Assert.Equal("Wanderer", entry.ShipName);
        Assert.Equal(12.5, entry.Latitude);
        Assert.Equal(-40.25, entry.Longitude);
        Assert.Equal("Logged.", said);
        Assert.Equal(0, bench.Model.CallCount);
    }

    [Theory]
    [InlineData("log entry: fuel is low", "fuel is low")]
    [InlineData("Captains log, fuel is low", "fuel is low")]
    [InlineData("captain log: fuel is low", "fuel is low")]
    [InlineData("captain’s log entry, fuel is low", "fuel is low")]
    public async Task EveryTranscriptionOfTheOpeningSavesTheSameWay(string input, string text)
    {
        var bench = new Bench();

        await bench.SaidAsync(input);

        Assert.Equal(text, Assert.Single(bench.Store.For(Commander)).Text);
    }

    [Theory]
    [InlineData("captain's log entries from last week")]
    [InlineData("captain's logistics")]
    public void ReadingBackIsNotTakenAsAnEntry(string input)
    {
        Assert.IsNotType<CaptainsLogReading.Entry>(CaptainsLogPhrase.Read(input));
    }

    [Fact]
    public async Task TheOpeningAloneIsNotSavedAndAnswersGoAhead()
    {
        var bench = new Bench();

        var said = await bench.SaidAsync("captain's log");

        Assert.Empty(bench.Store.For(Commander));
        Assert.Equal("Go ahead.", said);
        Assert.Equal(0, bench.Model.CallCount);
    }

    [Fact]
    public async Task NobodyFlyingMeansNothingIsSavedAndTheRefusalIsSpoken()
    {
        var bench = new Bench(flying: false);

        var said = await bench.SaidAsync("captain's log, hello");

        Assert.Equal("Nobody is flying, so there is nobody to log for.", said);
        Assert.Null(bench.Files.ReadText(bench.Store.Path));
        Assert.Equal(0, bench.Model.CallCount);
    }

    [Fact]
    public async Task TheNextPromptHoldsThePhraseAndNotTheWords()
    {
        var bench = new Bench();

        await bench.SaidAsync("captain's log, the vault code is nine nine four");
        await bench.SaidAsync("what did I just say");

        var carried = string.Join(
            ' ',
            bench.Model.LastRequest!.Prompt.History[0].Content.OfType<ConversationContent.Text>().Select(part => part.Value));

        Assert.Contains("<said-aloud>", carried, StringComparison.Ordinal);
        Assert.Contains("captain's log entry", carried, StringComparison.Ordinal);
        Assert.DoesNotContain("vault code", carried, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheModelIsRefusedAddingAnEntry()
    {
        var bench = new Bench();

        var result = await bench.Registry.InvokeAsync(
            CaptainsLogCapability.AddTool,
            new ToolArguments(new Dictionary<string, string> { ["text"] = "planted" }),
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.Empty(bench.Store.For(Commander));
    }

    [Fact]
    public async Task EntriesSurviveARestart()
    {
        var bench = new Bench();

        await bench.SaidAsync("log entry: fuel is low");

        var reopened = new CaptainsLogStore(bench.Store.Path, bench.Files, NullLogger<CaptainsLogStore>.Instance);
        reopened.Load();

        Assert.Equal("fuel is low", Assert.Single(reopened.For(Commander)).Text);
    }
}
