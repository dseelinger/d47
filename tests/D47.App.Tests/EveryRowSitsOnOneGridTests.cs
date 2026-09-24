using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every settings row has the same columns, so labels, controls and reset gutters each sit at one x
/// down a page (#437).
/// </summary>
public sealed class EveryRowSitsOnOneGridTests
{
    private const double Rounding = 1.0;

    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    [AvaloniaTheory]
    [InlineData("voice-input")]
    [InlineData("voice")]
    [InlineData("sounds")]
    public void LabelsControlsAndResetGuttersEachShareOneX(string placeId)
    {
        var host = OpenOn(placeId, 1400);

        var rows = Rows(host);
        Assert.NotEmpty(rows);

        var labels = new List<double>();
        var controls = new List<double>();
        var resets = new List<double>();

        foreach (var row in rows)
        {
            var origin = X(row, host.View);

            var caption = (Control)row.Children.Single(child => Grid.GetColumn(child) == 0);
            var label = caption.GetVisualDescendants().OfType<TextBlock>().First();
            labels.Add(X(label, host.View) - caption.Margin.Left);

            var control = row.Children.Single(child => Grid.GetColumn(child) == 2);
            controls.Add(X(control, host.View));

            resets.Add(origin + row.ColumnDefinitions.Take(4).Sum(column => column.ActualWidth));
        }

        AssertOneX("label", labels);
        AssertOneX("control", controls);
        AssertOneX("reset gutter", resets);

        host.Close();
    }

    /// <summary>A protected row draws its bar in A; every other row's bar is transparent.</summary>
    [AvaloniaFact]
    public void OnlyAProtectedRowDrawsItsBar()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        var host = Show(SettingsHost.Open(settings, viewState, paths, width: 1400), "voice-input");

        var protectedLabels = settings.RowsForPlace("voice-input")
            .Where(row => row.Protected)
            .Select(row => row.Label)
            .ToHashSet();

        Assert.NotEmpty(protectedLabels);

        foreach (var row in Rows(host))
        {
            var bar = Bar(row);
            var label = row.GetVisualDescendants().OfType<TextBlock>().First().Text;
            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(bar.BorderBrush);

            Assert.Equal(3, bar.BorderThickness.Left);
            Assert.True(
                protectedLabels.Contains(label!) == (brush.Color.A != 0),
                $"'{label}' has a bar alpha of {brush.Color.A}");
        }

        host.Close();
    }

    [AvaloniaFact]
    public void OnAWidePageTheRowsStopAtNineHundred()
    {
        var host = OpenOn("voice-input", 1400);

        foreach (var row in Rows(host))
        {
            Assert.Equal(SettingsView.RowsMaxWidth, Bar(row).Bounds.Width, 1);
        }

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData("voice-input")]
    [InlineData("voice")]
    [InlineData("sounds")]
    public void AtNineHundredTwentyFourEveryRowFitsWithoutAHorizontalScroll(string placeId)
    {
        var host = OpenOn(placeId, 924);

        var scroller = host.View.FindControl<ScrollViewer>("Scroller")!;
        Assert.True(
            scroller.Extent.Width <= scroller.Viewport.Width + Rounding,
            $"the page is {scroller.Extent.Width:0} wide in a {scroller.Viewport.Width:0} viewport");

        foreach (var row in Rows(host))
        {
            var controlColumnEnd = row.ColumnDefinitions.Take(3).Sum(column => column.ActualWidth);

            foreach (var child in row.Children.Where(child => child.IsVisible && Grid.GetColumn(child) == 2))
            {
                var right = child.TranslatePoint(new Point(child.Bounds.Width, 0), row)!.Value.X;

                Assert.True(
                    right <= controlColumnEnd + Rounding,
                    $"a control ends at {right:0}, past its column's end at {controlColumnEnd:0}");
            }
        }

        host.Close();
    }

    private static SettingsHost OpenOn(string placeId, double width)
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        return Show(SettingsHost.Open(settings, viewState, paths, width: width), placeId);
    }

    private static SettingsHost Show(SettingsHost host, string placeId)
    {
        var index = SettingsLayout.Areas.SelectMany(area => area.Places).ToList()
            .FindIndex(place => place.Id == placeId);

        host.View.ShowPlace(index);
        Jobs();
        Jobs();

        return host;
    }

    private static List<Grid> Rows(SettingsHost host) =>
        host.View.GetVisualDescendants()
            .OfType<Grid>()
            .Where(grid => grid.Classes.Contains(SettingsView.CompactRowClass)
                && grid.IsEffectivelyVisible
                && grid.Bounds.Width > 0)
            .ToList();

    private static Border Bar(Grid row) => (Border)((Border)row.Parent!).Parent!;

    private static double X(Visual visual, Visual relativeTo) =>
        visual.TranslatePoint(default, relativeTo)!.Value.X;

    private static void AssertOneX(string what, List<double> xs)
    {
        var spread = xs.Max() - xs.Min();

        Assert.True(
            spread <= Rounding,
            $"every {what} should share one x, but they run from {xs.Min():0.#} to {xs.Max():0.#}");
    }
}
