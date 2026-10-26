using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>After the beacon scan, a long-haul story's chapter may be drawn as a sightseeing chapter, asked for with the sights it may name.</summary>
public sealed class ALongHaulStoryDrawsSightseeingChaptersTests
{
    private sealed class Places(Func<IReadOnlyList<NotablePlace>> near) : INotablePlacesService
    {
        public Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(near());
    }

    private static NotablePlace Sight(string name, string system, double x) => new(name, "Nebulae", system, null, new StarPosition(x, 0, 0));

    private static bool Rolls(string id) => ChapterDraw.Sightsee(CareerWeights.Even, false, ChapterDraw.SightSeed(id, 1));

    private static string StoryThat(bool sightsees) =>
        Enumerable.Range(0, 200).Select(index => $"sight-story-{index}").First(id => Rolls(id) == sightsees);

    private static StoryFixtures AfterTheScan(string id, double x, long credits = 300_000_000, bool scanned = true)
    {
        var fixtures = new StoryFixtures(
            new RoundScriptedLlmProvider(
                RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsElsewhere), RoundScriptedLlmProvider.Saying(NothingObjected)),
            secret: Secret with { Id = id },
            card: Card with { Id = id, Length = StoryPacing.ThreeMonths.Key });

        if (scanned)
        {
            fixtures.Stories.Save("F1", new Story
            {
                Id = Other.Id,
                Title = Other.Title,
                PublicLayer = Other.Describe(),
                Length = StoryPacing.OneYear.Key,
                State = StoryState.Finished,
                PickedAt = Now.AddDays(-200),
                BeaconScanAt = Now.AddDays(-199),
                BeaconSystems = [BeaconAddress],
                StoppedAt = Now.AddDays(-1),
            });
        }

        var state = Loaded(credits);

        state.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"FSDJump", "StarSystem":"Here", "SystemAddress":7, "StarPos":[{{x}},0,0] }"""));
        fixtures.Director.Game = () => state;
        return fixtures;
    }

    private static async Task<AdventureAsk> Asked(StoryFixtures fixtures, string id)
    {
        await fixtures.Director.PickAsync("F1", id, Now, CancellationToken.None);

        return fixtures.Asks[0];
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ADrawnChapterIsAskedForAnywhereWithItsSights()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300), Sight("Cave", "Beta", 400)]);

        var ask = await Asked(fixtures, id);

        Assert.Equal(AdventureReach.Anywhere, ask.Reach);
        Assert.True(ask.Story!.LongHaul);
        Assert.Equal(["Veil", "Cave"], ask.Story.Sights!.Select(sight => sight.Name));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterTheDrawLeavesOutIsNotSightseeing()
    {
        var id = StoryThat(sightsees: false);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300)]);

        Assert.Null((await Asked(fixtures, id)).Story!.Sights);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task WithNotablePlacesOffNoChapterIsSightseeing()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => null;

        Assert.Null((await Asked(fixtures, id)).Story!.Sights);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AnUnreachableCatalogueLeavesTheChapterWrittenWithoutSights()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100);

        fixtures.Director.Places = () => new Places(() => throw new GalaxyUnavailableException("The catalogue is down."));

        var ask = await Asked(fixtures, id);

        Assert.Null(ask.Story!.Sights);
        Assert.NotEmpty(fixtures.Stories.Current("F1")!.Chapters);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task OutsideTheBubbleOnlySightsInTheSolBandAreOffered()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 1000);

        fixtures.Director.Places = () => new Places(() => [Sight("Near", "Alpha", 100), Sight("InBand", "Beta", 800), Sight("Far", "Gamma", 950)]);

        Assert.Equal(["InBand"], (await Asked(fixtures, id)).Story!.Sights!.Select(sight => sight.Name));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AStoryThatIsNotLongHaulNeverSightsees()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100, credits: 1_000);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300)]);

        var ask = await Asked(fixtures, id);

        Assert.Null(ask.Story!.Sights);
        Assert.Equal(AdventureReach.Session, ask.Reach);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AChapterBeforeTheBeaconScanNeverSightsees()
    {
        var id = StoryThat(sightsees: true);
        using var fixtures = AfterTheScan(id, 100, scanned: false);

        fixtures.Director.Places = () => new Places(() => [Sight("Veil", "Alpha", 300)]);

        Assert.Null((await Asked(fixtures, id)).Story!.Sights);
    }
}
