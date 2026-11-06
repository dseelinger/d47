using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Journal;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

/// <summary>The unvisited Guardian sites, tourist beacons and generation ships within reach are gathered for the chapter planner, which may stop a travelling chapter at one.</summary>
public sealed class ASiteWithinReachIsOfferedToTheStoryWriterTests
{
    private const long FarReach = 2870300000002;

    private static readonly BodySummary GuardianBody = new()
    {
        Name = "Fixture Beta 3 b",
        SystemName = "Fixture Beta",
        SystemAddress = 3,
        BodyId = 9,
        Distance = 20,
        Position = new StarPosition(120, 0, 0),
        IsLandable = true,
    };

    private static readonly string Tsv = string.Join(
        "\n",
        SignalIndex.Header,
        "3\tFixture Beta\t120\t0\t0\tTouristBeacon\tLantern Watch\t2026-10-10",
        $"{FarReach}\tFar Reach\t900\t0\t0\tTouristBeacon\tToo Far\t2026-10-10") + "\n";

    private static readonly string GenerationTsv = string.Join(
        "\n",
        SignalIndex.Header,
        "3\tFixture Beta\t120\t0\t0\tMegaship\tGeneration Ship Lycaon\t2026-10-01",
        "3\tFixture Beta\t120\t0\t0\tMegaship\tGeneration Ship Lycaon\t2026-10-10",
        "3\tFixture Beta\t120\t0\t0\tMegaship\tMegaship Cobra Wanderer\t2026-10-10") + "\n";

    private static readonly uint[] Seeds = [.. Enumerable.Range(1, 40).Select(seed => (uint)seed)];

    [Fact]
    public async Task OnlyALandableGuardianBodyIsOffered()
    {
        var galaxy = new PlannedChapterWorld.Galaxy();
        galaxy.GuardianBodies.AddRange([GuardianBody with { Name = "Fixture Beta 4", IsLandable = false }, GuardianBody]);

        var places = await Places(galaxy, index: null);

        Assert.Contains(galaxy.BodyQueries, query => query.Signal == "Guardian" && query.Landable == true);
        var site = Assert.Single(places.Sites);
        Assert.Equal((AdventureTrigger.GuardianSite, "Fixture Beta 3 b", 9), (site.Filter, site.Name, site.BodyId));
    }

    [Fact]
    public async Task WithOnlyNonLandableGuardianBodiesNoSiteIsOffered()
    {
        var galaxy = new PlannedChapterWorld.Galaxy();
        galaxy.GuardianBodies.Add(GuardianBody with { IsLandable = false });

        Assert.Empty((await Places(galaxy, index: null)).Sites);
    }

