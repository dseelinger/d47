using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Catalog;
using D47.Core.Configuration;
using D47.Core.Conversation;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace D47.Core.Tests.Catalog;

public class BuildTalkUsesTheCatalogsHighModelTests
{
    [Fact]
    public void AnthropicUsesItsHighModel()
    {
        Assert.Equal("claude-opus-5-5", HighModels.Resolve(new D47Settings()));
    }

    [Fact]
    public void OpenAiUsesItsHighModel()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiId } };

        Assert.Equal("gpt-6-astra", HighModels.Resolve(settings));
    }

    [Fact]
    public void ACustomEndpointKeepsTheConversationModel()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Endpoint = "http://localhost:1234", Model = "local-model" } };

        Assert.Equal("local-model", HighModels.Resolve(settings));
    }

    [Fact]
    public void ASetRowWinsOverTheCatalog()
    {
        var settings = new D47Settings { Llm = new LlmSettings { HighModel = "claude-sonnet-5-5" } };

        Assert.Equal("claude-sonnet-5-5", HighModels.Resolve(settings));
    }

    [Fact]
    public void ASetRowNamesTheModelOnACustomEndpoint()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Endpoint = "http://localhost:1234", Model = "small", HighModel = "big" } };

        Assert.Equal("big", HighModels.Resolve(settings));
    }

    [Fact]
    public void ChangingProviderOrEndpointClearsTheRow()
    {
        var rows = ConversationRows();
        var start = new D47Settings { Llm = new LlmSettings { HighModel = "big" } };

        foreach (var key in new[] { ConversationCapability.ProviderKey, ConversationCapability.EndpointKey })
        {
            var row = Assert.Single(rows, r => r.Key == key);
            var value = key == ConversationCapability.EndpointKey ? "http://localhost:1234" : LlmProviderCatalog.OpenAiId;

            Assert.Null(row.Binding!.Write!(start, value).Llm.HighModel);
        }
    }

    private static IReadOnlyList<SettingRow> ConversationRows()
    {
        var install = new MemoryInstall();
        var store = new SettingsStore(install.Paths, install.Files, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), install.Files, NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        return ConversationCapability.Create(
            settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            new TurnCancellation(NullLogger<TurnCancellation>.Instance),
            () => { },
            verifyKey: null,
            speechSpend: null,
            endpointModels: null).Settings;
    }
}
