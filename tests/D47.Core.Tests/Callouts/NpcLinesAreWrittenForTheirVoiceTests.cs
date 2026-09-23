using D47.Core.Audio;
using D47.Core.Callouts;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// An NPC's voice is chosen before its line is written, and the prompt names that voice's accent (#415).
/// </summary>
public class NpcLinesAreWrittenForTheirVoiceTests
{
    private static readonly VoiceInfo[] Listed =
    [
        new("gb-woman", "Sonia", "en-GB", "Female"),
        new("us-man", "Guy", "en-US", "Male"),
        new("ie-woman", "Emily", "en-IE", "Female"),
        new("au-man", "William", "en-AU", "Male"),
        new("plain", "Plain", "en"),
    ];

    private static VoiceCast Cast(params VoiceInfo[] voices)
    {
        var listed = voices.Length > 0 ? voices : Listed;

        return new VoiceCast
        {
            Pool = VoicePool.From(listed),
            Feminine = VoicePool.Feminine(listed),
            British = VoicePool.British(listed),
            Voices = listed.ToDictionary(voice => voice.Id, StringComparer.OrdinalIgnoreCase),
            DefaultVoice = "core",
        };
    }

    [Fact]
    public void AReVoicedCommsLineIsWrittenForTheVoiceThatSpeaksIt()
    {
        var cast = Cast(Listed[..4]);
        var line = new Announcement("message.npc", "Clear the lane.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Hauler Brandt",
            CommsChannel = "npc",
        };

        var brief = SpeakerAccent.For(cast, line);
        var spoken = SpeakerAccent.VoiceOf(cast, line).VoiceId;

        Assert.NotNull(brief);
        Assert.Contains($" {cast.AccentOf(spoken)} accent", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void ACarrierLineIsWrittenForItsPairedVoice()
    {
        var cast = Cast();
        cast.Assign(VoiceRole.TowerControl, "ie-woman");

        var line = new Announcement("carrier.welcome", "Welcome aboard.") { Voice = VoiceRole.TowerControl };

        Assert.Equal("ie-woman", SpeakerAccent.VoiceOf(cast, line).VoiceId);
        Assert.StartsWith("Your voice has an Irish accent.", SpeakerAccent.For(cast, line), StringComparison.Ordinal);
    }

    [Fact]
    public void ACoreLineGetsNoAccent()
    {
        var cast = Cast();
        cast.DefaultVoice = "gb-woman";

        Assert.Null(SpeakerAccent.For(cast, new Announcement("arrival", "We're here.")));
    }

    [Fact]
    public void AVoiceWithNoAccentAddsNoHint()
    {
        var cast = Cast(new VoiceInfo("plain", "Plain", "en"), new VoiceInfo("bare", "Bare", string.Empty));
        cast.Assign(VoiceRole.CarrierCaptain, "plain");
        cast.Assign(VoiceRole.TowerControl, "bare");

        Assert.Null(SpeakerAccent.For(cast, new Announcement("carrier.a", "x") { Voice = VoiceRole.CarrierCaptain }));
        Assert.Null(SpeakerAccent.For(cast, new Announcement("carrier.b", "x") { Voice = VoiceRole.TowerControl }));
        Assert.Equal("a brief", SpeakerAccent.Join("a brief", null));
    }

    [Fact]
    public void AnAccentLabelIsReadAsWellAsALocale()
    {
        Assert.Equal("British", VoicePool.AccentOf(new VoiceInfo("x", "x", "british")));
        Assert.Equal("American", VoicePool.AccentOf(new VoiceInfo("x", "x", "en-US")));
        Assert.Null(VoicePool.AccentOf(new VoiceInfo("x", "x", "en")));
        Assert.Null(VoicePool.AccentOf(new VoiceInfo("x", "x", "fr-FR")));
    }

    [Fact]
    public void ThePromptPermitsIdiomAndForbidsCaricature()
    {
        var rules = SpeakerAccent.Rules;

        Assert.Contains("regional word choice, idiom and rhythm", rules, StringComparison.Ordinal);
        Assert.Contains("never spell the accent out phonetically", rules, StringComparison.Ordinal);
        Assert.Contains("never lean on a stereotype", rules, StringComparison.Ordinal);
        Assert.Contains("never make the dialect the joke", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void AChatterLineIsSpokenInItsSlotsVoiceWhateverItsName()
    {
        var cast = Cast();
        var roster = NpcChatterRoster.Cast(cast, NpcChatterKind.Passersby, 4, "Shinrarta Dezhra");

        var lines = NpcChatter.Parse(
            "Mags Tolliver [A]: Kettle's on.\nOld Pike [B]: Took your time.",
            NpcChatterKind.Passersby,
            roster: roster);

        Assert.Equal(roster.Slots[0].VoiceId, lines[0].VoiceId);
        Assert.Equal(roster.Slots[1].VoiceId, lines[1].VoiceId);

        foreach (var line in lines)
        {
            cast.Keep(line.Name, line.VoiceId!);
        }

        var said = new Announcement(NpcChatter.LineKey, "Kettle's on.")
        {
            Voice = VoiceRole.Comms,
            Speaker = "Mags Tolliver",
            CommsChannel = "npc",
        };

        Assert.Equal(roster.Slots[0].VoiceId, SpeakerAccent.VoiceOf(cast, said).VoiceId);
    }

    [Fact]
    public void TheChatterPromptNamesEachSlotsAccent()
    {
        var cast = Cast();
        var roster = NpcChatterRoster.Cast(cast, NpcChatterKind.Passersby, 4, "Shinrarta Dezhra");

        var instruction = NpcChatter.Instruction(NpcChatterKind.Passersby, roster: roster);

        foreach (var slot in roster.Slots)
        {
            Assert.Contains($"[{slot.Tag}]", instruction, StringComparison.Ordinal);
            if (slot.Accent is { } accent)
            {
                Assert.Contains($"{accent} accent", instruction, StringComparison.Ordinal);
            }
        }

        Assert.Contains(SpeakerAccent.Rules, instruction, StringComparison.Ordinal);
        Assert.Contains("Name [slot]: words", instruction, StringComparison.Ordinal);
    }

    [Fact]
    public void ALineWithAMissingOrUnknownSlotIsDropped()
    {
        var roster = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Passersby, 4, "Sol");

        var lines = NpcChatter.Parse(
            "Mags [A]: Kettle's on.\nPike: Took your time.\nVex [Q]: Who's asking?\nDot [B]: Me.",
            NpcChatterKind.Passersby,
            roster: roster);

        Assert.Equal(["Mags", "Dot"], lines.Select(line => line.Name));
    }

    [Fact]
    public void TwoSlotsCannotShareAName()
    {
        var roster = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Passersby, 4, "Sol");

        var lines = NpcChatter.Parse(
            "Mags [A]: Kettle's on.\nMags [B]: Took your time.\nMags [A]: Suit yourself.",
            NpcChatterKind.Passersby,
            roster: roster);

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal(roster.Slots[0].VoiceId, line.VoiceId));
    }

    [Fact]
    public void AnNpcAlreadyHeardHereKeepsTheirVoice()
    {
        var cast = Cast();
        cast.Keep("Jen Okafor", "au-man");

        var roster = NpcChatterRoster.Cast(cast, NpcChatterKind.Passersby, 9, "Sol");
        var met = Assert.Single(roster.Slots, slot => slot.Name == "Jen Okafor");

        Assert.Equal("au-man", met.VoiceId);
        Assert.DoesNotContain(roster.Slots, slot => slot.Name is null && slot.VoiceId == "au-man");
        Assert.Contains("Jen Okafor, already heard in this system", NpcChatter.Instruction(NpcChatterKind.Passersby, roster: roster), StringComparison.Ordinal);

        // Written into a fresh slot, the name still carries the voice it already had.
        var lines = NpcChatter.Parse(
            "Jen Okafor [A]: Back again.\nTam [B]: Never left.",
            NpcChatterKind.Passersby,
            roster: roster);

        Assert.Equal("au-man", lines[0].VoiceId);
    }

    [Fact]
    public void AnEmpireStationsRosterPrefersBritishVoices()
    {
        var cast = Cast();
        var roster = NpcChatterRoster.Cast(cast, NpcChatterKind.Hail, 2, "Achenar", allegiance: "Empire");

        Assert.Equal("gb-woman", Assert.Single(roster.Slots).VoiceId);
    }

    [Fact]
    public void TheSameExchangeInTheSameSystemIsCastTheSameWay()
    {
        var first = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Passersby, 12, "Sol");
        var again = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Passersby, 12, "Sol");

        Assert.Equal(first.Slots, again.Slots);
    }

    [Fact]
    public void TheTowerAndCaptainAreDescribedByTheirVoicesAccents()
    {
        var carrier = new NpcChatterCarrier { Owned = true, Present = true, JumpScheduled = true };
        var roster = NpcChatterRoster.Cast(Cast(), NpcChatterKind.Controller, 1, "Sol", towerAccent: "Irish", captainAccent: "British");

        var instruction = NpcChatter.Instruction(NpcChatterKind.Controller, carrier, docked: true, roster: roster);

        Assert.Contains("Tower's voice has an Irish accent and Captain's voice has a British accent.", instruction, StringComparison.Ordinal);

        var lines = NpcChatter.Parse("Tower: Pad four.\nRook [A]: Copy.", NpcChatterKind.Controller, carrier, roster);

        Assert.Equal(VoiceRole.TowerControl, lines[0].Role);
        Assert.Null(lines[0].VoiceId);
        Assert.Equal(roster.Slots[0].VoiceId, lines[1].VoiceId);
    }
}
