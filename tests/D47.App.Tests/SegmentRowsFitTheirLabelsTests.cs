using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>A settings <see cref="Segment"/> row grows to fit its buttons rather than clipping them (#408).</summary>
public sealed class SegmentRowsFitTheirLabelsTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static SettingsHost Open()
    {
        var (service, viewState, paths) = TestSurface.Create();
        return SettingsHost.Open(service, viewState, paths);
    }

    private static int AreaIndex(string title) =>
        SettingsLayout.Areas.ToList().FindIndex(area => area.Title == title);

    private static void AssertFitsInside(Segment segment)
    {
        var bounds = segment.Bounds;

        foreach (var button in segment.GetVisualDescendants().OfType<RadioButton>())
        {
            Assert.True(button.Bounds.Height > 0);

            var bottomRight = button.TranslatePoint(
                new Point(button.Bounds.Width, button.Bounds.Height), segment) ?? default;

            Assert.True(bottomRight.Y <= bounds.Height + 0.5);
        }
    }

    [AvaloniaFact]
    public void TheThemeRowsButtonsEachLieInsideItsSegmentAndNameATheme()
    {
        var host = Open();

        host.View.SelectArea(AreaIndex("Screens"));
        Jobs();

        var segment = host.View.GetVisualDescendants().OfType<Segment>()
            .Single(s => s.ItemsSource.SequenceEqual(ThemeCatalog.All.Select(t => t.Name)));

        AssertFitsInside(segment);

        var buttons = segment.GetVisualDescendants().OfType<RadioButton>().ToList();
        Assert.Equal(ThemeCatalog.All.Count, buttons.Count);
        Assert.Equal(
            ThemeCatalog.All.Select(t => t.Name),
            buttons.Select(button => (string?)button.Content));

        host.Close();
    }

    /// <summary>Every choice row on the page, so a future fixed height on any of them fails the build.</summary>
    [AvaloniaFact]
    public void EverySegmentOnEveryAreaFitsItsButtons()
    {
        var host = Open();

        for (var i = 0; i < SettingsLayout.Areas.Count; i++)
        {
            host.View.SelectArea(i);
            Jobs();

            foreach (var segment in host.View.GetVisualDescendants().OfType<Segment>())
            {
                AssertFitsInside(segment);
            }
        }

        host.Close();
    }
}
