using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A finale does not start while the current stage lies in a span with a destination the Commander has not reached (#2056).</summary>
public sealed class TheFinaleWaitsForTheCommanderToArriveTests
{
    private static readonly StorySecret HomeBound = Secret with { Black = [new StoryBlackSpan("breakIntoTwo", "breakIntoThree", "Sol")] };

    private static readonly StorySecret TwoStops = Secret with
    {
        Black = [new StoryBlackSpan("breakIntoTwo", "breakIntoThree", new StoryToward([new StoryStop("A", null, [0, 0, 25000]), new StoryStop("Home", null, [0, 0, 0])]) { IsList = true })],
    };

    private static readonly StorySecret NoDestination = Secret with { Black = [new StoryBlackSpan("breakIntoTwo", "breakIntoThree")] };

    [Fact]
    public async Task TheFinaleDoesNotStartWhileTheCommanderIsFarFromTheDestination()
    {
        using var fixtures = At(HomeBound, new StarPosition(0, 0, 20000), false);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(361), CancellationToken.None));

        var asked = Assert.Single(fixtures.Asks).Story!;

        Assert.Null(asked.FinaleChapter);
        Assert.Equal("Sol", asked.Toward);
        Assert.Null(fixtures.Stories.Current("F1")!.FinaleFrom);
    }

    [Fact]
    public async Task TheFinaleWaitsForTheLastStopOfAListNotTheFirst()
    {
        using var fixtures = At(TwoStops, new StarPosition(0, 0, 24500), false, legs: new Dictionary<int, int> { [0] = 0 });

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(361), CancellationToken.None));

        var asked = Assert.Single(fixtures.Asks).Story!;

        Assert.Null(asked.FinaleChapter);
        Assert.Equal("Home", asked.Toward);
        Assert.Equal(StarPosition.Origin, asked.TowardAt);
        Assert.Equal(1, fixtures.Stories.Current("F1")!.TowardLegs[0]);
    }

    [Fact]
    public async Task TheFinaleStartsOnceTheCommanderIsNearTheLastStopOfAList()
    {
        using var fixtures = At(TwoStops, new StarPosition(0, 0, 500), legs: new Dictionary<int, int> { [0] = 1 });

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(361), CancellationToken.None));

        Assert.Equal(1, Assert.Single(fixtures.Asks).Story!.FinaleChapter);
    }

    [Fact]
    public async Task TheFinaleStartsOnceTheCommanderIsWithinAThousandLightYears()
    {
        using var fixtures = At(HomeBound, new StarPosition(0, 0, 900));

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(361), CancellationToken.None));

        Assert.Equal(1, Assert.Single(fixtures.Asks).Story!.FinaleChapter);
    }

    [Fact]
    public async Task TheFinaleStartsAsBeforeWithNoKnownPosition()
    {
        using var fixtures = At(HomeBound, null);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(361), CancellationToken.None));

        Assert.Equal(1, Assert.Single(fixtures.Asks).Story!.FinaleChapter);
    }

    [Fact]
    public async Task ASpanWithNoDestinationDoesNotHoldTheFinale()
    {
        using var fixtures = At(NoDestination, new StarPosition(0, 0, 20000));

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now.AddDays(361), CancellationToken.None));

        Assert.Equal(1, Assert.Single(fixtures.Asks).Story!.FinaleChapter);
    }

    private static StoryFixtures At(StorySecret secret, StarPosition? position, bool finale = true, IReadOnlyDictionary<int, int>? legs = null)
    {
        var fixtures = new StoryFixtures(finale
            ? new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NextLines), RoundScriptedLlmProvider.Saying(NothingObjected))
            : new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NextLines)), secret);
        var game = Loaded(1_000_000);

        if (position is { } at)
        {
            game.Apply(AdventureFixtures.Event(
                $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Location", "StarSystem":"Somewhere", "StarPos":[{{at.X}},{{at.Y}},{{at.Z}}], "SystemAddress":7, "Docked":false }"""));
        }

        fixtures.Director.Game = () => game;
        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now,
            BeaconScanAt = Now,
            CluesGiven = 14,
            TowardLegs = legs ?? new Dictionary<int, int>(),
        });

        return fixtures;
    }
}
