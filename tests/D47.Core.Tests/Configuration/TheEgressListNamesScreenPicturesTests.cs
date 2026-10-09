using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The <c>screen</c> disclosure in each of its states.</summary>
public class TheEgressListNamesScreenPicturesTests
{
    private static D47Settings On(string? endpoint = null) =>
        new() { Llm = new LlmSettings { LookAtScreen = true, Endpoint = endpoint } };

    [Fact]
    public void OffNothingIsCaptured()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.Screen, new D47Settings(), llmKeyPresent: true);

        Assert.False(entry.Active);
        Assert.Equal("Screen pictures", entry.Name);
        Assert.Equal("Looking at the screen is off, so nothing is captured.", entry.What);
    }

    [Fact]
    public void OnWithNoUsableModelNoTurnRuns()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.Screen, On(), llmKeyPresent: false);

        Assert.False(entry.Active);
        Assert.Contains("no language model is usable, so no turn runs and nothing is captured", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OnWithAModelThatCannotReadPicturesNothingIsCaptured()
    {
        var entry = EgressDisclosure.Entry(
            EgressDisclosure.Screen, On(), llmKeyPresent: true, imagesAvailable: false);

        Assert.False(entry.Active);
        Assert.Contains("does not read pictures, so nothing is captured", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void OnAndUsableItNamesTheEndpointAndWhatLeaves()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.Screen, On(), llmKeyPresent: true);

        Assert.True(entry.Active);
        Assert.Equal("https://api.anthropic.com", entry.Destination);
        Assert.Contains("one JPEG of Elite's window, or of the headset's left eye", entry.What, StringComparison.Ordinal);
        Assert.Contains("D47 never saves it", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void AtALoopbackEndpointThePictureDoesNotLeaveThisMachine()
    {
        var settings = On("http://localhost:11434/v1");
        settings = settings with { Llm = settings.Llm with { Provider = LlmProviderCatalog.OpenAiCompatibleId } };

        var entry = EgressDisclosure.Entry(EgressDisclosure.Screen, settings, llmKeyPresent: true);

        Assert.False(entry.Active);
        Assert.Contains("The picture does not leave this machine.", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEntryFollowsWebSearchInTheFixedList()
    {
        var ids = EgressDisclosure.Ids.ToList();

        Assert.Equal(ids.IndexOf(EgressDisclosure.WebSearch) + 1, ids.IndexOf(EgressDisclosure.Screen));
    }

    [Fact]
    public async Task TheToolAndThePrivacyRowAnswerFromTheModelInUse()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance);
        secrets.Set(LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId).KeySecretName!, "sk-test");

        var settings = new SettingsService(store, secrets, On(), NullLogger<SettingsService>.Instance);
        var privacy = PrivacyCapability.Create(settings, imagesAvailable: () => false);

        var report = await privacy.Tools.Single().Handler(
            D47.Core.Capabilities.ToolArguments.Empty, TestContext.Current.CancellationToken);
        var row = privacy.Settings.Single(setting => setting.Key == $"egress.{EgressDisclosure.Screen}");

        Assert.Contains("Screen pictures → nothing sent", report.Content, StringComparison.Ordinal);
        Assert.Contains("does not read pictures", row.DetailBinding!(settings.Current) ?? string.Empty, StringComparison.Ordinal);
    }
}
