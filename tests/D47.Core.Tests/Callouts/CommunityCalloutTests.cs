using D47.Core.Callouts;
using D47.Core.Interface;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The line that points at the D47 Discord every two weeks.</summary>
public class CommunityCalloutTests
{
    private static readonly DateTimeOffset Launch = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private sealed class Day
    {
        public string? Value { get; set; }
    }

    private static CommunityCallout Make(Day day) => new()
    {
        LastSaidDay = () => day.Value,
        RememberSaidDay = value => day.Value = value,
    };

    private static string Local(DateTimeOffset at) => at.LocalDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static List<Announcement> Say(CommunityCallout callout, DateTimeOffset now, GameStatus status, bool priming = false) =>
        [.. callout.Examine(new CalloutContext(now, priming, null, status, NavRoute.None, []))];

    private static readonly GameStatus Docked = new() { Flags = StatusFlags.Docked | StatusFlags.InMainShip };

    private static readonly GameStatus Flying = new() { Flags = StatusFlags.InMainShip };

    private static readonly GameStatus Scooping = new() { Flags = StatusFlags.Supercruise | StatusFlags.ScoopingFuel | StatusFlags.InMainShip };

    /// <summary>Ticks live from <see cref="Launch"/> so the session has a first live tick, then docks for the hold.</summary>
    private static List<Announcement> DockedTenMinutesIn(CommunityCallout callout, GameStatus? status = null)
    {
        Say(callout, Launch, status ?? Docked);

        return Say(callout, Launch.AddMinutes(10).AddSeconds(1), status ?? Docked);
    }

    [Fact]
    public void WithNoDayRecordedTodayIsRecordedAndNothingIsSaid()
    {
        var day = new Day();
        var callout = Make(day);

        Assert.Empty(DockedTenMinutesIn(callout));
        Assert.Equal(Local(Launch), day.Value);
    }

    [Fact]
    public void ThirteenDaysIsNotEnough()
    {
        var day = new Day { Value = Local(Launch.AddDays(-13)) };

        Assert.Empty(DockedTenMinutesIn(Make(day)));
    }

    [Fact]
    public void FourteenDaysDockedForNinetySecondsTenMinutesInSaysOneLineAndRecordsToday()
    {
        var day = new Day { Value = Local(Launch.AddDays(-14)) };
        var callout = Make(day);

        var said = Assert.Single(DockedTenMinutesIn(callout));

        Assert.Equal(Community.SourceKey, said.Key);
        Assert.Contains(Community.Phrase, said.Text, StringComparison.Ordinal);
        Assert.Equal(CalloutUrgency.Routine, said.Urgency);
        Assert.Null(said.Chatter);
        Assert.Equal(Local(Launch.AddMinutes(10)), day.Value);
    }

    [Fact]
    public void ASecondQuietMomentInTheSameLaunchSaysNothing()
    {
        var day = new Day { Value = Local(Launch.AddDays(-30)) };
        var callout = Make(day);
        Assert.Single(DockedTenMinutesIn(callout));

        Assert.Empty(Say(callout, Launch.AddHours(3), Docked));
    }

    [Fact]
    public void NormalSpaceAndScoopingSayNothing()
    {
        var day = new Day { Value = Local(Launch.AddDays(-30)) };

        Assert.Empty(DockedTenMinutesIn(Make(day), Flying));
        Assert.Empty(DockedTenMinutesIn(Make(day), GameStatus.Unknown));
        Assert.Empty(DockedTenMinutesIn(Make(day), Scooping));
    }

    [Fact]
    public void ASituationHeldForLessThanNinetySecondsSaysNothing()
    {
        var day = new Day { Value = Local(Launch.AddDays(-30)) };
        var callout = Make(day);
        Say(callout, Launch, Flying);
        Say(callout, Launch.AddMinutes(20), Flying);

        Assert.Empty(Say(callout, Launch.AddMinutes(20).AddSeconds(10), Docked));
        Assert.Single(Say(callout, Launch.AddMinutes(21).AddSeconds(40), Docked));
    }

    [Fact]
    public void LessThanTenMinutesIntoTheSessionSaysNothing()
    {
        var day = new Day { Value = Local(Launch.AddDays(-30)) };
        var callout = Make(day);
        Say(callout, Launch, Docked);

        Assert.Empty(Say(callout, Launch.AddMinutes(9), Docked));
    }

    [Fact]
    public void PrimingSaysNothingAndDoesNotStartTheSession()
    {
        var day = new Day { Value = Local(Launch.AddDays(-30)) };
        var callout = Make(day);
        Say(callout, Launch, Docked, priming: true);

        Assert.Empty(Say(callout, Launch.AddMinutes(11), Docked));
        Assert.Empty(Say(callout, Launch.AddMinutes(12), Docked));
        Assert.Single(Say(callout, Launch.AddMinutes(22), Docked));
    }
}
