using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// One visible window (list.md Phase 12, "Settings is a tab of the main window, not a second
/// window"). Settings is the fourth page of the strip that already carried Conversation,
/// Technical and Log file, and takes the place of the transcript when it is selected.
/// </summary>
public class SettingsIsATabTests
{
    private static PanelView Panel(bool withSettings)
    {
        var view = new PanelView { DataContext = new PanelViewModel() };

        if (withSettings)
        {
            view.EnableSettings(() => new TextBlock { Text = "settings" });
        }

        return view;
    }

    /// <summary>
    /// By name scope rather than by visual descent, so a view that has never been shown — the
    /// headset builds one and rasterises it offscreen — answers the same question.
    /// </summary>
    private static Control Named(PanelView view, string name) =>
        view.FindControl<Control>(name) ?? throw new InvalidOperationException($"no {name}");

    /// <summary>
    /// The tab is a capability the host grants. The desktop window grants it and the headset
    /// never does, which is what keeps a 1180-pixel nav column out of a quad a metre away
    /// without anybody having to remember to leave it out.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheTabAppearsOnlyWhereSettingsWereEnabled(bool enabled)
    {
        var view = Panel(enabled);
        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();

        Assert.Equal(enabled, Named(view, "SettingsTab").IsVisible);

        window.Close();
    }

    /// <summary>
    /// The headset's own instantiation has one when it is given a builder, and does not when it
    /// is not.
    /// <para>
    /// It used to have none by construction, on the reasoning that a nav column beside a
    /// 700-pixel minimum has no business on a quad a metre away. The quad is 1024 wide and the
    /// surface collapses its nav below 900, so what it renders is the arrangement the desktop
    /// window shows at its default size — and with a headset on there is no other way to reach a
    /// setting at all (remediation.md, "The VR big panel should carry the Settings tab").
    /// </para>
    /// <para>
    /// Read out of the real <see cref="Headset.VrPanelSurface"/> rather than out of a view built
    /// here to resemble it. Resembling it is exactly what a test of this cannot afford: the claim
    /// is about what the overlay does.
    /// </para>
    /// </summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheHeadsetCopyHasSettingsWhenItIsGivenThem(bool given)
    {
        var (settings, _, _) = TestSurface.Create();

        using var surface = new Headset.VrPanelSurface(
            new PanelViewModel(),
            settings,
            _ => null,
            settingsPage: given ? () => new TextBlock { Text = "settings" } : null);

        var view = (PanelView)surface.GetType()
            .GetField("_view", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(surface)!;

        Assert.Equal(given, Named(view, "SettingsTab").IsVisible);
    }

    /// <summary>
    /// A surface with no settings page cannot be put on one, whatever sets the property. The
    /// alternative is an empty pane with no way back to the transcript.
    /// </summary>
    [AvaloniaFact]
    public void ASurfaceWithoutSettingsRefusesTheSettingsPage()
    {
        var view = Panel(withSettings: false);
        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();

        view.Page = TranscriptPage.Technical;
        view.Page = TranscriptPage.Settings;

        Assert.Equal(TranscriptPage.Technical, view.Page);
        Assert.True(Named(view, "TranscriptPane").IsVisible);

        window.Close();
    }

    /// <summary>
    /// The settings page takes the transcript's place, and the ask line gives way to the footer
    /// the settings surface brings with it.
    /// </summary>
    [AvaloniaFact]
    public void TheSettingsPageReplacesTheTranscriptAndTheAskLine()
    {
        var view = Panel(withSettings: true);
        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();

        Assert.True(Named(view, "AskRow").IsVisible);

        view.Page = TranscriptPage.Settings;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(Named(view, "TranscriptPane").IsVisible);
        Assert.True(Named(view, "SettingsPane").IsVisible);
        Assert.False(Named(view, "AskRow").IsVisible);

        // Effectively rather than directly: the provenance line shares a row with the microphone
        // indicator since Phase 13, and it is the row that is hidden. Asserting what is actually
        // on screen is the claim this test is making anyway, and it survives the next time
        // something moves.
        Assert.False(Named(view, "TurnLine").IsEffectivelyVisible);

        // The header stays: the avatar and the help glyph are as true on this page as on any
        // other. What left it is the gear, which this tab replaces.
        Assert.True(Named(view, "Header").IsVisible);
        Assert.Null(view.FindControl<Control>("SettingsButton"));

        window.Close();
    }

    /// <summary>Built on first selection rather than at startup — ninety-odd rows is not free.</summary>
    [AvaloniaFact]
    public void TheSettingsPageIsNotBuiltUntilItIsSelected()
    {
        var built = 0;
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableSettings(() =>
        {
            built++;
            return new TextBlock { Text = "settings" };
        });

        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();

        Assert.Equal(0, built);

        view.Page = TranscriptPage.Settings;
        view.Page = TranscriptPage.Conversation;
        view.Page = TranscriptPage.Settings;

        Assert.Equal(1, built);

        window.Close();
    }

    /// <summary>
    /// Escape puts back the page it covered up, not a fixed default: a Commander reading the log
    /// who opens settings and changes their mind is put back on the log.
    /// </summary>
    [AvaloniaFact]
    public void EscapeReturnsToThePageYouCameFrom()
    {
        var view = Panel(withSettings: true);
        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();

        view.Page = TranscriptPage.Log;
        view.Page = TranscriptPage.Settings;

        Assert.True(view.LeaveSettings());
        Assert.Equal(TranscriptPage.Log, view.Page);

        // And there is nothing to leave when it is not showing, so the key stays available to
        // whatever else wants it.
        Assert.False(view.LeaveSettings());

        window.Close();
    }

    /// <summary>Through the window's own key handling, which is where the gesture actually lands.</summary>
    [AvaloniaFact]
    public void EscapeOnTheMainWindowLeavesTheSettingsPage()
    {
        var (settings, _, _) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(settings.Current.Ui.Theme);

        var window = new MainWindow(host: null);

        // No host under a headless run, so the page is enabled the way a host would.
        window.Panel.EnableSettings(() => new TextBlock { Text = "settings" });
        window.Show();

        window.Panel.Page = TranscriptPage.Settings;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(TranscriptPage.Conversation, window.Panel.Page);

        window.Close();
    }

    /// <summary>
    /// The nav column collapses on a narrow page and comes back on a wide one. This is what
    /// makes the second window genuinely removed rather than the first one made too big: the
    /// panel opens at 820 and is meant to sit beside a running game.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(820, false)]
    [InlineData(1180, true)]
    public void TheNavColumnFollowsTheWidth(double width, bool shown)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, width: width);

