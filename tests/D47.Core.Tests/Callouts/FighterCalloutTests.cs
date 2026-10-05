using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The hired pilot speaks when the fighter moves. The events are written from the field names in Frontier's
/// Journal manual, not recorded from play.
/// </summary>
public class TheFighterPilotCallsTheLaunchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Written(string kind, bool? playerControlled = null)
    {
        var fields = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-10-05T12:00:00Z",
            ["event"] = kind,
            ["ID"] = 3,
        };

        if (playerControlled is { } controlled)
        {
            fields["Loadout"] = "starter";
            fields["PlayerControlled"] = controlled;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(fields), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState WithPilot(bool active = true)
    {
        var hired = Row("CrewHire", ("Name", "Vance Ilo"), ("CrewID", 7), ("CombatRank", "Competent"));
        var assigned = Row("CrewAssign", ("Name", "Vance Ilo"), ("CrewID", 7), ("Role", active ? "Active" : "OnShoreLeave"));
        var state = new CommanderGameState(new CommanderIdentity("F735466", "TEST"));
        state.Apply(hired);
        state.Apply(assigned);
        return state;
    }

    private static JournalEvent Row(string kind, params (string Key, object Value)[] fields)
    {
        var all = new Dictionary<string, object?> { ["timestamp"] = "2026-10-05T11:00:00Z", ["event"] = kind };
        foreach (var (key, value) in fields)
        {
            all[key] = value;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(all), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static List<Announcement> Say(CommanderGameState? state, bool priming, params JournalEvent[] events) =>
        [.. new FighterCallout().Examine(new CalloutContext(Now, priming, state, GameStatus.Unknown, NavRoute.None, events))];

    [Fact]
    public void APilotFlyingTheFighterSaysItIsAway()
    {
        var said = Assert.Single(Say(WithPilot(), false, Written("LaunchFighter", playerControlled: false)));

        Assert.Equal("Vance Ilo", said.Speaker);
        Assert.Equal(VoiceRole.Crew, said.Voice);
        Assert.Equal("Fighter away. I'll stay on your wing.", said.Text);
    }

    [Fact]
    public void ACommanderFlyingTheFighterHearsThePilotHoldTheShip()
    {
        var said = Assert.Single(Say(WithPilot(), false, Written("LaunchFighter", playerControlled: true)));

        Assert.Equal("I have the ship, Commander.", said.Text);
        Assert.Equal("Vance Ilo", said.Speaker);
    }

    [Theory]
    [InlineData("DockFighter", "Fighter's back in the bay.")]
    [InlineData("FighterDestroyed", "We've lost the fighter.")]
    [InlineData("FighterRebuilt", "Replacement fighter's ready in the bay.")]
    public void TheOtherThreeMomentsEachHaveTheirLine(string kind, string line)
    {
        var said = Assert.Single(Say(WithPilot(), false, Written(kind)));

        Assert.Equal(line, said.Text);
        Assert.Equal(VoiceRole.Crew, said.Voice);
    }

    [Fact]
    public void ABacklogReplayedOnStartupIsNotSpoken()
    {
        Assert.Empty(Say(WithPilot(), true, Written("DockFighter")));
    }
}

public class NoPilotNoFighterLinesTests
{
    private static JournalEvent Written(string kind)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-10-05T12:00:00Z",
            ["event"] = kind,
            ["ID"] = 3,
            ["PlayerControlled"] = false,
        });
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Theory]
    [InlineData("LaunchFighter")]
    [InlineData("DockFighter")]
    [InlineData("FighterDestroyed")]
    [InlineData("FighterRebuilt")]
    public void WithNobodyAssignedNothingIsSaid(string kind)
    {
        var context = new CalloutContext(
            new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero), false, new CommanderGameState(new CommanderIdentity("F735466", "TEST")),
            GameStatus.Unknown, NavRoute.None, [Written(kind)]);

        Assert.Empty(new FighterCallout().Examine(context));
    }
}
