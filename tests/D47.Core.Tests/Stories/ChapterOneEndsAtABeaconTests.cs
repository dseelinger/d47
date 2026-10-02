using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Picking a story writes chapter one, whose last beat is the scan of the beacon nearest the Commander.</summary>
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
        // Placed at the beacon d47 chose, though the model wrote another system.
        Assert.Equal(TriggerKind.Beacon, chapter.Beats[^1].Trigger.Kind);
        Assert.Equal(BeaconAddress, chapter.Beats[^1].Trigger.SystemAddress);
        Assert.Equal(Beacon, chapter.Beats[^1].Trigger.System);
        Assert.Equal($"Next: scan the Guardian beacon in {Beacon} with the ship's data-link scanner.", chapter.Beats[^1].Trigger.HandOff());

        var prompt = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains($"its last beat is \"beacon\", the Commander scanning the Guardian beacon in {Beacon}", prompt);
        Assert.Contains("chapter 1 of \"The Test Story\"", prompt);
        Assert.Contains(Card.InYourWords, prompt);
        Assert.Contains(Secret.Secret, prompt);

        var beats = fixtures.Provider.Requests[1].Prompt.History[0].Text;

        Assert.Contains("one of thirty-three things", beats);
        Assert.Contains("\"livery\"|\"wing\"|\"multicrew\"|\"squadron\"|\"beacon\"", beats);
    }

    [Fact]
    public async Task AChapterOneThatEndsElsewhereIsRefused()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsElsewhere),
            RoundScriptedLlmProvider.Saying(BeatsElsewhere)));

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Contains($"The last beat must be \"beacon\", where the Commander scans the Guardian beacon in {Beacon}", refusal);
        Assert.Empty(fixtures.Book.Store.For("F1"));
        Assert.Empty(fixtures.Stories.Current("F1")!.Chapters);
        Assert.True(fixtures.Director.WriteFailed("F1"));

        // The refusal went back through the beats turn once before the Commander saw it.
        Assert.Contains("The last beat must be", fixtures.Provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AChapterOneThatArrivesAtTheBeaconIsRefused()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsArrivingAtTheBeacon),
            RoundScriptedLlmProvider.Saying(BeatsArrivingAtTheBeacon)));

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Contains("The last beat must be \"beacon\"", refusal);
        Assert.Empty(fixtures.Book.Store.For("F1"));
    }

    [Fact]
    public async Task ABeaconBeatBeforeTheLastIsRefused()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsWithAnEarlyBeacon),
            RoundScriptedLlmProvider.Saying(BeatsWithAnEarlyBeacon)));

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Contains("Beat 1 (Too Soon) is a \"beacon\" beat; only the last beat of the chapter that ends act one may be one.", refusal);
        Assert.DoesNotContain("Beat 3", refusal);
        Assert.Empty(fixtures.Book.Store.For("F1"));
    }

    [Fact]
    public async Task ALaterChapterMayNotEndOnABeacon()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon),
            RoundScriptedLlmProvider.Saying(NextSpine),
            RoundScriptedLlmProvider.Saying(NextBeatsWithABeacon),
            RoundScriptedLlmProvider.Saying(NextBeatsWithABeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));
        fixtures.Finish("F1", Assert.Single(fixtures.Stories.Current("F1")!.Chapters), Now);

        var refusal = await fixtures.Director.WriteNextAsync("F1", Now.AddDays(1), CancellationToken.None);

        Assert.Contains("Beat 2 (The Beacon Again) is a \"beacon\" beat", refusal);
        Assert.Single(fixtures.Stories.Current("F1")!.Chapters);
        Assert.DoesNotContain("\"beacon\"", fixtures.Provider.Requests[2].Prompt.History[0].Text);
    }
}
