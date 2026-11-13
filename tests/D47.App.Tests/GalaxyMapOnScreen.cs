using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Tests;

/// <summary>The galaxy map pages' controls and words, found through the visual tree as a Commander sees them.</summary>
internal static class GalaxyMapOnScreen
{
    public static GalaxyMapView Map(this Control owner) => owner.GetVisualDescendants().OfType<GalaxyMapView>().Single();

    public static Segment Framing(this GalaxyMapPage page) => Offering(page, "Galaxy");

    public static Segment Range(this GalaxyMapPage page) => Offering(page, "30 days");

    /// <summary>The layer switch named <paramref name="name"/> on screen.</summary>
    public static CheckBox Switch(this Control page, string name) =>
        page.GetVisualDescendants().OfType<CheckBox>().Single(box => AutomationProperties.GetName(box) == name || (box.Content as string) == name);

    /// <summary>Where <paramref name="at"/> is on the map at its viewport and size.</summary>
    public static Point Projected(this GalaxyMapView map, StarPosition at)
    {
        var (x, y) = map.Viewport.ToScreen(at, map.Bounds.Width, map.Bounds.Height);

        return new Point(x, y);
    }

    public static RadioButton Option(this Segment segment, string label) =>
        segment.GetVisualDescendants().OfType<RadioButton>().Single(radio => (radio.Content as string) == label);

    /// <summary>Presses and releases the left button at <paramref name="at"/> on the map in its window, and returns what is then selected.</summary>
    public static GalaxyMapStar? TapAt(this GalaxyMapView map, Point at)
    {
        var window = (Window)TopLevel.GetTopLevel(map)!;
        var inWindow = map.TranslatePoint(at, window)!.Value;

        window.MouseDown(inWindow, MouseButton.Left);
        window.MouseUp(inWindow, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        return map.Selected;
    }

    /// <summary>The name of what a tap on <paramref name="at"/> selects, or null; leaves nothing selected. A point off the map selects nothing.</summary>
    public static string? SelectsAt(this GalaxyMapView map, StarPosition at)
    {
        var point = map.Projected(at);

        if (!new Rect(map.Bounds.Size).Contains(point))
        {
            return null;
        }

        var picked = map.TapAt(point)?.Name;
        map.Clear();
        Dispatcher.UIThread.RunJobs();

        return picked;
    }

    /// <summary>The legend's entries as drawn, in order, each entry's words joined by a space, or empty while it has none.</summary>
    public static IReadOnlyList<string> LegendEntries(this GalaxyMapPage page) =>
        page.GetVisualDescendants().OfType<WrapPanel>()
            .FirstOrDefault(panel => panel.Children.OfType<StackPanel>().Any(entry => entry.Children.OfType<D47.App.Controls.Glyph>().Any())) is { } legend
            ? [.. legend.Children.OfType<StackPanel>().Select(entry => string.Join(" ", entry.Children.OfType<TextBlock>().Select(Words)))]
            : [];

    /// <summary>The words of each text block shown under <paramref name="owner"/>, in tree order, leaving out button labels.</summary>
    public static IReadOnlyList<string> Lines(this Control owner) =>
        [.. owner.GetVisualDescendants().OfType<TextBlock>().Where(block => Shown(block, owner)).Select(Words)];

    /// <summary>The words shown in place of the mini panel's map, or empty while the map is drawn.</summary>
    public static string Note(this GalaxyMapMini mini) => mini.Content is TextBlock note ? Words(note) : string.Empty;

    /// <summary>The strip that names the selected system, found by its Clear selection button.</summary>
    public static Border Strip(this GalaxyMapPage page) =>
        page.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == GalaxyMapPage.ClearSelection)
            .GetVisualAncestors().OfType<Border>().First(border => border.Child is DockPanel);

    public static bool StripShown(this GalaxyMapPage page) => page.Strip().IsVisible;

    public static string StripName(this GalaxyMapPage page) => Words(StripRow(page, 0, 0));

    public static string StripFacts(this GalaxyMapPage page) => Words(StripRow(page, 0, 1));

    /// <summary>The strip's second line, one entry per kind as drawn: "TAG: names".</summary>
    public static IReadOnlyList<string> StripKinds(this GalaxyMapPage page)
    {
        var kinds = (StackPanel)StripWords(page).Children[1];

        return !kinds.IsVisible
            ? []
            : [.. kinds.Children.OfType<StackPanel>().Select(kind => kind.Children.OfType<TextBlock>().ToList()).Select(words => $"{Words(words[0])}: {Words(words[1])}")];
    }

    /// <summary>The reply under the strip's name, or empty while none shows.</summary>
    public static string StripReply(this GalaxyMapPage page)
    {
        var reply = (TextBlock)StripWords(page).Children[2];

        return reply.IsVisible ? Words(reply) : string.Empty;
    }

    /// <summary>The band naming a stored plan drawn over the map, found by its Plotted route button.</summary>
    public static Border PlanBand(this GalaxyMapPage page) => PlottedRoute(page).GetVisualAncestors().OfType<Border>().First(border => border.Child is StackPanel);

    public static Button PlottedRoute(this GalaxyMapPage page) =>
        page.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Plotted route");

    /// <summary>The band's words, as "KIND PLAN: Headline", or empty while no stored plan is drawn.</summary>
    public static string PlanName(this GalaxyMapPage page)
    {
        var band = page.PlanBand();

        return band.IsVisible ? string.Join(" ", ((StackPanel)band.Child!).Children.OfType<TextBlock>().Select(Words)) : string.Empty;
    }

    /// <summary>The kind of stored plan the band names, or null while the plotted route is drawn.</summary>
    public static RoutePlanKind? PlanShown(this GalaxyMapPage page)
    {
        var band = page.PlanBand();

        if (!band.IsVisible)
        {
            return null;
        }

        var said = Words(((StackPanel)band.Child!).Children.OfType<TextBlock>().First());

        return Enum.GetValues<RoutePlanKind>().Single(kind => $"{GalaxyMapPage.PlanKindWord(kind)} plan:".ToUpperInvariant() == said);
    }

    public static string Words(TextBlock block) => block.Inlines is { Count: > 0 } inlines
        ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
        : block.Text ?? string.Empty;

    private static Segment Offering(Control page, string option) =>
        page.GetVisualDescendants().OfType<Segment>().Single(segment => segment.ItemsSource?.Cast<object>().Contains(option) == true);

    private static bool Shown(TextBlock block, Control owner) =>
        block.GetSelfAndVisualAncestors().TakeWhile(visual => visual != owner).All(visual => visual.IsVisible && visual is not Button);

    /// <summary>The strip's words: a row of name and facts, the row of kinds, then the reply.</summary>
    private static StackPanel StripWords(GalaxyMapPage page) =>
        page.Strip().GetVisualDescendants().OfType<StackPanel>().First(panel => panel.Children.Count == 3 && panel.Children[0] is StackPanel && panel.Children[2] is TextBlock);

    private static TextBlock StripRow(GalaxyMapPage page, int row, int column) =>
        (TextBlock)((StackPanel)StripWords(page).Children[row]).Children[column];
}
