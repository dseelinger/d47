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

    private static StoryFixtures At(StorySecret secret, StarPosition? position, bool finale = true)
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
        });

        return fixtures;
    }
}
