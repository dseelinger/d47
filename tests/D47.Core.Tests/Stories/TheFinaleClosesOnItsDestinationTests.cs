using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>The destination finale chapter 1 is planned toward is kept on the story and survives a restart.</summary>
public sealed class TheFinaleClosesOnItsDestinationTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public async Task TheDestinationIsKeptAndSurvivesARestart()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(StoryFixtures.Lines()),
            RoundScriptedLlmProvider.Saying(StoryFixtures.NothingObjected),
            RoundScriptedLlmProvider.Saying(StoryFixtures.NextLines),
            RoundScriptedLlmProvider.Saying(StoryFixtures.NothingObjected)));

        Assert.Null(await fixtures.Director.PickAsync("F1", StoryFixtures.Id, StoryFixtures.Now, CancellationToken.None));

        var last = fixtures.Stories.Current("F1")!.CurrentChapter!;

        fixtures.Stories.Update("F1", StoryFixtures.Id, story => story with { Chapters = ["one", "two", last], FinaleFrom = 4, BeaconScanAt = StoryFixtures.Now });
        fixtures.Finish("F1", last, StoryFixtures.Now);

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", StoryFixtures.Now.AddDays(1), CancellationToken.None));
        Assert.Equal(1, fixtures.Asks[^1].Story!.FinaleChapter);

        var expected = new AdventureDestination(AdventureFixtures.Lantern, "Ossen's Lantern", 6, "Ossen's Lantern 2 a");
        var reopened = StoryStore.Open(fixtures.StoryPath, fixtures.Files, NullLogger<StoryStore>.Instance);

        Assert.Equal(expected, fixtures.Stories.Current("F1")!.FinaleDestination);
        Assert.Equal(expected, reopened.Current("F1")!.FinaleDestination);
    }
}
