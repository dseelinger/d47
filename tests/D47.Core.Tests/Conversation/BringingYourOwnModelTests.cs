using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// What changed in Core so a model on the Commander's own machine could be reached at all
/// (list.md Phase 29).
/// <para>
/// Two of these are about a property that silently changed meaning rather than about new code —
/// <see cref="LlmProviderInfo.NeedsKey"/> stopped being "there is a key row" — and they are here
/// because a derived property becoming right by itself is exactly the sort of correctness nothing
/// would report if it were wrong.
/// </para>
/// </summary>
public class BringingYourOwnModelTests
{
    private static LlmProviderInfo Local => LlmProviderCatalog.Selected(LlmProviderCatalog.OpenAiCompatibleId);

    private static LlmProviderInfo OpenAi => LlmProviderCatalog.Selected(LlmProviderCatalog.OpenAiId);

    /// <summary>
    /// The change the phase exists for. Before it, the most private configuration d47 can offer
    /// was unreachable by construction rather than by anybody's decision.
    /// </summary>
    [Fact]
    public void AKeyCanBeAcceptedWithoutBeingRequired()
    {
        Assert.True(Local.AcceptsKey);
        Assert.False(Local.NeedsKey);

        // The hosted providers are unchanged, which is the other half of the assertion.
        Assert.True(OpenAi.NeedsKey);
        Assert.True(LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId).NeedsKey);
    }

    /// <summary>
    /// A provider that needs no key is a complete configuration on its own. Guiding somebody to
    /// paste a key they do not need is how a first run teaches that d47 asks for things it does
    /// not use.
    /// </summary>
    [Fact]
    public void TheFirstRunHasNothingToAskForWhenTheKeyIsOptional()
    {
        Assert.False(FirstRun.IsNeeded(Local, _ => false));
        Assert.True(FirstRun.IsNeeded(OpenAi, _ => false));
    }

    /// <summary>
    /// The key row still exists — a gateway speaking the same protocol may want one — and it says
    /// which of the two states it is in rather than repeating the required row's wording.
    /// </summary>
    [Fact]
    public void TheKeyRowIsStillDrawnAndSaysItIsOptional()
    {
        var rows = SettingsCapabilityRows();

        var optional = Assert.Single(rows, row => row.Key == ConversationCapability.KeyRowFor(Local));
        var required = Assert.Single(rows, row => row.Key == ConversationCapability.KeyRowFor(OpenAi));

        Assert.Contains("Optional", optional.Help, StringComparison.Ordinal);
        Assert.DoesNotContain("Optional", required.Help, StringComparison.Ordinal);

        // The settings key is permanent the moment it is written: the settings file is
        // append-only and a property is never renamed or removed.
        Assert.Equal("llm.openaiCompatible.apiKey", optional.Key);
        Assert.Equal("llm.openai.apiKey", required.Key);
    }

    /// <summary>
    /// Every id a catalog offers is one the price table can quote — that is the field's contract,
    /// and it is what keeps a running total honest for anything picked from the picker.
    /// </summary>
    [Fact]
    public void EveryOfferedModelHasAPrice()
    {
        foreach (var provider in LlmProviderCatalog.All)
        {
            foreach (var model in provider.Models)
            {
                Assert.NotNull(PriceTable.Default.For(provider.Id, model));
            }
        }
    }

    /// <summary>
    /// The endpoint's own list fills the picker only where the provider has none of its own. The
    /// order is not a preference: a curated id is priced and a discovered one is not, so a
    /// discovered list must never displace one.
    /// </summary>
    [Fact]
    public void TheEndpointsOwnModelsFillThePickerOnlyWhereThereWasNothing()
    {
        var rows = SettingsCapabilityRows(endpointModels: () => ["qwen3:30b", "llama3.3:70b"]);
        var model = Assert.Single(rows, row => row.Key == ConversationCapability.ModelKey);

        var localSettings = new D47Settings
        {
            Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiCompatibleId },
        };

        Assert.Equal(["qwen3:30b", "llama3.3:70b"], model.ChoiceSource!(localSettings));

        // OpenAI's own address has a curated list, so the discovered one does not displace it.
        var hostedSettings = new D47Settings
        {
            Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiId },
        };

        Assert.Equal(OpenAi.Models, model.ChoiceSource!(hostedSettings));
    }

    /// <summary>
    /// The first time in d47's life that the honest answer to <em>what is leaving</em> is
    /// <em>nothing</em>. Silent rather than active, because saying what leaves is the row's whole
    /// job — but the address is still named, so a Commander can check it themselves.
    /// </summary>
    [Fact]
    public void ALoopbackEndpointReadsAsNothingLeavingThisMachine()
    {
        var settings = new D47Settings
        {
            Llm = new LlmSettings
            {
                Provider = LlmProviderCatalog.OpenAiCompatibleId,
                Endpoint = "http://127.0.0.1:11434/v1",
            },
        };

        var entry = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, settings, llmKeyPresent: false);

        Assert.False(entry.Active);
        Assert.Contains("nothing leaves this machine", entry.What, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1", entry.What, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same provider pointed at a gateway is a different disclosure entirely, and this is the
    /// assertion that stops the sentence above being written once and believed forever.
    /// </summary>
    [Fact]
    public void ARemoteEndpointIsDisclosedInFullWithItsAddress()
    {
        var settings = new D47Settings
        {
            Llm = new LlmSettings
            {
                Provider = LlmProviderCatalog.OpenAiCompatibleId,
                Endpoint = "https://gateway.example.com/v1",
            },
        };

        var entry = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, settings, llmKeyPresent: false);

        Assert.True(entry.Active);
        Assert.Equal("https://gateway.example.com/v1", entry.Destination);
        Assert.DoesNotContain("nothing leaves this machine", entry.What, StringComparison.Ordinal);
    }

    /// <summary>
    /// A provider selected with no key stored used to be inert and disclosed as silent. For one
    /// whose key is optional it is neither, and the disclosure has to follow — it was the reading
    /// of <see cref="LlmProviderInfo.NeedsKey"/> that made it so.
    /// </summary>
    [Fact]
    public void AProviderWithNoKeyIsStillActiveWhenItNeedsNone()
    {
        var settings = new D47Settings
        {
            Llm = new LlmSettings
            {
                Provider = LlmProviderCatalog.OpenAiCompatibleId,
                Endpoint = "https://gateway.example.com/v1",
            },
        };

        Assert.True(EgressDisclosure.Entry(EgressDisclosure.LanguageModel, settings, llmKeyPresent: false).Active);

        var hosted = settings with
        {
            Llm = settings.Llm with { Provider = LlmProviderCatalog.OpenAiId, Endpoint = null },
        };

        Assert.False(EgressDisclosure.Entry(EgressDisclosure.LanguageModel, hosted, llmKeyPresent: false).Active);
    }

    /// <summary>
    /// The rows this capability declares, built on a throwaway install. Rows are declared rather
    /// than computed from live state, so the store behind them only has to exist.
    /// </summary>
    private static IReadOnlyList<SettingRow> SettingsCapabilityRows(
        Func<IReadOnlyList<string>>? endpointModels = null)
    {
        using var install = new TempInstall();

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
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
            endpointModels).Settings;
    }
}
