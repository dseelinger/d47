using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class TheCarrierKnowsAFightInItsSystemTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly CarrierState Carrier = new() { CallSign = "BNH-T2F", StarSystem = "Sol" };

    private static JournalEvent Event(string json)
    {
        var root = JsonDocument.Parse(json).RootElement;

        return new JournalEvent(Start, root.GetProperty("event").GetString()!, root);
    }

    private static NearbyFight FoughtIn(string system, bool priming = false, bool kill = true)
    {
        var fight = new NearbyFight();
        fight.Fold(Event($$"""{ "event":"Location", "StarSystem":"{{system}}" }"""), Start, priming: true);
        fight.Fold(Event("""{ "event":"UnderAttack", "Target":"You" }"""), Start, priming);

        if (kill)
        {
            fight.Fold(
                Event("""{ "event":"Bounty", "VictimFaction":"Sol Workers' Party", "Target":"viper", "PilotName_Localised":"Rex" }"""),
                Start,
                priming);
        }

        return fight;
    }

    [Fact]
    public void BeingAttackedWithNoKillsIsSaidWithHowLongAgo()
    {
        var fight = FoughtIn("Sol", kill: false);

        var fact = CarrierFight.Describe(fight.Snapshot, "Sol", Carrier, Start.AddMinutes(3));

        Assert.Equal("The Commander was attacked in this system 3 minutes ago.", fact);
    }

    [Fact]
    public void KillsAreCountedWithTheLastVictimsFaction()
    {
        var fight = FoughtIn("Sol");

        var fact = CarrierFight.Describe(fight.Snapshot, "Sol", Carrier, Start.AddMinutes(1));

        Assert.StartsWith("The Commander has destroyed 1 ship in this system; the last was 1 minute ago, of ", fact);
        Assert.Contains("Workers", fact);
    }

    [Fact]
    public void ACarrierInAnotherSystemKnowsNothing()
    {
        var fight = FoughtIn("Sol");

        Assert.Null(CarrierFight.Describe(fight.Snapshot, "Sol", Carrier with { StarSystem = "Achenar" }, Start));
    }

    [Fact]
    public void NoOwnedCarrierKnowsNothing()
    {
        var fight = FoughtIn("Sol");

        Assert.Null(CarrierFight.Describe(fight.Snapshot, "Sol", CarrierState.None, Start));
    }

    [Fact]
    public void AJumpOutEndsIt()
    {
        var fight = FoughtIn("Sol");
        fight.Fold(Event("""{ "event":"FSDJump", "StarSystem":"Achenar" }"""), Start, priming: false);

        Assert.Null(CarrierFight.Describe(fight.Snapshot, "Achenar", Carrier with { StarSystem = "Achenar" }, Start));
        Assert.Null(CarrierFight.Describe(fight.Snapshot, "Achenar", Carrier, Start));
    }

    [Fact]
    public void AFightReadWhilePrimingHasNoTimeAndSoNoFact()
    {
        var fight = FoughtIn("Sol", priming: true);

        Assert.Equal(1, fight.Snapshot.Kills);
        Assert.Null(CarrierFight.Describe(fight.Snapshot, "Sol", Carrier, Start));
    }

    [Fact]
    public void TheTowerAndCaptainBriefsCarryTheFactAndOthersDoNot()
    {
        var tower = new Announcement("carrier.test", "Tower here.") { Voice = VoiceRole.TowerControl };
        var other = new Announcement("carrier.test", "Tower here.") { Voice = VoiceRole.ShipAi };

        var with = FlavourBriefs.For(tower, personalityEnabled: true, fight: "The Commander was attacked in this system 3 minutes ago.");
        var without = FlavourBriefs.For(tower, personalityEnabled: true);

        Assert.Contains("attacked in this system 3 minutes ago", with!.Instruction);
        Assert.DoesNotContain("attacked", without!.Instruction);
        Assert.DoesNotContain(
            "attacked in this system",
            FlavourBriefs.For(other, personalityEnabled: true, fight: "The Commander was attacked in this system 3 minutes ago.")?.Instruction ?? string.Empty);
    }
}
