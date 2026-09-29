using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A kill in normal space brings a combat exchange once the fight goes quiet (#583).</summary>
public class BystandersReactToTheCommandersKillsTests
{
    private static readonly DateTimeOffset T0 = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Bounty(string pilot, string target = "eagle") => Event(
        $$"""{"timestamp":"2026-09-24T14:05:09Z","event":"Bounty","PilotName":"$npc_name_decorate:#name={{pilot}};","PilotName_Localised":"{{pilot}}","Target":"{{target}}","TotalReward":1000,"VictimFaction":"Arakang Purple Drug Empire"}""");

    private static readonly JournalEvent PlayerKill = Event(
        """{"timestamp":"2026-09-24T14:05:09Z","event":"Bounty","PilotName":"$cmdr_decorate:#name=Jameson;","PilotName_Localised":"CMDR Jameson","Target":"python","TotalReward":1000}""");

    private static CalloutContext Context(DateTimeOffset now, StatusFlags flags = StatusFlags.InMainShip) =>
        new(now, false, null, GameStatus.Unknown with { Flags = flags }, NavRoute.None, []);

    private static NpcChatterCallout Callout(NearbyFight fight) => new(fight)
    {
        Interval = TimeSpan.FromMinutes(5),
        Longest = TimeSpan.FromMinutes(5),
        Settle = TimeSpan.FromSeconds(90),
    };

    [Fact]
    public void AKillIsFollowedByACombatExchangeAfterTwentySecondsOfQuiet()
    {
        var fight = new NearbyFight();
        var callout = Callout(fight);

        _ = callout.Examine(Context(T0)).ToArray();
        fight.Fold(Bounty("Vance"), T0 + TimeSpan.FromSeconds(10), priming: false);

        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromSeconds(20))));

        var marker = Assert.Single(callout.Examine(Context(T0 + TimeSpan.FromSeconds(31))));
        Assert.Equal(NpcChatterKind.Combat, NpcChatter.KindOf(marker.Key));
        Assert.Equal(string.Empty, marker.Text);
    }

    [Fact]
    public void NoTwoCombatExchangesFallCloserThanTheInterval()
    {
        var fight = new NearbyFight();
        var callout = Callout(fight);

        _ = callout.Examine(Context(T0)).ToArray();
        fight.Fold(Bounty("Vance"), T0 + TimeSpan.FromSeconds(10), priming: false);
        Assert.Single(callout.Examine(Context(T0 + TimeSpan.FromSeconds(31))));

        fight.Fold(Bounty("Osei"), T0 + TimeSpan.FromSeconds(100), priming: false);
        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromSeconds(140))));

        var later = T0 + TimeSpan.FromSeconds(31) + TimeSpan.FromMinutes(5);
        fight.Fold(Bounty("Osei"), later, priming: false);
        Assert.Single(callout.Examine(Context(later + TimeSpan.FromSeconds(21))));
    }

    [Theory]
    [InlineData(StatusFlags.Supercruise | StatusFlags.InMainShip)]
    [InlineData(StatusFlags.Docked | StatusFlags.InMainShip)]
    [InlineData(StatusFlags.None)]
    public void NoCombatExchangeIsMadeOutsideNormalSpace(StatusFlags flags)
    {
        var fight = new NearbyFight();
        var callout = Callout(fight);

        _ = callout.Examine(Context(T0, flags)).ToArray();
        fight.Fold(Bounty("Vance"), T0 + TimeSpan.FromSeconds(10), priming: false);

        Assert.DoesNotContain(
            callout.Examine(Context(T0 + TimeSpan.FromSeconds(40), flags)),
            marker => NpcChatter.KindOf(marker.Key) == NpcChatterKind.Combat);
    }

    [Fact]
    public void AKillOlderThanTwoMinutesIsNotReactedTo()
    {
        var fight = new NearbyFight();
        var callout = Callout(fight);

        _ = callout.Examine(Context(T0)).ToArray();
        fight.Fold(Bounty("Vance"), T0 + TimeSpan.FromSeconds(10), priming: false);

        Assert.DoesNotContain(
            callout.Examine(Context(T0 + TimeSpan.FromSeconds(131))),
            marker => NpcChatter.KindOf(marker.Key) == NpcChatterKind.Combat);
    }

    [Fact]
    public void ACombatExchangeRestartsTheOrdinaryGap()
    {
        var fight = new NearbyFight();
        var callout = Callout(fight);

        _ = callout.Examine(Context(T0)).ToArray();
        fight.Fold(Bounty("Vance"), T0 + TimeSpan.FromSeconds(4 * 60 + 40), priming: false);
        Assert.Single(callout.Examine(Context(T0 + TimeSpan.FromSeconds(5 * 60 + 1))));

        Assert.Empty(callout.Examine(Context(T0 + TimeSpan.FromSeconds(5 * 60 + 100))));
    }

    [Fact]
    public void TheCombatSceneNamesAnNpcPilotAsDestroyedAndNeverAPlayer()
    {
        var fight = new NearbyFight();
        fight.Fold(Bounty("Vance"), T0, priming: false);

        var npc = NpcChatter.Instruction(NpcChatterKind.Combat, fight: fight.Snapshot);

        Assert.Contains("Vance", npc, StringComparison.Ordinal);
        Assert.Contains("No destroyed pilot speaks", npc, StringComparison.Ordinal);
        Assert.Contains("Arakang Purple Drug Empire", npc, StringComparison.Ordinal);

        var players = new NearbyFight();
        players.Fold(PlayerKill, T0, priming: false);

        var player = NpcChatter.Instruction(NpcChatterKind.Combat, fight: players.Snapshot);

        Assert.DoesNotContain("Jameson", player, StringComparison.Ordinal);
        Assert.DoesNotContain("A Commander", player, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCombatKeyReadsBackAsCombat() =>
        Assert.Equal(NpcChatterKind.Combat, NpcChatter.KindOf("npc.chatter.combat"));
}
