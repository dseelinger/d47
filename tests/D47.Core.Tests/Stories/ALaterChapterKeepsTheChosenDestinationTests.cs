using D47.Core.Adventures;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Storage;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

public sealed class ALaterChapterKeepsTheChosenDestinationTests
{
    private static readonly AdventureHeading Chosen = new("Tau Ceti", 1234, new StarPosition(5300, 0, 0));

    private static readonly AdventureHeading Colonia = new("Colonia", 99, ExpeditionKit.Colonia, Home: true);

    [Fact]
    public void TheDestinationSurvivesReopeningTheStoryFile()
    {
        var files = new MemoryFileSystem();
        var path = "story.json";

        StoryStore.Open(path, files, NullLogger<StoryStore>.Instance).Save("F1", new Story { Id = Id, Title = Card.Title, PublicLayer = Card.Describe(), ChosenDestination = Chosen });
        StoryStore.Open(path, files, NullLogger<StoryStore>.Instance).Save("F2", new Story { Id = Id, Title = Card.Title, PublicLayer = Card.Describe() });

        var reopened = StoryStore.Open(path, files, NullLogger<StoryStore>.Instance);
        Assert.Equal(Chosen, reopened.Current("F1")!.ChosenDestination);
        Assert.Null(reopened.Current("F2")!.ChosenDestination);
    }

    [Fact]
    public async Task ANextChapterIsAskedToHeadForItAndTheBandAndHomeLegStandDown()
    {
        using var fixtures = At(new StarPosition(3000, 0, 0), 7, Chosen, homeTarget: HomeLeg.Sol);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        var asked = Assert.Single(fixtures.Asks).Story!;
        Assert.Equal(Chosen, asked.Chosen);
        Assert.Null(asked.Home);
        Assert.Equal(Chosen, fixtures.Stories.Current("F1")!.ChosenDestination);
        Assert.Equal(AdventureReach.Anywhere, fixtures.Asks[0].Reach);
    }

    [Fact]
    public async Task AChapterBuiltInTheDestinationsSystemClearsItAndIsWrittenWithoutOne()
    {
        using var fixtures = At(new StarPosition(5300, 0, 0), Chosen.SystemAddress, Chosen);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        Assert.Null(Assert.Single(fixtures.Asks).Story!.Chosen);
        Assert.Null(fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    [Fact]
    public async Task AHomeDestinationClearsWithinAThousandLightYearsOfIt()
    {
        using var fixtures = At(new StarPosition(ExpeditionKit.Colonia.X + 900, ExpeditionKit.Colonia.Y, ExpeditionKit.Colonia.Z), 5, Colonia);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        Assert.Null(fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    [Fact]
    public async Task ASpanWithItsOwnDestinationIgnoresItAndKeepsIt()
    {
        var secret = Secret with { Black = [new StoryBlackSpan("breakIntoTwo", "midpoint", HomeLeg.Sol)] };
        using var fixtures = At(new StarPosition(3000, 0, 0), 7, Chosen, secret);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        Assert.Null(Assert.Single(fixtures.Asks).Story!.Chosen);
        Assert.Equal(Chosen, fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    [Fact]
    public async Task ABeaconChapterIgnoresItAndKeepsIt()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon), RoundScriptedLlmProvider.Saying(NothingObjected)));
        fixtures.Director.Game = () => Flying(7.6, scoop: true);
        fixtures.Stories.Save("F1", new Story { Id = Id, Title = Card.Title, PublicLayer = Card.Describe(), PickedAt = Now.AddDays(-1), ChosenDestination = Chosen });

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        var asked = Assert.Single(fixtures.Asks).Story!;
        Assert.NotNull(asked.Beacon);
        Assert.Null(asked.Chosen);
        Assert.Equal(Chosen, fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    [Fact]
    public async Task AFinaleChapterIgnoresItAndKeepsIt()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeatsNamingTheEnd), RoundScriptedLlmProvider.Saying(NothingObjected)));
        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-400),
            BeaconScanAt = Now.AddDays(-390),
            FinaleFrom = 1,
            ChosenDestination = Chosen,
        });

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        var asked = Assert.Single(fixtures.Asks).Story!;
        Assert.Equal(1, asked.FinaleChapter);
        Assert.Null(asked.Chosen);
        Assert.Equal(Chosen, fixtures.Stories.Current("F1")!.ChosenDestination);
    }

    private static StoryFixtures At(StarPosition position, long address, AdventureHeading chosen, StorySecret? secret = null, string? homeTarget = null)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats)), secret);
        var game = Loaded(1_000_000);

        game.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Location", "StarSystem":"Somewhere", "StarPos":[{{position.X}},{{position.Y}},{{position.Z}}], "SystemAddress":{{address}}, "Docked":false }"""));
        fixtures.Director.Game = () => game;
        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            HomeTarget = homeTarget,
            ChosenDestination = chosen,
        });

        return fixtures;
    }
}
