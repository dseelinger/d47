using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Stories;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>The generation turns, driven with a scripted model and a galaxy of five systems.</summary>
public sealed class AdventureGeneratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TheRealPlacesWithinReachAreGatheredForThePlanner()
    {
        var galaxy = new Galaxy();

        var places = await Generator(null!, galaxy).ChapterPlacesAsync(new AdventureAsk(Length: AdventureLength.Short), CancellationToken.None);

        Assert.Contains(places.Stations, station => station.Name == "Maren Anchorage");
        Assert.Contains(places.Stations, station => station.Name == "Lantern Dock");
        Assert.Contains(places.Bodies, body => body.Name == "Ossen's Lantern 2 a");
        var stations = Assert.Single(galaxy.StationQueries, query => query.MaxDistance > 1 && query.Services.Count == 0);
        Assert.Equal("Oppi", stations.ReferenceSystem);
        Assert.Equal(80, stations.MaxDistance);
        var bodies = Assert.Single(galaxy.BodyQueries, query => query.SystemNames.Count == 0 && query.Signal is null);
        Assert.Equal(80, bodies.MaxDistance);
        Assert.True(bodies.Landable);
        var guardian = Assert.Single(galaxy.BodyQueries, query => query.Signal == "Guardian");
        Assert.Equal(80, guardian.MaxDistance);
    }

    [Fact]
    public async Task PeopleAreToldAboutAndNeverMet()
    {
        var ask = PlannedChapterWorld.Ask();
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var lines = provider.Requests[0].Prompt.History[0].Text;
        Assert.Contains("A line never gives the Commander a task.", lines);
        Assert.Contains("never \"ask the clerk\"", lines);

        Assert.Contains("nobody in it can be met, spoken to or watched", AdventureContext.Label);
        Assert.Contains("Asked what to do next, say where the next objective is", AdventureContext.Label);
    }

    /// <summary>Every generation turn asks for no warmth, the repair and the checks included.</summary>
    [Fact]
    public async Task EveryGenerationTurnAsksForNoWarmth()
    {
        const string objection = """{"objections": [{"objective": 1, "quote": "The ledger has another page, the one.", "kind": "action", "reason": "The objective does not confirm a page."}]}""";
        var ask = PlannedChapterWorld.Ask();
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)),
            RoundScriptedLlmProvider.Saying(objection),
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Repair(1)),
            RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));

        var outcome = await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Contains(provider.Requests, request => request.ResponseSchema?.Name == "story_line_repairs");
        Assert.All(provider.Requests, request => Assert.Equal(LlmSampling.Adventure, request.Sampling));
        Assert.Equal(0.0, provider.Requests[0].Sampling.Temperature);
    }

    [Fact]
    public void NoBodyIsScannedAfterALandingOnItOrLandedOnTwice()
    {
        var inputs = PlannerWorld.Inputs(PlannerWorld.Bounty, PlannerWorld.Codex, PlannerWorld.Salvage);
        var bodies = 0;

        for (uint seed = 1; seed <= 60; seed++)
        {
            var onBodies = PlannerWorld.Steps(PlannerWorld.Planned(inputs, PlannerWorld.Places(), seed))
                .Where(step => step.Kind is TriggerKind.Land or TriggerKind.Scan)
                .Select(step => (step.System, step.BodyId))
                .ToList();

            bodies += onBodies.Count;
            Assert.Equal(onBodies.Distinct().Count(), onBodies.Count);
        }

        Assert.True(bodies > 0);
    }

    private static string EngineerBeats(string engineer) => $$"""
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Workshop", "function": "resolution", "kind": "engineer", "reason": "Someone there knows about the burst.", "engineer": "{{engineer}}", "stage": "Unlocked", "line": "She lets you in."}
        ]}
        """;

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

    internal static AdventureGenerator Generator(ILlmProvider provider, IGalaxyService galaxy, int combat = 1, params string[] more) =>
        Generator(provider, galaxy, NullLogger.Instance, combat, more);

    internal static AdventureGenerator Generator(ILlmProvider provider, IGalaxyService galaxy, Microsoft.Extensions.Logging.ILogger logger, int combat = 1, params string[] more)
    {
        var state = State(combat, more);

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
            logger);
    }

    /// <summary>As the first overload, asking for the effort <paramref name="storyEffort"/> names for the model, unless <paramref name="effort"/> overrides it.</summary>
    internal static AdventureGenerator Generator(ILlmProvider provider, IGalaxyService galaxy, Func<string, string, ThinkingEffort?> storyEffort, ThinkingEffort? effort = null)
    {
        var state = State(1, []);

        return new AdventureGenerator(
            () => provider,
            () => "story-model",
            () => "You are the ship.",
            () => "core",
            () => null,
            () => state,
            () => galaxy,
            () => null,
            null,
            null,
            NullLogger.Instance,
            storyEffort)
        {
            Effort = effort,
        };
    }

    /// <summary>As <see cref="Generator(ILlmProvider, IGalaxyService, Microsoft.Extensions.Logging.ILogger, int, string[])"/>, its call limits on <paramref name="time"/>.</summary>
    internal static AdventureGenerator Generator(ILlmProvider provider, IGalaxyService galaxy, Microsoft.Extensions.Logging.ILogger logger, TimeProvider time)
    {
        var state = State(1, []);

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
            logger)
        {
            Time = time,
        };
    }

    /// <summary>As the first overload, with the Commander's journal dated <paramref name="on"/> and in Oppi at <paramref name="starPos"/>, its call limits on <paramref name="time"/>.</summary>
    internal static AdventureGenerator Generator(ILlmProvider provider, IGalaxyService galaxy, DateTimeOffset on, string starPos, TimeProvider? time = null)
    {
        var state = State(1, [], on, starPos);

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
            NullLogger.Instance)
        {
            Time = time ?? TimeProvider.System,
        };
    }

    /// <summary>A Commander in Oppi, where the 2026-08-22 report was made, holding Trade 7.</summary>
    internal static CommanderGameState State(int combat, string[] more, DateTimeOffset? on = null, string starPos = "3.68750,-44.62500,131.00000")
    {
        var day = (on ?? new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero)).ToString("yyyy-MM-dd");
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     $$"""{ "timestamp":"{{day}}T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     $$"""{ "timestamp":"{{day}}T11:00:01Z", "event":"Rank", "Combat":{{combat}}, "Trade":7, "Explore":5, "Soldier":0, "Exobiologist":0, "Empire":1, "Federation":1, "CQC":0 }""",
                     $$"""{ "timestamp":"{{day}}T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[{{starPos}}], "Docked":false }""",
                 }.Concat(more))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    /// <summary>Five systems: here, two within reach, Colonia, and nothing else.</summary>
    internal sealed class Galaxy : StubGalaxyService
    {
        private static readonly Dictionary<string, long> Systems = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Oppi"] = 3382387380970,
            ["Ossen's Lantern"] = AdventureFixtures.Lantern,
            ["Dyson's Hollow"] = AdventureFixtures.Hollow,
            ["Colonia"] = 3238296097059,
            ["IC 2391 Sector MX-T b3-6"] = 13872878396833,
            ["Shinrarta Dezhra"] = 3932277478106,
            ["HIP 97950"] = 3932277478107,
        };

        public List<StationQuery> StationQueries { get; } = [];

        public List<BodyQuery> BodyQueries { get; } = [];

        /// <summary>
        /// Whether stations and bodies carry star positions, about Oppi at x = 3.7: the Lantern 8 light years on, the Hollow 12 and
        /// the two closed systems 30. Off, the chapter planner finds no place it can measure a hop to.
        /// </summary>
        public bool Positioned { get; set; }

        private static StarPosition Along(double lightYears) => new(3.6875 + lightYears, -44.625, 131);

        /// <summary>Stations returned alongside the three every search knows.</summary>
        public IReadOnlyList<StationSummary> Extra { get; init; } = [];

        /// <summary>Bodies a search for the Guardian surface signal returns.</summary>
        public IReadOnlyList<BodySummary> GuardianBodies { get; init; } = [];

        public override Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var reference = query.ReferenceSystem ?? string.Empty;
            var canonical = Systems.Keys.FirstOrDefault(name => string.Equals(name, reference, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(canonical is null
                ? new GalaxySearchResult(reference, 0, [])
                : new GalaxySearchResult(canonical, 1, [new SystemSummary { Name = canonical, SystemAddress = Systems[canonical], Distance = 0 }]));
        }

        public override Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(
                string.Equals(from, "Colonia", StringComparison.OrdinalIgnoreCase) || string.Equals(to, "Colonia", StringComparison.OrdinalIgnoreCase)
                    ? 21886
                    : 12);

        /// <summary>How many station lookups for a named system report the search down before one answers.</summary>
        public int StationOutages { get; set; }

        /// <summary>Whether every station search wider than a named system's reports the search down.</summary>
        public bool WideStationsUnavailable { get; set; }

        public override Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken)
        {
            StationQueries.Add(query);

            if (query.MaxDistance > 1 && WideStationsUnavailable)
            {
                throw new GalaxyUnavailableException("The station search reported a server error.");
            }

            if (query.MaxDistance <= 1 && StationOutages > 0)
            {
                StationOutages--;
                throw new GalaxyUnavailableException("The station search reported a server error.");
            }

            IReadOnlyList<StationSummary> all =
            [
                new() { Name = "Lantern Dock", SystemName = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, MarketId = 1001, Distance = 8, HasLargePad = false, Type = "Outpost", Position = Positioned ? Along(8) : null },
                new() { Name = "Maren Anchorage", SystemName = "Dyson's Hollow", SystemAddress = AdventureFixtures.Hollow, MarketId = AdventureFixtures.Anchorage, Distance = 12, HasLargePad = true, Type = "Coriolis Starport", Position = Positioned ? Along(12) : null },
                new() { Name = "Jameson Memorial", SystemName = "Shinrarta Dezhra", SystemAddress = 3932277478106, MarketId = 128666762, Distance = 30, HasLargePad = true, Type = "Orbis Starport", Position = Positioned ? Along(30) : null },
                new() { Name = "Starter Port", SystemName = "HIP 97950", SystemAddress = 3932277478107, MarketId = 128666763, Distance = 30, HasLargePad = true, Type = "Orbis Starport", Position = Positioned ? Along(30) : null },
                .. Extra,
            ];

            // Within a light year of a named system is the resolver's question; wider is the candidate list.
            var stations = query.MaxDistance <= 1
                ? all.Where(station => string.Equals(station.SystemName, query.ReferenceSystem, StringComparison.OrdinalIgnoreCase)).ToList()
                : all;

            return Task.FromResult(new StationSearchResult(query.ReferenceSystem, stations.Count, stations));
        }

        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            BodyQueries.Add(query);

            if (query.Signal is not null)
            {
                return Task.FromResult(new BodySearchResult(query.ReferenceSystem, GuardianBodies.Count, GuardianBodies));
            }

            IReadOnlyList<BodySummary> all =
            [
                new() { Name = "Ossen's Lantern 2 a", SystemName = "Ossen's Lantern", SystemAddress = AdventureFixtures.Lantern, BodyId = 6, Distance = 8, IsLandable = true, Position = Positioned ? Along(8) : null },
            ];

            var bodies = query.SystemNames.Count > 0
                ? all.Where(body => query.SystemNames.Contains(body.SystemName, StringComparer.OrdinalIgnoreCase)).ToList()
                : all;

            return Task.FromResult(new BodySearchResult(query.ReferenceSystem, bodies.Count, bodies));
        }

        /// <summary>The bodies <see cref="SystemBodiesAsync"/> reports for every system, or null to refuse the call.</summary>
        public IReadOnlyList<BodySummary>? SystemBodies { get; init; }

        /// <summary>The surveyed bodies <see cref="SystemBiologyAsync"/> reports for every system, or null to refuse the call.</summary>
        public IReadOnlyList<SurveyedBody>? SystemBiology { get; init; }

        public override Task<IReadOnlyList<BodySummary>> SystemBodiesAsync(long systemAddress, CancellationToken cancellationToken) =>
            SystemBodies is { } bodies ? Task.FromResult(bodies) : throw new NotSupportedException();

        public override Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            SystemBiology is { } surveyed ? Task.FromResult(new SystemBiology(systemAddress, surveyed)) : throw new NotSupportedException();
    }
}