    [Fact]
    public async Task WithNoSiteTheIndexNoteIsLogged()
    {
        var logger = new RecordingLogger();

        var places = await Places(new PlannedChapterWorld.Galaxy(), index: null, logger: logger);

        Assert.Empty(places.Sites);
        Assert.Contains(logger.Lines, line => line.Contains("The signal index is off, so no tourist beacon or generation ship was offered.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATouristBeaconWithinReachIsOfferedAndOneBeyondItIsNot()
    {
        var places = await Places(new PlannedChapterWorld.Galaxy(), index: Source(Tsv));

        var site = Assert.Single(places.Sites);
        Assert.Equal((AdventureTrigger.TouristSite, "Fixture Beta"), (site.Filter, site.System));
    }

    [Fact]
    public async Task AGenerationShipIsListedOnceAndOtherMegashipsAreNot()
    {
        var places = await Places(new PlannedChapterWorld.Galaxy(), index: Source(GenerationTsv));

        var site = Assert.Single(places.Sites);
        Assert.Equal((AdventureTrigger.GenerationSite, "Generation Ship Lycaon"), (site.Filter, site.Name));
    }

    [Fact]
    public async Task ASiteInAVisitedSystemIsNotOffered()
    {
        const string root = "C:/d47-test/visits";
        var files = new MemoryFileSystem();
        files.WriteBytes(
            Path.Combine(root, "4700", VisitedStarsCache.FileName),
            VisitedStarsCacheIsReadFromItsBytesTests.File(512, [(3L, 2, VisitedStarsCacheIsReadFromItsBytesTests.Second)]));
        var galaxy = new PlannedChapterWorld.Galaxy();
        galaxy.GuardianBodies.Add(GuardianBody);

        var places = await Places(galaxy, index: Source(Tsv), visited: new VisitedStarsBook(root, files));

        Assert.Empty(places.Sites);
    }

    [Fact]
    public async Task WithTheIndexOffNothingIsRequestedFromIt()
    {
        var source = Source(Tsv);

        await Places(new PlannedChapterWorld.Galaxy(), index: source, indexOn: false);

        Assert.Equal(0, source.Requests);
    }

    [Fact]
    public void AListedTouristBeaconStandsOnItsSystemWithNoName()
    {
        var site = new ChapterSite(AdventureTrigger.TouristSite, "Fixture Beta", 3, new StarPosition(120, 0, 0), "Lantern Watch");

        Assert.All(Stops(site), step => Assert.Equal(new AdventureTrigger { Kind = TriggerKind.Site, Filter = AdventureTrigger.TouristSite, SystemAddress = 3, System = "Fixture Beta" }, step));
    }

    [Fact]
    public void AListedGenerationShipStandsOnItsSystemAndName()
    {
        var site = new ChapterSite(AdventureTrigger.GenerationSite, "Fixture Beta", 3, new StarPosition(120, 0, 0), "Generation Ship Lycaon");

        Assert.All(Stops(site), step => Assert.Equal(new AdventureTrigger { Kind = TriggerKind.Site, Filter = AdventureTrigger.GenerationSite, SystemAddress = 3, System = "Fixture Beta", SiteName = "Generation Ship Lycaon" }, step));
    }

    [Fact]
    public async Task TheFidelityCheckReadsAGuardianSiteObjectiveAsTheGuardianSiteAlone()
    {
        var galaxy = new PlannedChapterWorld.Galaxy();
        galaxy.GuardianBodies.Add(GuardianBody);

        var confirms = await FidelityConfirms(galaxy, index: null);

        Assert.Contains("Guardian site on that body", confirms);
        Assert.DoesNotContain("tourist", confirms, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheFidelityCheckReadsATouristSiteObjectiveAsTheBeaconScanAlone()
    {
        var confirms = await FidelityConfirms(new PlannedChapterWorld.Galaxy(), index: Source(Tsv));

        Assert.Contains("tourist beacon in that system was scanned", confirms);
        Assert.DoesNotContain("Guardian", confirms);
    }

    [Fact]
    public void TheWritersSectionNamesBothFiltersOfASiteOnce()
    {
        var section = TriggerConfirms.Section(["site"]);

        Assert.Single(section.Split('\n'), line => line.StartsWith("- \"site\":", StringComparison.Ordinal));
        Assert.Contains("\"guardian\" filter", section);
        Assert.Contains("\"tourist\" filter", section);
    }

    private static IEnumerable<AdventureTrigger> Stops(ChapterSite site)
    {
        var inputs = PlannerWorld.Inputs() with
        {
            Beacon = new AdventureBeacon(13872878396833, "IC 2391 Sector MX-T b3-6", EasierWith: 18),
            Stage = StoryStage.ActOne,
        };
        var places = PlannerWorld.Places() with { Sites = [site] };

        var steps = Seeds.SelectMany(seed => PlannerWorld.Steps(PlannerWorld.Planned(inputs, places, seed))).Where(step => step.Kind == TriggerKind.Site).ToList();

        Assert.NotEmpty(steps);

        return steps;
    }

    private static AdventureAsk Travelling(int chapter) => PlannedChapterWorld.Ask(
        new AdventureStory("fixture-story", "Fixture Story", "Public", "Hidden", Chapter: chapter, DaysRunning: 0, DaysSinceBeacon: null, Stage: "Act One")
        {
            Beacon = new AdventureBeacon(13872878396833, "IC 2391 Sector MX-T b3-6", EasierWith: 18),
        });

    /// <summary>The "Confirms" line the fidelity check is given for the site objective of a planned travelling chapter.</summary>
    private static async Task<string> FidelityConfirms(PlannedChapterWorld.Galaxy galaxy, Index? index)
    {
        for (var chapter = 1; chapter <= 60; chapter++)
        {
            var ask = Travelling(chapter);
            var places = await Generator(null, galaxy, index).ChapterPlacesAsync(ask, TestContext.Current.CancellationToken);
            var plan = ChapterPlanner.Plan(ChapterInputs.Of(PlannedChapterWorld.State(), ask), places, AdventureGenerator.PlanSeed(ask)).Plan;

            if (plan is null || !PlannerWorld.Steps(plan).Any(step => step.Kind == TriggerKind.Site))
            {
                continue;
            }

            var provider = new RoundScriptedLlmProvider(Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), Saying(PlannedChapterWorld.NoObjections));
            var outcome = await Generator(provider, galaxy, index).GenerateAsync(ask, new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);

            Assert.True(outcome.Succeeded, outcome.Refusal);

            var prompt = provider.Requests.First(request => request.ResponseSchema?.Name == "story_fidelity").Prompt.History[0].Text;

            return prompt.Split('\n')
                .Where(line => line.TrimStart().StartsWith("Confirms:", StringComparison.Ordinal))
                .First(line => line.Contains("site", StringComparison.OrdinalIgnoreCase) || line.Contains("beacon", StringComparison.OrdinalIgnoreCase));
        }

        throw new InvalidOperationException("No seed planned a site step.");
    }

    private static Task<ChapterPlaces> Places(PlannedChapterWorld.Galaxy galaxy, Index? index, bool indexOn = true, VisitedStarsBook? visited = null, ILogger? logger = null) =>
        Generator(null, galaxy, index, indexOn, visited, logger).ChapterPlacesAsync(PlannedChapterWorld.Ask(), TestContext.Current.CancellationToken);

    private static AdventureGenerator Generator(ILlmProvider? provider, PlannedChapterWorld.Galaxy galaxy, Index? index, bool indexOn = true, VisitedStarsBook? visited = null, ILogger? logger = null)
    {
        var state = PlannedChapterWorld.State();

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
            logger ?? NullLogger.Instance,
            signalIndex: () => indexOn ? index : null,
            visited: visited);
    }

    private static Index Source(string tsv) => new(SignalIndex.Read(new StringReader(tsv)));

    private sealed class Index(SignalIndex index) : ISignalIndexSource
    {
        public int Requests { get; private set; }

        public Task<SignalIndex?> GetAsync(CancellationToken cancellationToken)
        {
            Requests++;

            return Task.FromResult<SignalIndex?>(index);
        }
    }
}
