using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A card with <c>beaconTrip</c> opens with the trip to a beacon even when an earlier story recorded a scan.</summary>
public sealed class ABeaconTripCardAlwaysFliesToTheBeaconTests
{
    private static Story OnRecord() => new()
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
    };

    private static StoryFixtures Fixtures(StoryCard card, string beats) => new(
        new RoundScriptedLlmProvider(
        [
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(beats),
            RoundScriptedLlmProvider.Saying(NothingObjected),
        ]),
        card: card);

    [Fact]
    public async Task TheFlagMakesTheStoryFlyWithAScanOnRecord()
    {
        var card = Card with { Length = StoryPacing.ThreeMonths.Key, BeaconTrip = true };
        using var fixtures = Fixtures(card, BeatsToTheBeacon);
        fixtures.Stories.Save("F1", OnRecord());

        Assert.False(fixtures.Director.Remembers("F1", card));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        var story = fixtures.Stories.Current("F1")!;

        Assert.Null(story.BeaconScanAt);
        Assert.Equal(0, story.BeaconsRemembered);
        Assert.False(fixtures.Asks[0].Story!.Remembered);
        Assert.NotNull(fixtures.Asks[0].Story!.Beacon);
    }

    [Fact]
    public async Task TheSameStoryWithoutTheFlagOpensRemembered()
    {
        var card = Card with { Length = StoryPacing.ThreeMonths.Key };
        using var fixtures = Fixtures(card, BeatsElsewhere);
        fixtures.Stories.Save("F1", OnRecord());

        Assert.True(fixtures.Director.Remembers("F1", card));
        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        Assert.Equal(Now, fixtures.Stories.Current("F1")!.BeaconScanAt);
    }

    [Fact]
    public void ACardWithoutTheFieldLoadsWithoutTheFlag()
    {
        Assert.False(Card.BeaconTrip);
    }

    [Fact]
    public void ABeaconTripOnAShortStoryIsAFault()
    {
        var card = Card with { Length = StoryPacing.TwoWeeks.Key, BeaconTrip = true };

        Assert.Contains(StoryCatalog.Faults(card, Secret), fault => fault.Contains("beaconTrip", StringComparison.Ordinal));
        Assert.DoesNotContain(
            StoryCatalog.Faults(card with { BeaconTrip = false }, Secret),
            fault => fault.Contains("beaconTrip", StringComparison.Ordinal));
    }
}
