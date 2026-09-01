using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// The generation turns, driven with a scripted model and a galaxy of five systems. What these
/// hold is the shape of the ask rather than the prose: that the model is handed real places to
/// build on, that the refusal pass is shown the draft it is fixing, and that a rank beat is read
/// however the model wrote it.
/// </summary>
public sealed class AdventureGeneratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Unrecoverable Column", "premise": "A ledger will not balance.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The column balances."}
        """;

    /// <summary>Three beats, every place real and within reach, the career written as the person rather than the ladder.</summary>
    private const string GoodBeats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "station": null, "body": null, "career": null, "rank": null, "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "body": null, "career": null, "rank": null, "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    /// <summary>The same story with its middle beat a Colonia-distance hop on a "near here" ask.</summary>
    private const string FarBeats = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "station": null, "body": null, "career": null, "rank": null, "line": "Scoop here."},
          {"title": "Where The Freight Went", "function": "turn", "kind": "arrive", "system": "Colonia", "station": null, "body": null, "career": null, "rank": null, "line": "Twenty-two thousand light years."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "system": null, "station": null, "body": null, "career": "Trade", "rank": 8, "line": "It balances."}
        ]}
        """;

    [Fact]
    public async Task TheModelIsHandedTheRealPlacesWithinReach()
    {
        var galaxy = new Galaxy();
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(GoodBeats));

        var outcome = await Generator(provider, galaxy).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(2, provider.CallCount);

        // Both turns see the same list: the spine is anchored to real places before the beats are.
        foreach (var request in provider.Requests)
        {
            var instruction = request.Prompt.History[0].Text;
            Assert.Contains("Real places within reach", instruction);
            Assert.Contains("- Dyson's Hollow (12 ly): stations Maren Anchorage (large pad)", instruction);
            Assert.Contains("- Ossen's Lantern (8 ly): stations Lantern Dock (no large pad); landable bodies Ossen's Lantern 2 a", instruction);
        }

        // Nearest first, whichever list a system came from.
        var beats = provider.Requests[1].Prompt.History[0].Text;
        Assert.True(beats.IndexOf("- Ossen's Lantern (8 ly)", StringComparison.Ordinal) < beats.IndexOf("- Dyson's Hollow (12 ly)", StringComparison.Ordinal));

        // The lists were asked for within the reach, and the bodies had to be landable.
        var stations = Assert.Single(galaxy.StationQueries, query => query.MaxDistance > 1);
        Assert.Equal("Oppi", stations.ReferenceSystem);
        Assert.Equal(80, stations.MaxDistance);
        var bodies = Assert.Single(galaxy.BodyQueries, query => query.SystemNames.Count == 0);
        Assert.Equal(80, bodies.MaxDistance);
        Assert.True(bodies.Landable);
    }

    /// <summary>
    /// Both turns are told that invented people are told about and never met, and that a line
    /// never sets the Commander a task. The first story flown from the field ended its first beat
    /// with "ask the clerk who countersigns" (2026-08-22), which Elite has no act for. Wording can
    /// only be held by its presence; whether the model obeys it is read by hand.
    /// </summary>
    [Fact]
    public async Task PeopleAreToldAboutAndNeverMet()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(GoodBeats));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var spine = provider.Requests[0].Prompt.History[0].Text;
        Assert.Contains("Invented people are told about, never met", spine);
        Assert.Contains("cannot find, speak to or watch anyone", spine);

        var beats = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("A line never gives the Commander a task.", beats);
        Assert.Contains("never \"ask the clerk\"", beats);

        Assert.Contains("nobody in it can be met, spoken to or watched", AdventureContext.Label);
        Assert.Contains("Asked what to do next, say where the next beat is", AdventureContext.Label);
    }

    /// <summary>
    /// <b>Every generation turn asks for no warmth</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/98">#98</a>), the refusal pass included.
    /// A story is the one output here that looks creative and is not: the beats are validated
    /// against the real galaxy and re-asked where they cannot stand, so this call's failure is
    /// naming places that do not exist. Variety comes from the systems within reach.
    /// <para>
    /// The refusal pass is not endangered by asking the same question the same way twice, because
    /// it is not the same question: it goes back with the refusals <em>and</em> the beats that
    /// stood, which is what the test below holds.
    /// </para>
    /// </summary>
    [Fact]
    public async Task EveryGenerationTurnAsksForNoWarmth()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(FarBeats),
            RoundScriptedLlmProvider.Saying(GoodBeats));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);

        Assert.All(
            provider.Requests,
            request => Assert.Equal(LlmSampling.Adventure, request.Sampling));

        Assert.Equal(0.0, provider.Requests[0].Sampling.Temperature);
    }

    [Fact]
    public async Task TheRefusalPassIsShownTheDraftItIsFixing()
    {
        var galaxy = new Galaxy();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(FarBeats),
            RoundScriptedLlmProvider.Saying(GoodBeats));

        var outcome = await Generator(provider, galaxy).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);
        Assert.Contains(outcome.Notes, note => note.StartsWith("Rewrote 1 beat", StringComparison.Ordinal));

        var retry = provider.Requests[2].Prompt.History[0].Text;
        Assert.Contains("Your previous draft of the beats:", retry);
        Assert.Contains("1. The Lantern (setup) — arrive: Ossen's Lantern — \"Scoop here.\"", retry);
        Assert.Contains("2. Where The Freight Went (turn) — arrive: Colonia", retry);
        Assert.Contains("3. The Column Will Not Balance (resolution) — rank: Trade 8", retry);
        Assert.Contains("Beat 2 (Where The Freight Went) is 21886 light years from the previous stop; the reach is 80.", retry);
        Assert.Contains("Keep the beats that were not refused", retry);

        // What survived is the retry's draft, not the first one.
        Assert.Equal(["The Lantern", "The Anchorage", "The Column Will Not Balance"], outcome.Draft!.Beats.Select(beat => beat.Title));
    }

    /// <summary>
    /// A scan of a body the story has already landed on is refused in the dry run, so it goes back
    /// through the turn with the reason rather than reaching the Commander as a story that cannot
    /// finish — the shape the first story flown had (2026-08-22).
    /// </summary>
    [Fact]
    public async Task AScanAfterALandingOnTheSameBodyGoesBackThroughTheTurn()
    {
        const string landThenScan = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
              {"title": "The Consignee", "function": "turn", "kind": "land", "system": "Ossen's Lantern", "body": "Ossen's Lantern 2 a", "line": "Dust."},
              {"title": "Disposition", "function": "resolution", "kind": "scan", "system": "Ossen's Lantern", "body": "Ossen's Lantern 2 a", "line": "Four hundred tonnes."}
            ]}
            """;

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(landThenScan),
            RoundScriptedLlmProvider.Saying(GoodBeats));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);
        Assert.Contains(
            "Beat 3 (Disposition) scans Ossen's Lantern 2 a after Beat 2 (The Consignee) lands on it; a body is scanned on the way in, before any landing, so the scan must come before the landing or be of another body.",
            provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ACareerIsReadAsTheLadderHoweverTheModelSaidIt()
    {
        var galaxy = new Galaxy();
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(GoodBeats));

        var outcome = await Generator(provider, galaxy).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        var rank = outcome.Draft!.Beats[2].Trigger;
        Assert.Equal(TriggerKind.Rank, rank.Kind);
        Assert.Equal("Trade", rank.Career);
        Assert.Equal(8, rank.Rank);
    }

    [Fact]
    public async Task ARankBeatNestedUnderItsOwnKeyStillReads()
    {
        const string nested = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
              {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "rank": {"career": "Trade", "rank": "8"}, "line": "It balances."}
            ]}
            """;

        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(nested));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("Trade", outcome.Draft!.Beats[1].Trigger.Career);
        Assert.Equal(8, outcome.Draft.Beats[1].Trigger.Rank);
    }

    [Fact]
    public async Task ARankBeatWithNoCareerIsRefusedInWordsTheModelCanActOn()
    {
        const string careerless = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
              {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "rank": 8, "line": "It balances."}
            ]}
            """;

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(careerless),
            RoundScriptedLlmProvider.Saying(careerless));

        var outcome = await Generator(provider, new Galaxy()).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("Beat 2 (The Column Will Not Balance) is a rank beat but names no career; \"career\" must be one of Combat, Trade, Exploration, Mercenary, Exobiology, CQC.", outcome.Refusal);
        Assert.DoesNotContain("\"\"", outcome.Refusal);
    }

    [Fact]
    public async Task APromotionWhereTheCommanderIsAlreadyEliteIsRefusedAsSuch()
    {
        const string elite = """
            {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
              {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
              {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Combat", "rank": 8, "line": "It balances."}
            ]}
            """;

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(elite),
            RoundScriptedLlmProvider.Saying(elite));

        var outcome = await Generator(provider, new Galaxy(), combat: 8).GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("where the Commander is already Elite; make it another career or another kind of beat", outcome.Refusal);
    }

    [Fact]
    public void CareersMatchThePersonAsWellAsTheLadder()
    {
        Assert.Equal("Trade", Careers.Match("Trader"));
        Assert.Equal("Trade", Careers.Match("trading."));
        Assert.Equal("Explore", Careers.Match("Explorer"));
        Assert.Equal("Explore", Careers.Match("Exploration"));
        Assert.Equal("Soldier", Careers.Match("Mercenary"));
        Assert.Equal("Exobiologist", Careers.Match("Exobiology"));
        Assert.Null(Careers.Match("Smuggler"));
        Assert.Null(Careers.Match(""));
    }

    private static AdventureGenerator Generator(ILlmProvider provider, Galaxy galaxy, int combat = 1)
    {
        var state = State(combat);

        return new AdventureGenerator(
            () => provider,
            () => null,
            () => "You are the ship.",
            () => "core",
            () => null,
            () => state,
            () => galaxy,
            () => null,
            null,
            null,
            NullLogger.Instance);
    }

    /// <summary>A Commander in Oppi, where the 2026-08-22 report was made, holding Trade 7.</summary>
    private static CommanderGameState State(int combat)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-22T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     $$"""{ "timestamp":"2026-08-22T11:00:01Z", "event":"Rank", "Combat":{{combat}}, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     """{ "timestamp":"2026-08-22T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[3.68750,-44.62500,131.00000], "Docked":false }""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    /// <summary>
    /// Five systems: here, two within reach, Colonia, and nothing else. Distances are what the
    /// report measured — anything involving Colonia is 21,886 light years, anything else is 12.
    /// </summary>
    private sealed class Galaxy : IGalaxyService
    {
        private static readonly Dictionary<string, long> Systems = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Oppi"] = 3382387380970,
            ["Ossen's Lantern"] = AdventureFixtures.Lantern,
            ["Dyson's Hollow"] = AdventureFixtures.Hollow,
            ["Colonia"] = 3238296097059,
        };

        public List<StationQuery> StationQueries { get; } = [];

        public List<BodyQuery> BodyQueries { get; } = [];

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var reference = query.ReferenceSystem ?? string.Empty;
            var canonical = Systems.Keys.FirstOrDefault(name => string.Equals(name, reference, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(canonical is null
                ? new GalaxySearchResult(reference, 0, [])
                : new GalaxySearchResult(canonical, 1, [new SystemSummary { Name = canonical, SystemAddress = Systems[canonical], Distance = 0 }]));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(
                string.Equals(from, "Colonia", StringComparison.OrdinalIgnoreCase) || string.Equals(to, "Colonia", StringComparison.OrdinalIgnoreCase)
                    ? 21886
                    : 12);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            StationQueries.Add(query);

            IReadOnlyList<StationSummary> all =
            [
                new() { Name = "Lantern Dock", SystemName = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, MarketId = 1001, Distance = 8, HasLargePad = false },
                new() { Name = "Maren Anchorage", SystemName = "Dyson's Hollow", SystemAddress = AdventureFixtures.Hollow, MarketId = AdventureFixtures.Anchorage, Distance = 12, HasLargePad = true },
            ];

            // Within a light year of a named system is the resolver's question; wider is the candidate list.
            var stations = query.MaxDistance <= 1
                ? all.Where(station => string.Equals(station.SystemName, query.ReferenceSystem, StringComparison.OrdinalIgnoreCase)).ToList()
                : all;

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, stations.Count, stations));
        }

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            BodyQueries.Add(query);

            IReadOnlyList<BodySummary> all =
            [
                new() { Name = "Ossen's Lantern 2 a", SystemName = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, BodyId = 6, Distance = 8, IsLandable = true },
            ];

            var bodies = query.SystemNames.Count > 0
                ? all.Where(body => query.SystemNames.Contains(body.SystemName, StringComparer.OrdinalIgnoreCase)).ToList()
                : all;

            return Task.FromResult(new BodySearchResult(query.ReferenceSystem, bodies.Count, bodies));
        }

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
