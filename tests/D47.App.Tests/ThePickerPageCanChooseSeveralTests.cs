using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using Xunit;

namespace D47.App.Tests;

/// <summary>A several-at-once picker ticks any number of rows and returns them together.</summary>
public class ThePickerPageCanChooseSeveralTests
{
    private static readonly string[] Services = ["Market", "Refuel", "Repair", "Shipyard", "Outfitting"];

    private static PickerRequest Several(params string[] ticked) => new()
    {
        Prompt = "Services",
        Choices = Services,
        Several = true,
        CurrentSeveral = ticked,
        DefaultDisplay = "ANY SERVICES",
    };

    private static (PickerPage Page, List<PickerResult> Chosen, int[] Cancelled) Open(PickerRequest request)
    {
        var chosen = new List<PickerResult>();
        var cancelled = new int[1];
        var page = PickerPage.For(request, chosen.Add, () => cancelled[0]++);

        return (page, chosen, cancelled);
    }

    private static void Press(PickerPage page, string name) =>
        page.GetControl<Button>(name).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void ThreeOfFiveTickedComeBackInTheOrderOfTheChoices()
    {
        var (page, chosen, _) = Open(Several("Shipyard", "Market", "Repair"));

        Press(page, "AcceptButton");

        Assert.Equal(["Market", "Repair", "Shipyard"], Assert.Single(chosen).Values);
    }

    [AvaloniaFact]
    public void TheButtonCountsWhatIsTicked()
    {
        var (page, _, _) = Open(Several("Market", "Refuel"));

        Assert.Equal("Use these · 2", page.GetControl<Button>("AcceptButton").Content);

        page.Show();
        var boxes = page.GetVisualDescendants().OfType<CheckBox>().ToArray();
        boxes[2].IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Use these · 3", page.GetControl<Button>("AcceptButton").Content);
        page.Close();
    }

    [AvaloniaFact]
    public void FilteringHidesATickedRowButStillReturnsIt()
    {
        var (page, chosen, _) = Open(Several("Market"));

        page.Show();
        page.GetControl<TextBox>("FilterBox").Text = "Refuel";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Refuel"], page.GetControl<ListBox>("Choices").ItemsSource!.Cast<PickerChoice>().Select(c => c.Value));
        Assert.Equal("Use these · 1", page.GetControl<Button>("AcceptButton").Content);

        Press(page, "AcceptButton");

        Assert.Equal(["Market"], Assert.Single(chosen).Values);
        page.Close();
    }

    [AvaloniaFact]
    public void TheDefaultTileReturnsAnEmptySetAndSaysItsOwnWords()
    {
        var (page, chosen, _) = Open(Several("Market"));

        Assert.Equal("ANY SERVICES", page.GetControl<TextBlock>("DefaultButtonText").Text);

        Press(page, "DefaultButton");

        Assert.Empty(Assert.Single(chosen).Values!);
    }

    [AvaloniaFact]
    public void BackReturnsNothingAndCallsNoCallback()
    {
        var (page, chosen, cancelled) = Open(Several("Market"));

        Press(page, "BackButton");

        Assert.Empty(chosen);
        Assert.Equal(1, cancelled[0]);
    }

    [AvaloniaFact]
    public void EveryRowIsAtLeastFortyFourPixelsTall()
    {
        var (page, _, _) = Open(Several());

        page.Show();

        var items = page.GetVisualDescendants().OfType<ListBoxItem>().ToArray();

        Assert.Equal(Services.Length, items.Length);
        Assert.All(items, item => Assert.True(item.Bounds.Height >= 44, $"a row is {item.Bounds.Height}px tall."));
        page.Close();
    }

    [AvaloniaFact]
    public void PressingARowTicksItAndChoosesNothing()
    {
        var (page, chosen, _) = Open(Several());

        page.Show();
        page.GetVisualDescendants().OfType<CheckBox>().First().IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(chosen);
        page.Close();
    }
}
