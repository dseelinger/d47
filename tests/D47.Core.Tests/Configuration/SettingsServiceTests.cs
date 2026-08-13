using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Interface;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class SettingsServiceTests
{
    private static readonly string AnthropicKeyRow =
        ConversationCapability.KeyRowFor(LlmProviderCatalog.Find(LlmProviderCatalog.AnthropicId)!);

    [Fact]
    public void AChangeIsPersistedWithoutASaveStep()
    {
        // There is no save button and no dirty state: what is in memory and what is on disk
        // cannot disagree, because a rejected value never reaches either.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(InterfaceCapability.ThemeKey, ThemeCatalog.Guardian, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
        Assert.Equal(ThemeCatalog.Guardian, surface.Settings.Current.Ui.Theme);

        // Read back through a second store over the same folder — a restart, in effect.
        var reloaded = TestSurface.For(install);
        Assert.Equal(ThemeCatalog.Guardian, reloaded.Settings.Current.Ui.Theme);
    }

    [Fact]
    public void SettingAValueTwiceIsNotAWrite()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(InterfaceCapability.ThemeKey, ThemeCatalog.Light, SettingsCaller.Panel);
        var again = surface.Settings.Apply(InterfaceCapability.ThemeKey, ThemeCatalog.Light, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Unchanged, again.Status);
    }

    [Fact]
    public void AChangeIsAnnouncedOnceWithTheKeyThatChanged()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var announced = new List<string>();
        surface.Settings.Changed += change => announced.Add(change.Key);

        surface.Settings.Apply(InterfaceCapability.ThemeKey, ThemeCatalog.Dark, SettingsCaller.Panel);

        Assert.Equal([InterfaceCapability.ThemeKey], announced);
    }

    /// <summary>
    /// Changed means "a change was persisted, go and re-read". Applied means "somebody tried
    /// this row, here is how it went" — including the tries that changed nothing, which is the
    /// difference that makes it worth having a second event rather than a wider first one.
    /// </summary>
    [Fact]
    public void EveryAttemptIsAnnouncedWithItsStatusIncludingTheOnesThatChangedNothing()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var announced = new List<SettingApplied>();
        surface.Settings.Applied += announced.Add;

        surface.Settings.Apply(InterfaceCapability.ThemeKey, ThemeCatalog.Dark, SettingsCaller.Panel);
        surface.Settings.Apply(InterfaceCapability.ThemeKey, ThemeCatalog.Dark, SettingsCaller.Panel);
        surface.Settings.Apply(InterfaceCapability.ThemeKey, "sparkly", SettingsCaller.Panel);

        Assert.Equal(
            [
                new SettingApplied(InterfaceCapability.ThemeKey, SettingApplyStatus.Applied),
                new SettingApplied(InterfaceCapability.ThemeKey, SettingApplyStatus.Unchanged),
                new SettingApplied(InterfaceCapability.ThemeKey, SettingApplyStatus.Rejected),
            ],
            announced);
    }

    /// <summary>
    /// The outcome most worth seeing, and the one Changed can never carry: a valid value that
    /// could not be written. Changed must stay silent, or every subscriber re-reads state after
    /// a save that did not happen.
    /// </summary>
    [Fact]
    public void AFailedSaveIsAnnouncedAsAppliedButNotAsChanged()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var applied = new List<SettingApplied>();
        var changed = new List<string>();
        surface.Settings.Applied += applied.Add;
        surface.Settings.Changed += change => changed.Add(change.Key);

        // A directory sitting where the pending write wants to put a file. The store writes to
        // a ".writing" sibling and moves it into place, so this is the write failing rather
        // than anything about the value.
        Directory.CreateDirectory(install.Paths.SettingsFile + ".writing");

        var result = surface.Settings.Apply(
            InterfaceCapability.ThemeKey, ThemeCatalog.Guardian, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Failed, result.Status);
        Assert.Equal([new SettingApplied(InterfaceCapability.ThemeKey, SettingApplyStatus.Failed)], applied);
        Assert.Empty(changed);
    }

    /// <summary>
    /// Keys are matched case-insensitively, so "Ui.Theme" and "ui.theme" are one row. Both
    /// events announce the row's own spelling, or a subscriber matching against the registry
    /// would miss whichever one it was not expecting.
    /// </summary>
    [Fact]
    public void BothEventsAnnounceTheRowsOwnKeyNotTheCallersSpelling()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var applied = new List<string>();
        var changed = new List<string>();
        surface.Settings.Applied += a => applied.Add(a.Key);
        surface.Settings.Changed += c => changed.Add(c.Key);

        var shouted = InterfaceCapability.ThemeKey.ToUpperInvariant();
        Assert.NotEqual(InterfaceCapability.ThemeKey, shouted);

        var result = surface.Settings.Apply(shouted, ThemeCatalog.Guardian, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
        Assert.Equal([InterfaceCapability.ThemeKey], applied);
        Assert.Equal([InterfaceCapability.ThemeKey], changed);
    }

    [Fact]
    public void AnUnknownKeyIsNamedRatherThanIgnored()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply("ui.chrome", "sparkly", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.UnknownKey, result.Status);
        Assert.Contains("ui.chrome", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SettingsCaller.Panel)]
    [InlineData(SettingsCaller.Hotkey)]
    [InlineData(SettingsCaller.KeywordRouter)]
    public void EveryCallerButTheModelReachesAProtectedRow(SettingsCaller caller)
    {
        // Protected is a property of the caller, not of the modality (architecture.md §7).
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(PrivacyCapability.UpdateCheckKey, "false", caller);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
        Assert.False(surface.Settings.Current.Updates.CheckOnStartup);
    }

    [Fact]
    public void TheModelIsRefusedAProtectedRow()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(PrivacyCapability.UpdateCheckKey, "false", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
        Assert.True(surface.Settings.Current.Updates.CheckOnStartup);
    }

    [Fact]
    public void TheModelIsRefusedASecretEvenWhenTheRowIsNotMarkedProtected()
    {
        // Belt and braces on purpose: a key must be unreachable from the tool surface whether or
        // not whoever declared the row remembered to also mark it protected.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.False(surface.Settings.Find(AnthropicKeyRow)!.Protected);

        var result = surface.Settings.Apply(AnthropicKeyRow, "sk-not-a-real-key", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
        Assert.False(surface.Settings.HasSecret(LlmProviderCatalog.Find(LlmProviderCatalog.AnthropicId)!.KeySecretName));
    }

    [Fact]
    public void ASecretIsWriteOnlyFromEverySurface()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var row = surface.Settings.Find(AnthropicKeyRow)!;

        var stored = surface.Settings.Apply(AnthropicKeyRow, "sk-not-a-real-key", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, stored.Status);
        Assert.True(surface.Settings.HasSecret(row.SecretName));

        // Presence, never the value — including for the panel that just set it.
        Assert.Null(surface.Settings.Read(AnthropicKeyRow));

        var cleared = surface.Settings.Apply(AnthropicKeyRow, null, SettingsCaller.Panel);
        Assert.Equal(SettingApplyStatus.Applied, cleared.Status);
        Assert.False(surface.Settings.HasSecret(row.SecretName));
    }

    [Fact]
    public void ADisclosureRowIsReportedRatherThanSet()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply($"egress.{EgressDisclosure.LanguageModel}", "off", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Refused, result.Status);
    }

    [Fact]
    public void AValueOutsideAClosedChoiceIsRefusedWithTheRealList()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(InterfaceCapability.ThemeKey, "sparkly", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
        Assert.Contains(ThemeCatalog.Guardian, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AToggleAcceptsTheWordsAPersonWouldSayAndStoresOneForm()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal(
            SettingApplyStatus.Applied,
            surface.Settings.Apply(PrivacyCapability.UpdateCheckKey, "OFF", SettingsCaller.Panel).Status);

        Assert.Equal("false", surface.Settings.Read(PrivacyCapability.UpdateCheckKey));

        // Same setting, said differently: not a change, so not a write and not an announcement.
        Assert.Equal(
            SettingApplyStatus.Unchanged,
            surface.Settings.Apply(PrivacyCapability.UpdateCheckKey, "no", SettingsCaller.Panel).Status);
    }

    [Fact]
    public void ClearingARowRestoresTheDefaultTheePlaceholderAdvertised()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.ModelKey, "claude-sonnet-5", SettingsCaller.Panel);
        Assert.Equal("claude-sonnet-5", surface.Settings.Read(ConversationCapability.ModelKey));

        surface.Settings.Apply(ConversationCapability.ModelKey, null, SettingsCaller.Panel);

        Assert.Null(surface.Settings.Read(ConversationCapability.ModelKey));
        Assert.Equal(
            "claude-opus-5",
            surface.Settings.Find(ConversationCapability.ModelKey)!.DefaultDisplayFor(surface.Settings.Current));
    }

    [Fact]
    public void ChangingTheEndpointResetsTheModelListAndTheSelection()
    {
        // A model id belongs to its endpoint's namespace. Carrying one across is a stale
        // selection that fails at the first turn (list.md Phase 4).
        //
        // Driven through the row's own binding rather than through Apply, because no shipped
        // provider currently offers the endpoint row: Anthropic has one address and no reason to
        // accept another, so the row does not apply and Apply would refuse it. The guarantee
        // still has to hold for the OpenAI-shaped providers this row exists for, and a guarantee
        // that stops being exercised the moment its only caller goes away is one that rots
        // quietly until somebody needs it.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.ModelKey, "claude-opus-5", SettingsCaller.Panel);
        Assert.Equal("claude-opus-5", surface.Settings.Current.Llm.Model);

        var endpoint = surface.Settings.Find(ConversationCapability.EndpointKey);
        Assert.NotNull(endpoint);

        var moved = endpoint.Binding!.Write!(surface.Settings.Current, "https://gateway.example/v1");

        Assert.Null(moved.Llm.Model);
        Assert.Empty(surface.Settings.Find(ConversationCapability.ModelKey)!.ChoicesFor(moved));
    }

    /// <summary>
    /// Having an address and being pointable at another are different facts. Offering a
    /// Commander a protected text box to retype "https://api.anthropic.com" into is a setting
    /// that can only be got wrong.
    /// </summary>
    [Fact]
    public void TheEndpointRowDoesNotApplyToAProviderWithNowhereElseToPoint()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(
            ConversationCapability.ProviderKey, LlmProviderCatalog.AnthropicId, SettingsCaller.Panel);

        var endpoint = surface.Settings.Find(ConversationCapability.EndpointKey);

        Assert.NotNull(endpoint);
        Assert.False(endpoint.Applies(surface.Settings.Current));
    }

    [Fact]
    public void AModelNameTheEndpointOnlyKnowsIsStillAccepted()
    {
        // The picker's fail-soft contract: with an empty list you can still type one.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.EndpointKey, "https://gateway.example/v1", SettingsCaller.Panel);

        var result = surface.Settings.Apply(ConversationCapability.ModelKey, "house-model-7", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, result.Status);
        Assert.Equal("house-model-7", surface.Settings.Current.Llm.Model);
    }

    [Fact]
    public void ChangingTheProviderClearsWhatBelongedToTheOldOne()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.EndpointKey, "https://gateway.example/v1", SettingsCaller.Panel);
        surface.Settings.Apply(ConversationCapability.ModelKey, "house-model-7", SettingsCaller.Panel);

        surface.Settings.Apply(ConversationCapability.ProviderKey, LlmProviderCatalog.NoneId, SettingsCaller.Panel);

        Assert.Null(surface.Settings.Current.Llm.Endpoint);
        Assert.Null(surface.Settings.Current.Llm.Model);
    }

    [Fact]
    public void ARowThatDoesNotApplyIsNotWritableEither()
    {
        // Settings adapt to the selected provider rather than showing a hardwired set. A row that
        // is off screen must not be reachable behind the screen's back.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.ProviderKey, LlmProviderCatalog.NoneId, SettingsCaller.Panel);

        Assert.False(surface.Settings.Find(ConversationCapability.EndpointKey)!.Applies(surface.Settings.Current));

        var result = surface.Settings.Apply(
            ConversationCapability.EndpointKey, "https://gateway.example/v1", SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Rejected, result.Status);
    }

    [Fact]
    public void ALogLevelChangeIsLiveAndPersistedFromOnePath()
    {
        // The tool, the panel and the settings file all write the same row, and the verbosity
        // control follows the row. There is no second path that only does half of it.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(DiagnosticsCapability.LevelRowFor("Journal"), "Trace", SettingsCaller.Panel);

        Assert.Equal(LogLevel.Trace, surface.Verbosity.Levels["Journal"]);
        Assert.Equal(LogLevel.Trace, TestSurface.For(install).Verbosity.Levels["Journal"]);
    }

    [Fact]
    public void ARowWithNothingBehindItFailsAtStartupRatherThanOnScreen()
    {
        using var install = new TempInstall();

        var descriptor = new CapabilityDescriptor
        {
            Id = "hollow",
            Group = "Test",
            Name = "Hollow",
            Summary = "A capability with a row that does nothing.",
            Settings =
            [
                new SettingRow
                {
                    Key = "hollow.row",
                    Label = "Hollow",
                    Help = "Nothing is bound behind this.",
                    Kind = SettingKind.Text,
                },
            ],
        };

        var store = new SettingsStore(install.Paths, Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsStore>.Instance);
        var secrets = new SecretStore(
            install.Paths,
            new ReversibleProtector(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SecretStore>.Instance);

        var service = new SettingsService(
            store,
            secrets,
            new D47Settings(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsService>.Instance);

        Assert.Throws<CapabilityRegistrationException>(() =>
            service.Bind(CapabilityRegistry.Build([descriptor])));
    }
}

public class SettingsSurfaceShapeTests
{
    [Fact]
    public void EveryRowIsDocumentedAndReachableFromItsPage()
    {
        // Item 8 of the phase: a setup-guide link per row. A row with no anchor has nowhere to
        // point, so the requirement is structural rather than a habit.
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var missing = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => string.IsNullOrWhiteSpace(row.DocsAnchor))
            .Select(row => row.Key)
            .ToArray();

        Assert.True(missing.Length == 0, $"Settings rows with no documentation anchor: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EverySettingsKeyIsUniqueAcrossCapabilities()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var keys = surface.Settings.Sections.SelectMany(s => s.Rows).Select(r => r.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void EveryModelTheCatalogueOffersHasAPrice()
    {
        // Anything picked from the list keeps the running total honest. A typed model is priced
        // as unknown, which is a different and visible thing.
        var unpriced =
            (from provider in LlmProviderCatalog.All
             from model in provider.Models
             where PriceTable.Default.For(provider.Id, model) is null
             select $"{provider.Id}/{model}").ToArray();

        Assert.True(unpriced.Length == 0, $"Offered models with no price-table entry: {string.Join(", ", unpriced)}");
    }
}

public class EgressDisclosureTests
{
    [Fact]
    public void WithNoProviderNothingIsLeaving()
    {
        var settings = new D47Settings
        {
            Llm = new LlmSettings { Provider = LlmProviderCatalog.NoneId },
            Updates = new UpdateSettings { CheckOnStartup = false },

            // The voice provider counts. Edge Neural is free, not local — every line D47 speaks
            // goes to speech.platform.bing.com to be turned into audio. Until Phase 11 the
            // disclosure had no text-to-speech entry at all, so this assertion used to pass with
            // Edge selected, which meant it was asserting something untrue.
            Speech = new SpeechSettings { Provider = Core.Audio.TtsProviderCatalog.NoneId },
        };

        Assert.All(EgressDisclosure.For(settings, llmKeyPresent: false), entry => Assert.False(entry.Active));
        Assert.Contains("Nothing is leaving", EgressDisclosure.Describe(settings, false), StringComparison.Ordinal);
    }

    [Fact]
    public void AProviderWithNoKeyIsSelectedButInert()
    {
        // Selected is not the same as sending, and the disclosure has to say which one it is.
        var settings = new D47Settings();

        var entry = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, settings, llmKeyPresent: false);

        Assert.False(entry.Active);
        Assert.Contains("no key stored", entry.What, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AConfiguredProviderNamesTheEndpointItSendsTo()
    {
        var settings = new D47Settings { Llm = new LlmSettings { Endpoint = "https://gateway.example/v1" } };

        var entry = EgressDisclosure.Entry(EgressDisclosure.LanguageModel, settings, llmKeyPresent: true);

        Assert.True(entry.Active);
        Assert.Equal("https://gateway.example/v1", entry.Destination);
    }

    [Fact]
    public void TheDisclosureNamesEveryDestinationIncludingTheSilentOnes()
    {
        // A disclosure that lists only what does send invites the question of what else exists.
        var report = EgressDisclosure.Describe(new D47Settings(), llmKeyPresent: true);

        foreach (var id in EgressDisclosure.Ids)
        {
            Assert.Contains(EgressDisclosure.NameOf(id), report, StringComparison.Ordinal);
        }
    }
}
