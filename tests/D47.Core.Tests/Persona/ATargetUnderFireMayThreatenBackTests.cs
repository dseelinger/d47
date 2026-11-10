using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class ATargetUnderFireMayThreatenBackTests
{
    private static readonly TargetedShip Pilot = new("Cobra Mk III", "Jessa Corvane");

    private static async Task<Speaker> SpeakerAsync(bool? fightOn)
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { Target = Pilot });
        var line = new TargetLine(() => Pilot, fightOn: fightOn is { } on ? () => on : null);

        var decision = await line.RouteAsync(channels.Selected!, "who are you", TestContext.Current.CancellationToken);

        return Assert.IsType<LineDecision.Taken>(decision).Speaker;
    }

    private static async Task<string> ScreenedAsync(bool? fightOn, string reply) =>
        (await SpeakerAsync(fightOn)).Screen!(reply);

    [Fact]
    public async Task AThreatIsSpokenWhileAFightIsOn()
    {
        Assert.Equal("Try that again and I'll blow you apart", await ScreenedAsync(true, "Try that again and I'll blow you apart"));
        Assert.Equal("I'll shoot you down.", await ScreenedAsync(true, "I'll shoot you down."));
    }

    [Theory]
    [InlineData("I'm scanning you now")]
    [InlineData("Drop your cargo")]
    [InlineData("I'll fine you for that")]
    public async Task OtherEscalationsStillBecomeASignOffDuringAFight(string reply)
    {
        Assert.Equal(NpcsHeardHere.SignOff("Jessa Corvane"), await ScreenedAsync(true, reply));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task AThreatBecomesASignOffWhenNoFightIsOn(bool? fightOn)
    {
        Assert.Equal(NpcsHeardHere.SignOff("Jessa Corvane"), await ScreenedAsync(fightOn, "Try that again and I'll blow you apart. Guns on you."));
    }

    [Fact]
    public async Task TheBriefSaysThereIsAFightAndAllowsThreatsOnlyDuringOne()
    {
        var fight = (await SpeakerAsync(true)).Brief;
        var calm = (await SpeakerAsync(false)).Brief;

        Assert.Contains("There is a fight going on around you.", fight, StringComparison.Ordinal);
        Assert.Contains("threaten the Commander and trade insults", fight, StringComparison.Ordinal);
        Assert.DoesNotContain("interdict, target", fight, StringComparison.Ordinal);
        Assert.Contains("scan, interdict, fine", fight, StringComparison.Ordinal);
        Assert.DoesNotContain("fight going on", calm, StringComparison.Ordinal);
        Assert.Contains("scan, interdict, target, fine", calm, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("I'll shoot you")]
    [InlineData("guns on you")]
    [InlineData("lock on to you")]
    [InlineData("I'm going to destroy you")]
    [InlineData("You're under attack")]
    [InlineData("Scanning you now")]
    [InlineData("Drop your cargo")]
    public void EscalatesStillMatchesWeaponsAndTheRest(string text)
    {
        Assert.True(NpcChatter.Escalates(text));
    }

    [Theory]
    [InlineData("I'll shoot you")]
    [InlineData("guns on you")]
    [InlineData("lock on to you")]
    [InlineData("I'm going to destroy you")]
    [InlineData("You're under attack")]
    public void EscalatesBeyondAFightLeavesWeaponsAlone(string text)
    {
        Assert.False(NpcChatter.EscalatesBeyondAFight(text));
    }

    [Theory]
    [InlineData("Scanning you now")]
    [InlineData("Drop your cargo")]
    [InlineData("I'm scanning you now")]
    public void EscalatesBeyondAFightKeepsTheRest(string text)
    {
        Assert.True(NpcChatter.EscalatesBeyondAFight(text));
    }
}
