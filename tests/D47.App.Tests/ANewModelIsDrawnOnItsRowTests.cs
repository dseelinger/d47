using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.App.Tests;

/// <summary>Lists models on the shared catalog source, so nothing may run beside it.</summary>
[CollectionDefinition(nameof(SharedModelListingCollection), DisableParallelization = true)]
public class SharedModelListingCollection;

/// <summary>A model the provider listed that the catalog does not name, chosen and drawn on its row.</summary>
[Collection(nameof(SharedModelListingCollection))]
public sealed class ANewModelIsDrawnOnItsRowTests : IDisposable
{
    public void Dispose()
    {
        ModelCatalogSource.Shared.List(LlmProviderCatalog.AnthropicId, []);
        ModelCatalogSource.Shared.ListSpeech(TtsProviderCatalog.ElevenLabsId, []);
    }

    [AvaloniaFact]
    public void TheElevenLabsRowShowsTheNewModelChosen()
    {
        ModelCatalogSource.Shared.ListSpeech(
            TtsProviderCatalog.ElevenLabsId,
            [new ListedModel("eleven_test", "Eleven Test")]);

        var (settings, viewState, paths) = TestSurface.Create();
        settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.ElevenLabsId, SettingsCaller.Panel);
        settings.Apply(SpeechCapability.ElevenLabsModelKey, "eleven_test", SettingsCaller.Panel);

        Assert.Equal("eleven_test", settings.Current.Speech.ElevenLabsModel);

        Capture(settings, viewState, paths, SpeechCapability.ElevenLabsModelKey, "elevenlabs-new-model-row.png", "Eleven Test — new — priced as unknown");
    }

    [AvaloniaFact]
    public void TheModelRowShowsTheNewModelChosen()
    {
        ModelCatalogSource.Shared.List(
            LlmProviderCatalog.AnthropicId,
            [new ListedModel("claude-test-9", "Claude Test 9", DateTimeOffset.MaxValue)]);

        var (settings, viewState, paths) = TestSurface.Create();
        settings.Apply(ConversationCapability.ProviderKey, LlmProviderCatalog.AnthropicId, SettingsCaller.Panel);
        settings.Apply(ConversationCapability.ModelKey, "claude-test-9", SettingsCaller.Panel);

        Assert.Equal("claude-test-9", settings.Current.Llm.Model);

        Capture(settings, viewState, paths, ConversationCapability.ModelKey, "anthropic-new-model-row.png", "claude-test-9 — new — priced as unknown");
    }

    private static void Capture(
        SettingsService settings,
        ViewStateStore viewState,
        AppPaths paths,
        string key,
        string file,
        string shown)
    {
        var host = SettingsHost.Open(settings, viewState, paths, width: 820, height: 1100);
        host.View.ShowPlaceOf(key);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        host.Window.CaptureRenderedFrame()!.SaveCapture(file);

        Assert.Contains(
            host.View.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == shown && text.IsEffectivelyVisible);

        host.Close();
    }
}
