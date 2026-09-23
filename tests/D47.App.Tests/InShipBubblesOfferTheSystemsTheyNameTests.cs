using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The In Ship reading offers the systems a turn names: a chip strip under each bubble, found by
/// <see cref="SystemNameFinder"/> over the known set <see cref="EnableSystemNames"/> hands it (#159).
/// </summary>
public class InShipBubblesOfferTheSystemsTheyNameTests
{
    private static PanelView Laid(PanelView panel)
    {
        var window = new Window { Width = 900, Height = 560, Content = panel };

        window.Show();

        var bounds = new Rect(0, 0, 900, 560);
        window.Measure(bounds.Size);
        window.Arrange(bounds);
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    /// <summary>The copy chips this reading drew, in first-appearance order — the toolbar's own "copy the
    /// whole page" button lives outside <c>Bubbles</c> and is never among them.</summary>
    private static IReadOnlyList<Button> Chips(PanelView panel) =>
        [.. panel.GetControl<StackPanel>("Bubbles").GetVisualDescendants().OfType<Button>()];

    private static (PanelView Panel, RecordingClipboard Clipboard) Open(string said, SystemsInPlay? known)
    {
        var model = new PanelViewModel();

        model.Append(said);

        var clipboard = new RecordingClipboard();
        var panel = new PanelView { DataContext = model };

        panel.EnableCopy(clipboard);

        if (known is not null)
        {
            panel.EnableSystemNames(known);
        }

        Laid(panel);
        return (panel, clipboard);
    }

    private static SystemsInPlay Known(params string[] names)
    {
        var known = new SystemsInPlay();

        known.Add(() => names);
        return known;
    }

    [AvaloniaFact]
    public void TwoNamesInOneTurnDrawTwoChipsInOrderAndEachCopiesItsOwnName()
    {
        var (panel, clipboard) = Open(
            "Plot to Dryafea PO-X d2-0 then Deciat",
            Known("Deciat"));

        var chips = Chips(panel);

        Assert.Equal(
            ["Copy Dryafea PO-X d2-0", "Copy Deciat"],
            chips.Select(AutomationProperties.GetName));

        foreach (var chip in chips)
        {
            chip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Dryafea PO-X d2-0", "Deciat"], clipboard.Written);
    }

    [AvaloniaFact]
    public void ATurnNamingNoSystemDrawsNoStrip()
    {
        var (panel, _) = Open("Hope this helps.", Known("Deciat"));

        Assert.Empty(Chips(panel));
    }

    [AvaloniaFact]
    public void ATurnNamingOneSystemTwiceDrawsOneChip()
    {
        var (panel, _) = Open("Deciat is close, Deciat is home.", Known("Deciat"));

        Assert.Equal(["Copy Deciat"], Chips(panel).Select(AutomationProperties.GetName));
    }

    /// <summary>A name absent from the shipped table, offered only through the known set.</summary>
    [AvaloniaFact]
    public void ANameOnlyTheKnownSetHoldsStillDrawsAChip()
    {
        var (panel, _) = Open("Selling this at Farport Reach.", Known("Farport Reach"));

        Assert.Equal(["Copy Farport Reach"], Chips(panel).Select(AutomationProperties.GetName));
    }

    /// <summary>The Commander's current system is named in cyan; any other system in A.</summary>
    [AvaloniaFact]
    public void TheCurrentSystemsChipIsCyanAndAnyOtherIsA()
    {
        new D47.App.Theming.ThemeManager(
            Application.Current!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance).Apply(themeId: null);

        var model = new PanelViewModel();

        model.Append("Plot from Giryak to Deciat.");

        var panel = new PanelView { DataContext = model };

        panel.EnableCopy(new RecordingClipboard());
        panel.EnableSystemNames(Known("Giryak", "Deciat"), () => "Giryak");
        Laid(panel);

        var labels = panel.GetControl<StackPanel>("Bubbles")
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(label => label.Text is "GIRYAK" or "DECIAT")
            .ToDictionary(label => label.Text!, label => ((Avalonia.Media.ISolidColorBrush)label.Foreground!).Color);

        Assert.Equal(Resource(panel, D47.App.Theming.ThemeManager.CyanKey), labels["GIRYAK"]);
        Assert.Equal(Resource(panel, D47.App.Theming.ThemeManager.AKey), labels["DECIAT"]);
    }

    private static Avalonia.Media.Color Resource(Control near, string key) =>
        ((Avalonia.Media.ISolidColorBrush)near.FindResource(key)!).Color;

    [AvaloniaFact]
    public void ASurfaceWithNoEnableSystemNamesDrawsNoStrip()
    {
        var (panel, _) = Open("Made it to Deciat.", known: null);

        Assert.Empty(Chips(panel));
    }
}
