using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;
using Finale = D47.Core.Tests.Stories.TheFinaleEndsWhereItsFirstChapterSaidTests;

namespace D47.Core.Tests.Adventures;

/// <summary>Each refusal that rests on a measured distance reads word for word: a stop too far, a distance the search has no figure for, and a search that cannot measure.</summary>
public sealed class AMeasuredRefusalKeepsItsWordsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A search failure that is not an outage, so it is neither retried nor stops the write.</summary>
    private const string NoRecord = "The galaxy search has no record of that system.";

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 1, 10, 3);

    private const string Spine = """
        {"name": "The Long Way", "premise": "A way back.", "want": "To get home.",
         "stake": "Whether the ship lasts.", "turn": "The road is shorter.", "ending": "It is done."}
        """;

    private const string Beats = """
        {"opening": "A road.", "reply": "Here it is.", "beats": [
          {"title": "The Arrival", "function": "setup", "kind": "arrive", "reason": "Someone there knows the way.", "system": "Ossen's Lantern", "station": null, "body": null, "career": null, "rank": null, "line": "Here."},
          {"title": "The Turn", "function": "turn", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Turn."},
          {"title": "The End", "function": "resolution", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "Done."}
        ]}
        """;

    private const string SiteBeats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Ruins", "function": "setup", "kind": "site", "reason": "The survey flagged a structure there.", "system": "Dyson's Hollow", "body": "Dyson's Hollow 3 b", "filter": "guardian", "line": "Stone older than the colonies."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private static readonly BodySummary GuardianBody = new()
    {
        Name = "Dyson's Hollow 3 b",
        SystemName = "Dyson's Hollow",
        SystemAddress = AdventureFixtures.Hollow,
        BodyId = 9,
        Distance = 12,
        IsLandable = true,
    };

    [Fact]
    public async Task AFinaleChapterThatEndsNoCloser()
    {
        var beats = Finale.Beats(Finale.Arrive("Kestrel"));

        var outcome = await WriteFinale(Finale.Ask(finale: 2, destination: Finale.DeepMoon), "Kestrel", "Deep Moon", () => 2700, beats, Repairs.Replacing(beats, 1), Repairs.Replacing(beats, 1));

        Assert.Equal(
            "The story names places that cannot stand: The chapter starts 2500 light years from the finale's destination in Deep Moon and its last stop is 2700 light years from it; its last stop must be closer to the destination than where it starts.",
            outcome.Refusal);
    }

    [Fact]
    public async Task AFinaleChapterWhoseLastStopTheSearchCannotMeasure()
    {
        var beats = Finale.Beats(Finale.Arrive("Kestrel"));

        var outcome = await WriteFinale(Finale.Ask(finale: 2, destination: Finale.DeepMoon), "Kestrel", "Deep Moon", Unmeasured, beats, Repairs.Replacing(beats, 1), Repairs.Replacing(beats, 1));

        Assert.Equal(
            "The story names places that cannot stand: The chapter's last stop could not be measured against the finale's destination: The galaxy search has no record of that system.",
            outcome.Refusal);
    }

    [Fact]
    public async Task AFinaleChapterWhoseLastStopHasNoDistancePasses()
    {
        var outcome = await WriteFinale(Finale.Ask(finale: 2, destination: Finale.DeepMoon), "Kestrel", "Deep Moon", () => null, Finale.Beats(Finale.Arrive("Kestrel")));

        Assert.True(outcome.Succeeded, outcome.Refusal);
    }

    [Fact]
    public async Task AFinaleDestinationTheSearchCannotMeasure()
    {
        var beats = Finale.Beats(Finale.Arrive("Kestrel"), destination: ("Far Moon", "Far Moon 1 a"));

        var outcome = await WriteFinale(Finale.Ask(finale: 1), "Oppi", "Far Moon", Unmeasured, beats, Repairs.ReplacingDestination(beats), Repairs.ReplacingDestination(beats));

        Assert.Equal(
            "The story names places that cannot stand: The finale's destination could not be measured: The galaxy search has no record of that system.",
            outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopOutsideTheBand()
    {
        var outcome = await WriteBeyondTheBubble(() => 550);

        Assert.Equal(
            "The story names places that cannot stand: The chapter starts 588 light years from Sol and its last stop is 550 light years from Sol; its last stop must be between 288 and 488 light years from Sol.",
            outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopTheSearchCannotMeasureAgainstSol()
    {
        var outcome = await WriteBeyondTheBubble(Unmeasured);

        Assert.Equal(
            "The story names places that cannot stand: The chapter's last stop could not be measured against Sol: The galaxy search has no record of that system.",
            outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopWithNoDistanceFromSol()
    {
        var outcome = await WriteBeyondTheBubble(() => null);

        Assert.Equal(
            "The story names places that cannot stand: The chapter's last stop could not be measured against Sol.",
            outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopNotFarEnoughHome()
    {
        var outcome = await WriteHome(() => 2000);

        Assert.Equal(
            "The story names places that cannot stand: The chapter starts 3000 light years from Sol and its last stop is 2000 light years from it; the story is heading home, so its last stop must be at least 1125 light years nearer Sol than the start, or within 1000 light years of it.",
            outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopTheSearchCannotMeasureAgainstHome()
    {
        var outcome = await WriteHome(Unmeasured);

        Assert.Equal(
            "The story names places that cannot stand: The chapter's last stop could not be measured against Sol: The galaxy search has no record of that system.",
            outcome.Refusal);
    }

    [Fact]
    public async Task ALastStopWithNoDistanceFromHome()
    {
        var outcome = await WriteHome(() => null);

        Assert.Equal(
            "The story names places that cannot stand: The chapter's last stop could not be measured against Sol.",
            outcome.Refusal);
    }

    [Fact]
    public async Task ASiteBeyondTheReach()
    {
        var outcome = await WriteSite(() => 500);

        Assert.Equal(
            "The story names places that cannot stand: Objective 1 (The Ruins) is 500 light years from the previous stop; the reach is 80.",
            outcome.Refusal);
    }

    [Fact]
    public async Task ASiteTheSearchCannotMeasure()
    {
        var outcome = await WriteSite(Unmeasured);

        Assert.Equal(
            "The story names places that cannot stand: Objective 1 (The Ruins) could not be measured: The galaxy search has no record of that system.",
            outcome.Refusal);
    }

    [Fact]
    public async Task APlaceTheSearchCannotMeasure()
    {
        var provider = Provider(AdventureGeneratorTests.Spine, AdventureGeneratorTests.GoodBeats, 1, 2);
        var galaxy = new Measuring(new AdventureGeneratorTests.Galaxy(), "Ossen's Lantern", Unmeasured);

        var outcome = await Generator(provider, galaxy, AdventureGeneratorTests.State(1, [])).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.Equal(
            "The story names places that cannot stand: Objective 1 (The Lantern) could not be measured: The galaxy search has no record of that system.",
            outcome.Refusal);
    }

    private static double? Unmeasured() => throw new GalaxyUnavailableException(NoRecord);

    private static Task<AdventureOutcome> WriteFinale(AdventureAsk ask, string from, string to, Func<double?> distance, params string[] beats)
    {
        var provider = new RoundScriptedLlmProvider([Saying(Finale.Spine), .. beats.Select(beat => Saying(beat))]);
        var state = State($$"""{ "timestamp":"2026-10-01T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[3.68750,-44.62500,131.00000], "Docked":false }""", jump: true);

        return Generator(provider, new Measuring(new Finale.Galaxy(), to, distance, from), state).GenerateAsync(ask, Now, CancellationToken.None);
    }

    private static Task<AdventureOutcome> WriteBeyondTheBubble(Func<double?> distance) =>
        Generator(Provider(Spine, Beats, 1, 1), new Measuring(new AChapterOutsideTheBubbleWorksTowardSolTests.Galaxy(), "Sol", distance), StateAt(588))
            .GenerateAsync(new AdventureAsk(Reach: AdventureReach.NearHere, Story: Chapter), Now, CancellationToken.None);

    private static Task<AdventureOutcome> WriteHome(Func<double?> distance) =>
        Generator(Provider(Spine, Beats, 1, 1), new Measuring(new AStoryInTheBlackHeadsHomeAfterTwoChangedChaptersTests.Galaxy(), "Sol", distance), StateAt(3000))
            .GenerateAsync(new AdventureAsk(Reach: AdventureReach.Anywhere, Story: new AdventureStory("s", "S", "public", "hidden", 2, 10, 3) { Home = "Sol" }), Now, CancellationToken.None);

    private static Task<AdventureOutcome> WriteSite(Func<double?> distance) =>
        Generator(Provider(AdventureGeneratorTests.Spine, SiteBeats, 1, 1), new Measuring(new AdventureGeneratorTests.Galaxy { GuardianBodies = [GuardianBody] }, "Dyson's Hollow", distance), AdventureGeneratorTests.State(1, []))
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    /// <summary>The spine, the beats, then the same beats written again in each of two repair rounds with objective <paramref name="first"/> to <paramref name="last"/> replaced.</summary>
    private static RoundScriptedLlmProvider Provider(string spine, string beats, int first, int last)
    {
        int[] replaced = [.. Enumerable.Range(first, last - first + 1)];

        return new RoundScriptedLlmProvider(Saying(spine), Saying(beats), Saying(Repairs.Replacing(beats, replaced)), Saying(Repairs.Replacing(beats, replaced)));
    }

    private static CommanderGameState StateAt(double lightYears) =>
        State($$"""{ "timestamp":"2026-10-01T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[{{lightYears}},0,0], "Docked":false }""", jump: false);

    private static CommanderGameState State(string location, bool jump)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-01T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     jump
                         ? """{ "timestamp":"2026-10-01T11:00:01Z", "event":"Loadout", "Ship":"sidewinder", "ShipID":1, "MaxJumpRange":30, "Modules":[] }"""
                         : """{ "timestamp":"2026-10-01T11:00:01Z", "event":"Rank", "Combat":1, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     location,
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    private static AdventureGenerator Generator(ILlmProvider provider, IGalaxyService galaxy, CommanderGameState state) =>
        new(() => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => null, null, null, NullLogger.Instance);

    /// <summary>A galaxy whose distance to <paramref name="to"/>, from <paramref name="from"/> where it is given, comes from <paramref name="distance"/>; every other call goes to <paramref name="inner"/>.</summary>
    private sealed class Measuring(IGalaxyService inner, string to, Func<double?> distance, string? from = null) : IGalaxyService
    {
        public Task<double?> DistanceAsync(string start, string there, CancellationToken cancellationToken)
        {
            if (!string.Equals(there, to, StringComparison.OrdinalIgnoreCase) || (from is not null && !string.Equals(start, from, StringComparison.OrdinalIgnoreCase)))
            {
                return inner.DistanceAsync(start, there, cancellationToken);
            }

            try
            {
                return Task.FromResult(distance());
            }
            catch (GalaxyUnavailableException ex)
            {
                return Task.FromException<double?>(ex);
            }
        }

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken) => inner.SearchAsync(query, cancellationToken);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) => inner.FindStationsAsync(query, cancellationToken);

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) => inner.FindBodiesAsync(query, cancellationToken);

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            inner.ScanForColonisationAsync(query, cancellationToken);

        public Task<MissionStationScan> FindMissionStationsAsync(MissionStationQuery query, CancellationToken cancellationToken) =>
            inner.FindMissionStationsAsync(query, cancellationToken);

        public Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            inner.SystemBodiesAsync(systemAddress, cancellationToken);

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            inner.SystemBiologyAsync(systemAddress, cancellationToken);
    }
}
