using System.Globalization;

using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// What a model picker says about each id
/// (<a href="https://github.com/dseelinger/d47/issues/152">#152</a>).
/// <para>
/// The rows used to be bare ids — a version ladder and nothing more — while d47's own price
/// table sat one type away, unshown. Reported in front of the OpenAI picker: <em>"All this tells
/// me is 5.4 is older than 5.5 is older than 5.6."</em>
/// </para>
/// <para>
/// Every assertion below is about a <b>derived</b> fact, which is the design rather than an
/// accident of what was easy: nothing here may become a second copy of anybody's marketing,
/// because capability prose ages silently and a price read from the billing table does not.
/// </para>
/// </summary>
public class AModelRowSaysWhatItCostsTests
{
    private static readonly LlmProviderInfo OpenAi = LlmProviderCatalog.Selected(LlmProviderCatalog.OpenAiId);

    private static readonly LlmProviderInfo Anthropic = LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId);

    /// <summary>
    /// The one fact the row exists to carry. Read from <see cref="PriceTable.Default"/>, which is
    /// the table the turn loop bills against — so a Commander who shops on this line and a
    /// Commander who reads the spend dialog are reading one number.
    /// </summary>
    [Fact]
    public void EveryOfferedModelCarriesItsPrice()
    {
        foreach (var provider in new[] { OpenAi, Anthropic })
        {
            var describe = ModelChoice.Describer(provider, endpoint: null, PriceTable.Default);

            foreach (var model in provider.Models)
            {
                var price = PriceTable.Default.For(provider.Id, model)!;
                var row = describe(model);

                Assert.Contains("per million", row, StringComparison.Ordinal);
                Assert.DoesNotContain("priced as unknown", row, StringComparison.Ordinal);

                // Both halves, because a row quoting input alone would make gpt-5.6-luna and
                // gpt-5.4-nano — $0.20 in apiece — indistinguishable, which is the exact
                // confusion this is here to end.
                Assert.Contains(Money(price.InputPerMillion), row, StringComparison.Ordinal);
                Assert.Contains(Money(price.OutputPerMillion), row, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// The id is the value and stays the first thing on the line: it is what the settings file
    /// holds, what a support answer names, and what the picker's filter box is matched against.
    /// </summary>
    [Fact]
    public void TheIdIsStillTheFirstThingTheRowSays()
    {
        var describe = ModelChoice.Describer(OpenAi, endpoint: null, PriceTable.Default);

        foreach (var model in OpenAi.Models)
        {
            Assert.StartsWith(model, describe(model), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <em>The provider's default</em> is <see cref="LlmProviderInfo.DefaultModel"/> rather than a
    /// word written beside the list, so it cannot go stale the way prose would — and exactly one
    /// row wears it.
    /// </summary>
    [Fact]
    public void TheProvidersDefaultIsMarkedAndOnlyIt()
    {
        foreach (var provider in new[] { OpenAi, Anthropic })
        {
            var describe = ModelChoice.Describer(provider, endpoint: null, PriceTable.Default);

            var marked = provider.Models
                .Where(model => describe(model).Contains("the provider's default", StringComparison.Ordinal))
                .ToList();

            Assert.Equal(provider.DefaultModel, Assert.Single(marked));
        }
    }

    /// <summary>
    /// <em>Cheapest here</em> is a minimum over the ids actually offered, and the tie-break is
    /// the reason it is stated as a rule: <c>gpt-5.6-luna</c> and <c>gpt-5.4-nano</c> both cost
    /// $0.20 in, so a comparison that stopped at the input rate would mark both or neither.
    /// </summary>
    [Fact]
    public void TheCheapestListedModelIsMarked()
    {
        Assert.Equal(["gpt-5.6-luna"], Cheapest(OpenAi));
        Assert.Equal(["claude-haiku-4-5"], Cheapest(Anthropic));

        static IReadOnlyList<string> Cheapest(LlmProviderInfo provider)
        {
            var describe = ModelChoice.Describer(provider, endpoint: null, PriceTable.Default);

            return
            [
                .. provider.Models.Where(model =>
                    describe(model).Contains("cheapest here", StringComparison.Ordinal)),
            ];
        }
    }

    /// <summary>
    /// An id the Commander typed, or one a custom endpoint reported, has no row in the table —
    /// and says so. Silence would read as free, which is the one wrong answer worth ruling out:
    /// the running total already refuses to make that claim.
    /// </summary>
    [Fact]
    public void AnIdOffTheListSaysItIsPricedAsUnknown()
    {
        var describe = ModelChoice.Describer(OpenAi, endpoint: null, PriceTable.Default);

        Assert.Equal("grok-4 — priced as unknown", describe("grok-4"));
    }

    /// <summary>
    /// A model served from this machine is free, and that is a fact about the <em>address</em>
    /// rather than about the id — no table row could hold it, because the id is whatever the
    /// local server calls the weights it loaded. Reported as free rather than as unknown, for the
    /// reason <see cref="PriceTable.Free"/> exists: "unknown" about a model that costs nothing is
    /// noise pretending to be rigour.
    /// </summary>
    [Fact]
    public void AModelOnThisMachineIsFreeRatherThanUnpriced()
    {
        var local = LlmProviderCatalog.Selected(LlmProviderCatalog.OpenAiCompatibleId);
        var describe = ModelChoice.Describer(local, endpoint: null, PriceTable.Default);

        // Null endpoint means the provider's own, and this provider's own is Ollama on loopback.
        Assert.Equal("qwen3:30b — free on this machine", describe("qwen3:30b"));

        // Pointed at somebody else's gateway, the same id is a stranger's model at a stranger's
        // prices, and d47 holds none of them.
        var remote = ModelChoice.Describer(local, "https://openrouter.ai/api/v1", PriceTable.Default);

        Assert.Equal("qwen3:30b — priced as unknown", remote("qwen3:30b"));
    }

    /// <summary>
    /// A discovered list gets no <em>cheapest here</em>. <see cref="LlmProviderInfo.ModelsFor"/>
    /// answers nothing for an address d47 does not recognise, so there is no "here" to be
    /// cheapest of — and a marker computed over a list nobody is choosing from would be a claim
    /// about models that are not on screen.
    /// </summary>
    [Fact]
    public void ADiscoveredListIsNotRankedAgainstTheCuratedOne()
    {
        var describe = ModelChoice.Describer(OpenAi, "https://api.x.ai/v1", PriceTable.Default);

        Assert.DoesNotContain("cheapest here", describe("gpt-5.6-luna"), StringComparison.Ordinal);

        // Still priced, because the turn itself would be billed against the same
        // (provider, model) key — the picker and the invoice read one table.
        Assert.Contains("per million", describe("gpt-5.6-luna"), StringComparison.Ordinal);

        // And an id only that endpoint has ever heard of is unpriced, as it is anywhere else.
        Assert.Equal("grok-4 — priced as unknown", describe("grok-4"));
    }

    /// <summary>
    /// The whole line, in the wording the Commander reads. Pinned to one culture because the rate
    /// is written as money and the symbol is the machine's, not the test's.
    /// </summary>
    [Fact]
    public void TheWholeLineReadsAsASentence()
    {
        WithCulture("en-US", () =>
        {
            var describe = ModelChoice.Describer(OpenAi, endpoint: null, PriceTable.Default);

            Assert.Equal(
                "gpt-5.6-terra — the provider's default — $2 in / $12 out per million",
                describe("gpt-5.6-terra"));

            // The cents only where there are cents: "$2.00" beside "$0.20" reads as a table of
            // figures, and this is a sentence.
            Assert.Equal(
                "gpt-5.6-luna — cheapest here — $0.20 in / $1.20 out per million",
                describe("gpt-5.6-luna"));

            Assert.Equal("gpt-5.4-nano — $0.20 in / $1.25 out per million", describe("gpt-5.4-nano"));
        });
    }

    /// <summary>
    /// Both LLM model rows, from one source. The conversation model and the quiet one are chosen
    /// from the same list against the same rates, and two labels that agree on the day they are
    /// written are not the same thing as one that cannot disagree.
    /// </summary>
    [Fact]
    public void BothModelRowsSayTheSameWords()
    {
        var rows = SettingsCapabilityRows();
        var settings = new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiId } };

        var model = Assert.Single(rows, row => row.Key == ConversationCapability.ModelKey);
        var background = Assert.Single(rows, row => row.Key == ConversationCapability.BackgroundModelKey);

        foreach (var id in OpenAi.Models)
        {
            Assert.Equal(
                ModelChoice.Describer(OpenAi, endpoint: null, PriceTable.Default)(id),
                model.LabelForChoice(id, settings));

            Assert.Equal(model.LabelForChoice(id, settings), background.LabelForChoice(id, settings));
        }
    }

    /// <summary>
    /// And the label follows the provider selected right now, which is why it is a function of
    /// settings rather than a string captured when the row was registered.
    /// </summary>
    [Fact]
    public void TheLabelFollowsTheSelectedProvider()
    {
        var model = Assert.Single(SettingsCapabilityRows(), row => row.Key == ConversationCapability.ModelKey);

        var onAnthropic = model.LabelForChoice(
            "claude-haiku-4-5",
            new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.AnthropicId } });

        Assert.Contains("cheapest here", onAnthropic, StringComparison.Ordinal);

        // The same id means nothing to OpenAI, so it is priced as unknown rather than quoted at
        // Anthropic's rate.
        var onOpenAi = model.LabelForChoice(
            "claude-haiku-4-5",
            new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiId } });

        Assert.Equal("claude-haiku-4-5 — priced as unknown", onOpenAi);
    }

    /// <summary>
    /// A row that has nothing settings-dependent to say still reads as it always did, so widening
    /// the hook cannot have quietly changed every other picker in d47.
    /// </summary>
    [Fact]
    public void ARowWithoutASettingsAwareLabelIsUnchanged()
    {
        var provider = Assert.Single(SettingsCapabilityRows(), row => row.Key == ConversationCapability.ProviderKey);

        Assert.Equal(
            "OpenAI",
            provider.LabelForChoice(LlmProviderCatalog.OpenAiId, D47Settings.Defaults));
    }

    /// <summary>
    /// The nano tier is offered (<a href="https://github.com/dseelinger/d47/issues/151">#151</a>).
    /// <para>
    /// The price table quoted <c>gpt-5.4-nano</c> at $0.20/$1.25 while the picker offered mini and
    /// stopped, so the cheapest OpenAI id d47 could price was not one it offered — which reads as
    /// an oversight rather than as curation. <c>EveryOfferedModelHasAPrice</c> guards the contract
    /// in the other direction and always did; nothing guarded this one.
    /// </para>
    /// </summary>
    [Fact]
    public void TheNanoTierIsOnTheOpenAiList()
    {
        Assert.Contains("gpt-5.4-nano", OpenAi.Models);
        Assert.NotNull(PriceTable.Default.For(LlmProviderCatalog.OpenAiId, "gpt-5.4-nano"));
    }

    private static string Money(decimal dollars) =>
        dollars == decimal.Truncate(dollars)
            ? dollars.ToString("C0", CultureInfo.CurrentCulture)
            : dollars.ToString("C2", CultureInfo.CurrentCulture);

    private static void WithCulture(string name, Action body)
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);

        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>
    /// The rows this capability declares, built on a throwaway install. Rows are declared rather
    /// than computed from live state, so the store behind them only has to exist.
    /// </summary>
    private static IReadOnlyList<SettingRow> SettingsCapabilityRows()
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
            endpointModels: null).Settings;
    }
}
