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

/// <summary>The ElevenLabs model row, on the drawn page.</summary>
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
    /// v3 Conversational is what a Commander who never opens the row is speaking through, so it is what
    /// the row shows before anybody touches it.
    /// </summary>
    [AvaloniaFact]
    public void ItOpensOnTheModelThatSpeaksWhenNobodyHasChosen()
    {
        var host = OnElevenLabs(out var settings);

        Assert.Equal(ElevenLabsModels.V3, ElevenLabsModels.Named(settings.Current.Speech.ElevenLabsModel));
        Assert.Equal(ElevenLabsModels.V3, ElevenLabsModels.Default);
    }

    /// <summary>
    /// The visible consequence. v3 has no speaking rate — it accepts every value and acts on none — so
    /// the row goes rather than sitting there doing nothing.
    /// </summary>
    [AvaloniaFact]
    public void ChoosingV3TakesTheSpeakingRateOffThePageAndFlashBringsItBack()
    {
        var host = OnElevenLabs(out var settings);

        settings.Apply(SpeechCapability.ElevenLabsModelKey, ElevenLabsModels.Flash, SettingsCaller.Panel);
        Assert.True(Drawn(host, RateLabel));

        settings.Apply(SpeechCapability.ElevenLabsModelKey, ElevenLabsModels.V3, SettingsCaller.Panel);
        Assert.False(Drawn(host, RateLabel));

        // Back again, because a row that went and stayed gone would pass the assertion above for the wrong
        // reason.
        settings.Apply(SpeechCapability.ElevenLabsModelKey, ElevenLabsModels.Flash, SettingsCaller.Panel);
        Assert.True(Drawn(host, RateLabel));
    }

    /// <summary>
    /// And the row itself is only there while something speaks through ElevenLabs — a Commander on Edge
    /// alone is not asked to choose between two models they will never hear.
    /// </summary>
    [AvaloniaFact]
    public void ItIsNotOnThePageForACommanderWhoDoesNotUseElevenLabsAtAll()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.EdgeId, SettingsCaller.Panel);

        Assert.False(Drawn(SettingsHost.Open(settings, viewState, paths), ModelLabel));
    }

    /// <summary><c>IsEffectivelyVisible</c>, not merely present.</summary>
    private static bool Drawn(SettingsHost host, string label)
    {
        // Painted, not merely reconciled.
        host.Window.CaptureRenderedFrame();

        return host.View.GetVisualDescendants().OfType<TextBlock>()
            .Any(text => text.Text == label && text.IsEffectivelyVisible);
    }

    private static Grid Row(SettingsHost host, string label) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3 && grid.ColumnDefinitions[1].Width.IsAbsolute)
            .First(grid => grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));
}
