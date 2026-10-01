using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Persona;
using Xunit;
using static D47.Core.Tests.Stories.OdysseySupport;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Time after the beacon scan spent playing without Odyssey does not count toward a clue's day.</summary>
public sealed class TimeWithoutOdysseyIsLeftOutTests
{
    private static readonly Story Scanned = new()
    {
        Id = Id,
        Title = Card.Title,
        PublicLayer = Card.Describe(),
        PickedAt = Now.AddDays(-1),
        BeaconScanAt = Now,
    };

    [Fact]
    public async Task TenDaysWithoutThenTenWithCountsTen()
    {
        using var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        Assert.Null(await fixtures.Director.PickAsync("F1", Id, Now, CancellationToken.None));

        fixtures.Director.Observe(BeaconFixture.JumpTo(Beacon, BeaconAddress), "F1");
        fixtures.Director.Observe(BeaconFixture.DataPoint(), "F1");

        var scan = fixtures.Stories.Current("F1")!.BeaconScanAt!.Value;

        fixtures.Director.Observe(LoadGame(scan, odyssey: false), "F1");
        fixtures.Director.Observe(LoadGame(scan.AddDays(5), odyssey: false), "F1");
        fixtures.Director.Observe(LoadGame(scan.AddDays(10), odyssey: true), "F1");

        Assert.Equal(TimeSpan.FromDays(10), fixtures.Stories.Current("F1")!.SinceBeacon(scan.AddDays(20)));
    }

    [Fact]
    public void AnOpenStretchStopsTheClock()
    {
        var without = Scanned.Loaded(odyssey: false, Now.AddDays(2));

        Assert.True(without.IsWithoutOdyssey);
        Assert.Equal(TimeSpan.FromDays(2), without.SinceBeacon(Now.AddDays(30)));
    }

    [Fact]
    public void TimeSwitchedOffAndWithoutOdysseyIsLeftOutOnce()
    {
        var story = Scanned
            .SwitchedOff(Now.AddDays(1))
            .Loaded(odyssey: false, Now.AddDays(2))
            .SwitchedOn(Now.AddDays(4))
            .Loaded(odyssey: true, Now.AddDays(6));

        Assert.Equal(TimeSpan.FromDays(5), story.SinceBeacon(Now.AddDays(10)));
        Assert.False(story.IsOff);
        Assert.False(story.IsWithoutOdyssey);
    }
}
