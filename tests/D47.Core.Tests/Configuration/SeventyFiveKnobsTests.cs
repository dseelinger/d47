using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// The calm settings page (<a href="https://github.com/dseelinger/d47/issues/60">#60</a>).
/// <para>
/// Asked for so the page is <i>"helpful, not anxiety-inducing"</i> for a Commander new to Elite
/// or to AI. The fold list was proposed and approved on 2026-08-26.
/// </para>
/// </summary>
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
    /// nothing. The way it breaks is a well-meaning tidy-on-save pass, so it is asserted rather
    /// than commented.
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
    /// The calm page still has enough on it to configure d47 from nothing: a provider, a model, a
    /// key, a voice, a microphone and a way to make it stop talking.
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
    /// A hidden row with no default and no value is a row that silently does nothing — and a
    /// Commander who cannot see the key box cannot work out why nothing speaks.
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

    /// <summary>
    /// The rows that decide what leaves this machine stay on the calm page, named one by one.
    /// <para>
    /// <b>This used to be a rule about a property and is now a list, and the list is the honest
    /// form.</b> The rule exempted anything carrying an <c>EgressId</c>, which turned out to reach
    /// exactly two kinds of row: the API key rows, already exempt for being secrets, and the five
    /// per-slot voice provider rows — which are not consent at all. It never touched the rows
    /// below, none of which carries a disclosure. What keeps these visible is that they are not
    /// marked <c>Advanced</c>, and that is worth asserting by name rather than inferring.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("llm.webSearch")]
    [InlineData("knowledge.galaxy")]
    [InlineData("knowledge.notablePlaces")]
    [InlineData("privacy.memory")]
    [InlineData("privacy.habits")]
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
    /// And a slot provider row <em>is</em> folded, which is the narrowing itself (the Commander's
    /// instruction, 2026-08-26). It chooses which of several providers speaks a line that is
    /// already going out; it does not decide whether anything goes. The provider row above it,
    /// which does, stays on the page.
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
    /// definition something they did. It also makes the rule self-adjusting: a new Commander has
    /// changed nothing and sees the calm page; a tinkerer sees their own work.
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

    /// <summary>
    /// Eighty-eight spoken phrases write these rows. Saying "use my local model" must work whether
    /// or not the row is drawn — anything else is a phrase that silently edits invisible state.
    /// </summary>
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

    /// <summary>
    /// The toggle says how much it is folding. A fold that will not say reads as a secret; one
    /// that does reads as tidy.
    /// </summary>
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
    /// The toggle itself is on the calm page, or a Commander who folded the settings away has no
    /// way to unfold them.
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
