using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The warning that a joined community goal is about to expire.</summary>
public class CommunityGoalExpiryCalloutTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Board(params (int Id, TimeSpan Left, long Contribution, bool Complete)[] goals)
    {
        var payload = new Dictionary<string, object?>
        {
            ["timestamp"] = Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["event"] = "CommunityGoal",
            ["CurrentGoals"] = goals.Select(g => new Dictionary<string, object?>
            {
                ["CGID"] = g.Id,
                ["Title"] = $"Goal {g.Id}",
                ["Expiry"] = (Now + g.Left).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["IsComplete"] = g.Complete,
                ["PlayerContribution"] = g.Contribution,
            }).ToList(),
        };

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(payload), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static List<Announcement> Say(
        CommunityGoalExpiryCallout callout, CommanderGameState state, DateTimeOffset now, bool priming = false) =>
        [.. callout.Examine(new CalloutContext(now, priming, state, GameStatus.Unknown, NavRoute.None, []))];

    private static CommanderGameState StateWith(params (int Id, TimeSpan Left, long Contribution, bool Complete)[] goals)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        state.Apply(Board(goals));
        return state;
    }

    [Fact]
    public void AJoinedGoalSevenHoursOutSpeaksOnce()
    {
        var callout = new CommunityGoalExpiryCallout();
        var state = StateWith((7, TimeSpan.FromHours(7.5), 100, false));

        var said = Assert.Single(Say(callout, state, Now));

        Assert.Equal(CommunityGoalExpiryCallout.Key, said.Key);
        Assert.Equal("The Goal 7 community goal ends in 7 hours.", said.Text);
        Assert.Empty(Say(callout, state, Now.AddMinutes(10)));
    }

    [Fact]
    public void AGoalNineHoursOutIsSilentUntilEightAreLeft()
    {
        var callout = new CommunityGoalExpiryCallout();
        var state = StateWith((7, TimeSpan.FromHours(9), 100, false));

        Assert.Empty(Say(callout, state, Now));

        var said = Assert.Single(Say(callout, state, Now.AddHours(1)));
        Assert.Equal("The Goal 7 community goal ends in 8 hours.", said.Text);
    }

    [Fact]
    public void AGoalNotJoinedCompleteOrExpiredIsSilent()
    {
        var callout = new CommunityGoalExpiryCallout();
        var state = StateWith(
            (1, TimeSpan.FromHours(3), 0, false),
            (2, TimeSpan.FromHours(3), 100, true),
            (3, TimeSpan.FromHours(-1), 100, false));

        Assert.Empty(Say(callout, state, Now));
    }

    [Fact]
    public void LessThanAnHourSaysOneHour()
    {
        var state = StateWith((7, TimeSpan.FromMinutes(20), 100, false));

        var said = Assert.Single(Say(new CommunityGoalExpiryCallout(), state, Now));

        Assert.Equal("The Goal 7 community goal ends in 1 hour.", said.Text);
    }

    [Fact]
    public void PrimingSpeaksNothingAndTheFirstLiveTickDoes()
    {
        var callout = new CommunityGoalExpiryCallout();
        var state = StateWith((7, TimeSpan.FromHours(3), 100, false));

        Assert.Empty(Say(callout, state, Now, priming: true));
        Assert.Single(Say(callout, state, Now));
    }

    [Fact]
    public void TheRowExistsAndDefaultsOn()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        var row = surface.Registry.All
            .SelectMany(capability => capability.Descriptor.Settings)
            .Single(row => row.Key == CalloutCapability.CommunityGoalExpiryKey);

        Assert.Equal(SettingKind.Toggle, row.Kind);
        Assert.True(new CalloutSettings().CommunityGoalExpiry);
        Assert.False(row.Binding!.Write!(D47Settings.Defaults, "false")!.Callouts.CommunityGoalExpiry);
    }
}
