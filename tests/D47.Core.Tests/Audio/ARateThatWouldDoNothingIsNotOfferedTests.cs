using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// A provider that cannot be told a speaking rate is never offered one (Phase 60).
/// <para>
/// <b>This is Phase 58's rule arriving at a different provider, and the failure it prevents is
/// stranger.</b> OpenAI has no language field at all. Cartesia <em>has</em> a speed control, in
/// <c>voice.__experimental_controls</c>, and validates it precisely — <c>2.0</c> is a <c>400</c>
/// naming the field and the range — and then does not act on it. Three runs per setting put the
/// largest difference between settings (1.19s) below the largest spread within one setting
/// (2.14s), and <c>slowest</c> came out shorter than <c>normal</c>
/// (docs/spikes/cartesia-voices-and-speed.md §3).
/// </para>
/// <para>
/// So a rate row here would be a control that appears to work and does nothing, which is the
/// exact failure <c>docs/capabilities/listening.md</c> names. It is refused in two places, because
/// a rule living only in a dropdown is one a text editor walks straight past and
/// <c>settings.json</c> is a file a Commander reads.
/// </para>
/// </summary>
public class ARateThatWouldDoNothingIsNotOfferedTests
{
    private static D47Settings On(string provider, double? rate = null, string? model = null) => new()
    {
        Speech = new SpeechSettings
        {
            Provider = provider,

            // Flash unless a test says otherwise: from #291 ElevenLabs answers this question per
            // model, and Flash is the one that still honours a rate. The v3 default's absence of
            // one is asserted below rather than assumed by every case here.
            ElevenLabsModel = model ?? ElevenLabsModels.Flash,
            ProviderRates = rate is { } chosen
                ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { [provider] = chosen }
                : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
        },
    };

    private static SettingRow RateRow() =>
        SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Beds = () => [],
        }).Settings.First(row => row.Key == SpeechCapability.RateKey);

    [Fact]
    public void TheThreeThatWereHereFirstAllHonourARate()
    {
        // Measured, all three: Edge takes a percentage offset, ElevenLabs a multiplier it rejects
        // outside 0.7–1.2, and OpenAI one honoured monotonically across 0.25–4.0. False is the
        // exception rather than the default, which is why the property defaults to true.
        Assert.True(TtsProviderCatalog.Edge.RateCanBeSet);
        Assert.True(TtsProviderCatalog.ElevenLabs.RateCanBeSet);
        Assert.True(TtsProviderCatalog.OpenAi.RateCanBeSet);

        Assert.False(TtsProviderCatalog.Cartesia.RateCanBeSet);
    }

    [Fact]
    public void TheRowIsOnScreenForAProviderThatWouldObeyIt()
    {
        var row = RateRow();

        Assert.True(row.AppliesWhen!(On(TtsProviderCatalog.EdgeId)));
        Assert.True(row.AppliesWhen!(On(TtsProviderCatalog.ElevenLabsId)));
        Assert.True(row.AppliesWhen!(On(TtsProviderCatalog.OpenAiId)));
    }

    /// <summary>
    /// <b>The same fault as Cartesia's, one level down: per model rather than per provider</b>
    /// (#291). ElevenLabs Flash 2.5 honours 0.7 to 1.2 and refuses anything outside with a message
    /// naming the range. v3 Conversational accepts <c>0.5</c> through <c>2.0</c> — a four-fold
    /// span — and returns the same eight and a half seconds of audio throughout, with the spread
    /// within one setting wider than the spread across all eleven
    /// (docs/spikes/elevenlabs-v3-conversational.md §3).
    /// <para>
    /// So the descriptor cannot answer alone any more, and the row is hidden for the model that
    /// would ignore it rather than narrowed to a range it does not have.
    /// </para>
    /// </summary>
    [Fact]
    public void ButNotForTheElevenLabsModelThatIgnoresIt()
    {
        var row = RateRow();

        Assert.True(row.AppliesWhen!(On(TtsProviderCatalog.ElevenLabsId, model: ElevenLabsModels.Flash)));
        Assert.False(row.AppliesWhen!(On(TtsProviderCatalog.ElevenLabsId, model: ElevenLabsModels.V3)));

        // And that is the default, so a Commander who has never opened the row sees no rate.
        Assert.False(row.AppliesWhen!(On(TtsProviderCatalog.ElevenLabsId, model: null!) with
        {
            Speech = new SpeechSettings { Provider = TtsProviderCatalog.ElevenLabsId },
        }));
    }

    /// <summary>
    /// The half a dropdown cannot enforce, for the model as well as for the provider. A rate
    /// written against v3 in a hand-edited file is not a faster voice; it is a number the row
    /// would otherwise read back as though it meant something.
    /// </summary>
    [Fact]
    public void AndAModelThatIgnoresItSpeaksAtItsOwnPaceWhateverTheFileSays()
    {
        var settings = On(TtsProviderCatalog.ElevenLabsId, rate: 0.8, model: ElevenLabsModels.V3);

        Assert.Equal(1.0, SpeechCapability.RateFor(settings));

        // The same file with Flash selected honours it, so this is the model being read and not
        // the rate being lost.
        Assert.Equal(
            0.8,
            SpeechCapability.RateFor(On(TtsProviderCatalog.ElevenLabsId, rate: 0.8, model: ElevenLabsModels.Flash)));
    }

    [Fact]
    public void AndOffItForOneThatWouldNot()
    {
        Assert.False(RateRow().AppliesWhen!(On(TtsProviderCatalog.CartesiaId)));
    }

    /// <summary>
    /// Unchanged by this phase, and worth keeping asserted: silence has no speaking rate either,
    /// and that row has been hidden for "none" since it was written.
    /// </summary>
    [Fact]
    public void SilenceStillHasNoRateEither()
    {
        Assert.False(RateRow().AppliesWhen!(On(TtsProviderCatalog.NoneId)));
    }

    /// <summary>
    /// The half the picker cannot enforce. A number typed into <c>settings.json</c> against a
    /// provider that ignores it is not a fast voice — it is a value the row would then read back
    /// as though it meant something.
    /// </summary>
    [Fact]
    public void AHandEditedFileNamingARateForItIsNotObeyed()
    {
        Assert.Equal(1.0, SpeechCapability.RateFor(
            On(TtsProviderCatalog.CartesiaId, rate: 1.9), TtsProviderCatalog.CartesiaId));

        // Nor through the general rate, which is the other road to the same value.
        var general = new D47Settings { Speech = new SpeechSettings { Rate = 0.6 } };

        Assert.Equal(1.0, SpeechCapability.RateFor(general, TtsProviderCatalog.CartesiaId));
    }

    /// <summary>
    /// And a slot on it speaks at its own pace while the rest of the cast keeps the Commander's
    /// rate — which is the whole of what six slots made possible, read through the provider that
    /// cannot take part in it.
    /// </summary>
    [Fact]
    public void ASlotOnItIsUnaffectedByTheRateEveryOtherSlotKeeps()
    {
        var settings = new D47Settings
        {
            Speech = new SpeechSettings
            {
                Provider = TtsProviderCatalog.EdgeId,
                Rate = 1.3,
                GroupProviders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["npcs"] = TtsProviderCatalog.CartesiaId,
                },
            },
        };

        Assert.Equal(1.3, SpeechCapability.RateFor(settings, TtsProviderCatalog.EdgeId));
        Assert.Equal(
            1.0,
            SpeechCapability.RateFor(
                settings, VoiceGroups.ProviderFor(settings.Speech, VoiceGroup.Npcs)));
    }
}
