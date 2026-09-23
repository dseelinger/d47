using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The turn line's Details button is a quiet <c>SPEND</c> (#360).</summary>
public class SpendIsAQuietWordTests
{
    private static Button Details(PanelView view) =>
        view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "TurnDetails");

    private static PanelView Shown()
    {
        var view = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = view, Width = 900, Height = 700 };

        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return view;
    }

    [AvaloniaFact]
    public void TheButtonSaysSpend()
    {
        var button = Details(Shown());

        Assert.Equal("SPEND", button.Content);
    }

    [AvaloniaFact]
    public void TheSentenceIsOnTheTooltipAndTheAccessibleName()
    {
        var button = Details(Shown());
        const string Says = "Tokens, cost, and what this has come to over time";

        Assert.Equal(Says, ToolTip.GetTip(button));
        Assert.Equal(Says, AutomationProperties.GetName(button));
    }
}
