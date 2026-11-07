using D47.Core.Activities;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Activities;

public sealed class VoiceAnswersForWhenAnActivityWasLastDoneTests
{
    private const string Fid = "F123";

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryInstall _install = new();

    private static string Line(string at, string kind, string extra = "") =>
        $"{{\"timestamp\":\"{at}\",\"event\":\"{kind}\"{extra}}}";

    private (TestSurface Surface, ActivityLedger Ledger) Open(bool folded = true, params (string Kind, string At)[] done)
    {
        var lines = new List<string> { Line("2024-01-01T00:00:00Z", "LoadGame", $",\"FID\":\"{Fid}\"") };
        lines.AddRange(done.Select(item => Line(item.At, item.Kind)));

        var file = Path.Combine(_install.Paths.Data, "Journal.test.log");
        _install.Files.CreateFolder(_install.Paths.Data);
        _install.Files.WriteLines(file, lines);

        var ledger = new ActivityLedger(_install.Files, NullLogger.Instance);

        if (folded)
        {
            ledger.FoldHistory([file], TestContext.Current.CancellationToken);
        }

        var state = new GameStateStore();

        Assert.True(JournalEvent.TryParse(
            Line("2024-01-01T00:00:00Z", "Commander", $",\"FID\":\"{Fid}\",\"Name\":\"Tester\""),
            NullLogger.Instance,
            out var commander));
        state.Apply(commander!);
        Assert.Equal(Fid, state.Active?.Identity.FrontierId);

        return (TestSurface.For(_install, state, activities: ledger, now: () => Now), ledger);
    }

    private static readonly (string Kind, string At)[] Four =
    [
        ("SearchAndRescue", "2025-02-02T00:00:00Z"),
        ("PowerplayMerits", "2025-06-01T00:00:00Z"),
        ("FactionKillBond", "2025-09-01T00:00:00Z"),
        ("MiningRefined", "2026-09-02T00:00:00Z"),
    ];

    private static async Task<ToolResult> Run(
        TestSurface surface,
        string tool,
        Dictionary<string, string>? arguments = null,
        ToolCaller caller = ToolCaller.Commander) =>
        await surface.Registry.InvokeAsync(
            tool, new ToolArguments(arguments ?? []), TestContext.Current.CancellationToken, caller);

    [Fact]
    public async Task ADatedActivitySaysTheGalacticDateAndTheAge()
    {
        var (surface, _) = Open(done: Four);

        var result = await Run(surface, "get_activity_last_done", new() { ["activity"] = "mining" });

        Assert.Equal("Mining, last on 2 September 3312. Four weeks ago.", result.Content);
    }

    [Fact]
    public async Task AnUndatedActivityIsNotInTheJournalsSinceTheyBegan()
    {
        var (surface, _) = Open(done: Four);

        var result = await Run(surface, "get_activity_last_done", new() { ["activity"] = "ship-launched fighter" });

        Assert.Equal("Ship-launched fighter: not in your journals since 1 January 3310.", result.Content);
    }

    [Fact]
    public async Task AnActivityOutsideTheCatalogueIsSaidNotToBeKept()
    {
        var (surface, _) = Open(done: Four);

        var result = await Run(surface, "get_activity_last_done", new() { ["activity"] = "racing" });

        Assert.Equal("I don't keep a date for racing. The Activities page lists the fourteen I do.", result.Content);
    }

    [Fact]
    public async Task UntilTheHistoryIsFoldedTheReadSaysSo()
    {
        var (surface, _) = Open(folded: false, Four);

        var result = await Run(surface, "get_activity_last_done", new() { ["activity"] = "mining" });

        Assert.Equal("I'm still reading your journals. Ask again in a minute.", result.Content);
    }

    [Fact]
    public void TheSuggestingToolsAreGone()
    {
        var (surface, _) = Open(done: Four);
        var names = surface.Registry.Find(ActivitiesCapability.Id)!.Descriptor.Tools.Select(tool => tool.Name);

        Assert.Equal(["get_activity_last_done"], names);
    }

    [Theory]
    [InlineData("What's something I haven't done in a while")]
    [InlineData("what haven't I done in a while")]
    [InlineData("don't suggest combat zones")]
    [InlineData("suggest bounty hunting again")]
    public void NoActivitiesToolAnswersTheSuggestingPhrases(string said)
    {
        var (surface, _) = Open(done: Four);

        Assert.Null(surface.Router.Match(said));
        Assert.Null(surface.Router.MatchToolCommand(said));
    }

    [Fact]
    public void WhenDidILastGoMiningReachesTheToolWithNoModel()
    {
        var (surface, _) = Open(done: Four);

        var match = surface.Router.MatchToolCommand("when did I last go mining");

        Assert.NotNull(match);
        Assert.Equal("get_activity_last_done", match.ToolName);
        Assert.True(match.Arguments.TryGetString("activity", out var key));
        Assert.Equal("mining", key);
    }

    [Theory]
    [InlineData("2026-10-06T01:00:00Z", "today")]
    [InlineData("2026-09-23T10:00:00Z", "thirteen days ago")]
    [InlineData("2026-09-08T10:00:00Z", "four weeks ago")]
    [InlineData("2026-04-06T10:00:00Z", "six months ago")]
    [InlineData("2025-10-06T10:00:00Z", "a year ago")]
    [InlineData("2025-09-06T10:00:00Z", "a year and one month ago")]
    [InlineData("2024-10-06T10:00:00Z", "two years ago")]
    public void AgesAreSaidInWords(string at, string expected) =>
        Assert.Equal(
            expected,
            ActivityAge.SayInWords(DateTimeOffset.Parse(at, null, System.Globalization.DateTimeStyles.AssumeUniversal), Now));
}
