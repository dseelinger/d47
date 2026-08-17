using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// One search affordance, on the tab that is selected (list.md Phase 12). What a match
/// <em>does</em> differs by page and that difference is the design: the transcript pages are
/// prose so a query highlights and steps, and Settings is 92 rows so a query filters.
/// </summary>
public class SearchTheTabTests
{
    private static PanelViewModel Said()
    {
        var model = new PanelViewModel();

        model.Append("Fuel at 12 percent.\n");
        model.Append("\n> where is the fuel\n");
        model.Append("Fixture Anchorage has fuel.");

        return model;
    }

    private static (Window Window, PanelView View) Open(PanelViewModel model)
    {
        // Themed, because the highlight is a theme resource: unthemed, every brush lookup
        // resolves to nothing and a test reading the fills back would be comparing two absences.
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var view = new PanelView { DataContext = model };
        view.EnableSearch();

        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (window, view);
    }

    private static Control Named(PanelView view, string name) =>
        view.FindControl<Control>(name) ?? throw new InvalidOperationException($"no {name}");

    private static TextBox Box(PanelView view) => (TextBox)Named(view, "SearchInput");

    private static string Count(PanelView view) => ((TextBlock)Named(view, "SearchCount")).Text ?? string.Empty;

    /// <summary>Every run of the transcript that is drawn with a highlight behind it.</summary>
    private static List<Run> Highlighted(PanelView view) =>
        [.. ((SelectableTextBlock)Named(view, "Transcript")).Inlines!
            .OfType<Run>()
            .Where(run => run.Background is not null)];

