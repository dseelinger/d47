using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;
using Sex = D47.Core.Audio.VoiceGender;

namespace D47.Core.Tests.Callouts;

/// <summary>An invented speaker states their sex on their first line, and d47 keeps it while they are in the system (#2008).</summary>
public class AnInventedSpeakerKeepsTheSexTheirFirstLineStatedTests
{
    private static readonly DateTimeOffset Now = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly VoiceInfo[] Listed =
    [
        new("woman", "Sonia", "en-GB", "Female"),
        new("man", "Guy", "en-US", "Male"),
        new("plain", "Plain", "en"),
        new("plain2", "Plain Two", "en"),
    ];

    private static VoiceCast Cast(params VoiceInfo[] voices) => new()
    {
        Pool = VoicePool.From(voices),
        Feminine = VoicePool.Feminine(voices),
        British = VoicePool.British(voices),
        Voices = voices.ToDictionary(voice => voice.Id, StringComparer.OrdinalIgnoreCase),
        DefaultVoice = "core",
    };

    private static NpcChatterRoster RosterOf(Sex first) =>
        new([new NpcChatterSlot("A", "v1", null, first), new NpcChatterSlot("B", "v2", null, Sex.Unlabelled)]);

    private static IReadOnlyList<NpcChatterLine> Parsed(string script, NpcChatterRoster roster) =>
        NpcChatter.Parse(script, NpcChatterKind.Passersby, roster: roster);

    private static Announcement Said(NpcChatterLine line) =>
        new(NpcChatter.LineKey, line.Text) { Speaker = line.Name, CommsChannel = "npc", Invented = new NpcChatterHeard(line, 1) };

    private static Speaker Taken(LineDecision decision) => Assert.IsType<LineDecision.Taken>(decision).Speaker;

    [Fact]
    public void AStatedSexIsReadOffTheLineHead()
    {
        var lines = Parsed("Mira Kessel [A, female]: Kettle's on.\nOld Pike [B, male]: Took your time.", RosterOf(Sex.Unlabelled));

        Assert.Equal(2, lines.Count);
        Assert.Equal("Mira Kessel", lines[0].Name);
        Assert.Equal(Sex.Feminine, lines[0].Sex);
        Assert.Equal(Sex.Masculine, lines[1].Sex);
    }

    [Fact]
    public void APersonSlotRecordsTheSexTheLineStates()
    {
        var lines = Parsed("Mira Kessel [A, male]: Kettle's on.\nOld Pike [B]: Took your time.", RosterOf(Sex.Unlabelled));

        Assert.Equal(Sex.Masculine, lines[0].Sex);
    }

    [Fact]
    public void AWomansSlotWinsOverALineThatStatesMale()
    {
        var lines = Parsed("Mira Kessel [A, male]: Kettle's on.\nOld Pike [B]: Took your time.", RosterOf(Sex.Feminine));

        Assert.Equal(Sex.Feminine, lines[0].Sex);
    }

    [Fact]
    public void ALineWithNoStatedSexRecordsIndeterminateAndStillSpeaks()
    {
        var lines = Parsed("Mira Kessel [A]: Kettle's on.\nOld Pike [B]: Took your time.", RosterOf(Sex.Unlabelled));

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal(Sex.Unlabelled, line.Sex));
    }

    [Fact]
    public void TheRosterRecordsTheSexAndAnUnsexedLaterLineKeepsIt()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(Said(new NpcChatterLine("Mira Kessel", "Kettle's on.", null, "v1", Sex.Feminine)), "v1", Now);
        roster.Heard(Said(new NpcChatterLine("Mira Kessel", "Again.", null, "v1")), "v1", Now);

        Assert.Equal(Sex.Feminine, roster.Find("Mira Kessel")!.Sex);
    }

    [Fact]
    public void AnExchangeThatOffersTheNpcBackDescribesThemWithTheRecordedSex()
    {
        var cast = Cast(Listed[2..]);
        cast.Keep("Mira Kessel", "plain");
        cast.Keep("Old Pike", "plain2");

        var roster = NpcChatterRoster.Cast(
            cast,
            NpcChatterKind.Passersby,
            4,
            "Sol",
            sexOf: name => name == "Mira Kessel" ? Sex.Feminine : Sex.Unlabelled);

        var prompt = NpcChatter.Instruction(NpcChatterKind.Passersby, NpcChatterCarrier.None, false, false, 4, null, roster);

        Assert.Contains("Mira Kessel, already heard in this system, a woman", prompt, StringComparison.Ordinal);
        Assert.Contains("Old Pike, already heard in this system, a person", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplyBriefForAFemaleInventedNpcSaysSoAndIndeterminateSaysNothing()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(Said(new NpcChatterLine("Mira Kessel", "Kettle's on.", null, "v1", Sex.Feminine)), "v1", Now);
        roster.Heard(Said(new NpcChatterLine("Old Pike", "Took your time.", null, "v2")), "v2", Now);

        var line = new ChatterLine(() => "Sol", roster: () => roster);
        line.Heard(new NpcChatterLine("Mira Kessel", "Kettle's on.", null, "v1", Sex.Feminine), 1);
        line.Heard(new NpcChatterLine("Old Pike", "Took your time.", null, "v2"), 1);

        var token = TestContext.Current.CancellationToken;
        var kessel = Taken(await line.RouteAsync(OnComms.System("Mira Kessel"), "hello", token));
        var pike = Taken(await line.RouteAsync(OnComms.System("Old Pike"), "hello", token));

        Assert.Contains("You are a woman.", kessel.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain("You are a woman", pike.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain("You are a man", pike.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReplyBriefForACommsNpcWithAFemaleNameReadingSaysSo()
    {
        var roster = new NpcsHeardHere();
        roster.Heard(new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", "Hand over the cargo.") { Speaker = "Vik Rane", CommsChannel = "npc" }, null, Now);
        roster.Heard(new Announcement(IncomingMessages.CannedKeyPrefix + "Pirate", "Hand over the cargo.") { Speaker = "Sam Orr", CommsChannel = "npc" }, null, Now);

        var line = new ChatterLine(
            () => "Sol",
            roster: () => roster,
            sexOfGivenName: given => given == "Vik" ? "female" : "indeterminate");

        var token = TestContext.Current.CancellationToken;
        var vik = Taken(await line.RouteAsync(OnComms.System("Vik Rane"), "hello", token));
        var sam = Taken(await line.RouteAsync(OnComms.System("Sam Orr"), "hello", token));

        Assert.Contains("You are a woman.", vik.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain("You are a woman", sam.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain("You are a man", sam.Brief, StringComparison.Ordinal);
    }
}
