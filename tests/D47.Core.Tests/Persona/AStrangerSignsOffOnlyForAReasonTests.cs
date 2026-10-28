using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The brief of a stranger or a comms NPC makes leaving conditional and rules out invented reasons.</summary>
public class AStrangerSignsOffOnlyForAReasonTests
{
    private static readonly DateTimeOffset Now = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static void AssertConditional(string brief, string name)
    {
        Assert.Contains("Keep talking for as long as the Commander does.", brief, StringComparison.Ordinal);
        Assert.Contains("Never invent an emergency, a scanner contact or an interruption", brief, StringComparison.Ordinal);
        Assert.Contains("When you do leave, end your reply with exactly: " + NpcsHeardHere.SignOff(name), brief, StringComparison.Ordinal);
        Assert.DoesNotContain("When you are done talking", brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInventedStrangersBriefLeavesOnlyForAReason()
    {
        var roster = new NpcsHeardHere();
        var line = new ChatterLine(() => "Sol", roster: () => roster);
        var said = new NpcChatterLine("Vance", "Nice ship, Commander.", VoiceId: null);

        roster.Heard(
            new Announcement(NpcChatter.LineKey, said.Text) { Speaker = "Vance", Invented = new NpcChatterHeard(said, 7) },
            null,
            Now);
        line.Heard(said, 7);

        var decision = await line.RouteAsync(OnComms.System("Vance"), "hello", TestContext.Current.CancellationToken);

        AssertConditional(Assert.IsType<LineDecision.Taken>(decision).Speaker.Brief, "Vance");
    }

    [Fact]
    public async Task ACommsNpcsBriefLeavesOnlyForAReason()
    {
        var roster = new NpcsHeardHere();
        var line = new ChatterLine(() => "Sol", roster: () => roster);

        roster.Heard(
            new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", "Their own words.") { Speaker = "Vik Rane", CommsChannel = "npc" },
            null,
            Now);

        var decision = await line.RouteAsync(OnComms.System("Vik Rane"), "hello", TestContext.Current.CancellationToken);

        AssertConditional(Assert.IsType<LineDecision.Taken>(decision).Speaker.Brief, "Vik Rane");
    }
}
