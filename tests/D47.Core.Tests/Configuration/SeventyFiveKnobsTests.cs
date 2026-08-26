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
    /// The Commander's ruling on the one item flagged in the proposed list, 2026-08-26. These are
    /// the rows that decide what leaves the machine, and a page that went calm by no longer
    /// mentioning egress would be calm about the wrong thing.
    /// </summary>
    [Fact]
    public void NothingCarryingAnEgressDisclosureIsFolded()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var carrying = Rows(surface)
            .Where(row => row.EgressId is not null || row.EgressFor is not null)
            .ToList();

        Assert.NotEmpty(carrying);
        Assert.All(carrying, row => Assert.False(Folded(surface, row)));
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
