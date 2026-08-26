using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The three settings a floor and a ceiling arrive as (list.md Phase 54), at the store seam.
/// <para>
/// The behaviour half — what the clamp does with them and which callers take the background
/// model — is asserted where it happens: <c>EffortRangeTests</c> and <c>TurnLoopTests</c>.
/// </para>
/// </summary>
public class AFloorAndACeilingTests
{
    private static SettingsStore StoreFor(TempInstall install) =>
        new(install.Paths, NullLogger<SettingsStore>.Instance);

    /// <summary>
    /// Decision 3 of the phase: all three null means behaviour identical to today. A file
    /// written by any earlier build has none of these keys, and this is what it loads as.
    /// </summary>
    [Fact]
    public void AFileWrittenBeforeTheBoundsExistedLoadsWithNone()
    {
        using var install = new TempInstall();
        File.WriteAllText(
            install.Paths.SettingsFile,
            """{"schemaVersion":1,"llm":{"provider":"anthropic","model":"claude-opus-5"}}""");

        var settings = StoreFor(install).Load();

        Assert.Equal("claude-opus-5", settings.Llm.Model);
        Assert.Null(settings.Llm.BackgroundModel);
        Assert.Null(settings.Llm.EffortFloor);
        Assert.Null(settings.Llm.EffortCeiling);
    }

    [Fact]
    public void TheThreeSurviveARoundTrip()
    {
        using var install = new TempInstall();
        var store = StoreFor(install);

        store.Save(new D47Settings
        {
            Llm = new LlmSettings
            {
                Model = "claude-opus-5",
                BackgroundModel = "claude-haiku-4-5",
                EffortFloor = ThinkingEffort.Medium,
                EffortCeiling = ThinkingEffort.Xhigh,
            },
        });

        var reloaded = store.Load();

        Assert.Equal("claude-haiku-4-5", reloaded.Llm.BackgroundModel);
        Assert.Equal(ThinkingEffort.Medium, reloaded.Llm.EffortFloor);
        Assert.Equal(ThinkingEffort.Xhigh, reloaded.Llm.EffortCeiling);
    }

    /// <summary>
    /// The rung the phase added, through the seam that writes it down. <c>Xhigh</c> camel-cases
    /// to a single word, so a file that spelled it any other way would not load.
    /// </summary>
    [Fact]
    public void TheFifthRungIsWrittenAsOneWord()
    {
        using var install = new TempInstall();
        var store = StoreFor(install);

        store.Save(new D47Settings { Llm = new LlmSettings { EffortCeiling = ThinkingEffort.Xhigh } });

        Assert.Contains("\"xhigh\"", File.ReadAllText(install.Paths.SettingsFile), StringComparison.Ordinal);
        Assert.Equal(ThinkingEffort.Xhigh, store.Load().Llm.EffortCeiling);
    }

    /// <summary>
    /// The highest-risk detail in the phase. A model id belongs to its endpoint's namespace, so
    /// a background model carried across a provider switch is a request that fails where nothing
    /// is watching: <c>FlavourTurn</c> logs at Debug and returns null, and every ambient line
    /// then falls back to its authored text with nothing on screen.
    /// </summary>
    [Fact]
    public void SwitchingProviderClearsTheBackgroundModelAsWellAsTheConversationOne()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.ModelKey, "claude-opus-5", SettingsCaller.Panel);
        surface.Settings.Apply(ConversationCapability.BackgroundModelKey, "claude-haiku-4-5", SettingsCaller.Panel);

        surface.Settings.Apply(
            ConversationCapability.ProviderKey,
            LlmProviderCatalog.OpenAiId,
            SettingsCaller.KeywordRouter);

        Assert.Null(surface.Settings.Current.Llm.Model);
        Assert.Null(surface.Settings.Current.Llm.BackgroundModel);
    }

    /// <summary>
    /// The same rule at the other seam. On OpenAI, because Anthropic has one address and its
    /// endpoint row does not apply — which is the catalogue's decision, not this phase's.
    /// </summary>
    [Fact]
    public void ChangingTheEndpointClearsTheBackgroundModelAsWell()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.ProviderKey, LlmProviderCatalog.OpenAiId, SettingsCaller.Panel);
        surface.Settings.Apply(ConversationCapability.ModelKey, "gpt-5.6-sol", SettingsCaller.Panel);
        surface.Settings.Apply(ConversationCapability.BackgroundModelKey, "gpt-5.4-mini", SettingsCaller.Panel);

        var applied = surface.Settings.Apply(
            ConversationCapability.EndpointKey,
            "https://gateway.example/v1",
            SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, applied.Status);
        Assert.Null(surface.Settings.Current.Llm.Model);
        Assert.Null(surface.Settings.Current.Llm.BackgroundModel);
    }

    /// <summary>
    /// Each bound offers only the rungs the other allows, so the picker cannot produce a floor
    /// above a ceiling in the first place. The clamp still copes with one, because a settings
    /// file is not written only by the picker.
    /// </summary>
    [Fact]
    public void EachEffortRowTruncatesAgainstTheOther()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(
            ConversationCapability.EffortCeilingKey,
            ThinkingEffortRange.Name(ThinkingEffort.High),
            SettingsCaller.Panel);

        Assert.Equal(
            ["Low", "Medium", "High"],
            Row(surface, ConversationCapability.EffortFloorKey).ChoicesFor(surface.Settings.Current));

        surface.Settings.Apply(ConversationCapability.EffortCeilingKey, null, SettingsCaller.Panel);
        surface.Settings.Apply(
            ConversationCapability.EffortFloorKey,
            ThinkingEffortRange.Name(ThinkingEffort.High),
            SettingsCaller.Panel);

        Assert.Equal(
            ["High", "Xhigh", "Max"],
            Row(surface, ConversationCapability.EffortCeilingKey).ChoicesFor(surface.Settings.Current));
    }

    /// <summary>
    /// Five rungs and a drop-down, not a search window. Open vocabulary is a source with no
    /// choices behind it, so declaring only the source would turn a closed ladder into a box to
    /// type a rung into.
    /// </summary>
    [Fact]
    public void TheEffortRowsAreAClosedLadderRatherThanAnOpenVocabulary()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.False(Row(surface, ConversationCapability.EffortFloorKey).IsOpenVocabulary);
        Assert.False(Row(surface, ConversationCapability.EffortCeilingKey).IsOpenVocabulary);

        // The model rows are the other kind, and stay that way.
        Assert.True(Row(surface, ConversationCapability.BackgroundModelKey).AllowsFreeText);
    }

    /// <summary>
    /// Two phrases, one of which clears the bound rather than setting one. Both are matched on
    /// the whole utterance, so neither can be hit in passing.
    /// </summary>
    [Fact]
    public void TheCeilingCanBeSetAndClearedByVoice()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new KeywordRouter(surface.Registry);

        var quieter = router.MatchSetting("stop thinking so hard");
        Assert.NotNull(quieter);
        surface.Settings.Apply(quieter.Row.Key, quieter.Value, SettingsCaller.KeywordRouter);
        Assert.Equal(ThinkingEffort.Medium, surface.Settings.Current.Llm.EffortCeiling);

        var loosed = router.MatchSetting("think as hard as you like");
        Assert.NotNull(loosed);
        surface.Settings.Apply(loosed.Row.Key, loosed.Value, SettingsCaller.KeywordRouter);
        Assert.Null(surface.Settings.Current.Llm.EffortCeiling);
    }

    private static SettingRow Row(TestSurface surface, string key) =>
        surface.Settings.Sections.SelectMany(section => section.Rows).Single(row => row.Key == key);
}
