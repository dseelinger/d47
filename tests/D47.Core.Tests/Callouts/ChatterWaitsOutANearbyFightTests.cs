using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Invented chatter is held while a fight is on nearby, and the fight is folded from the journal (#582).</summary>
public class ChatterWaitsOutANearbyFightTests
{
    private static readonly DateTimeOffset T0 = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Bounty(string pilot, string target = "eagle") => Event(
        $$"""{"timestamp":"2026-09-24T14:05:09Z","event":"Bounty","PilotName":"$npc_name_decorate:#name={{pilot}};","PilotName_Localised":"{{pilot}}","Target":"{{target}}","TotalReward":1000,"VictimFaction":"Arakang Purple Drug Empire"}""");

    private static readonly JournalEvent UnderAttack =
        Event("""{"timestamp":"2026-09-24T14:09:03Z","event":"UnderAttack","Target":"You"}""");

    private static JournalEvent Jump(string system) => Event(
        $$"""{"timestamp":"2026-09-24T14:20:00Z","event":"FSDJump","StarSystem":"{{system}}","StarPos":[0,0,0]}""");

    private static CalloutContext Context(DateTimeOffset now, StatusFlags flags = StatusFlags.InMainShip) =>
        new(now, false, null, GameStatus.Unknown with { Flags = flags }, NavRoute.None, []);

    private static NpcChatterCallout Callout(NearbyFight fight) => new(fight)
    {
        Interval = TimeSpan.FromMinutes(20),
        Longest = TimeSpan.FromMinutes(20),
        Settle = TimeSpan.Zero,
    };

    private static IEnumerable<JournalEvent> Mot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var path = Path.Combine(
            directory.FullName, "tests", "fixtures", "kills", "Journal.2026-09-24T090721.01.log");

        return File.ReadAllLines(path).Where(line => line.Length > 0).Select(Event);
    }

    [Fact]
    public void TheKillsFixtureFoldsEveryKillAndEveryPilot()
    {
        var fight = new NearbyFight();

        foreach (var journalEvent in Mot())
        {
            fight.Fold(journalEvent, journalEvent.Timestamp, priming: false);
        }

        Assert.Equal(6, fight.Snapshot.Kills);
        Assert.Equal(
            ["Brian Lewis", "KazDav Cain", "Malbadin Thelatin", "Paul Curnow", "Peter Pringle", "Sillanp"],
            fight.Snapshot.Dead.Order(StringComparer.Ordinal));
        Assert.Equal("Malbadin Thelatin", fight.Snapshot.LastVictim);
        Assert.Equal("Narveti Purple Drug Empire", fight.Snapshot.LastVictimFaction);
    }

    [Fact]
    public void AnExchangeDueDuringAnAttackWaitsUntilTheFightHasHeld()
    {
        var fight = new NearbyFight();
        var callout = Callout(fight);

        _ = callout.Examine(Context(T0)).ToArray();

        var attacked = T0 + TimeSpan.FromMinutes(21);

        fight.Fold(UnderAttack, attacked, priming: false);

        Assert.Empty(callout.Examine(Context(attacked)));
        Assert.Empty(callout.Examine(Context(attacked + TimeSpan.FromSeconds(59))));
        Assert.Single(callout.Examine(Context(attacked + NearbyFight.Holds)));
    }

    [Fact]
    public void AKillOnItsOwnHoldsTheExchangeToo()
    {
        var fight = new NearbyFight();
        var callout = Callout(fight);

        _ = callout.Examine(Context(T0)).ToArray();

        var killed = T0 + TimeSpan.FromMinutes(21);

        fight.Fold(Bounty("Paul Curnow"), killed, priming: false);

        Assert.Empty(callout.Examine(Context(killed + TimeSpan.FromSeconds(10))));

        var marker = Assert.Single(callout.Examine(Context(killed + TimeSpan.FromSeconds(30))));
        Assert.Equal(NpcChatterKind.Combat, NpcChatter.KindOf(marker.Key));
    }

    [Fact]
    public void AnExchangeDueWhileInDangerWaitsForTheFlagToClear()
    {
        var callout = Callout(new NearbyFight());

        _ = callout.Examine(Context(T0)).ToArray();

        Assert.Empty(callout.Examine(Context(
            T0 + TimeSpan.FromMinutes(21), StatusFlags.InMainShip | StatusFlags.InDanger)));
        Assert.Single(callout.Examine(Context(T0 + TimeSpan.FromMinutes(22))));
    }

    [Fact]
    public void AKillReadAtStartupNamesTheDeadButHoldsNothing()
    {
        var fight = new NearbyFight();

        fight.Fold(Bounty("Paul Curnow"), T0, priming: true);

        Assert.Contains("Paul Curnow", fight.Snapshot.Dead);
        Assert.Equal(1, fight.Snapshot.Kills);
        Assert.Null(fight.Snapshot.LastKillAt);
        Assert.Null(fight.Snapshot.LastActionAt);
        Assert.False(fight.On(T0, GameStatus.Unknown));
    }

    [Fact]
    public void APlayerKillPutsNoPlayerNameInTheSnapshot()
    {
        var fight = new NearbyFight();

        fight.Fold(
            Event("""{"timestamp":"2026-09-24T14:05:09Z","event":"Bounty","PilotName":"$cmdr_decorate:#name=Jameson;","PilotName_Localised":"CMDR Jameson","Target":"python","TotalReward":1000}"""),
            T0,
            priming: false);

        Assert.Empty(fight.Snapshot.Dead);
        Assert.Equal("A Commander", fight.Snapshot.LastVictim);
        Assert.DoesNotContain("Jameson", fight.Snapshot.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABondCountsAKillButNamesNobody()
    {
        var fight = new NearbyFight();

        fight.Fold(Bounty("Paul Curnow"), T0, priming: false);
        fight.Fold(
            Event("""{"timestamp":"2025-07-06T23:07:13Z","event":"FactionKillBond","Reward":34070,"AwardingFaction":"HIP 92720 Independents","VictimFaction":"Sagartians Federal Industries"}"""),
            T0,
            priming: false);

        Assert.Equal(2, fight.Snapshot.Kills);
        Assert.Equal(["Paul Curnow"], fight.Snapshot.Dead);
        Assert.Null(fight.Snapshot.LastVictim);
        Assert.Null(fight.Snapshot.LastShip);
        Assert.Equal("Sagartians Federal Industries", fight.Snapshot.LastVictimFaction);
    }

    [Fact]
    public void AnOnFootKillIsAKillWithNoShip()
    {
        var fight = new NearbyFight();

        fight.Fold(Bounty("Hugh Juarez", "lightassaultsuitai_class1"), T0, priming: false);

        Assert.Contains("Hugh Juarez", fight.Snapshot.Dead);
        Assert.Null(fight.Snapshot.LastShip);
    }

    [Fact]
    public void ArrivingInAnotherSystemEndsTheFight()
    {
        var fight = new NearbyFight();

        fight.Fold(Jump("Mot"), T0, priming: false);
        fight.Fold(Bounty("Paul Curnow"), T0, priming: false);
        fight.Fold(
            Event("""{"timestamp":"2026-09-24T14:21:00Z","event":"Location","StarSystem":"Mot","StarPos":[0,0,0]}"""),
            T0,
            priming: false);

        Assert.Equal(1, fight.Snapshot.Kills);

        fight.Fold(Jump("Deciat"), T0, priming: false);

        Assert.Same(FightSnapshot.None, fight.Snapshot);
    }

    [Fact]
    public void LoadGameAndDeathEndTheFight()
    {
        foreach (var ending in new[] { "LoadGame", "Died" })
        {
            var fight = new NearbyFight();

            fight.Fold(Bounty("Paul Curnow"), T0, priming: false);
            fight.Fold(Event($$"""{"timestamp":"2026-09-24T14:30:00Z","event":"{{ending}}"}"""), T0, priming: false);

            Assert.Same(FightSnapshot.None, fight.Snapshot);
        }
    }
}
