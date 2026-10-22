using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Windowing;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Find box lists at most eight results, moves a selection, opens the chosen one and closes (#1474).</summary>
public sealed class FindOpensAndChoosesTests
{
    private static void Jobs()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<PanelFindResult> Hits(string query) =>
        query.Length == 0
            ? []
            : [.. Enumerable.Range(1, 12).Select(i => new PanelFindResult(PanelFindKind.Page, $"{query} {i}", "Asset Mgmt", $"key{i}", i))];

    private static (FindDialog Dialog, Window Window, List<PanelFindResult> Opened) Shown()
    {
        var opened = new List<PanelFindResult>();
        var window = new Window { Content = new Grid(), Width = 900, Height = 700 };
        window.Show();

        var dialog = new FindDialog(Hits, opened.Add, "Ctrl+K");
        _ = dialog.Over(window);
        Jobs();

        return (dialog, window, opened);
    }

    [AvaloniaFact]
    public void AnEmptyQueryShowsNoResults()
    {
        var (dialog, _, _) = Shown();

        Assert.Empty(dialog.Shown);
    }

    [AvaloniaFact]
    public void ResultsStopAtEightAndTheFirstIsSelected()
    {
        var (dialog, _, _) = Shown();

        dialog.Query = "ships";
        Jobs();

        Assert.Equal(FindDialog.MostResults, dialog.Shown.Count);
        Assert.Equal(0, dialog.SelectedIndex);
    }

    [AvaloniaFact]
    public void DownMovesTheSelectionAndEnterOpensItAndClosesTheBox()
    {
        var (dialog, window, opened) = Shown();

        dialog.Query = "ships";
        var field = dialog.GetVisualDescendants().OfType<TextBox>().Single();
        field.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Jobs();

        Assert.Equal("key2", Assert.Single(opened).Target);
        Assert.Null(ModalHost.For(window).Top);
    }

    [AvaloniaFact]
    public void EscapeClosesTheBoxAndOpensNothing()
    {
        var (dialog, window, opened) = Shown();

        dialog.Query = "ships";
        dialog.GetVisualDescendants().OfType<TextBox>().Single().Focus();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Jobs();

        Assert.Empty(opened);
        Assert.Null(ModalHost.For(window).Top);
    }

    [AvaloniaFact]
    public void ANoMatchSaysSoAndNamesTheKeyWhenTheQueryIsEmpty()
    {
        var (dialog, _, _) = Shown();
        var hint = dialog.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == FindDialog.HintName);

        Assert.False(hint.IsVisible);

        dialog.Query = string.Empty;
        Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "CTRL+K");
    }

    [AvaloniaFact]
    public void TheBoxIsDrawnBelowTheTopNotCentred()
    {
        var (_, window, _) = Shown();
        var frame = ModalHost.For(window).GetVisualDescendants().OfType<Border>().Single(b => b.Name == ModalHost.FrameName);

        Assert.Equal(Avalonia.Layout.VerticalAlignment.Top, frame.VerticalAlignment);
        Assert.Equal(Modal.TopOffset - Modal.ScrimPadding, frame.Margin.Top);
    }
}
