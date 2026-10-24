using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The held target and a real comms NPC answer only on their own channel, from a personality and never from their own words.</summary>
public class ATargetAndARealCommsNpcAnswerTests
{
    private static readonly DateTimeOffset Now = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Written = "Blunt and impatient. Talks in short orders.";

    private const string TheirWords = "Hand over the cargo or burn, Commander.";

    private static readonly TargetedShip Pilot = new("Cobra Mk III", "Jessa Corvane", "Dangerous", "Federation", "Wanted");

    private static CommsChannels Channels(TargetedShip? target, NpcsHeardHere? roster = null)
    {
        var channels = new CommsChannels();
        channels.Update(new CommsInputs { Target = target, Heard = roster?.Present ?? [] });
        return channels;
    }

    private static Task<string?> Write(NpcHeard npc, CancellationToken cancellationToken) => Task.FromResult<string?>(Written);

    private static Speaker Taken(LineDecision decision) => Assert.IsType<LineDecision.Taken>(decision).Speaker;

    [Fact]
    public async Task TheTargetAnswersWithItsPersonalityShipAndLegalStatus()
    {
        var channels = Channels(Pilot);
        var line = new TargetLine(() => Pilot, writePersonality: Write);

        var speaker = Taken(await line.RouteAsync(channels.Selected!, "who are you", TestContext.Current.CancellationToken));

        Assert.Equal("Jessa Corvane", speaker.Name);
        Assert.Contains("Your personality: " + Written, speaker.Brief, StringComparison.Ordinal);
        Assert.Contains("Cobra Mk III", speaker.Brief, StringComparison.Ordinal);
        Assert.Contains("Wanted", speaker.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACommanderTargetNeverReachesTheModel()
    {
        var commander = new TargetedShip("Python", IsCommander: true);
        var channels = Channels(commander);
        var asked = false;
        var line = new TargetLine(
            () => commander,
            writePersonality: (_, _) =>
            {
                asked = true;
                return Task.FromResult<string?>(Written);
            });

        Assert.Null(channels.Selected);
        Assert.IsType<LineDecision.NotMine>(
            await line.RouteAsync(OnComms.Target("Python"), "who are you", TestContext.Current.CancellationToken));
        Assert.False(asked);
    }

    [Fact]
    public async Task TheTargetIsNotReachedWhenAnotherChannelIsSelected()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", TheirWords) { Speaker = "Vik", CommsChannel = "npc" }, null, Now);
        var channels = Channels(Pilot, roster);
        Assert.True(channels.Select(CommsChannel.System));
        var line = new TargetLine(() => Pilot);

        Assert.IsType<LineDecision.NotMine>(await line.RouteAsync(channels.Selected!, "who are you", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARealCommsNpcAnswersInItsFamilysVoiceWithoutItsMessageInThePrompt()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", TheirWords) { Speaker = "Vik", CommsChannel = "npc" }, null, Now);
        var channels = Channels(null, roster);
        Assert.True(channels.Select(CommsChannel.System, "Vik"));

        var line = new ChatterLine(() => "Diaguandri", roster: () => roster, writePersonality: Write);

        var speaker = Taken(await line.RouteAsync(channels.Selected!, "what do you want", TestContext.Current.CancellationToken));

        Assert.Equal("Vik", speaker.Name);
        Assert.Contains("pirate", speaker.Brief, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Your personality: " + Written, speaker.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain("burn", speaker.Brief, StringComparison.Ordinal);
        Assert.Equal(Written, roster.Find("Vik")!.Personality);
    }

    [Fact]
    public async Task ARealCommsNpcIsNotReachedFromAnotherChannel()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", TheirWords) { Speaker = "Vik", CommsChannel = "npc" }, null, Now);
        var line = new ChatterLine(() => "Diaguandri", roster: () => roster);

        Assert.IsType<LineDecision.NotMine>(
            await line.RouteAsync(OnComms.Target("Vik"), "Vik, what do you want", TestContext.Current.CancellationToken));
    }
}
