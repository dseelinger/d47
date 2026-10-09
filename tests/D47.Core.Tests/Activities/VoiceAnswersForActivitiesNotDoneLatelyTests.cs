using D47.Core.Activities;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Activities;

[Trait("Category", "Integration")]
public sealed class VoiceAnswersForActivitiesNotDoneLatelyTests : IDisposable
{
    private const string Fid = "F123";

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly TempInstall _install = new();

    public void Dispose() => _install.Dispose();

    private static string Line(string at, string kind, string extra = "") =>
        $"{{\"timestamp\":\"{at}\",\"event\":\"{kind}\"{extra}}}";

    private (TestSurface Surface, ActivityLedger Ledger) Open(bool folded = true, params (string Kind, string At)[] done)
    {
        var lines = new List<string> { Line("2024-01-01T00:00:00Z", "LoadGame", $",\"FID\":\"{Fid}\"") };
        lines.AddRange(done.Select(item => Line(item.At, item.Kind)));

        var file = Path.Combine(_install.Paths.Data, "Journal.test.log");
        Directory.CreateDirectory(_install.Paths.Data);
        File.WriteAllLines(file, lines);

        var ledger = new ActivityLedger(Path.Combine(_install.Paths.Data, "activities.json"), _install.Files, NullLogger.Instance);

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
    public async Task ThreeStaleActivitiesAreNamedOldestFirstWithTheirAges()
    {
        var (surface, _) = Open(done: Four);

        var result = await Run(surface, "get_stale_activities");

        Assert.Equal(
            "Search and rescue, a year and eight months ago. Powerplay, a year and four months ago. "
            + "Combat zones, a year and one month ago. The rest are on the Activities page.",
            result.Content);
    }

    [Fact]
    public async Task FewerThanThreeSaysHowManyThereAre()
    {
        var (surface, _) = Open(done: [Four[0], Four[3]]);

        var result = await Run(surface, "get_stale_activities");

        Assert.Equal(
            "Only two I can suggest. Search and rescue, a year and eight months ago. Mining, four weeks ago.",
            result.Content);
    }

    [Fact]
    public async Task OneActivityIsSaidInTheSingular()
    {
        var (surface, _) = Open(done: [Four[3]]);

        Assert.Equal("Only one I can suggest. Mining, four weeks ago.", (await Run(surface, "get_stale_activities")).Content);
    }

    [Fact]
    public async Task WhenEveryDatedActivityIsSwitchedOffNothingIsSuggested()
    {
        var (surface, ledger) = Open(done: [Four[3]]);

        ledger.SetSuggested("mining", Fid, false);

        Assert.Equal(
            "Nothing to suggest. Every activity I can date is switched off. The Activities page has them all.",
            (await Run(surface, "get_stale_activities")).Content);
    }

    [Fact]
    public async Task WhenNothingIsDatedTheAnswerSaysSo()
    {
        var (surface, _) = Open();

        Assert.StartsWith(
            "Nothing to suggest. None of your activities has a date",
            (await Run(surface, "get_stale_activities")).Content);
    }

    [Fact]
    public async Task ASwitchedOffActivityIsAbsentFromTheSuggestionsButStillDated()
    {
        var (surface, ledger) = Open(done: Four);

        ledger.SetSuggested("search-and-rescue", Fid, false);

        var stale = (await Run(surface, "get_stale_activities")).Content;
        var dated = (await Run(surface, "get_activity_last_done", new() { ["activity"] = "search and rescue" })).Content;

        Assert.DoesNotContain("Search and rescue", stale);
        Assert.Equal("Search and rescue, last on 2 February 3311. A year and eight months ago.", dated);
    }

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

    [Theory]
    [InlineData("get_stale_activities", null)]
    [InlineData("get_activity_last_done", "mining")]
    public async Task UntilTheHistoryIsFoldedBothReadsSaySo(string tool, string? activity)
    {
        var (surface, _) = Open(folded: false, Four);

        var result = await Run(surface, tool, activity is null ? null : new() { ["activity"] = activity });

        Assert.Equal("I'm still reading your journals. Ask again in a minute.", result.Content);
    }

    [Fact]
    public async Task SwitchingOffAndBackOnAreSaidAndRemembered()
    {
        var (surface, ledger) = Open(done: Four);

        var off = await Run(surface, "set_activity_suggested", new() { ["activity"] = "bounty hunting", ["suggested"] = "false" });
        var again = await Run(surface, "set_activity_suggested", new() { ["activity"] = "bounty hunting", ["suggested"] = "false" });

        Assert.Equal("Not suggesting: bounty hunting. It stays on the Activities page, with its date.", off.Content);
        Assert.Equal("Bounty hunting is already off the suggestions.", again.Content);
        Assert.False(ledger.IsSuggested("bounty-hunting", Fid));

        var on = await Run(surface, "set_activity_suggested", new() { ["activity"] = "bounty hunting", ["suggested"] = "true" });
        var onAgain = await Run(surface, "set_activity_suggested", new() { ["activity"] = "bounty hunting", ["suggested"] = "true" });

        Assert.Equal("Back in the suggestions: bounty hunting.", on.Content);
        Assert.Equal("Bounty hunting is already in the suggestions.", onAgain.Content);
        Assert.True(ledger.IsSuggested("bounty-hunting", Fid));
    }

    [Fact]
    public async Task TheModelIsRefusedTheSuggestionAndToldWhatToSay()
    {
        var (surface, ledger) = Open(done: Four);

        var refused = await Run(
            surface,
            "set_activity_suggested",
            new() { ["activity"] = "mining", ["suggested"] = "false" },
            ToolCaller.Model);

        Assert.True(refused.IsError);
        Assert.Contains("Say \"don't suggest combat zones\" and it is done.", refused.Content);
        Assert.True(ledger.IsSuggested("mining", Fid));

        var accepted = await Run(surface, "set_activity_suggested", new() { ["activity"] = "mining", ["suggested"] = "false" });

        Assert.False(accepted.IsError);
        Assert.False(ledger.IsSuggested("mining", Fid));
    }

    [Theory]
    [InlineData("What's something I haven't done in a while")]
    [InlineData("what haven't I done in a while")]
    public void AskingWhatIsStaleReachesTheToolWithNoModel(string said)
    {
        var (surface, _) = Open(done: Four);

        var match = surface.Router.Match(said);

        Assert.NotNull(match);
        Assert.Equal(ActivitiesCapability.Id, match.CapabilityId);
        Assert.Equal("get_stale_activities", match.ToolName);
    }

    [Theory]
    [InlineData("when did I last go mining", "get_activity_last_done", "mining", null)]
    [InlineData("don't suggest combat zones", "set_activity_suggested", "combat-zones", "false")]
    [InlineData("don't suggest bounty hunting anymore", "set_activity_suggested", "bounty-hunting", "false")]
    [InlineData("suggest bounty hunting again", "set_activity_suggested", "bounty-hunting", "true")]
    public void ThePhrasesReachTheirToolsWithNoModel(string said, string tool, string activity, string? suggested)
    {
        var (surface, _) = Open(done: Four);

        var match = surface.Router.MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(tool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("activity", out var key));
        Assert.Equal(activity, key);

        if (suggested is not null)
        {
            Assert.True(match.Arguments.TryGetString("suggested", out var value));
            Assert.Equal(suggested, value);
        }
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
