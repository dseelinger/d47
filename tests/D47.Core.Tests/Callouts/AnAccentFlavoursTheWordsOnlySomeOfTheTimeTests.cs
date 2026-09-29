using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class AnAccentFlavoursTheWordsOnlySomeOfTheTimeTests
{
    private static readonly VoiceInfo[] Listed =
    [
        new("gb-woman", "Sonia", "en-GB", "Female"),
        new("us-man", "Guy", "en-US", "Male"),
        new("ie-woman", "Emily", "en-IE", "Female"),
        new("au-man", "William", "en-AU", "Male"),
    ];

    private static VoiceCast Cast() => new()
    {
        Pool = VoicePool.From(Listed),
        Feminine = VoicePool.Feminine(Listed),
        British = VoicePool.British(Listed),
        Voices = Listed.ToDictionary(voice => voice.Id, StringComparer.OrdinalIgnoreCase),
        DefaultVoice = "core",
    };

    private static Announcement NpcLine() => new("message.npc", "Clear the lane.")
    {
        Voice = VoiceRole.Comms,
        Speaker = "Hauler Brandt",
        CommsChannel = "npc",
    };

    [Fact]
    public void AtZeroNoLineIsWrittenForItsAccentAndAtOneHundredEveryOneIs()
    {
        var roll = new AccentRoll(new Random(7));
        var cast = Cast();

        for (var i = 0; i < 200; i++)
        {
            Assert.Null(SpeakerAccent.For(cast, NpcLine(), roll, 0));
            Assert.Contains("accent", SpeakerAccent.For(cast, NpcLine(), roll, 100), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AtFiftyAboutHalfTheRollsHit()
    {
        var roll = new AccentRoll(new Random(47));
        var hits = Enumerable.Range(0, 1000).Count(_ => roll.Hits(50));

        Assert.InRange(hits, 450, 550);
    }

    [Fact]
    public void TheChatterPromptCarriesTheAccentSentenceOnlyForSlotsThatRolledIt()
    {
        var roster = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Passersby, 4, "Sol");

        var none = NpcChatter.Instruction(NpcChatterKind.Passersby, roster: roster.Rolled(new AccentRoll(new Random(1)), 0));
        var all = NpcChatter.Instruction(NpcChatterKind.Passersby, roster: roster.Rolled(new AccentRoll(new Random(1)), 100));

        Assert.DoesNotContain(SpeakerAccent.Rules, none, StringComparison.Ordinal);
        Assert.Contains(SpeakerAccent.Rules, all, StringComparison.Ordinal);
        Assert.Contains("Each speaker's words suit their slot's accent", all, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCarrierPostsAreRolledToo()
    {
        var roster = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Passersby, 4, "Sol", towerAccent: "British", captainAccent: "Irish");
        var carrier = new NpcChatterCarrier { Owned = true, Present = true, Called = "Bonny", JumpScheduled = true };

        var none = NpcChatter.Instruction(NpcChatterKind.Passersby, carrier, roster: roster.Rolled(new AccentRoll(new Random(1)), 0));
        var all = NpcChatter.Instruction(NpcChatterKind.Passersby, carrier, roster: roster.Rolled(new AccentRoll(new Random(1)), 100));

        Assert.DoesNotContain("words suit it", none, StringComparison.Ordinal);
        Assert.Contains("Their words suit it the same way", all, StringComparison.Ordinal);
    }

    [Fact]
    public void TheVoiceIsTheSameWhicheverWayTheRollGoes()
    {
        var voices = new[] { 0, 100 }.Select(percent =>
        {
            var cast = Cast();
            var line = NpcLine();
            SpeakerAccent.For(cast, line, new AccentRoll(new Random(3)), percent);
            return SpeakerAccent.VoiceOf(cast, line).VoiceId;
        }).ToList();

        Assert.Equal(voices[0], voices[1]);

        var roster = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Passersby, 4, "Sol");
        Assert.Equal(
            roster.Slots.Select(slot => slot.VoiceId),
            roster.Rolled(new AccentRoll(new Random(3)), 0).Slots.Select(slot => slot.VoiceId));
    }

    [Fact]
    public void ALineSpokenAsReceivedIsNotHandedToTheModel()
    {
        var line = new Announcement("message.npc", "Clear the lane.") { Voice = VoiceRole.Comms, CommsChannel = "npc" };

        Assert.Null(FlavourBriefs.For(line, personalityEnabled: true));
    }
}
