using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Once Elite changes the route in-game, the panels draw its next system and jumps left, not the file's hops (#2063).</summary>
public class TheRoutePanelsShowAChangedRouteFromItsTargetTests
{
    private static readonly RouteTarget Target = new("Wolf 359", "M", 3);

    private static NavRoute PlottedFile() => new()
    {
        Hops = [.. new[] { "Sol", "Alpha Centauri", "Barnard's Star", "Colonia" }.Select((name, index) => new RouteHop(name, "G") { Position = (index * 10.0, 0, 0) })],
    };

    private static NavRoute Changed() => NavRoute.None with { LiveTarget = Target };

    private static Window Show(Control content)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);
        Application.Current!.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml") });

        var window = new Window { Content = content, Width = 900, Height = 500, Background = (IBrush?)Application.Current.Resources[ThemeManager.BgKey] };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static string Text(Control root) => string.Join(
        "|",
        root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.Select(i => (i as Avalonia.Controls.Documents.Run)?.Text)) : block.Text));

    [AvaloniaFact]
    public void TheProgressPageNamesTheTargetAndTheJumpsLeftAndNoneOfTheFile()
    {
        var page = new RouteProgressPage(Changed, () => "Sol");
        var window = Show(page);
        var text = Text(page);

        Assert.Contains("Wolf 359", text);
        Assert.Contains("3", text);
        Assert.Contains("jumps left", text);
        Assert.DoesNotContain("Colonia", text);
        Assert.DoesNotContain("Barnard", text);
        Assert.DoesNotContain("systems on the route", text);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-progress-changed.png");
    }

    [AvaloniaFact]
    public void TheProgressPageStillDrawsTheFileWhenTheRouteIsNotChanged()
    {
        var page = new RouteProgressPage(PlottedFile, () => "Sol");
        var window = Show(page);
        var text = Text(page);

        Assert.Contains("Colonia", text);
        Assert.Contains("systems on the route", text);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-progress-file.png");
    }

    [AvaloniaFact]
    public void TheGalaxyMiniSaysTheNextJumpInsteadOfDrawingTheFile()
    {
        var mini = new GalaxyMapMini(() => null, Changed, () => "Sol", null, () => null);
        var window = Show(mini);

        Assert.Equal(Target.NextJump, mini.Note);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("galaxy-mini-changed.png");
    }

    [AvaloniaFact]
    public void TheRouteMiniSaysTheNextJumpWhenNoPlanIsDrawn()
    {
        var mini = new RouteMini(new RoutePlanBook("route-plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance), Changed, () => "Sol", null, () => null);
        var window = Show(mini);

        Assert.Contains(Target.NextJump, Text(mini));

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("route-mini-changed.png");
    }
}
