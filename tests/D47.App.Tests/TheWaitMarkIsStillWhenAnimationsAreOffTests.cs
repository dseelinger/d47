using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>With Windows' animation effects off the wait mark is drawn still: five Line cells, none lit.</summary>
public class TheWaitMarkIsStillWhenAnimationsAreOffTests
{
    private static object? Ink(string key) =>
        Application.Current!.TryFindResource(key, out var brush) ? brush : null;

    private static List<IBrush?> Cells(WaitingState mark) =>
        [.. mark.GetLogicalDescendants().OfType<Border>().Select(cell => cell.Background)];

    [AvaloniaFact]
    public void AShownMarkDrawsFiveLineCellsAndNeverMoves()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var mark = new WaitingState(animated: () => false) { IsVisible = false };
        var window = new Window { Content = new StackPanel { Children = { mark } }, Width = 200, Height = 100 };
        window.Show();

        mark.IsVisible = true;

        for (var beat = 0; beat < WaitingState.Cells * WaitingState.BeatsPerStep * 2; beat++)
        {
            Assert.False(mark.Beat());
        }

        var cells = Cells(mark);

        Assert.Equal(WaitingState.Cells, cells.Count);
        Assert.All(cells, cell => Assert.Same(Ink(ThemeManager.LineKey), cell));

        window.Close();
    }

    [AvaloniaFact]
    public void TheSettingIsReadEachTimeTheMarkIsShown()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var animated = false;
        var mark = new WaitingState(animated: () => animated) { IsVisible = false };
        var window = new Window { Content = new StackPanel { Children = { mark } }, Width = 200, Height = 100 };
        window.Show();

        mark.IsVisible = true;

        Assert.False(mark.Beat());
        Assert.False(mark.Beat());

        mark.IsVisible = false;
        animated = true;
        mark.IsVisible = true;

        Assert.False(mark.Beat());
        Assert.True(mark.Beat());
        Assert.Single(Cells(mark), cell => ReferenceEquals(cell, Ink(ThemeManager.AKey)));

        window.Close();
    }
}
