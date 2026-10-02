using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>Finale chapter 1 names a landable destination within the finale's hops, and each middle finale chapter ends closer to it.</summary>
public sealed class TheFinaleEndsWhereItsFirstChapterSaidTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    internal const string Spine = """
        {"name": "The Last Light", "premise": "The voice names a place.", "want": "To get there.",
         "stake": "Whether it is real.", "turn": "It was always there.", "ending": "It is reached."}
        """;

    [Fact]
    public async Task AFirstFinaleChapterWithNoDestinationIsRefused()
    {
        var outcome = await Write(Ask(finale: 1), Beats(Arrive("Kestrel")), Beats(Arrive("Kestrel")));

        Assert.False(outcome.Succeeded);
        Assert.Contains("Finale chapter 1 names no destination", outcome.Refusal);
    }

    [Fact]
    public async Task AYearLongFinaleMayNameAMoonThreeThousandLightYearsAway()
    {
        var outcome = await Write(Ask(finale: 1), Beats(Arrive("Kestrel"), destination: ("Far Moon", "Far Moon 1 a")));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(new AdventureDestination(Galaxy.Address("Far Moon"), "Far Moon", 1, "Far Moon 1 a"), outcome.Destination);
    }

    [Fact]
    public async Task AYearLongFinaleMayNotNameAMoonSixThousandLightYearsAway()
    {
        var beats = Beats(Arrive("Kestrel"), destination: ("Too Far", "Too Far 1 a"));
        var outcome = await Write(Ask(finale: 1), beats, beats);

        Assert.False(outcome.Succeeded);
        Assert.Contains("The finale's destination, Too Far 1 a in Too Far, is 6000 light years from the Commander; the finale can cover at most 5400.", outcome.Refusal);
    }

    [Fact]
    public async Task AWeekLongFinaleCoversFiveHops()
    {
        var near = await Write(Ask(finale: 1, finaleChapters: 2), Beats(Arrive("Kestrel"), destination: ("Middle", "Middle 1 a")));
        var beyond = Beats(Arrive("Kestrel"), destination: ("Beyond", "Beyond 1 a"));
        var far = await Write(Ask(finale: 1, finaleChapters: 2), beyond, beyond);

        Assert.True(near.Succeeded, near.Refusal);
        Assert.False(far.Succeeded);
        Assert.Contains("the finale can cover at most 1800", far.Refusal);
    }

    [Fact]
    public async Task ADestinationThatCannotBeLandedOnIsRefused()
    {
        var beats = Beats(Arrive("Kestrel"), destination: ("Far Moon", "Far Moon 1"));
        var outcome = await Write(Ask(finale: 1), beats, beats);

        Assert.False(outcome.Succeeded);
        Assert.Contains("The finale's destination lands on Far Moon 1, which cannot be landed on.", outcome.Refusal);
    }

    [Fact]
    public async Task AMiddleFinaleChapterThatEndsFartherAwayIsRefused()
    {
        var beats = Beats(Arrive("Kestrel"));
        var outcome = await Write(Ask(finale: 2, destination: DeepMoon), beats, beats);

        Assert.False(outcome.Succeeded);
        Assert.Contains(
            "The chapter starts 2500 light years from the finale's destination in Deep Moon and its last stop is 2700 light years from it",
            outcome.Refusal);
    }

    [Fact]
    public async Task AMiddleFinaleChapterThatEndsCloserPasses()
    {
        var outcome = await Write(Ask(finale: 2, destination: DeepMoon), Beats(Arrive("Closer")));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Destination);
    }

    [Fact]
    public async Task TheBriefNamesTheDestinationItsDistanceAndTheChaptersLeft()
    {
        var provider = Provider(Beats(Arrive("Closer")));

        Assert.True((await Generator(provider).GenerateAsync(Ask(finale: 3, destination: DeepMoon), Now, CancellationToken.None)).Succeeded);

        var brief = provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("The story's finale ends on Deep Moon 1 a, in Deep Moon, 2500 light years from the Commander's position. One finale chapter is left after this one.", brief);
        Assert.Contains("about 360 light years, the longest one hop may be. A place farther away is reached over several hops.", brief);
        Assert.DoesNotContain("\"destination\":", brief);
    }

    [Fact]
    public async Task AHopLongerThanTheReachOutsideTheFinaleIsStillRefused()
    {
        var beats = Beats(Arrive("Kestrel"), Land("Edge Moon", "Edge Moon 1 a"));
        var outcome = await Write(Ask(finale: null), beats, beats);

        Assert.False(outcome.Succeeded);
        Assert.Contains("is 900 light years from the previous stop; the reach is 360.", outcome.Refusal);
    }

    internal static readonly AdventureDestination DeepMoon = new(Galaxy.Address("Deep Moon"), "Deep Moon", 1, "Deep Moon 1 a");

    internal static AdventureAsk Ask(int? finale, int finaleChapters = 4, AdventureDestination? destination = null) => new(
        AdventureReach.Session,
        Story: new AdventureStory(
            "the-test-story",
            "The Test Story",
            "A public layer.",
            "A hidden layer.",
            Chapter: 10,
            DaysRunning: 300,
            DaysSinceBeacon: 290,
            Stage: "Finale",
            FinaleChapter: finale,
            FinaleChapters: finaleChapters,
            Destination: destination));

    internal static string Arrive(string system) =>
        $$"""{"title": "To {{system}}", "function": "setup", "kind": "arrive", "system": "{{system}}", "line": "Here."}""";

    internal static string Land(string system, string body) =>
        $$"""{"title": "Down on {{body}}", "function": "finale", "kind": "land", "system": "{{system}}", "body": "{{body}}", "line": "Down."}""";

    internal static string Beats(string first, string? second = null, (string System, string Body)? destination = null) =>
        $$"""{"opening": "Go.", "reply": "Here.", "beats": [{{first}}{{(second is null ? string.Empty : ", " + second)}}]{{(destination is { } place ? $$""", "destination": {"system": "{{place.System}}", "body": "{{place.Body}}"}""" : string.Empty)}}}""";

    internal static RoundScriptedLlmProvider Provider(params string[] beats) =>
        new([RoundScriptedLlmProvider.Saying(Spine), .. beats.Select(beat => RoundScriptedLlmProvider.Saying(beat))]);

    internal static Task<AdventureOutcome> Write(AdventureAsk ask, params string[] beats) =>
        Generator(Provider(beats)).GenerateAsync(ask, Now, CancellationToken.None);

    /// <summary>A generator for a Commander in Oppi whose ship jumps 30 light years, so a session's reach is 360.</summary>
    internal static AdventureGenerator Generator(RoundScriptedLlmProvider provider)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-10-01T11:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-10-01T11:00:01Z", "event":"Loadout", "Ship":"sidewinder", "ShipID":1, "MaxJumpRange":30, "Modules":[] }""",
                     """{ "timestamp":"2026-10-01T11:01:00Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[3.68750,-44.62500,131.00000], "Docked":false }""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
            store.Apply(parsed!);
        }

        var state = store.Active!;
        var galaxy = new Galaxy();

        return new AdventureGenerator(
            () => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => null, null, null, NullLogger.Instance);
    }

    /// <summary>Oppi, stops near it, and moons at set distances; every system has a landable moon "1 a" and a planet "1" that is not.</summary>
    internal sealed class Galaxy : IGalaxyService
    {
        private static readonly string[] Systems = ["Oppi", "Kestrel", "Closer", "Far Moon", "Too Far", "Middle", "Beyond", "Deep Moon", "Edge Moon"];

        private static readonly Dictionary<(string, string), double> Distances = new()
        {
            [("Oppi", "Kestrel")] = 100,
            [("Oppi", "Closer")] = 300,
            [("Oppi", "Far Moon")] = 3000,
            [("Oppi", "Too Far")] = 6000,
            [("Oppi", "Middle")] = 1500,
            [("Oppi", "Beyond")] = 2000,
            [("Oppi", "Deep Moon")] = 2500,
            [("Kestrel", "Deep Moon")] = 2700,
            [("Closer", "Deep Moon")] = 1800,
            [("Oppi", "Edge Moon")] = 1000,
            [("Kestrel", "Edge Moon")] = 900,
        };

        public static long Address(string system) => 1000 + Array.IndexOf(Systems, system);

        public Task<GalaxySearchResult> SearchAsync(GalaxyQuery query, CancellationToken cancellationToken)
        {
            var name = Systems.FirstOrDefault(system => string.Equals(system, query.ReferenceSystem, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(name is null
                ? new GalaxySearchResult(query.ReferenceSystem ?? string.Empty, 0, [])
                : new GalaxySearchResult(name, 1, [new SystemSummary { Name = name, SystemAddress = Address(name), Distance = 0 }]));
        }

        public Task<double?> DistanceAsync(string from, string to, CancellationToken cancellationToken) =>
            Task.FromResult<double?>(
                Distances.TryGetValue((from, to), out var there) ? there
                : Distances.TryGetValue((to, from), out var back) ? back
                : 50);

        public Task<StationSearchResult> FindStationsAsync(StationQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new StationSearchResult(query.ReferenceSystem, 0, []));

        public Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken)
        {
            IReadOnlyList<BodySummary> bodies =
            [
                .. Systems
                    .Where(system => query.SystemNames.Count == 0 || query.SystemNames.Contains(system, StringComparer.OrdinalIgnoreCase))
                    .SelectMany(system => new[]
                    {
                        new BodySummary { Name = $"{system} 1", SystemName = system, SystemAddress = Address(system), BodyId = 0, IsLandable = false },
                        new BodySummary { Name = $"{system} 1 a", SystemName = system, SystemAddress = Address(system), BodyId = 1, IsLandable = true },
                    }),
            ];

            return Task.FromResult(new BodySearchResult(query.ReferenceSystem, bodies.Count, bodies));
        }

        public Task<ColonisationScan> ScanForColonisationAsync(ColonisationQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SystemBiology> SystemBiologyAsync(long systemAddress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
