using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The model's view of community goals: what it is told with no key, with a key, and when the
/// key is there but the site is not. The assertions that matter are about what is *not* said —
/// an expired goal offered as live, and an external listing quietly presented as knowing
/// something about this Commander.
/// </summary>
public class CommunityGoalCapabilityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-15T10:00:00Z");

    /// <summary>A listing that answers from a script, and records that it was asked.</summary>
    private sealed class FakeListing : ICommunityGoalService
    {
        public bool IsConfigured { get; set; } = true;

        public int Calls { get; private set; }

        public Exception? Throws { get; set; }

        public IReadOnlyList<CommunityGoalListing> Goals { get; set; } =
        [
            new CommunityGoalListing
            {
                Name = "Rescue Operation in the Pleiades",
                SystemName = "Pleiades Sector IR-W d1-55",
                StationName = "The Oracle",
                Expiry = Now.AddDays(2),
                TierReached = 6,
                Contributors = 2038,
                ContributionsTotal = 40001,
                Objective = "Deliver Occupied Escape Pods",
            },
        ];

        public Task<CommunityGoalReport> RecentAsync(CancellationToken cancellationToken)
        {
            Calls++;

            return Throws is not null
                ? Task.FromException<CommunityGoalReport>(Throws)
                : Task.FromResult(new CommunityGoalReport(Goals));
        }
    }

    private static CommanderGameState Commander(params string[] lines)
    {
        var state = new CommanderGameState(new CommanderIdentity("F123", "Jameson"));

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            state.Apply(parsed!);
        }

        return state;
    }

    private static string Board(
        int id = 726,
        string title = "Alliance Research Initiative",
        string expiry = "2026-08-20T14:58:14Z") =>
        $$"""
          { "timestamp":"2026-08-15T09:00:00Z", "event":"CommunityGoal", "CurrentGoals":[
            { "CGID":{{id}}, "Title":"{{title}}", "SystemName":"Kaushpoos",
              "MarketName":"Neville Horizons", "Expiry":"{{expiry}}", "IsComplete":false,
              "CurrentTotal":10062, "PlayerContribution":562, "NumContributors":101,
              "TopTier":{ "Name":"Tier 5", "Bonus":"" }, "TopRankSize":10, "PlayerInTopRank":false,
              "TierReached":"Tier 1", "PlayerPercentileBand":50, "Bonus":200000 } ] }
          """;

    private static CapabilityRegistry Build(CommanderGameState? commander, ICommunityGoalService? listing) =>
        CapabilityRegistry.Build([CommunityGoalCapability.Create(() => commander, listing, () => Now)]);

    private static async Task<ToolResult> Ask(
        CapabilityRegistry registry,
        params (string Name, string Value)[] arguments) =>
        await registry.InvokeAsync(
            "get_community_goals",
            new ToolArguments(arguments.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task WithNoKeyTheJournalStillAnswersAndTheAnswerSaysWhatItIs()
    {
        // A capability that is off is state, not a guard (Phase 3): the journal half
        // needs nothing, so refusing the whole question for want of a key would be the tool
        // dead-ending on a destination it does not need.
        var registry = Build(Commander(Board()), listing: null);

        var result = await Ask(registry);

        Assert.False(result.IsError);
        Assert.Contains("Alliance Research Initiative", result.Content, StringComparison.Ordinal);
        Assert.Contains("Inara API key", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGoalThatHasExpiredIsNotOfferedAsSomethingToFlyFor()
    {
        // The failure this whole capability is shaped around. The board is a snapshot and it
        // routinely arrives days stale, so an ended goal listed among the running ones reads
        // exactly like the feature working.
        var registry = Build(Commander(Board(expiry: "2026-08-11T16:00:00Z")), listing: null);

        var result = await Ask(registry);

        Assert.DoesNotContain("Alliance Research Initiative", result.Content, StringComparison.Ordinal);
        Assert.Contains("No community goals are running", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskingForFinishedGoalsBringsThemBackSayingWhenTheyEnded()
    {
        var registry = Build(Commander(Board(expiry: "2026-08-11T16:00:00Z")), listing: null);

        var result = await Ask(registry, ("include_finished", "true"));

        Assert.Contains("Alliance Research Initiative", result.Content, StringComparison.Ordinal);
        Assert.Contains("ended", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingHeardYetIsNotTheSameAnswerAsNoGoals()
    {
        var registry = Build(Commander(), listing: null);

        var result = await Ask(registry);

        Assert.Contains("no community goals yet", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoJournalAtAllItSaysSoRatherThanReportingAnEmptyBoard()
    {
        var registry = Build(commander: null, listing: null);

        var result = await Ask(registry);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task AStoredKeyBringsInGoalsTheJournalHasNeverSeenUnderTheirOwnHeading()
    {
        var listing = new FakeListing();
        var registry = Build(Commander(Board()), listing);

        var result = await Ask(registry);

        Assert.Equal(1, listing.Calls);
        Assert.Contains("Rescue Operation in the Pleiades", result.Content, StringComparison.Ordinal);

        // Said out loud, because the figures beside an Inara goal are the galaxy's and not the
        // Commander's, and a merged list would invite reading them as theirs.
        Assert.Contains("Nothing here says anything about your own contribution", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoKeyMeansTheSiteIsNeverReached()
    {
        var listing = new FakeListing { IsConfigured = false };
        var registry = Build(Commander(Board()), listing);

        await Ask(registry);

        Assert.Equal(0, listing.Calls);
    }

    [Fact]
    public async Task AGoalTheJournalAlreadyKnowsIsNotListedTwice()
    {
        var listing = new FakeListing
        {
            Goals =
            [
                new CommunityGoalListing { Name = "Alliance Research Initiative", Expiry = Now.AddDays(3) },
            ],
        };

        var registry = Build(Commander(Board()), listing);

        var result = await Ask(registry);

        var first = result.Content.IndexOf("Alliance Research Initiative", StringComparison.Ordinal);

        Assert.True(first >= 0);
        Assert.Equal(
            -1,
            result.Content.IndexOf("Alliance Research Initiative", first + 1, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ANameTheTwoSourcesSpellDifferentlyIsAllowedToAppearTwice()
    {
        // Deliberate. A listing entry carries no CGID, so a name is all there is to match on —
        // and a fuzzy match that merged the wrong two goals would put the Commander's
        // contribution against a goal they have never flown. A duplicate is visible; a wrong
        // merge is not.
        var listing = new FakeListing
        {
            Goals =
            [
                new CommunityGoalListing { Name = "Alliance Research Initiative – Trade", Expiry = Now.AddDays(3) },
            ],
        };

        var registry = Build(Commander(Board()), listing);

        var result = await Ask(registry);

        Assert.Contains("Alliance Research Initiative – Trade", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSiteFailingDoesNotCostTheCommanderTheJournalHalf()
    {
        var listing = new FakeListing
        {
            Throws = new CommunityGoalsUnavailableException("Inara rejected the request. Check the API key."),
        };

        var registry = Build(Commander(Board()), listing);

        var result = await Ask(registry);

        Assert.False(result.IsError);
        Assert.Contains("Alliance Research Initiative", result.Content, StringComparison.Ordinal);
        Assert.Contains("Check the API key", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskingForOneGoalByNameNarrowsToIt()
    {
        var registry = Build(
            Commander(Board(), Board(id: 840, title: "Distant Worlds III")),
            listing: null);

        var result = await Ask(registry, ("name", "distant"));

        Assert.Contains("Distant Worlds III", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Alliance Research", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCommandersOwnStandingIsReported()
    {
        var registry = Build(Commander(Board()), listing: null);

        var result = await Ask(registry);

        Assert.Contains("562", result.Content, StringComparison.Ordinal);
        Assert.Contains("top 50%", result.Content, StringComparison.Ordinal);
        Assert.Contains("tier 1 of 5", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}
