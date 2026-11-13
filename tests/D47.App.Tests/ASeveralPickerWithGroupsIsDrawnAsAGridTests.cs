using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>A several-choice request that carries groups is laid out as a grid of cells by group.</summary>
public class ASeveralPickerWithGroupsIsDrawnAsAGridTests
{
    private static readonly string[] Nine = ["a1", "a2", "a3", "a4", "b1", "b2", "c1", "c2", "c3"];

    private static PickerRequest Request(IReadOnlyList<string> choices, Func<string, string?> groupOf, params string[] ticked) => new()
    {
        Prompt = "Layers",
        Help = "Choose the layers to draw.",
        Choices = choices,
        Several = true,
        CurrentSeveral = ticked,
        GroupOf = groupOf,
        SwatchOf = _ => (context, ink) => context.DrawRectangle(ink("D47.A"), null, new Rect(2, 2, 12, 10)),
        DefaultDisplay = "DEFAULT LAYERS",
    };

    private static string GroupOfNine(string value) => value[..1] switch { "a" => "Stars", "b" => "Bodies", _ => "Places" };

    private static (PickerPage Page, List<PickerResult> Chosen) Open(PickerRequest request, double width, double height, out Window window)
    {
        var chosen = new List<PickerResult>();
        var page = PickerPage.For(request, chosen.Add);

        window = new Window { Width = width, Height = height, Content = page };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (page, chosen);
    }

    [AvaloniaFact]
    public void NineValuesInThreeGroupsDrawThreeNamesAndNineBoxes()
    {
        var (page, chosen, window) = Open(Nine);

        var names = page.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray();
        Assert.Contains("STARS", names);
        Assert.Contains("BODIES", names);
        Assert.Contains("PLACES", names);

        var boxes = page.GetVisualDescendants().OfType<CheckBox>().ToArray();
        Assert.Equal(9, boxes.Length);
        Assert.Equal(["a2", "b1", "c3"], Nine.Where(value => boxes[Array.IndexOf(Nine, value)].IsChecked == true));
        Assert.Equal(9, page.GetVisualDescendants().OfType<Glyph>().Count());
        Assert.Equal("Use these · 3", page.GetControl<Button>("AcceptButton").Content);

        page.GetControl<Button>("AcceptButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["a2", "b1", "c3"], Assert.Single(chosen).Values);
        window.Close();
    }

    [AvaloniaFact]
    public void TickingACellChangesWhatIsAcceptedInChoicesOrder()
    {
        var (page, chosen, window) = Open(Nine);

        page.GetVisualDescendants().OfType<CheckBox>().First().IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        page.GetControl<Button>("AcceptButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(["a1", "a2", "b1", "c3"], Assert.Single(chosen).Values);
        window.Close();
    }

    [AvaloniaFact]
    public void AGridShowsNoFilterSortOrCount()
    {
        var (page, _, window) = Open(Nine);

        Assert.False(page.GetControl<Control>("FilterLine").IsVisible);
        Assert.False(page.GetControl<ListBox>("Choices").IsVisible);
        Assert.True(page.GetControl<Control>("GroupGrid").IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCellsOfABlockAreFourAcrossAndFortyFourTall()
    {
        var (page, _, window) = Open(Nine);

        var boxes = page.GetVisualDescendants().OfType<CheckBox>().ToArray();
        var tops = boxes.Select(box => box.TranslatePoint(default, page)!.Value).ToArray();

        Assert.All(boxes, box => Assert.Equal(44, box.Bounds.Height, 0.5));
        Assert.Equal(tops[0].Y, tops[3].Y, 0.5);
        Assert.True(tops[4].Y > tops[3].Y, "the second group does not start under the first.");
        Assert.Equal(boxes[1].Bounds.Width, boxes[0].Bounds.Width, 0.5);
        Assert.Equal(boxes[0].Bounds.Width + 2, tops[1].X - tops[0].X, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void TwentyOneValuesInFourGroupsFitTheBigPanelWithoutScrolling()
    {
        var sizes = new[] { 8, 5, 2, 6 };
        var choices = sizes.SelectMany((size, group) => Enumerable.Range(1, size).Select(n => $"g{group}-{n}")).ToArray();
        var (page, _) = Open(Request(choices, value => $"Group {value[1]}"), 1280, 435, out var window);

        var scroll = page.GetControl<ScrollViewer>("GroupGrid");

        Assert.Equal(choices.Length, page.GetVisualDescendants().OfType<CheckBox>().Count());
        Assert.True(scroll.Extent.Height <= scroll.Viewport.Height + 0.5, $"the grid is {scroll.Extent.Height}px in a {scroll.Viewport.Height}px view.");
        window.Close();
    }

    [AvaloniaFact]
    public void ARequestWithoutGroupsIsStillAList()
    {
        var request = Request(Nine, _ => null, "a1") with { GroupOf = null };
        var (page, _, window) = Open(request);

        Assert.True(page.GetControl<ListBox>("Choices").IsVisible);
        Assert.False(page.GetControl<Control>("GroupGrid").IsVisible);
        window.Close();
    }

    private static (PickerPage Page, List<PickerResult> Chosen, Window Window) Open(string[] choices) => Open(choices, GroupOfNine);

    private static (PickerPage Page, List<PickerResult> Chosen, Window Window) Open(PickerRequest request)
    {
        var (page, chosen) = Open(request, 1280, 600, out var window);

        return (page, chosen, window);
    }

    private static (PickerPage Page, List<PickerResult> Chosen, Window Window) Open(string[] choices, Func<string, string?> groupOf)
    {
        var (page, chosen) = Open(Request(choices, groupOf, "a2", "b1", "c3"), 1280, 600, out var window);

        return (page, chosen, window);
    }
}
