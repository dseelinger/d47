using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Presence;
using Xunit;

namespace D47.Core.Tests.Presence;

/// <summary>Away once the time passes with no activity the Commander caused; zero is never away.</summary>
public class TheCommanderIsAwayAfterNothingTheyCausedTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Five = TimeSpan.FromMinutes(5);

    [Fact]
    public void TheFirstCallIsPresent()
    {
        Assert.False(new PresenceWatch().IsAway(Start, Five));

        var observed = new PresenceWatch();
        observed.Observe([], Start);

        Assert.False(observed.IsAway(Start.AddMinutes(5).AddSeconds(-1), Five));
    }

    [Fact]
    public void TheCommanderIsAwayOnceTheTimePassesWithNoActivity()
    {
        var watch = new PresenceWatch();
        watch.Observe([], Start);

        for (var minute = 1; minute <= 5; minute++)
        {
            watch.Observe([], Start.AddMinutes(minute));
        }

        Assert.False(watch.IsAway(Start.AddMinutes(5).AddSeconds(-1), Five));
        Assert.True(watch.IsAway(Start.AddMinutes(5), Five));
    }

    [Fact]
    public void AStirReturnsThemToPresent()
    {
        var watch = new PresenceWatch();
        watch.Observe([], Start);
        Assert.True(watch.IsAway(Start.AddMinutes(6), Five));

        watch.Stir(Start.AddMinutes(6));

        Assert.False(watch.IsAway(Start.AddMinutes(10), Five));
        Assert.True(watch.IsAway(Start.AddMinutes(11), Five));
    }

    public static TheoryData<string> CommandedEvents() => [.. PresenceWatch.Commanded];

    [Theory]
    [MemberData(nameof(CommandedEvents))]
    public void EachEventTheCommanderCausesReturnsThemToPresent(string kind)
    {
        var watch = new PresenceWatch();
        watch.Observe([], Start);
        Assert.True(watch.IsAway(Start.AddMinutes(6), Five));

        var at = Start.AddMinutes(6);
        watch.Observe([Event(kind, at)], at);

        Assert.False(watch.IsAway(Start.AddMinutes(10), Five));
    }

    [Theory]
    [InlineData("Touchdown")]
    [InlineData("Liftoff")]
    public void ALandingOrTakeOffFlownByHandReturnsThemToPresent(string kind)
    {
        var watch = new PresenceWatch();
        watch.Observe([], Start);

        var at = Start.AddMinutes(6);
        watch.Observe([Event(kind, at, playerControlled: true)], at);

        Assert.False(watch.IsAway(Start.AddMinutes(10), Five));
    }

    [Theory]
    [InlineData("Bounty", null)]
    [InlineData("ReceiveText", null)]
    [InlineData("Music", null)]
    [InlineData("HullDamage", null)]
    [InlineData("Touchdown", false)]
    [InlineData("Liftoff", false)]
    [InlineData("Touchdown", null)]
    public void EventsNobodyAtTheControlsCausedDoNot(string kind, bool? playerControlled)
    {
        var watch = new PresenceWatch();
        watch.Observe([], Start);

        var at = Start.AddMinutes(4);
        watch.Observe([Event(kind, at, playerControlled)], at);

        Assert.True(watch.IsAway(Start.AddMinutes(5), Five));
    }

    [Fact]
    public void ZeroIsNeverAway()
    {
        var watch = new PresenceWatch();
        watch.Observe([], Start);

        Assert.False(watch.IsAway(Start.AddHours(10), TimeSpan.Zero));
    }

    private static JournalEvent Event(string kind, DateTimeOffset at, bool? playerControlled = null)
    {
        var fields = new Dictionary<string, object> { ["event"] = kind };

        if (playerControlled is { } controlled)
        {
            fields["PlayerControlled"] = controlled;
        }

        return new JournalEvent(at, kind, JsonSerializer.SerializeToElement(fields));
    }
}
