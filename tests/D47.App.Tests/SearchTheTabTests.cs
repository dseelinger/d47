using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
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
            .Where(grid => grid.ColumnDefinitions.Count == 3 && grid.IsEffectivelyVisible)
            .Select(grid => grid.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text)
            .Where(label => !string.IsNullOrEmpty(label))
            .Select(label => label!)];

    private static int Rows(SettingsHost host) => VisibleRowLabels(host).Count;
}
