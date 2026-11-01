using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Ten minutes with only background events is idle; anything else is activity (#2019).</summary>
public class NothingHappeningForTenMinutesIsIdleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OnlyBackgroundEventsForTenMinutesIsIdle()
    {
        var watch = new IdleWatch();
        watch.Observe([], Start);

        for (var minute = 1; minute <= 10; minute++)
        {
            var now = Start.AddMinutes(minute);
            watch.Observe(
                [Event("Music", now), Event("ReceiveText", now), Event("NpcCrewPaidWage", now), Event("CommunityGoal", now), Event("FSSSignalDiscovered", now)],
                now);
        }

        Assert.False(watch.IsIdle(Start.AddMinutes(10).AddSeconds(-1)));
        Assert.True(watch.IsIdle(Start.AddMinutes(10)));
    }

    [Theory]
    [InlineData("Docked")]
    [InlineData("MarketBuy")]
    [InlineData("Friends")]
    public void AnyOtherEventResetsTheClock(string kind)
    {
        var watch = new IdleWatch();
        watch.Observe([], Start);

        var at = Start.AddMinutes(9);
        watch.Observe([Event(kind, at)], at);

        Assert.False(watch.IsIdle(Start.AddMinutes(18)));
        Assert.True(watch.IsIdle(Start.AddMinutes(19)));
    }

    [Fact]
    public void AStirResetsTheClock()
    {
        var watch = new IdleWatch();
        watch.Observe([], Start);

        watch.Stir(Start.AddMinutes(9));

        Assert.False(watch.IsIdle(Start.AddMinutes(18)));
        Assert.True(watch.IsIdle(Start.AddMinutes(19)));
    }

    [Fact]
    public void NothingIsIdleBeforeTheFirstObservation()
    {
        var watch = new IdleWatch();

        Assert.False(watch.IsIdle(Start.AddHours(1)));

        watch.Observe([], Start);
        watch.Reset();

        Assert.False(watch.IsIdle(Start.AddHours(1)));
    }

    private static JournalEvent Event(string kind, DateTimeOffset at) =>
        new(at, kind, JsonDocument.Parse($$"""{"event":"{{kind}}"}""").RootElement.Clone());
}
