using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The protected chip became a bar on the row, and one legend line said once per screen (#333).
/// </summary>
public class AProtectedRowGetsABarAndTheScreenGetsALegendTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    /// <summary>No row says "protected" any more.</summary>
    [AvaloniaFact]
    public void NoRowDrawsAProtectedChip()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.DoesNotContain(
            host.View.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "protected");

        host.Close();
    }

    /// <summary>Push-to-talk is protected, and is distinguishable by a left bar with no chip drawn.</summary>
    [AvaloniaFact]
    public void AProtectedRowCarriesALeftBar()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        var label = host.View.LabelFor(ListeningCapability.PushToTalkKeyKey);
        Assert.NotNull(label);

        var bars = label!.GetVisualAncestors()
            .OfType<Border>()
            .Where(border => border.BorderThickness is { Left: > 0, Top: 0, Right: 0, Bottom: 0 })
            .ToList();

        Assert.NotEmpty(bars);

        host.Close();
    }

    /// <summary>The legend is there exactly on the pages that hold a protected row, and nowhere else.</summary>
    [AvaloniaFact]
    public void TheLegendMatchesWhetherThePageHasAProtectedRow()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);
        var places = SettingsLayout.Areas.SelectMany(area => area.Places).ToList();

        Assert.Contains(places, place => settings.RowsForPlace(place.Id).Any(row => row.Protected));
        Assert.Contains(places, place => !settings.RowsForPlace(place.Id).Any(row => row.Protected));

        for (var i = 0; i < places.Count; i++)
        {
            host.View.ShowPlace(i);
            Jobs();

            // A place with no page on a fresh surface is not one the Commander can open.
            if (host.View.ActiveSection != i)
            {
                continue;
            }

            var expected = settings.RowsForPlace(places[i].Id).Any(row => row.Protected);

            var legend = host.View.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(block => block.Text == SettingsView.ProtectedLegend);

            Assert.True(legend is not null, $"{places[i].Title} drew no legend line at all");
            Assert.Equal(expected, legend!.IsVisible);
        }

        host.Close();
    }
}
