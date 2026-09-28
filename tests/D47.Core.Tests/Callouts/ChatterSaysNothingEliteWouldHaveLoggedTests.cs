using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// An invented person never does to the Commander what Elite would write a journal event for. An exchange
/// with such a line is dropped (#631).
/// </summary>
public class ChatterSaysNothingEliteWouldHaveLoggedTests
{
    public static TheoryData<string, string> EscalatingLines => new()
    {
        { "Interdicted", "Stay right there, I'm going to interdict you." },
        { "Interdicted", "Pulling you out of supercruise, pilot." },
        { "Scanned", "Hold still, I'm scanning you." },
        { "Scanned", "You're being scanned, cut your engines." },
        { "UnderAttack", "One more word and I open fire on you." },
        { "UnderAttack", "Weapons locked on you, Commander." },
        { "CommitCrime", "Security's fining you for loitering." },
        { "CommitCrime", "You've been fined five hundred credits." },
        { "Bounty", "There's a bounty on you in three systems." },
        { "Bounty", "You're wanted in Sol now." },
        { "DockingDenied", "Commander, your docking request is denied." },
        { "DockingDenied", "Commander Vale, docking denied, clear the slot." },
        { "DockingGranted", "You're cleared to dock, pad nine." },
        { "CollectCargo", "I dropped you a few canisters of gold, scoop them up." },
        { "EjectCargo", "Dump your cargo and nobody gets hurt." },
        { "WingInvite", "Sent you a wing invite, accept it." },
        { "Friends", "I'll add you as a friend when we're done." },
    };

    [Theory]
    [MemberData(nameof(EscalatingLines))]
    public void EachEventEliteWouldLogHasALineThatTripsIt(string journalEvent, string line)
    {
        Assert.True(NpcChatter.Escalates(line), $"{journalEvent}: {line}");
    }

    [Theory]
    [InlineData("She's fine, just tired.")]
    [InlineData("The dock hand says pad four is clear.")]
    [InlineData("Fine work on that landing.")]
    [InlineData("I spent the morning scanning the market board.")]
    [InlineData("You look fine to me.")]
    [InlineData("Have you scanned the market yet?")]
    [InlineData("Got interdicted twice on the way in.")]
    [InlineData("Docking granted, pad twelve.")]
    [InlineData("That's fine for you, I suppose.")]
    public void OrdinaryWordsDoNotTripIt(string line)
    {
        Assert.False(NpcChatter.Escalates(line), line);
    }

    [Fact]
    public void OneScanOfTheCommanderDropsTheWholeExchange()
    {
        var lines = NpcChatter.Parse(
            "Vance: Quiet run tonight.\n"
            + "Ressa: Hold still, I'm scanning you.\n"
            + "Vance: Ha. Funny.",
            NpcChatterKind.Passersby);

        Assert.Empty(lines);
    }

    [Fact]
    public void ASlottedExchangeIsDroppedTheSameWay()
    {
        var roster = new NpcChatterRoster(
        [
            new NpcChatterSlot("A", "voice-a", null, VoiceGender.Feminine),
            new NpcChatterSlot("B", "voice-b", null, VoiceGender.Masculine),
        ]);

        Assert.Empty(NpcChatter.Parse(
            "Ressa [A]: Nice ship.\nVance [B]: There's a bounty on you, Commander.",
            NpcChatterKind.Passersby,
            roster: roster));

        Assert.Equal(2, NpcChatter.Parse(
            "Ressa [A]: Nice ship.\nVance [B]: Shame about the paint.",
            NpcChatterKind.Passersby,
            roster: roster).Count);
    }

    [Fact]
    public void AControllersYouIsItsOwnPilot()
    {
        var routine = NpcChatter.Parse(
            "Vera Kolt: Requesting docking, pad nine if it's free.\n"
            + "Dock Control: You're cleared to dock, pad nine. Customs will be scanning you on arrival.",
            NpcChatterKind.Controller);

        Assert.Equal(2, routine.Count);

        Assert.Empty(NpcChatter.Parse(
            "Vera Kolt: Requesting docking, pad nine.\n"
            + "Dock Control: Granted. And Commander, your docking request is denied.",
            NpcChatterKind.Controller));
    }

    [Fact]
    public void ARewrittenLineThatEscalatesIsNoLine()
    {
        Assert.Null(NpcChatter.Rewritten("Vance: Hold still, I'm scanning you.", null));
        Assert.Equal("Nice paint job.", NpcChatter.Rewritten("Vance: Nice paint job.", null));
    }

    [Fact]
    public void TheModelIsToldTheRule()
    {
        var instruction = NpcChatter.Instruction(
            NpcChatterKind.Hail,
            NpcChatterCarrier.None,
            docked: true,
            spotlight: false,
            exchangeIndex: 0);

        Assert.Contains("Nobody scans, interdicts, targets, fines or puts a bounty on the Commander", instruction, StringComparison.Ordinal);
    }
}
