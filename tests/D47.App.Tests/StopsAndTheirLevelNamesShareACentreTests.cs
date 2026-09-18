using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The per-subsystem level track (#283): every stop's horizontal centre lines up with its level
/// name's, because both are drawn in the same star column of one shared <see cref="Grid"/> rather
/// than positioned with margins.
/// </summary>
public class StopsAndTheirLevelNamesShareACentreTests
{
    [AvaloniaTheory]
    [InlineData(360)]
    [InlineData(900)]
    public void EveryStopsCentreMatchesItsLevelNamesCentre(double width)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        // The strip's own "Settings for this page" disclosure is closed by default; opened here so
        // the track underneath it is actually laid out (#218).
        viewState.Save(viewState.Load().With("log-levels", expanded: true));

        var view = new SettingsView();
        view.Attach(settings, viewState, paths, tabPlaceId: "log-levels");

        var window = new Window { Content = view, Width = width, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var grid = view.GetVisualDescendants()
            .OfType<Grid>()
            .First(candidate => candidate.RowDefinitions.Count > 1 && candidate.ColumnDefinitions.Count == 10);

        var names = grid.Children.OfType<TextBlock>()
            .Where(text => Grid.GetRow(text) == 0)
            .ToDictionary(Grid.GetColumn, text => text);

        var stops = grid.Children.OfType<Button>().Where(button => Grid.GetColumn(button) is >= 1 and <= 7);

        Assert.NotEmpty(stops);

        foreach (var stop in stops)
        {
            var column = Grid.GetColumn(stop);
            var name = names[column];

            var stopCentre = stop.Bounds.X + stop.Bounds.Width / 2;
            var nameCentre = name.Bounds.X + name.Bounds.Width / 2;

            Assert.True(
                Math.Abs(stopCentre - nameCentre) <= 1,
                $"column {column}: stop centre {stopCentre:0.##} against name centre {nameCentre:0.##}");
        }

        window.Close();
    }
}
