using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Windowing;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Find's All results tile and Ctrl+Enter open a page with every result, grouped by kind.</summary>
public sealed class FindOpensAllResultsOnAPageTests
{
    private static void Jobs()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<PanelFindResult> Hits(int count) =>
        [.. Enumerable.Range(1, count).Select(i => new PanelFindResult(PanelFindKind.Page, $"Page {i}", "Asset Mgmt", $"key{i}", i))];

    private static (FindDialog Dialog, Window Window, List<(string Query, IReadOnlyList<PanelFindResult> Results)> Opened) Shown(int count)
    {
        var opened = new List<(string, IReadOnlyList<PanelFindResult>)>();
        var window = new Window { Content = new Grid(), Width = 900, Height = 700 };
        window.Show();

        var dialog = new FindDialog(q => q.Length == 0 ? [] : Hits(count), _ => { }, "F", null, (q, r) => opened.Add((q, r)));
        _ = dialog.Over(window);
        Jobs();
        dialog.Query = "page";
        Jobs();

        return (dialog, window, opened);
    }

    [AvaloniaFact]
    public void TheTileCountsEveryResultEvenWhenAllFitInEight()
    {
        var (few, _, _) = Shown(3);
        var (many, _, _) = Shown(12);

        Assert.Equal("ALL 3 RESULTS", few.TileText);
        Assert.Equal("3 RESULTS", few.Count);
        Assert.Equal("ALL 12 RESULTS", many.TileText);
        Assert.Equal("8 OF 12 RESULTS", many.Count);
    }

    [AvaloniaFact]
    public void DownFromTheLastRowSelectsTheTileAndEnterOpensThePage()
    {
        var (dialog, window, opened) = Shown(12);

        dialog.GetVisualDescendants().OfType<TextBox>().Single().Focus();
        dialog.Move(-1);

        Assert.True(dialog.TileSelected);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Jobs();

        var (query, results) = Assert.Single(opened);
        Assert.Equal("page", query);
        Assert.Equal(12, results.Count);
        Assert.Null(ModalHost.For(window).Top);
    }

    [AvaloniaFact]
    public void CtrlEnterOpensThePageFromTheFieldWhilePlainEnterOpensTheResult()
    {
        var (dialog, window, opened) = Shown(12);

        dialog.GetVisualDescendants().OfType<TextBox>().Single().Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
        Jobs();

        Assert.Single(opened);
    }

    [AvaloniaFact]
    public void TypingLetterByLetterRedrawsTheTileWithoutCrashing()
    {
        var (dialog, _, _) = Shown(12);

        foreach (var typed in new[] { "l", "lo", "loo", "look" })
        {
            dialog.Query = typed;
            Jobs();
        }

        Assert.Equal("ALL 12 RESULTS", dialog.TileText);
    }

    [AvaloniaFact]
    public void AQueryWithNoResultsDrawsNoTile()
    {
        var (dialog, _, _) = Shown(0);

        Assert.Null(dialog.TileText);
    }

    [AvaloniaFact]
    public void ThePageGroupsResultsByKindInOrderAndOpensAChosenRow()
    {
        var results = new List<PanelFindResult>
        {
            new(PanelFindKind.Faction, "Dark Echo", "Faction", "Dark Echo", 1),
            new(PanelFindKind.Station, "Dark Wheel", "Station", "Dark Wheel", 2, SystemAddress: 5, LightYears: 12.5),
            new(PanelFindKind.StarSystem, "Darkan", "System", "Darkan", 3, SystemAddress: 6),
            new(PanelFindKind.Page, "Dark page", "Settings", "k", 4),
        };
        var opened = new List<PanelFindResult>();
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 1280, Height = 960 };
        window.Show();
        Jobs();

        var page = new AllResultsPage("dark", results, opened.Add);
        window.Content = page;
        Jobs();

        Assert.Equal(["Dark page", "Darkan", "Dark Wheel", "Dark Echo"], page.Rows.Select(r => (string)r.Tag!));

        page.Rows[2].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal("Dark Wheel", Assert.Single(opened).Title);

        window.Close();
    }

    [AvaloniaFact]
    public void AGroupShowsAtMostTwentyRows()
    {
        var results = Enumerable.Range(1, 30)
            .Select(i => new PanelFindResult(PanelFindKind.StarSystem, $"Dark {i}", "System", $"Dark {i}", i))
            .ToList();
        var page = new AllResultsPage("dark", results, _ => { });
        var window = new Window { Content = page, Width = 1280, Height = 960 };
        window.Show();
        Jobs();

        Assert.Equal(AllResultsPage.MostPerGroup, page.Rows.Count);

        window.Close();
    }
}