    [AvaloniaFact]
    public void AQueryHighlightsEveryHitAndSaysHowManyThereAre()
    {
        var (window, view) = Open(Said());

        Box(view).Text = "fuel";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var hits = Highlighted(view);

        Assert.Equal(3, hits.Count);
        Assert.All(hits, run => Assert.Equal("fuel", run.Text, ignoreCase: true));
        Assert.Equal("1 of 3", Count(view));

        // The lines around the hits stay where they were: reading back a conversation means
        // reading what was around it, so this highlights rather than filters.
        Assert.Contains("Fixture Anchorage", Drawn(view), StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// One of the hits is the current one and is drawn differently, or the count is describing a
    /// position nothing on screen shows.
    /// </summary>
    [AvaloniaFact]
    public void ExactlyOneHitIsAccentedAndSteppingMovesIt()
    {
        var (window, view) = Open(Said());

        Box(view).Text = "fuel";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, CurrentHit(view));

        ((Button)Named(view, "SearchNext")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, CurrentHit(view));
        Assert.Equal("2 of 3", Count(view));

        // And it wraps, at both ends.
        ((Button)Named(view, "SearchPrevious")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((Button)Named(view, "SearchPrevious")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("3 of 3", Count(view));

        window.Close();
    }

    /// <summary>
    /// The highlight, the count and the steppers, for a human to look at. Whether a marked hit
    /// reads as the current one and the rest read as the others is a question only eyes answer.
    /// </summary>
    [AvaloniaFact]
    public void TheSearchHighlightRendersToACapture()
    {
        var (window, view) = Open(Said());

        Box(view).Text = "fuel";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ((Button)Named(view, "SearchNext")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "search-highlight.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        window.Close();
    }

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>Everything on the transcript, hits and the prose around them alike.</summary>
    private static string Drawn(PanelView view) =>
        string.Concat(((SelectableTextBlock)Named(view, "Transcript")).Inlines!
            .OfType<Run>()
            .Select(run => run.Text ?? string.Empty));

    /// <summary>
    /// Which hit is the current one, told the way a reader tells: it is the one drawn in a
    /// different colour from the rest of them.
    /// </summary>
    private static int CurrentHit(PanelView view)
    {
        var fills = Highlighted(view).Select(run => Colour(run.Background)).ToList();
        var odd = fills.Select((fill, i) => (fill, i)).Where(pair => fills.Count(f => f == pair.fill) == 1).ToList();

        Assert.Single(odd);

        return odd[0].i;
    }

    /// <summary>
    /// A hit found in the live log stays found as lines arrive. This is the property the whole
    /// offset-rather-than-index arrangement exists for.
    /// </summary>
    [AvaloniaFact]
    public void AHitStaysSelectedAcrossAnAppend()
    {
        var model = Said();
        var (window, view) = Open(model);

        Box(view).Text = "fuel";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        ((Button)Named(view, "SearchNext")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("2 of 3", Count(view));

        model.Append("\nFuel scoop engaged. Fuel rising.");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Two more hits arrived after it, and the one being read is still the one being read.
        Assert.Equal("2 of 5", Count(view));

        window.Close();
    }

    [AvaloniaFact]
    public void AQueryThatMatchesNothingSaysSo()
    {
        var (window, view) = Open(Said());

        Box(view).Text = "thargoid";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(Highlighted(view));
        Assert.Equal("no matches", Count(view));

        window.Close();
    }

    /// <summary>
    /// The query belongs to the page. One string that filters here and highlights there is a
    /// control that behaves differently depending on where the Commander last clicked.
    /// </summary>
    [AvaloniaFact]
    public void TheQueryIsDroppedWhenThePageChanges()
    {
        var (window, view) = Open(Said());

        Box(view).Text = "fuel";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(Highlighted(view));

        view.Page = TranscriptPage.Technical;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, Box(view).Text ?? string.Empty);
        Assert.Empty(Highlighted(view));

        window.Close();
    }

    /// <summary>Ctrl+F reaches the box from inside the ask box, which is where the caret often is.</summary>
    [AvaloniaFact]
    public void ControlFFocusesTheBoxAndEscapeGivesThePageBack()
    {
        var (window, view) = Open(Said());

        view.FocusAsk();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Box(view).IsFocused);

        Box(view).Text = "fuel";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, Box(view).Text ?? string.Empty);
        Assert.False(Box(view).IsFocused);

        window.Close();
    }

    /// <summary>
    /// Mini shows no strip, and a surface that was not handed a search box does not have one —
    /// which is what keeps a box the Commander cannot type into out of the headset.
    /// </summary>
    [AvaloniaFact]
    public void MiniAndTheHeadsetHaveNoSearchBox()
    {
        var mini = new PanelView { DataContext = Said(), Mode = PanelMode.Mini };
        mini.EnableSearch();

        var plain = new PanelView { DataContext = Said() };

        var window = new Window { Content = new StackPanel { Children = { mini, plain } } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Enabled, but inside a strip mini does not show at all.
        Assert.False(Named(mini, "TabStrip").IsVisible);

        // And never enabled, which is the headset's case.
        Assert.False(Named(plain, "SearchRow").IsVisible);

        window.Close();
    }

    /// <summary>
    /// Settings filters. A row survives if its label, help or key matches; a card goes when
    /// every row in it has; and the nav shows only the sections still holding something.
    /// </summary>
    [AvaloniaFact]
    public void SettingsFiltersItsRowsItsCardsAndItsNav()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var cardsBefore = Cards(host);
        var navBefore = Nav(host);

        Assert.True(cardsBefore > 1, "there is more than one card to filter down from");

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;
        box.Text = "push-to-talk";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Cards(host) < cardsBefore, "a card holding nothing that matched is still shown");
        Assert.True(Nav(host) < navBefore, "the nav still lists a section holding nothing");
        Assert.True(Rows(host) > 0, "the filter left no rows at all");

        Assert.All(
            VisibleRowLabels(host),
            label => Assert.Contains("push-to-talk", label, StringComparison.OrdinalIgnoreCase));

        // And clearing it puts everything back.
        box.Text = string.Empty;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(cardsBefore, Cards(host));
        Assert.Equal(navBefore, Nav(host));

        host.Close();
    }

    /// <summary>
    /// The query is dropped when the page changes, and the page it was filtering is the page
    /// that has to hear about it.
    /// <para>
    /// The box empties itself on the way out, and its <c>TextChanged</c> hands the now-empty
    /// query to whatever page is <em>current</em> — which, by the time a page change has run, is
    /// the page being arrived at. So Settings kept the filter it was given and the Commander came
    /// back to an empty search box over four sections of eighteen, with nothing to type into it
    /// that would put the rest back (bugs.md 2).
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void LeavingTheSettingsPageClearsTheFilterItWasLeftWith()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var cardsBefore = Cards(host);
        var navBefore = Nav(host);

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;
        box.Text = "push-to-talk";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Nav(host) < navBefore, "the filter did not take");

