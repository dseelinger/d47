using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The ElevenLabs model row, on the drawn page
/// (<a href="https://github.com/dseelinger/d47/issues/291">#291</a>).
/// <para>
/// <b>Core can prove the row exists and cannot prove it is on the screen.</b> A row whose binding
/// comes from a host delegate nobody passed is absent rather than broken, and absent is the one
/// state a Core test cannot see. So the surface is asked here, through the same view the app
/// builds: the row is drawn, its choices are the two models, and picking v3 takes the speaking
/// rate off the page — which is the one visible consequence a Commander will notice and the one
/// most likely to look like a bug if it is not explained.
/// </para>
/// </summary>
public class TheModelRowIsDrawnAndTakesTheRateWithItTests
{
    private const string ModelLabel = "ElevenLabs model";
    private const string RateLabel = "Speaking rate";

    private static SettingsHost OnElevenLabs(out SettingsService settings)
    {
        var (created, viewState, paths) = TestSurface.Create();
        settings = created;

        settings.Apply(
            SpeechCapability.ProviderKey, TtsProviderCatalog.ElevenLabsId, SettingsCaller.Panel);

        return SettingsHost.Open(settings, viewState, paths);
    }

    [AvaloniaFact]
    public void TheRowIsDrawnWithBothModelsOnIt()
    {
        var host = OnElevenLabs(out _);
        var combo = Row(host, ModelLabel).GetVisualDescendants().OfType<ComboBox>().First();

        Assert.Equal(2, combo.ItemCount);
    }

    /// <summary>
    /// v3 Conversational is what a Commander who never opens the row is speaking through, so it is
    /// what the row shows before anybody touches it.
    /// </summary>
    [AvaloniaFact]
    public void ItOpensOnTheModelThatSpeaksWhenNobodyHasChosen()
    {
        var host = OnElevenLabs(out var settings);

        Assert.Equal(ElevenLabsModels.V3, ElevenLabsModels.Named(settings.Current.Speech.ElevenLabsModel));
        Assert.Equal(ElevenLabsModels.V3, ElevenLabsModels.Default);
    }

    /// <summary>
    /// <b>The visible consequence.</b> v3 has no speaking rate — it accepts every value and acts on
    /// none — so the row goes rather than sitting there doing nothing. Flash brings it back, which
    /// is what proves the rate is following the model rather than having been lost.
    /// </summary>
    [AvaloniaFact]
    public void ChoosingV3TakesTheSpeakingRateOffThePageAndFlashBringsItBack()
    {
        var host = OnElevenLabs(out var settings);

        settings.Apply(SpeechCapability.ElevenLabsModelKey, ElevenLabsModels.Flash, SettingsCaller.Panel);
        Assert.True(Drawn(host, RateLabel));

        settings.Apply(SpeechCapability.ElevenLabsModelKey, ElevenLabsModels.V3, SettingsCaller.Panel);
        Assert.False(Drawn(host, RateLabel));

        // Back again, because a row that went and stayed gone would pass the assertion above for
        // the wrong reason.
        settings.Apply(SpeechCapability.ElevenLabsModelKey, ElevenLabsModels.Flash, SettingsCaller.Panel);
        Assert.True(Drawn(host, RateLabel));
    }

    /// <summary>
    /// And the row itself is only there while something speaks through ElevenLabs — a Commander on
    /// Edge alone is not asked to choose between two models they will never hear.
    /// </summary>
    [AvaloniaFact]
    public void ItIsNotOnThePageForACommanderWhoDoesNotUseElevenLabsAtAll()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.EdgeId, SettingsCaller.Panel);

        Assert.False(Drawn(SettingsHost.Open(settings, viewState, paths), ModelLabel));
    }

    /// <summary>
    /// <b><c>IsEffectivelyVisible</c>, not merely present.</b> A row that does not apply is hidden
    /// rather than removed — its container's <c>IsVisible</c> goes false and the label stays in the
    /// visual tree. A search that only asks whether the TextBlock exists finds every hidden row too,
    /// which is a test that passes whatever the page does.
    /// </summary>
    private static bool Drawn(SettingsHost host, string label)
    {
        // Painted, not merely reconciled. A settings change updates each row's IsVisible, and
        // IsEffectivelyVisible does not follow until the tree has been laid out again — so without
        // this the answer is whatever it was one change ago.
        host.Window.CaptureRenderedFrame();

        return host.View.GetVisualDescendants().OfType<TextBlock>()
            .Any(text => text.Text == label && text.IsEffectivelyVisible);
    }

    private static Grid Row(SettingsHost host, string label) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3 && grid.ColumnDefinitions[1].Width.IsAbsolute)
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
}
