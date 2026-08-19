using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Pictures of the reworked chrome, for looking at rather than for asserting on
/// (remediation.md 10, items 1 to 4).
/// <para>
/// The assertions live in <c>TheTabStripFitsAnyWidthTests</c>. This is the other half of checking
/// a layout, and the repo's own convention for it: an overlap, a label hanging low and a glyph
/// off-centre in its highlight are all things a test can be written to miss and an eye cannot.
/// </para>
/// </summary>
public class TheReworkedChromeRendersToACaptureTests
{
    private static (Window Window, PanelView Panel) Open(double width, PanelTab tab = PanelTab.Transcript)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel();

        model.Append("Fixture One, docked. Fuel at 82 percent.\n");
        model.Append("\n> what is the beacon at Shinrarta Dezhra\n");
        model.Append("The beacon is quiet. Nothing on the board worth the detour.");

        var panel = new PanelView { DataContext = model };

        panel.Furnish(PanelTab.Checklist, _ => new TextBlock { Text = "checklist" }, new NavCrumb("checklist", "Checklist"));
        panel.Furnish(
            PanelTab.Loadout,
            crumb => new TextBlock { Text = crumb.Word },
            new NavCrumb("fleet", "Ships"),
            new NavCrumb("locker", "Suits and weapons"));
        panel.Furnish(PanelTab.Engineers, _ => new TextBlock { Text = "engineers" }, new NavCrumb("engineers", "Engineers"));
        panel.Furnish(PanelTab.Utilities, _ => new TextBlock { Text = "utilities" }, new NavCrumb("utilities", "Utilities"));
        panel.EnableSettings(() => new TextBlock { Text = "settings" });
        panel.EnableSearch();

        var window = new Window { Content = panel, Width = width, Height = 620 };

        window.Show();
        panel.Tab = tab;
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, name),
            new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void TheWideStrip()
    {
        var (window, _) = Open(1200);

        Save(window, "chrome-wide.png");
        window.Close();
    }

    /// <summary>The width the overlap was reported at.</summary>
    [AvaloniaFact]
    public void TheNarrowStrip()
    {
        var (window, _) = Open(620);

        Save(window, "chrome-narrow.png");
        window.Close();
    }

    /// <summary>Narrow enough that the steppers are carrying the strip on their own.</summary>
    [AvaloniaFact]
    public void TheVeryNarrowStrip()
    {
        var (window, _) = Open(380);

        Save(window, "chrome-tiny.png");
        window.Close();
    }

    /// <summary>The mode chooser open, which is where the readings live now.</summary>
    [AvaloniaFact]
    public void TheModeChooser()
    {
        var (window, panel) = Open(1200);

        panel.GetControl<Button>("ModeButton")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Save(window, "chrome-modes.png");
        window.Close();
    }

    /// <summary>
    /// The search row with matches in it, which is the state the gap was reported in
    /// (remediation.md 11, item 2).
    /// </summary>
    [AvaloniaFact]
    public void TheSearchRowWithMatches()
    {
        var (window, panel) = Open(1900);

        panel.GetControl<TextBox>("SearchInput").Text = "beacon";
        Dispatcher.UIThread.RunJobs();

        Save(window, "chrome-search.png");
        window.Close();
    }

    /// <summary>A furnished page, where Copy All must not be.</summary>
    [AvaloniaFact]
    public void AFurnishedPage()
    {
        var (window, _) = Open(1200, PanelTab.Checklist);

        Save(window, "chrome-checklist.png");
        window.Close();
    }
}
