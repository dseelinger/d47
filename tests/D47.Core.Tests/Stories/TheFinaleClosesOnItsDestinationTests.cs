using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.TheFinaleEndsWhereItsFirstChapterSaidTests;

namespace D47.Core.Tests.Stories;

/// <summary>The last finale chapter lands on the destination, which may be beyond the reach, and the destination survives a restart.</summary>
public sealed class TheFinaleClosesOnItsDestinationTests
{
    private static readonly AdventureDestination EdgeMoon = new(Galaxy.Address("Edge Moon"), "Edge Moon", 1, "Edge Moon 1 a");

    [Fact]
    public async Task TheLastFinaleChapterMayLandOnTheDestinationBeyondTheReach()
    {
        var outcome = await Write(Ask(finale: 4, destination: EdgeMoon), Beats(Arrive("Kestrel"), Land("Edge Moon", "Edge Moon 1 a")));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(EdgeMoon.Landing().BodyId, outcome.Draft!.Beats[^1].Trigger.BodyId);
    }

    [Fact]
    public async Task TheLastFinaleChapterThatEndsElsewhereIsRefused()
    {
        var beats = Beats(Arrive("Kestrel"), Arrive("Closer"));
        var outcome = await Write(Ask(finale: 4, destination: EdgeMoon), beats, beats);

        Assert.False(outcome.Succeeded);
        Assert.Contains("The last objective must be \"land\" on Edge Moon 1 a in Edge Moon, where the finale ends.", outcome.Refusal);
    }

    [Fact]
    public async Task OnlyTheLastBeatMayGoBeyondTheReach()
    {
        var beats = Beats(Land("Edge Moon", "Edge Moon 1 a"), Arrive("Kestrel"));
        var outcome = await Write(Ask(finale: 4, destination: EdgeMoon), beats, beats);

        Assert.False(outcome.Succeeded);
        Assert.Contains("is 1000 light years from the previous stop; the reach is 360.", outcome.Refusal);
    }

    [Fact]
    public async Task AThreeDayFinaleLandsOnTheDestinationItNames()
    {
        var landed = await Write(
            Ask(finale: 1, finaleChapters: 1),
            Beats(Arrive("Kestrel"), Land("Edge Moon", "Edge Moon 1 a"), destination: ("Edge Moon", "Edge Moon 1 a")));

        var elsewhere = Beats(Arrive("Kestrel"), Arrive("Closer"), destination: ("Edge Moon", "Edge Moon 1 a"));
        var refused = await Write(Ask(finale: 1, finaleChapters: 1), elsewhere, elsewhere);

        Assert.True(landed.Succeeded, landed.Refusal);
        Assert.Equal(EdgeMoon, landed.Destination);
        Assert.False(refused.Succeeded);
        Assert.Contains("The last objective must be \"land\" on Edge Moon 1 a in Edge Moon", refused.Refusal);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheDestinationIsKeptAndSurvivesARestart()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(StoryFixtures.Spine),
            RoundScriptedLlmProvider.Saying(StoryFixtures.BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(StoryFixtures.NextSpine),
            RoundScriptedLlmProvider.Saying(Beats(Arrive("Ossen's Lantern"), destination: ("Ossen's Lantern", "Ossen's Lantern 2 a")))));

        Assert.Null(await fixtures.Director.PickAsync("F1", StoryFixtures.Id, StoryFixtures.Now, CancellationToken.None));

        var last = fixtures.Stories.Current("F1")!.CurrentChapter!;

        fixtures.Stories.Update("F1", StoryFixtures.Id, story => story with { Chapters = ["one", "two", last], FinaleFrom = 4, BeaconScanAt = StoryFixtures.Now });
        fixtures.Finish("F1", last, StoryFixtures.Now);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", StoryFixtures.Now.AddDays(1), CancellationToken.None));
        Assert.Equal(1, fixtures.Asks[^1].Story!.FinaleChapter);

        var expected = new AdventureDestination(AdventureFixtures.Lantern, "Ossen's Lantern", 6, "Ossen's Lantern 2 a");
        var reopened = StoryStore.Open(fixtures.StoryPath, NullLogger<StoryStore>.Instance);

        Assert.Equal(expected, fixtures.Stories.Current("F1")!.FinaleDestination);
        Assert.Equal(expected, reopened.Current("F1")!.FinaleDestination);
    }
}
