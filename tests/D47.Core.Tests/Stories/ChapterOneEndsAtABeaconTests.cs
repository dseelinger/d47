using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
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
        Assert.Equal("IC 2391 Sector CQ-Y c16", GuardianCores.NearestBeacon(StarPosition.Origin).Name);
        Assert.Equal("IC 2391 Sector CQ-Y c16", GuardianCores.NearestBeacon(null).Name);
        Assert.Equal((BeaconAddress, Beacon), GuardianCores.NearestBeacon(BeyondTheBeacon(10)));
        Assert.Equal("NGC 2451A Sector LX-U d2-25", GuardianCores.NearestBeacon(new StarPosition(726, -163, -171)).Name);
        Assert.Equal(GuardianCores.Beacons.Keys.Order(), GuardianCores.BeaconPositions.Keys.Order());
        Assert.Equal(28, GuardianCores.Beacons.Count);
    }

    [Fact]
    public void AScanInAnyOfTheTwentyEightSystemsCountsAsABeaconScan()
    {
        var scan = AdventureFixtures.Event("""{ "timestamp":"2026-09-30T12:00:00Z", "event":"DataScanned", "Type":"$Datascan_AncientBeacon;" }""");

        Assert.True(GuardianCores.IsBeaconScan(scan, 84926173882));
        Assert.True(GuardianCores.IsBeaconScan(scan, 319849220451));
        Assert.False(GuardianCores.IsBeaconScan(scan, 1));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task PickingWritesAndBeginsAChapterThatEndsAtTheBeacon()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Lines()), RoundScriptedLlmProvider.Saying(NothingObjected)));

        var refusal = await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None);

        Assert.Null(refusal);
        Assert.Equal(Card.InYourWords, fixtures.Backstory);

        var story = fixtures.Stories.Current("F1")!;
        var chapter = fixtures.Book.Store.Find("F1", Assert.Single(story.Chapters))!;

        Assert.Equal(Id, chapter.StoryId);
        Assert.True(chapter.IsActive);
        Assert.Equal(TriggerKind.Beacon, chapter.Beats[^1].Trigger.Kind);
        Assert.Equal(BeaconAddress, chapter.Beats[^1].Trigger.SystemAddress);
        Assert.Equal(Beacon, chapter.Beats[^1].Trigger.System);
        Assert.Equal(
            $"Next: scan the Guardian beacon in {Beacon}. Charge its three pylons with laser or railgun fire, all within a few minutes, then data-link scan the core it opens. It releases an Ancient Key, which needs a cargo scoop or collector limpet and 1 t of free cargo.",
            chapter.Beats[^1].Trigger.HandOff());

        var prompt = fixtures.Provider.Requests[0].Prompt.History[0].Text;

        Assert.Contains("chapter 1 of \"The Test Story\"", prompt);
        Assert.Contains(Card.InYourWords, prompt);
        Assert.Contains(Secret.Secret, prompt);
        Assert.Contains($"scan the Guardian beacon in {Beacon}", prompt);
    }
}
