using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Picking a story writes chapter one, whose last beat arrives at the beacon system nearest the Commander.</summary>
public sealed class ChapterOneEndsAtABeaconTests
{
    [Fact]
    public void TheNearestBeaconIsMeasuredFromWhereTheCommanderIs()
    {
        Assert.Equal((BeaconAddress, Beacon), GuardianCores.NearestBeacon(StarPosition.Origin));
        Assert.Equal((BeaconAddress, Beacon), GuardianCores.NearestBeacon(null));
        Assert.Equal("NGC 2451A Sector LX-U d2-25", GuardianCores.NearestBeacon(new StarPosition(726, -163, -171)).Name);
        Assert.Equal(GuardianCores.Beacons.Keys.Order(), GuardianCores.BeaconPositions.Keys.Order());
    }

    [Fact]
    public async Task PickingWritesAndBeginsAChapterThatEndsAtTheBeacon()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Null(refusal);
        Assert.Equal(Card.InYourWords, fixtures.Backstory);

        var story = fixtures.Stories.Current("F1")!;
        var chapter = fixtures.Book.Store.Find("F1", Assert.Single(story.Chapters))!;

        Assert.Equal(Id, chapter.StoryId);
        Assert.True(chapter.IsActive);
        Assert.Equal(TriggerKind.Arrive, chapter.Beats[^1].Trigger.Kind);
        Assert.Equal(BeaconAddress, chapter.Beats[^1].Trigger.SystemAddress);

        var prompt = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains($"its last beat is \"arrive\" at {Beacon}", prompt);
        Assert.Contains("chapter 1 of \"The Test Story\"", prompt);
        Assert.Contains(Card.InYourWords, prompt);
        Assert.Contains(Secret.Secret, prompt);
    }

    [Fact]
    public async Task AChapterOneThatEndsElsewhereIsRefused()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsElsewhere),
            RoundScriptedLlmProvider.Saying(BeatsElsewhere)));

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Contains($"The last beat must be \"arrive\" at {Beacon}", refusal);
        Assert.Empty(fixtures.Book.Store.For("F1"));
        Assert.Empty(fixtures.Stories.Current("F1")!.Chapters);
        Assert.True(fixtures.Director.WriteFailed("F1"));

        // The refusal went back through the beats turn once before the Commander saw it.
        Assert.Contains("The last beat must be", fixtures.Provider.Requests[2].Prompt.History[0].Text);
    }
}
