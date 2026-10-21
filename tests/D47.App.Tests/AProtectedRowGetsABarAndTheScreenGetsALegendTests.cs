using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A protected row with no voice route gets a bar on the row, and one legend line says so once per screen (#333, #1454).
/// </summary>
[Trait("Category", "Integration")]
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
        using var look = AppLook.Put();
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        ShowPageOf(host, settings, ListeningCapability.PushToTalkKeyKey);

        var label = host.View.LabelFor(ListeningCapability.PushToTalkKeyKey);
        Assert.NotNull(label);

        var bars = label!.GetVisualAncestors()
            .OfType<Border>()
            .Where(IsDrawnBar)
            .ToList();

        Assert.NotEmpty(bars);

        host.Close();
    }

    private static void ShowPageOf(SettingsHost host, SettingsService settings, string key)
    {
        var places = SettingsLayout.Areas.SelectMany(area => area.Places).ToList();

        for (var i = 0; i < places.Count; i++)
        {
            if (!settings.RowsForPlace(places[i].Id).Any(row => row.Key == key))
            {
                continue;
            }

            host.View.ShowPlace(i);
            Jobs();
            return;
        }
    }

    private static bool IsDrawnBar(Border border) =>
        border.BorderThickness is { Left: > 0, Top: 0, Right: 0, Bottom: 0 }
        && border.BorderBrush is not null && border.BorderBrush != Brushes.Transparent;

    private static SettingRow Plain(bool protectedRow) => new()
    {
        Key = "test.row",
        Label = "Test",
        Help = "Test.",
        Kind = SettingKind.Text,
        Protected = protectedRow,
    };

    /// <summary>A protected row with a voice command is reachable by voice, so it carries no bar.</summary>
    [AvaloniaFact]
    public void AProtectedRowWithAVoiceCommandCarriesNoBar()
    {
        using var look = AppLook.Put();
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        var row = SettingsLayout.Areas.SelectMany(area => area.Places).SelectMany(place => settings.RowsForPlace(place.Id)).Single(r => r.Key == PersonaCapability.PersonaKey);
        Assert.True(row.Protected);
        Assert.True(row.HasVoiceRoute);

        ShowPageOf(host, settings, PersonaCapability.PersonaKey);

        var label = host.View.LabelFor(PersonaCapability.PersonaKey);
        Assert.NotNull(label);

        Assert.DoesNotContain(
            label!.GetVisualAncestors().OfType<Border>(),
            IsDrawnBar);

        host.Close();
    }

    /// <summary>Barred means protected with neither commands nor a sound route; the rule is one predicate.</summary>
    [Fact]
    public void ABarIsDrawnOnlyForAProtectedRowWithNoVoiceRoute()
    {
        var phrase = new SettingCommandPhrase("phrase", null);
        Assert.True(Plain(protectedRow: true).ShowsProtectedBar);
        Assert.False((Plain(protectedRow: true) with { Commands = [phrase] }).ShowsProtectedBar);
        Assert.False((Plain(protectedRow: true) with { ValuesBySound = _ => ValuesHeard.None }).ShowsProtectedBar);
        Assert.False(Plain(protectedRow: false).ShowsProtectedBar);
    }

    /// <summary>The legend is there exactly on the pages that hold a barred row, and nowhere else.</summary>
    [AvaloniaFact]
    public void TheLegendMatchesWhetherThePageHasABarredRow()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);
        var places = SettingsLayout.Areas.SelectMany(area => area.Places).ToList();

        Assert.Contains(places, place => settings.RowsForPlace(place.Id).Any(row => row.ShowsProtectedBar));
        Assert.Contains(places, place => !settings.RowsForPlace(place.Id).Any(row => row.ShowsProtectedBar));

        for (var i = 0; i < places.Count; i++)
        {
            host.View.ShowPlace(i);
            Jobs();

            // A place with no page on a fresh surface is not one the Commander can open.
            if (host.View.ActiveSection != i)
            {
                continue;
            }

            var expected = settings.RowsForPlace(places[i].Id).Any(row => row.ShowsProtectedBar);

            var legend = host.View.GetVisualDescendants().OfType<DockPanel>()
                .FirstOrDefault(line => line.Name == SettingsView.ProtectedLegendName);

            Assert.True(legend is not null, $"{places[i].Title} drew no legend line at all");
            Assert.Equal(expected, legend!.IsVisible);
        }

        host.Close();
    }

    /// <summary>The legend draws the bar it explains: the row bar's width and colour, beside the line.</summary>
    [AvaloniaFact]
    public void TheLegendDrawsTheProtectedRowsBar()
    {
        using var look = AppLook.Put();
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        var places = SettingsLayout.Areas.SelectMany(area => area.Places).ToList();
        var shown = Enumerable.Range(0, places.Count).First(i =>
        {
            host.View.ShowPlace(i);
            Jobs();
            return host.View.ActiveSection == i && settings.RowsForPlace(places[i].Id).Any(row => row.ShowsProtectedBar);
        });

        var legend = host.View.GetVisualDescendants().OfType<DockPanel>()
            .First(line => line.Name == SettingsView.ProtectedLegendName);
        Assert.True(legend.IsVisible, $"{places[shown].Title} holds a barred row but hid the legend");

        var accent = Assert.IsAssignableFrom<ISolidColorBrush>(Application.Current!.FindResource(ThemeManager.AKey)).Color;

        var legendBar = Assert.Single(legend.Children.OfType<Border>());
        Assert.Equal(SettingsView.RowBarWidth, legendBar.Width);
        Assert.Equal(accent, Assert.IsAssignableFrom<ISolidColorBrush>(legendBar.Background).Color);

        Assert.Contains(
            host.View.GetVisualDescendants().OfType<Border>(),
            border => border.BorderThickness is { Left: SettingsView.RowBarWidth, Top: 0, Right: 0, Bottom: 0 }
                      && border.BorderBrush is ISolidColorBrush { Color: var color } && color == accent);

        Assert.Contains(
            legend.Children.OfType<TextBlock>(),
            block => block.Text == SettingsView.ProtectedLegend);

        host.Close();
    }
}
