using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every tab back on the reading it was left on, and the settings page back where it was scrolled
/// to (<a href="https://github.com/dseelinger/d47/issues/268">#268</a>).
/// <para>
/// Half of this already worked: <c>PanelNavigator</c> keeps one current root per tab, so a tab
/// switch has always returned to the mode it left. What it never did was write that down, so every
/// launch started at the first reading each tab furnished.
/// </para>
/// </summary>
public sealed class EveryTabOpensWhereItWasLeftTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void ATabOpensOnTheReadingItWasLeftOn()
    {
        var store = Store();

        var (first, window) = Shown(store);

        first.Tab = PanelTab.Routing;
        first.Nav.SelectRoot(PanelTab.Routing, RoutingPages.CourseRoot);
        Jobs();

        Assert.Equal(RoutingPages.CourseRoot, first.Nav.RootKeyOf(PanelTab.Routing));

        window.Close();

        // A second panel over the same store, which is what the next launch has.
        var (next, second) = Shown(store);

        Assert.Equal(RoutingPages.CourseRoot, next.Nav.RootKeyOf(PanelTab.Routing));

        second.Close();
    }

    /// <summary>
    /// The transcript reading too, which is the one the request named first — and the one that
    /// takes the extra care, because Raw Journal is a root of this tab like the journal itself is.
    /// </summary>
    [AvaloniaFact]
    public void TheTranscriptOpensOnTheReadingItWasLeftOn()
    {
        var store = Store();

        var (first, window) = Shown(store);

        first.Page = TranscriptPage.Log;
        Jobs();

        window.Close();

        var (next, second) = Shown(store);

        Assert.Equal(TranscriptPage.Log, next.Page);

        second.Close();
    }

    /// <summary>
    /// <b>Raw Journal is written down as the journal.</b> Storing it as a root would restore it as
    /// one, and the Transcript would open on a wall of JSON — the exact thing
    /// <see cref="TheRawSwitchIsWhereItWasLeftTests"/> exists to keep from happening. How the
    /// journal reading is drawn is the switch's fact and is kept once.
    /// </summary>
    [AvaloniaFact]
    public void TheRawReadingIsNeverWhatIsRemembered()
    {
        var store = Store();

        var (first, window) = Shown(store);

        first.Page = TranscriptPage.RawJournal;
        Jobs();

        window.Close();

        Assert.Equal(
            PanelView.JournalRoot,
            store.Load().PanelRoots[PanelTab.Transcript.ToString()]);
    }

    /// <summary>
    /// A root nothing answers to costs the tab's first reading rather than raising — a renamed
    /// reading, a tab this surface never furnished, or a hand-edited file.
    /// </summary>
    [AvaloniaFact]
    public void ARootNothingAnswersToIsIgnored()
    {
        var store = Store();

        store.Save(store.Load()
            .With(PanelTab.Routing.ToString(), "routing.somewhere-that-was-renamed")
            .With("Cartography", "a.tab.that.never.existed"));

        var (panel, window) = Shown(store);

        Assert.Equal(RoutingPages.PlanRoot, panel.Nav.RootKeyOf(PanelTab.Routing));
        Assert.Equal(PanelTab.Transcript, panel.Tab);

        window.Close();
    }

    /// <summary>
    /// The settings page opens scrolled to the section it was left on — and <b>unfolds nothing</b>.
    /// <see cref="SettingsView.Reveal"/> expands the card it lands on because a help link to a
    /// folded card goes nowhere; arriving at the page you left is not that act.
    /// </summary>
    [AvaloniaFact]
    public void SettingsOpensOnTheSectionItWasLeftOn()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);

        var (_, view, window) = Settings(settings, viewState, paths);

        // Two along, so the answer cannot be the top of the page by accident.
        var section = view.SectionIds[2];

        view.Reveal(section);
        Jobs();

        // What the settle timer calls. Half a second of real time is not something a headless test
        // can wait out, and the length of the pause is not what is worth asserting.
        view.SettleSection();

        Assert.Equal(section, viewState.Load().SettingsSection);

        window.Close();

        // The next launch, over the same store.
        var (_, next, second) = Settings(settings, viewState, paths);

        Assert.Equal(section, next.SectionIds[next.ActiveSection]);

        second.Close();
    }

    /// <summary>
    /// And it <b>unfolds nothing</b>. <see cref="SettingsView.Reveal"/> expands the card it lands
    /// on because a help link to a folded card is a link that goes nowhere; arriving at the page
    /// you left is not that act, and should not quietly open cards the Commander had closed.
    /// </summary>
    [AvaloniaFact]
    public void RestoringTheSectionOpensNoCard()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);

        var (_, view, window) = Settings(settings, viewState, paths);

        var section = view.SectionIds[2];

        window.Close();

        // Left scrolled to a card the Commander had also closed.
        viewState.Save(viewState.Load().With(section, expanded: false) with { SettingsSection = section });

        var (_, next, second) = Settings(settings, viewState, paths);

        Assert.Equal(section, next.SectionIds[next.ActiveSection]);
        Assert.False(next.IsSectionExpanded(next.ActiveSection));

        second.Close();
    }

    /// <summary>
    /// A section this build no longer registers is a stale name, and a stale name is worth the top
    /// of the page rather than a failure.
    /// </summary>
    [AvaloniaFact]
    public void AStaleSectionNameLeavesThePageAtTheTop()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        viewState.Save(viewState.Load() with { SettingsSection = "a.capability.that.was.retired" });

        var (_, view, window) = Settings(settings, viewState, paths);

        Assert.Equal(0, view.ActiveSection);

        window.Close();
    }

    private static ViewStateStore Store() =>
        new(
            new D47.Core.AppPaths(TempFolders.Create("d47-tab-memory-tests")),
            NullLogger<ViewStateStore>.Instance);

    private static (PanelView Panel, Window Window) Shown(ViewStateStore store)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableRawJournal();
        panel.RememberJournalReading(new JournalReadingMemory(store));

        panel.EnableRouting(new RoutingSurface(() => NavRoute.None, () => null));

        // Last, exactly as the window wires it: a root can only be selected once the tab that owns
        // it has been furnished.
        panel.RememberRoots(new PanelRootMemory(store));

        var window = new Window { Content = panel, Width = 1180, Height = 800 };

        window.Show();
        Jobs();

        return (panel, window);
    }

    private static (PanelView Panel, SettingsView View, Window Window) Settings(
        SettingsService settings, ViewStateStore viewState, D47.Core.AppPaths paths)
    {
        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        var window = new Window { Content = panel, Width = 1180, Height = 880 };

        window.Show();
        Jobs();

        panel.Tab = PanelTab.Settings;
        Jobs();

        return (panel, view, window);
    }

}