        host.Panel.Page = TranscriptPage.Conversation;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        host.Panel.Page = TranscriptPage.Settings;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, box.Text ?? string.Empty);
        Assert.Equal(cardsBefore, Cards(host));
        Assert.Equal(navBefore, Nav(host));

        host.Close();
    }

    /// <summary>
    /// The cross appears only once there is a query behind it. A control that is present and
    /// inert for most of the time it can be seen is one a Commander learns to stop reading.
    /// </summary>
    [AvaloniaFact]
    public void TheClearGlyphIsOnlyThereWhenThereIsSomethingToClear()
    {
        var (window, view) = Open(Said());
        var clear = Named(view, "SearchClear");

        Assert.False(clear.IsVisible);

        Box(view).Text = "docked";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(clear.IsVisible);

        Box(view).Text = string.Empty;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(clear.IsVisible);

        window.Close();
    }

    /// <summary>
    /// The cross runs the same clear path Escape does, and the claim worth asserting is about the
    /// page rather than about the box: blanking the text without letting the query back through
    /// the handler would leave settings filtered to a handful of sections with an empty search
    /// box above them — which is bugs.md 2, reintroduced through a new control.
    /// </summary>
    [AvaloniaFact]
    public void TheClearGlyphPutsTheWholeFilteredPageBack()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var cardsBefore = Cards(host);
        var navBefore = Nav(host);

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;
        box.Text = "push-to-talk";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Nav(host) < navBefore, "the filter did not take");

        var clear = host.Panel.FindControl<Control>("SearchClear")!;
        clear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, box.Text ?? string.Empty);
        Assert.Equal(cardsBefore, Cards(host));
        Assert.Equal(navBefore, Nav(host));

        host.Close();
    }

    /// <summary>Every highlighted run under one control, for a claim about where a mark landed.</summary>
    private static List<Run> MarkedIn(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .SelectMany(block => (IEnumerable<Run>?)block.Inlines?.OfType<Run>() ?? [])
            .Where(run => run.Background is not null)];

    /// <summary>Every run on the settings page drawn with a highlight behind it.</summary>
    private static List<Run> MarkedRuns(SettingsHost host) =>
        [.. host.View.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .SelectMany(block => (IEnumerable<Run>?)block.Inlines?.OfType<Run>() ?? [])
            .Where(run => run.Background is not null)];

    /// <summary>
    /// The filter cuts the page down and the highlight says why each survivor is on it. Both, not
    /// one or the other: rows with nothing marked leave the Commander comparing the query against
    /// every word to work out what it caught.
    /// </summary>
    [AvaloniaFact]
    public void SurvivingRowsMarkWhatTheQueryFound()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.Empty(MarkedRuns(host));

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;
        box.Text = "microphone";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var marked = MarkedRuns(host);

        Assert.NotEmpty(marked);
        Assert.All(marked, run => Assert.Equal("microphone", run.Text, ignoreCase: true));

        // A TextBlock draws its Text and then its Inlines, so a caption that still holds both
        // renders twice — the plain string followed by the marked one. Nothing about the runs
        // says so, which is why the claim is made about the string they replaced.
        Assert.All(
            host.View.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.Inlines is { Count: > 0 }),
            block => Assert.True(
                string.IsNullOrEmpty(block.Text),
                $"a marked caption still has Text \"{block.Text}\" behind its runs, so it draws twice"));

        // And the marks come off with the query, or a row keeps a highlight for a string nobody
        // is searching for.
        box.Text = string.Empty;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(MarkedRuns(host));

        host.Close();
    }

    /// <summary>
    /// A row matched on its key alone used to survive with nothing on screen agreeing with the
    /// query — the filter looks broken when the one thing it matched is the one thing never
    /// drawn. The key appears for exactly that case, and stays away when the words already
    /// explain themselves.
    /// </summary>
    [AvaloniaFact]
    public void AKeyOnlyMatchShowsTheKeyThatMatched()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;
        box.Text = "listening.pushToTalk";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains("Push-to-talk key", VisibleRowLabels(host));

        var shown = host.View.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(Words)
            .ToList();

        // The whole key, though the query was only a prefix of it — the line is evidence about
        // the row, not an echo of what was typed.
        Assert.Contains("listening.pushToTalkKey", shown);

        // A query the label carries needs no identifier under it.
        box.Text = "Push-to-talk key";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            "listening.pushToTalkKey",
            host.View.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(Words));

        host.Close();
    }

    /// <summary>
    /// The filtered page with its hits marked, for a human to look at. Whether a highlight on a
    /// caption reads as an answer or as noise is a question only eyes settle.
    /// </summary>
    [AvaloniaFact]
    public void TheFilteredPageRendersToACapture()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;
        box.Text = "microphone";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "settings-filter-highlight.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        host.Close();
    }

    /// <summary>
    /// A row is reachable by the key the documentation and a hand-edited settings file call it,
    /// not only by the words on screen.
    /// </summary>
    [AvaloniaFact]
    public void ARowIsFoundByItsKeyAsWellAsItsLabel()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;
        box.Text = "listening.pushToTalk";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Contains("Push-to-talk key", VisibleRowLabels(host));

        host.Close();
    }

    /// <summary>
    /// A filter opens the card it matched in, whatever the Commander left it as. A card
    /// collapsed over the row that matched is a filter that hides its own answer — and clearing
    /// the query puts the card back the way they left it.
    /// </summary>
    [AvaloniaFact]
    public void AFilterOpensTheCardItMatchedInAndClosesItAgainAfter()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        // Diagnostics starts collapsed, which is exactly the case this is about.
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;

        var shut = Rows(host);

        box.Text = "log";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Rows(host) > 0, "the rows that matched are behind a shut card");

        box.Text = string.Empty;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(shut, Rows(host));

        host.Close();
    }

    /// <summary>
    /// A section name is a match too (change-requests.md 15). Typing "Speech" used to find rows
    /// and not the card called Speech, so a search for a section's own name looked like it had
    /// found nothing at the top of the thing it was looking for.
    /// </summary>
    [AvaloniaFact]
    public void ASectionIsFoundByItsOwnName()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;

        box.Text = "Speech";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // The name is marked in both places it is written — the card's own heading and the nav
        // item — rather than only in whichever rows happen to repeat the word.
        Assert.Contains(MarkedIn(host.View.FindControl<Control>("Cards")!), run => run.Text == "Speech");
        Assert.Contains(MarkedIn(host.View.FindControl<Control>("NavItems")!), run => run.Text == "Speech");

        // Whether a mark on a heading reads as an answer or as noise is a question only eyes
        // settle, and this one lands on a card title and a nav item at once.
        host.Window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "settings-section-name-highlight.png"),
            new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        // And a named section keeps the rows it has, rather than only the ones that happen to
        // repeat the word. "Audio mixer" is the case that proves it: nothing inside that card
        // says "audio mixer", so before this the card answered a search for its own name by
        // emptying itself and then vanishing for being empty.
        box.Text = "Audio mixer";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, Cards(host));
        Assert.NotEmpty(VisibleRowLabels(host));
        Assert.Contains(MarkedIn(host.View.FindControl<Control>("Cards")!), run => run.Text == "Audio mixer");

        box.Text = string.Empty;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Empty(MarkedRuns(host));

        host.Close();
    }

    /// <summary>The count and the steppers mean nothing on a page that filters, so they go.</summary>
    [AvaloniaFact]
    public void TheCountAndTheSteppersAreHiddenOnTheSettingsPage()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var box = (TextBox)host.Panel.FindControl<Control>("SearchInput")!;
        box.Text = "push-to-talk";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        foreach (var name in new[] { "SearchCount", "SearchNext", "SearchPrevious" })
        {
            Assert.False(host.Panel.FindControl<Control>(name)!.IsVisible);
        }

        host.Close();
    }

    private static int Cards(SettingsHost host) =>
        ((StackPanel)host.View.FindControl<Control>("Cards")!).Children.Count(card => card.IsVisible);

    private static int Nav(SettingsHost host) =>
        ((StackPanel)host.View.FindControl<Control>("NavItems")!).Children.Count(item => item.IsVisible);

    private static List<string> VisibleRowLabels(SettingsHost host) =>
        [.. host.View.GetVisualDescendants().OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass) && grid.IsEffectivelyVisible)
            .Select(grid => grid.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault())
            .Where(label => label is not null)
            .Select(label => Words(label!))
            .Where(label => !string.IsNullOrEmpty(label))];

    /// <summary>
    /// What a caption says, however it is put together. A block with a search hit in it is built
    /// out of runs and reports no <see cref="TextBlock.Text"/> of its own, so reading that
    /// property alone finds every row except the ones the query matched.
    /// </summary>
    private static string Words(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;

    private static int Rows(SettingsHost host) => VisibleRowLabels(host).Count;
}
