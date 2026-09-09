using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The calm settings page.</summary>
public class SeventyFiveKnobsTests
{
    private static IReadOnlyList<SettingRow> Rows(TestSurface surface) =>
        [.. surface.Settings.Sections.SelectMany(section => section.Rows)];

    private static bool Folded(TestSurface surface, SettingRow row, bool showEverything = false) =>
        SettingsFold.IsFolded(
            row,
            surface.Settings.Current,
            surface.Settings.IsChanged(row.Key),
            showEverything);

    /// <summary>
    /// The whole promise, and the one that is expensive to break: folding draws less and changes
    /// nothing.
    /// </summary>
    [Fact]
    public void FoldingWritesNothingAtAll()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(ConversationCapability.EffortCeilingKey, "Medium", SettingsCaller.Panel);

        var before = surface.Settings.Current;

        // Every row asked about, both ways, which is the whole of what drawing the page does.
        foreach (var row in Rows(surface))
        {
            _ = Folded(surface, row);
            _ = Folded(surface, row, showEverything: true);
        }

        Assert.Equal(before, surface.Settings.Current);
        Assert.Equal("Medium", surface.Settings.Read(ConversationCapability.EffortCeilingKey));
    }

    [Fact]
    public void WithTheToggleOnNothingIsFolded()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.All(Rows(surface), row => Assert.False(Folded(surface, row, showEverything: true)));
    }

    /// <summary>
    /// The calm page still has enough on it to configure d47 from nothing: a provider, a model, a key,
    /// a voice, a microphone and a way to make it stop talking.
    /// </summary>
    [Fact]
    public void TheCalmPageStillGetsACommanderRunning()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        string[] essential =
        [
            ConversationCapability.ProviderKey,
            ConversationCapability.ModelKey,
            SpeechCapability.ProviderKey,
            SpeechCapability.VoiceKey,
            ListeningCapability.ModeKey,
            ListeningCapability.DeviceKey,
            InterfaceCapability.ThemeKey,
            CalloutCapability.EnabledKey,
        ];

        foreach (var key in essential)
        {
            var row = surface.Settings.Find(key);

            Assert.NotNull(row);
            Assert.False(Folded(surface, row), $"{key} is folded and a Commander needs it.");
        }
    }

    /// <summary>
    /// A hidden row with no default and no value is a row that silently does nothing — and a Commander
    /// who cannot see the key box cannot work out why nothing speaks.
    /// </summary>
    [Fact]
    public void NoSecretIsEverFolded()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.All(
            Rows(surface).Where(row => row.Kind == SettingKind.Secret),
            row => Assert.False(Folded(surface, row)));
    }

    /// <summary>The rows that decide what leaves this machine stay on the calm page, named one by one.</summary>
    [Theory]
    [InlineData("llm.webSearch")]
    [InlineData("knowledge.galaxy")]
    [InlineData("knowledge.notablePlaces")]
    [InlineData("privacy.memory")]
    [InlineData("memory.enabled")]
    public void ARowThatDecidesWhatLeavesThisMachineIsNeverFolded(string key)
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Settings.Find(key);

        Assert.NotNull(row);
        Assert.False(row.Advanced, $"{key} decides what leaves this machine and must not be folded.");
        Assert.False(Folded(surface, row));
    }

    /// <summary>
    /// And a slot provider row is folded, which is the narrowing itself (the Commander's instruction,
    /// 2026-08-26).
    /// </summary>
    [Fact]
    public void APerSlotVoiceProviderRowIsFolded()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var slots = Rows(surface)
            .Where(row => row.Key.StartsWith("speech.provider.", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(5, slots.Count);
        Assert.All(slots, row => Assert.True(Folded(surface, row)));

        // The one that decides whether the ship's AI speaks at all is not folded.
        Assert.False(Folded(surface, surface.Settings.Find(SpeechCapability.ProviderKey)!));
    }

    /// <summary>
    /// The fold's promise is "you are not missing anything", and a row the Commander changed is by
    /// definition something they did.
    /// </summary>
    [Fact]
    public void ARowTheCommanderChangedIsNeverFolded()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Settings.Find(ConversationCapability.EffortCeilingKey)!;

        Assert.True(row.Advanced);
        Assert.True(Folded(surface, row));

        surface.Settings.Apply(ConversationCapability.EffortCeilingKey, "Medium", SettingsCaller.Panel);

        Assert.False(Folded(surface, row));

        // And putting it back folds it again, with nothing told to do so.
        surface.Settings.Reset(ConversationCapability.EffortCeilingKey, SettingsCaller.Panel);

        Assert.True(Folded(surface, row));
    }

    /// <summary>Eighty-eight spoken phrases write these rows.</summary>
    [Fact]
    public void TheVoiceRouteReachesAFoldedRow()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var router = new D47.Core.Conversation.KeywordRouter(surface.Registry);

        var quieter = router.MatchSetting("stop thinking so hard");

        Assert.NotNull(quieter);
        Assert.True(Folded(surface, quieter.Row), "This test wants a row that is folded.");

        var applied = surface.Settings.Apply(quieter.Row.Key, quieter.Value, SettingsCaller.KeywordRouter);

        Assert.Equal(SettingApplyStatus.Applied, applied.Status);
        Assert.Equal(
            D47.Core.Conversation.ThinkingEffort.Medium,
            surface.Settings.Current.Llm.EffortCeiling);
    }

    /// <summary>The toggle says how much it is folding.</summary>
    [Fact]
    public void TheFoldCanSayHowMuchItIsHiding()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var folded = SettingsFold.Folded(
            Rows(surface),
            surface.Settings.Current,
            row => surface.Settings.IsChanged(row.Key),
            showEverything: false);

        Assert.True(folded > 0, "Nothing is folded, so the fold is doing nothing.");
        Assert.Equal(
            0,
            SettingsFold.Folded(
                Rows(surface),
                surface.Settings.Current,
                row => surface.Settings.IsChanged(row.Key),
                showEverything: true));
    }

    /// <summary>
    /// The toggle itself is on the calm page, or a Commander who folded the settings away has no way to
    /// unfold them.
    /// </summary>
    [Fact]
    public void TheToggleIsNeverFoldedAwayByItself()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Settings.Find(InterfaceCapability.ShowEverySettingKey);

        Assert.NotNull(row);
        Assert.False(row.Advanced);
        Assert.False(Folded(surface, row));
    }
}
