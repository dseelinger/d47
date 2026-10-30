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

/// <summary>"Read my last log entry" and its kin are read by the grammar, answered from the store and never reach the model.</summary>
public class ReadCaptainsLogEntriesBackByVoiceTests
{
    private const string Commander = "F100";

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 9, 30, 0, TimeSpan.Zero);

    private sealed class Bench
    {
        public Bench()
        {
            Store = new CaptainsLogStore(
                Path.Combine(@"C:\d47-test", "data", "captains-log.json"), new MemoryFileSystem(), NullLogger<CaptainsLogStore>.Instance);

            Add(1, new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero), "Sol", "Refuel at Sol before Colonia");
            Add(2, new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero), "Colonia", "Colonia has a Guardian beacon");
            Add(3, new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), "Shinrarta Dezhra", "Jameson Memorial is the first stop");
            Add(4, new DateTimeOffset(2026, 10, 21, 7, 0, 0, TimeSpan.Zero), "Sol", "Scan the beacon at Sol");
            Add(5, new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero), "Sol", "Colonia route plotted");

            State = new CommanderGameState(new CommanderIdentity(Commander, "Kestrel Vane"));
            State.Apply(Event("""{"event":"LoadGame","Commander":"Kestrel Vane"}""", new DateTimeOffset(2026, 10, 21, 7, 30, 0, TimeSpan.Zero)));

            Registry = CapabilityRegistry.Build(
            [
                CaptainsLogCapability.Create(Store, () => Commander, () => State, () => new GameStatus(), () => Now),
            ]);
            Model = FakeLlmProvider.Answering("From the model.");

            Loop = new TurnLoop(
                Registry,
                new KeywordRouter(Registry),
                new LlmAvailabilityState(providerConfigured: true),
                new SpendTracker(),
                PriceTable.Default,
                NullLogger<TurnLoop>.Instance,
                Model,
                clock: new InstantClock());

            Loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        }

        public CaptainsLogStore Store { get; }

        public CommanderGameState State { get; }

        public CapabilityRegistry Registry { get; }

        public FakeLlmProvider Model { get; }

        public TurnLoop Loop { get; }

        private void Add(int number, DateTimeOffset at, string system, string text) =>
            Store.Add(Commander, new CaptainsLogEntry($"id{number}", text, at) { System = system });

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

    private static JournalEvent Event(string json, DateTimeOffset at)
    {
        var root = JsonDocument.Parse(json).RootElement;
        return new JournalEvent(at, root.GetProperty("event").GetString()!, root);
    }

    [Fact]
    public async Task TheLastEntryIsReadWithItsDateAndSystem()
    {
        var bench = new Bench();

        var said = await bench.SaidAsync("read my last log entry");

        Assert.Equal("October twenty-first thirty-three twelve, in Sol: Colonia route plotted", said);
        Assert.Equal(0, bench.Model.CallCount);
    }

    [Theory]
    [InlineData("read today's log entries", "Colonia route plotted|Scan the beacon at Sol")]
    [InlineData("read yesterday's log entries", "Jameson Memorial is the first stop")]
    [InlineData("read this week's log entries", "Colonia route plotted|Scan the beacon at Sol|Jameson Memorial|and one more. Ask for a shorter period, a system or a word to hear them")]
    [InlineData("read this session's log entries", "Colonia route plotted")]
    [InlineData("read log entries from sol", "Colonia route plotted|Scan the beacon at Sol|Refuel at Sol")]
    [InlineData("read log entries in Colonia", "Colonia has a Guardian beacon")]
    [InlineData("captain's log entries from Sol", "Colonia route plotted|Scan the beacon at Sol|Refuel at Sol")]
    [InlineData("read log entries about colonia", "Colonia route plotted|Colonia has a Guardian beacon|Refuel at Sol before Colonia")]
    [InlineData("read log entries about beacon sol", "Scan the beacon at Sol")]
    public async Task EachPhraseReadsTheEntriesTheStoreHoldsForItNewestFirst(string input, string expected)
    {
        var bench = new Bench();

        var lines = (await bench.SaidAsync(input)).Split('\n');
        var wanted = expected.Split('|');

        Assert.Equal(wanted.Length, lines.Length);

        for (var i = 0; i < wanted.Length; i++)
        {
            Assert.Contains(wanted[i], lines[i], StringComparison.Ordinal);
        }

        Assert.Equal(0, bench.Model.CallCount);
    }

    [Fact]
    public async Task MoreThanThreeEndWithACountPointingAtThePage()
    {
        var bench = new Bench();

        bench.Store.Add(Commander, new CaptainsLogEntry("id6", "More from Sol", Now) { System = "Sol" });
        var lines = (await bench.SaidAsync("read log entries from sol")).Split('\n');

        Assert.Equal(4, lines.Length);
        Assert.Equal("and one more. Ask for a shorter period, a system or a word to hear them.", lines[^1]);
        Assert.Contains("More from Sol", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadingTheSessionUsesTheLoadGameTheLedgerHolds()
    {
        var bench = new Bench();

        Assert.Equal(new DateTimeOffset(2026, 10, 21, 7, 30, 0, TimeSpan.Zero), bench.State.CodexLedger.SessionStart);
        Assert.DoesNotContain("Scan the beacon", await bench.SaidAsync("read this session's log entries"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCountIsSaidWithoutReadingAnyEntry()
    {
        var bench = new Bench();

        Assert.Equal("You have five log entries.", await bench.SaidAsync("how many log entries"));
    }

    [Fact]
    public async Task DeletingTheLastEntryNamesItByDateAndFirstWords()
    {
        var bench = new Bench();

        var said = await bench.SaidAsync("delete my last log entry");

        Assert.Equal("Deleted the entry from October twenty-first thirty-three twelve, in Sol: Colonia route plotted.", said);
        Assert.Equal(4, bench.Store.For(Commander).Count);
        Assert.DoesNotContain(bench.Store.For(Commander), entry => entry.Id == "id5");
    }

    [Theory]
    [InlineData("show me my logs")]
    [InlineData("what logs have I written")]
    [InlineData("read my logs")]
    public void ThePhrasesNeedEntryOrEntriesSoTheCommandersLogIsLeftAlone(string input)
    {
        Assert.Null(CaptainsLogPhrase.Read(input));
    }

    [Theory]
    [InlineData("captain's log entries from Sol")]
    [InlineData("read log entries about colonia")]
    public void ASearchIsNotAnEntry(string input)
    {
        Assert.IsType<CaptainsLogReading.Find>(CaptainsLogPhrase.Read(input));
    }

    [Fact]
    public async Task TheExchangeIsRecordedWithoutTheEntriesText()
    {
        var bench = new Bench();

        await bench.SaidAsync("read log entries from sol");
        await bench.SaidAsync("what did I just ask");

        var carried = string.Join(
            ' ',
            bench.Model.LastRequest!.Prompt.History.SelectMany(message => message.Content).OfType<ConversationContent.Text>().Select(part => part.Value));

        Assert.Contains("read three captain's log entries", carried, StringComparison.Ordinal);
        Assert.DoesNotContain("Scan the beacon", carried, StringComparison.Ordinal);
        Assert.DoesNotContain("Refuel at Sol", carried, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CaptainsLogCapability.FindTool)]
    [InlineData(CaptainsLogCapability.DeleteTool)]
    public async Task TheModelIsRefusedBothTools(string tool)
    {
        var bench = new Bench();

        var refused = await bench.Registry.InvokeAsync(
            tool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Equal(5, bench.Store.For(Commander).Count);
    }
}
