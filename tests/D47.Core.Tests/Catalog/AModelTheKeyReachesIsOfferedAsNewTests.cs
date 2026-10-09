using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;

using Xunit;

namespace D47.Core.Tests.Catalog;

/// <summary>A model the provider lists for the key and the catalog does not name is offered, labelled new.</summary>
[Trait("Category", "Integration")]
[Collection(nameof(SharedModelCatalogCollection))]
public sealed class AModelTheKeyReachesIsOfferedAsNewTests : IDisposable
{
    private static readonly DateTimeOffset Catalogued = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly TempInstall _install = new();

    public void Dispose()
    {
        ModelCatalogSource.Shared.List(LlmProviderCatalog.AnthropicId, []);
        ModelCatalogSource.Shared.ListSpeech(TtsProviderCatalog.ElevenLabsId, []);
        _install.Dispose();
    }

    private static void ListAnthropic(bool? adaptive = null) =>
        ModelCatalogSource.Shared.List(
            LlmProviderCatalog.AnthropicId,
            [
                new ListedModel("claude-test-9", "Claude Test 9", Catalogued.AddMonths(8)) { AdaptiveThinking = adaptive },
                new ListedModel("claude-sonnet-5", "Claude Sonnet 5", Catalogued),
                new ListedModel("claude-left-out-4", "Claude Left Out 4", Catalogued.AddMonths(-6)),
            ]);

    [Theory]
    [InlineData(ConversationCapability.ModelKey)]
    [InlineData(ConversationCapability.BackgroundModelKey)]
    public void TheModelRowsListTheCatalogThenTheNewModel(string key)
    {
        ListAnthropic();
        var surface = TestSurface.For(_install);
        var row = surface.Settings.Find(key)!;

        Assert.Equal(
            [.. LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId).Models, "claude-test-9"],
            row.ChoicesFor(surface.Settings.Current));
        Assert.Equal("claude-test-9 — new — priced as unknown", row.LabelForChoice("claude-test-9", surface.Settings.Current));
    }

    [Fact]
    public void AModelOlderThanOneTheCatalogNamesIsNotOffered()
    {
        ListAnthropic();

        Assert.Equal(
            ["claude-test-9"],
            LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId).NewModels);
    }

    [Fact]
    public void NoListLeavesTheCatalogAlone()
    {
        var surface = TestSurface.For(_install);

        Assert.Equal(
            LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId).Models,
            surface.Settings.Find(ConversationCapability.ModelKey)!.ChoicesFor(surface.Settings.Current));
        Assert.Equal(
            ElevenLabsModels.All.Select(model => model.Id),
            surface.Settings.Find(SpeechCapability.ElevenLabsModelKey)!.ChoicesFor(surface.Settings.Current));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(null, false)]
    public void ANewModelThinksAsItsListingSays(bool? adaptive, bool legacy)
    {
        ListAnthropic(adaptive);

        var traits = ModelCatalogSource.Shared.TraitsFor(LlmProviderCatalog.AnthropicId, "claude-test-9");

        Assert.Equal(legacy, traits.LegacyThinking);
        Assert.False(traits.ToolSearch);
        Assert.False(traits.OperatorSystemMessages);
    }

    [Fact]
    public void TheCatalogOutranksTheListingForAModelItNames()
    {
        ModelCatalogSource.Shared.List(
            LlmProviderCatalog.AnthropicId,
            [new ListedModel("claude-sonnet-5", null, Catalogued) { AdaptiveThinking = false }]);

        Assert.Equal(
            ModelCatalogSource.Shared.Current.TraitsFor(LlmProviderCatalog.AnthropicId, "claude-sonnet-5"),
            ModelCatalogSource.Shared.TraitsFor(LlmProviderCatalog.AnthropicId, "claude-sonnet-5"));
    }

    [Fact]
    public void TheElevenLabsRowOffersANewModelAndStoresIt()
    {
        ModelCatalogSource.Shared.ListSpeech(
            TtsProviderCatalog.ElevenLabsId,
            [
                new ListedModel(ElevenLabsModels.V4Turbo, "Eleven v4 Turbo"),
                new ListedModel("eleven_multilingual_v2", "Eleven Multilingual v2"),
                new ListedModel("eleven_test", "Eleven Test"),
            ]);

        var surface = TestSurface.For(_install);
        surface.Settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.ElevenLabsId, SettingsCaller.Panel);
        var row = surface.Settings.Find(SpeechCapability.ElevenLabsModelKey)!;

        Assert.Equal(
            [.. ElevenLabsModels.All.Select(model => model.Id), "eleven_test"],
            row.ChoicesFor(surface.Settings.Current));
        Assert.Equal("Eleven Test — new — priced as unknown", row.LabelForChoice("eleven_test", surface.Settings.Current));

        surface.Settings.Apply(SpeechCapability.ElevenLabsModelKey, "eleven_test", SettingsCaller.Panel);

        Assert.Equal("eleven_test", surface.Settings.Current.Speech.ElevenLabsModel);
        Assert.False(ElevenLabsModels.ReadsTags("eleven_test"));
        Assert.False(ElevenLabsModels.ReadsRate("eleven_test"));
        Assert.Equal(0, ElevenLabsModels.GroupsSentencesUpTo("eleven_test"));
    }
}