        var nav = host.View.GetVisualDescendants().OfType<Control>().First(c => c.Name == "Nav");
        var root = (Grid)host.View.GetVisualDescendants().OfType<Control>().First(c => c.Name == "Root");

        Assert.Equal(shown, nav.IsVisible);
        Assert.Equal(shown ? 224 : 0, root.ColumnDefinitions[0].Width.Value);

        // And the floor moves with it, or a page with no nav would still be asking for the width
        // of one.
        Assert.Equal(shown ? 700 : 476, root.MinWidth);

        host.Close();
    }

    /// <summary>
    /// The rows still get their width on the narrow page — the whole point of collapsing the nav
    /// is that the cards take what it was using.
    /// </summary>
    [AvaloniaFact]
    public void TheCardsKeepTheirShapeWithTheNavCollapsed()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, width: 820);

        // By the class the view marks its rows with, not by "three columns" — that also matched
        // the grid Avalonia builds a TextBox out of. See RowWidthTests.CompactRows.
        var rows = host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass) && grid.Bounds.Width > 0)
            .ToList();

        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            Assert.True(
                row.ColumnDefinitions[0].ActualWidth >= row.ColumnDefinitions[2].ActualWidth,
                $"a row gave {row.ColumnDefinitions[2].ActualWidth:0} to its control and only "
                + $"{row.ColumnDefinitions[0].ActualWidth:0} to its caption");
        }

        host.Close();
    }

    /// <summary>
    /// The page at the width the Commander actually opens it at, for a human to look at. The
    /// claim being checked by eye is that removing the second window did not cost the rows their
    /// shape — and that the selected tab still joins the page it belongs to.
    /// </summary>
    [AvaloniaFact]
    public void TheSettingsPageRendersToACaptureAtTheDefaultWindowWidth()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths, width: 820, height: 640);

        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "settings-tab-narrow.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        host.Close();
    }

    /// <summary>
    /// Hotkey capture sees a key the push-to-talk suppressor has already marked handled.
    /// <para>
    /// Settings is a page of the main window now, so the suppressor that stops push-to-talk
    /// typing into the ask box tunnels over the binder too. Without <c>handledEventsToo</c> the
    /// one rebind that could not be made would be rebinding push-to-talk to the key it already
    /// holds — which is exactly the rebind somebody attempting it is most likely to try.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void RebindingPushToTalkToTheKeyItAlreadyHoldsStillCaptures()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var view = new SettingsView();

        var window = new MainWindow(host: null) { PushToTalkGesture = () => "F9" };
        window.Panel.EnableSettings(() =>
        {
            view.Attach(settings, viewState, paths);
            return view;
        });

        window.Show();
        window.Panel.Page = TranscriptPage.Settings;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // The push-to-talk row itself, which is the row this is about: F9 is the key it already
        // holds, and F9 is the key the suppressor above is swallowing.
        var row = view.GetVisualDescendants().OfType<Grid>()
            .First(grid => grid.ColumnDefinitions.Count == 3
                && grid.GetVisualDescendants().OfType<TextBlock>()
                    .Any(text => text.Text == "Push-to-talk key"));

        var bind = row.GetVisualDescendants().OfType<Button>()
            .First(button => (button.Content as string) != "Unbind");

        bind.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.F9, RawInputModifiers.None, PhysicalKey.F9, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("F9", settings.Current.Listening.PushToTalkKey);

        window.Close();
    }
}
