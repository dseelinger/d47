using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Expand all and Collapse all, and the axis they must not touch
/// (<a href="https://github.com/dseelinger/d47/issues/223">#223</a>).
/// <para>
/// <b>There are two axes here and only one of them is this control's.</b> Card collapse is
/// whether a capability's card is open. The <em>fold</em> is a different thing: it decides which
/// rows a calm page shows at all, it is a persisted preference the Commander set, and
/// <c>SettingsFold</c>'s own rule is that folding is a pure display decision that writes nothing.
/// A chrome button that flipped a setting as a side effect would be a different kind of act from
/// opening a card, and the two are separately meaningful — every card open and still the calm row
/// set is a reasonable thing to want.
/// </para>
/// <para>
/// So the promise asserted here is narrow and total: the cards move, and <b>nothing is written</b>.
/// That is the promise a bulk control is most likely to break, because it touches every card at
/// once and a settings write hidden in that loop would look exactly like the feature working.
/// </para>
/// </summary>
public class EveryCardOpensAndShutsAtOnceTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static (Window Window, SettingsView View, SettingsService Settings) Open()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var view = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        var window = new Window { Content = panel, Width = 1180, Height = 880 };

        ZoomHost.Attach(window, settings);

        window.Show();
        Jobs();

        panel.Tab = PanelTab.Settings;
        Jobs();

        return (window, view, settings);
    }

    /// <summary>The card bodies — one per capability, and the thing these two controls move.</summary>
    private static IReadOnlyList<Control> Bodies(SettingsView view) =>
        [.. ((StackPanel)view.FindControl<Control>("Cards")!).Children
            .OfType<Border>()
            .Select(card => (StackPanel)card.Child!)
            .Where(body => body.Children.Count > 1)
            .Select(body => body.Children[1])];

    private static void Press(SettingsView view, string name)
    {
        // Found in the tree rather than by FindControl: the two glyphs are built in code now
        // (2026-09-01) so they carry no axaml namescope, and FindControl answers null.
        view.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();
    }

    [AvaloniaFact]
    public void ExpandAllOpensEveryCardAndCollapseAllShutsThemAll()
    {
        var (window, view, _) = Open();

        var bodies = Bodies(view);

        Assert.NotEmpty(bodies);

        // Including any card whose capability asked to start shut: a bulk control that skipped
        // those would leave the page in a state the Commander cannot get out of in one press.
        Press(view, "CollapseAll");
        Assert.All(bodies, body => Assert.False(body.IsVisible));

        Press(view, "ExpandAll");
        Assert.All(bodies, body => Assert.True(body.IsVisible));

        Press(view, "CollapseAll");
        Assert.All(bodies, body => Assert.False(body.IsVisible));

        window.Close();
    }

    /// <summary>
    /// The pair as drawn, for a human to look at. They are path data written in this repository
    /// rather than glyphs from a font — the kind of thing that compiles, lays out, passes every
    /// assertion here and still does not read as "open everything".
    /// </summary>
    [AvaloniaFact]
    public void TheTwoMarksAreDrawnForLookingAt()
    {
        var (window, _, _) = Open();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "settings-expand-all.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    /// <summary>
    /// <b>Neither touches the fold</b>, which is the separation the whole issue turns on. The cost
    /// is honest and worth stating: pressing Expand all with the fold on opens every card and
    /// still does not show every row. The answer to that reading badly is to make the fold's own
    /// control easier to find, never to have one button quietly drive two axes.
    /// </summary>
    [AvaloniaFact]
    public void NeitherTouchesTheFoldOrWritesAnySettingAtAll()
    {
        var (window, view, settings) = Open();

        var before = settings.Current;
        var folded = settings.Current.Ui.ShowEverySetting;

        var changes = 0;
        settings.Changed += _ => changes++;

        Press(view, "ExpandAll");
        Press(view, "CollapseAll");
        Press(view, "ExpandAll");

        Assert.Equal(folded, settings.Current.Ui.ShowEverySetting);

        // Not "the fold is unchanged" but "nothing is": the promise SettingsFold makes is that
        // this path writes, clears and defaults nothing at all.
        Assert.Equal(0, changes);
        Assert.Same(before, settings.Current);

        window.Close();
    }

    /// <summary>
    /// It persists, exactly as clicking each header by hand does — it is a thing the Commander
    /// pressed on purpose, and a bulk control whose effect died with the window would be a
    /// control that does half of what it looks like it does.
    /// </summary>
    [AvaloniaFact]
    public void WhatWasPressedSurvivesTheWindow()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var first = new SettingsView();
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableSettings(() =>
        {
            first.Attach(settings, viewState, paths);
            return first;
        });

        var window = new Window { Content = panel, Width = 1180, Height = 880 };
        ZoomHost.Attach(window, settings);
        window.Show();
        Jobs();

        panel.Tab = PanelTab.Settings;
        Jobs();

        Press(first, "CollapseAll");
        window.Close();

        // A second view over the same view state, which is what a restart is.
        var next = new SettingsView();
        var again = new PanelView { DataContext = new PanelViewModel() };

        again.EnableSettings(() =>
        {
            next.Attach(settings, viewState, paths);
            return next;
        });

        var window2 = new Window { Content = again, Width = 1180, Height = 880 };
        ZoomHost.Attach(window2, settings);
        window2.Show();
        Jobs();

        again.Tab = PanelTab.Settings;
        Jobs();

        Assert.All(Bodies(next), body => Assert.False(body.IsVisible));

        window2.Close();
    }

    /// <summary>
    /// And a card's own reset forgets what was said about it, so
    /// <c>Display.StartCollapsed</c> can decide again (#223).
    /// <para>
    /// Collapse all writes a state for every card at once, and a card with a written state never
    /// falls back to its default again — so without this, one press buries that default
    /// permanently and nothing brings it back.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void ACardsResetGivesItsOpeningDefaultBack()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        var state = viewState.Load();
        var shut = state.With(InterfaceCapability.Id, expanded: false);

        Assert.False(shut.IsExpanded(InterfaceCapability.Id, startCollapsed: false));

        // Forgetting is what the card's reset does, and the default decides again.
        var forgotten = shut.Forgetting(InterfaceCapability.Id);

        Assert.True(forgotten.IsExpanded(InterfaceCapability.Id, startCollapsed: false));
        Assert.False(forgotten.IsExpanded(InterfaceCapability.Id, startCollapsed: true));

        _ = settings;
        _ = paths;
    }
}
