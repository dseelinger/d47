using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AVoiceRowNamesTheVoiceThatStandsInTests
{
    private static ChatterboxVoice Voice(string id, string name, string gender, string pitch, string pace, VoiceRole? role = null, bool shipped = true, long bytes = 0) =>
        new(new VoiceInfo(id, name, "en", gender), role, "a test clip", id + ".wav") { Shipped = shipped, Pitch = pitch, Pace = pace, Bytes = bytes };

    private static readonly ChatterboxVoice[] Catalogue =
    [
        Voice("marlow", "Marlow", "female", "high", "slow", VoiceRole.ShipAi),
        Voice("verity", "Verity", "female", "high", "brisk", VoiceRole.Narrator),
        Voice("tobias", "Tobias", "male", "low", "even", VoiceRole.Crew),
        Voice("lucie", "Lucie", "female", "mid", "even", shipped: false, bytes: 247 * 1024),
        Voice("flora", "Flora", "female", "low", "even", shipped: false, bytes: 313 * 1024),
        Voice("lue", "Lue", "female", "mid", "slow", shipped: false, bytes: 298 * 1024),
    ];

    private static readonly D47Settings Chatterbox = D47Settings.Defaults with
    {
        Speech = D47Settings.Defaults.Speech with { Provider = TtsProviderCatalog.ChatterboxId },
    };

    private static SettingChoiceClips Clips(string key, IReadOnlyList<ChatterboxVoice>? catalogue = null, bool installed = true)
    {
        var voices = catalogue ?? Catalogue;

        return SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Voices = _ => [.. voices.Select(voice => voice.Voice.Id)],
            VoiceName = (_, id) => voices.FirstOrDefault(voice => voice.Voice.Id == id)?.Voice.Name,
            VoiceClip = (_, id) => id switch
            {
                "flora" => new ChatterboxClipState(ChatterboxClip.Fetching, 313 * 1024),
                "lue" => new ChatterboxClipState(ChatterboxClip.Failed, 298 * 1024) { Reason = "The release answered 503 Service Unavailable" },
                "lucie" => new ChatterboxClipState(ChatterboxClip.NotFetched, 247 * 1024),
                _ => new ChatterboxClipState(ChatterboxClip.Shipped, 0),
            },
            ChatterboxCatalogue = _ => voices,
            ChatterboxInstalled = () => installed,
        }).Settings.Single(row => row.Key == key).ChoiceClips!.Invoke(Chatterbox)!;
    }

    private static string StandInFor(string id, VoiceRole role) =>
        ChatterboxCatalog.StandIn(Catalogue.Single(voice => voice.Voice.Id == id), role, Catalogue)!.Voice.Name;

    [Theory]
    [InlineData(SpeechCapability.VoiceKey, VoiceRole.ShipAi)]
    [InlineData(SpeechCapability.NarratorVoiceKey, VoiceRole.Narrator)]
    public void TheStandInNamedIsTheOneStandInReturnsForTheRowsRole(string key, VoiceRole role)
    {
        var standIn = StandInFor("lucie", role);

        Assert.Equal(
            $"D47 fetches Lucie before its next line, and speaks in {standIn} if that takes more than 3 seconds.",
            Clips(key).Away("lucie")!.Text);
    }

    [Fact]
    public void TheTwoRowsNameDifferentStandIns() =>
        Assert.NotEqual(StandInFor("lucie", VoiceRole.ShipAi), StandInFor("lucie", VoiceRole.Narrator));

    [Fact]
    public void ANotFetchedVoiceSaysItIsNotOnThisPcWithItsSize()
    {
        var away = Clips(SpeechCapability.VoiceKey).Away("lucie")!;

        Assert.Equal(ChatterboxClip.NotFetched, away.Clip);
        Assert.Equal("NOT ON THIS PC", away.Head);
        Assert.Equal("247 KB", away.Mono);
    }

    [Fact]
    public void AFetchingVoiceNamesItsSizeTheHostAndTheStandIn()
    {
        var away = Clips(SpeechCapability.VoiceKey).Away("flora")!;

        Assert.Equal("FETCHING", away.Head);
        Assert.Equal("313 KB · GITHUB.COM", away.Mono);
        Assert.Equal("Until Flora arrives, D47 speaks in Marlow.", away.Text);
    }

    [Fact]
    public void AFailedFetchIsANoticeWithTheReasonKept()
    {
        var away = Clips(SpeechCapability.VoiceKey).Away("lue")!;

        Assert.Equal(ChatterboxClip.Failed, away.Clip);
        Assert.Equal("Lue could not be fetched", away.Label);
        Assert.Equal("D47 speaks in Marlow until it arrives. Retry asks github.com again.", away.Text);
        Assert.Equal("The release answered 503 Service Unavailable", away.Reason);
    }

    [Fact]
    public void AVoiceOnThisPcHasNoLine() =>
        Assert.Null(Clips(SpeechCapability.VoiceKey).Away("marlow"));

    [Fact]
    public void WhenNothingCanStandInTheLineSaysSo()
    {
        var clips = Clips(SpeechCapability.VoiceKey, [.. Catalogue.Where(voice => !voice.Shipped)]);

        Assert.Equal(ChatterboxCatalog.NoStandIn("Lucie"), clips.Away("lucie")!.Text);
        Assert.Equal(
            "Choosing Lucie fetches 247 KB from github.com. The Chatterbox voice Lucie is not on this PC yet, and no shipped voice can stand in.",
            clips.Choosing("lucie"));
    }

    [Fact]
    public void BeforeTheModelIsDownloadedNoVoiceIsNamedAsSpeaking()
    {
        var clips = Clips(SpeechCapability.VoiceKey, installed: false);

        Assert.Null(clips.Away("lucie")!.Text);
        Assert.Null(clips.Away("flora")!.Text);
        Assert.Equal("Retry asks github.com again.", clips.Away("lue")!.Text);
        Assert.Equal("Choosing Lucie fetches 247 KB from github.com.", clips.Choosing("lucie"));
    }

    [Fact]
    public void ThePickerSaysWhatChoosingAVoiceFetchesAndWhoSpeaksMeanwhile()
    {
        var clips = Clips(SpeechCapability.VoiceKey);

        Assert.Equal("Choosing Lucie fetches 247 KB from github.com. Until it arrives, D47 speaks in Marlow.", clips.Choosing("lucie"));
        Assert.Equal("Marlow is on this PC.", clips.Choosing("marlow"));
    }
}
