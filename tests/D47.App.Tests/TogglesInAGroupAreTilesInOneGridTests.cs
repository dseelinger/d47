using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>
/// A group's toggles are one grid of checkbox tiles where its first toggle falls, 2 across unless the
/// group says otherwise, closing up around a tile the filter or the fold removes (#441).
/// </summary>
public sealed class TogglesInAGroupAreTilesInOneGridTests
{
    private static void Jobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static TextBox Box(SettingsHost host) => (TextBox)host.Panel.FindControl<Control>("SearchInput")!;

    /// <summary>The grid holding the tile with this label.</summary>
    private static TileGrid Grid(SettingsView view, string label) =>
        Page(view).GetVisualDescendants().OfType<TileGrid>()
            .Single(grid => Tiles(grid).Any(tile => AutomationProperties.GetName(tile) == label));

    private static List<CheckBox> Tiles(TileGrid grid) =>
        [.. grid.Children.Where(child => child.IsVisible)
            .Select(child => child.GetVisualDescendants().OfType<CheckBox>().Single())];

    private static Button GroupReset(SettingsView view, string title) =>
        Page(view).GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == SettingsView.GroupResetName
                              && AutomationProperties.GetName(button) == $"Reset {title}");

    [AvaloniaFact]
    public void TheMicrophoneGroupEndsWithItsTwoTogglesSideBySide()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, "voice-input");

        var grid = Grid(host.View, "Cancel D47's own voice out of the microphone");
        var tiles = Tiles(grid);

        Assert.Equal(2, tiles.Count);
        Assert.Equal(tiles[0].Bounds.Y, tiles[1].Bounds.Y);
        Assert.True(tiles[1].TranslatePoint(default, grid)!.Value.X > tiles[0].TranslatePoint(default, grid)!.Value.X);

        // Last in the Microphone group: the next thing on the page is the following group's head.
        var content = (StackPanel)grid.Parent!;
        var next = content.Children
            .Skip(content.Children.IndexOf(grid) + 1)
            .First(child => child.IsVisible);
        Assert.Equal(SettingsView.GroupHeadName, next.Name);

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(ListeningCapability.EchoKey)]
    [InlineData(ListeningCapability.NoiseKey)]
    public void ClickingATilesLabelTogglesIt(string key)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, "voice-input");

        var before = settings.Read(key);
        var tile = (CheckBox)host.View.ControlFor(key)!;
        var label = (TextBlock)tile.Content!;

        var middle = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), host.Window)!.Value;
        host.Window.MouseDown(middle, MouseButton.Left);
        host.Window.MouseUp(middle, MouseButton.Left);
        Jobs();

        Assert.NotEqual(before, settings.Read(key));

        host.Close();
    }

    [AvaloniaFact]
    public void GuardianVoiceIsTwoRowsOfFourThenTheTest()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, "voice");

        var grid = Grid(host.View, "Cylon");
        var tiles = Tiles(grid);

        Assert.Equal(8, tiles.Count);
        double Top(CheckBox tile) => tile.TranslatePoint(default, grid)!.Value.Y;

        Assert.Single(tiles.Take(4).Select(Top).Distinct());
        Assert.Single(tiles.Skip(4).Select(Top).Distinct());
        Assert.True(Top(tiles[4]) > Top(tiles[0]));

        var testRow = host.View.ControlFor(SpeechCapability.GuardianTestKey)!;
        Assert.True(testRow.TranslatePoint(default, grid)!.Value.Y > grid.Bounds.Height);

        host.Close();
    }

    [AvaloniaFact]
    public void ChangingATreatmentEnablesTheGroupResetAndPressingItPutsItBack()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, "voice");

        var reset = GroupReset(host.View, "Guardian voice");
        Assert.False(reset.IsEnabled);

        ((CheckBox)host.View.ControlFor(SpeechCapability.GuardianReverbKey)!).IsChecked = true;
        Jobs();

        Assert.True(settings.IsChanged(SpeechCapability.GuardianReverbKey));
        Assert.True(reset.IsEnabled);

        reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.False(settings.IsChanged(SpeechCapability.GuardianReverbKey));
        Assert.False(((CheckBox)host.View.ControlFor(SpeechCapability.GuardianReverbKey)!).IsChecked);
        Assert.False(reset.IsEnabled);

        host.Close();
    }

    [AvaloniaFact]
    public void AtTheNarrowWidthNoTileIsUnder150AndNoLabelIsClipped()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: 924, height: 640);

        foreach (var placeId in new[] { "voice-input", "voice", "sounds" })
        {
            Open(host.View, placeId);

            foreach (var grid in Page(host.View).GetVisualDescendants().OfType<TileGrid>().Where(g => g.IsEffectivelyVisible))
            {
                foreach (var tile in Tiles(grid))
                {
                    var label = (TextBlock)tile.Content!;
                    var right = label.TranslatePoint(new Point(label.Bounds.Width, 0), tile)!.Value.X;

                    Assert.True(tile.Bounds.Width >= TileGrid.MinTileWidth, $"{label.Text} is {tile.Bounds.Width} wide");
                    Assert.True(right <= tile.Bounds.Width, $"{label.Text} runs to {right} in {tile.Bounds.Width}");
                    Assert.True(label.Bounds.Height >= label.DesiredSize.Height - 0.5, $"{label.Text} is cut short");
                }
            }
        }

        host.Close();
    }

    [AvaloniaFact]
    public void FilteringByReverbLeavesOneTileInTheGuardianGrid()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Box(host).Text = "reverb";
        Jobs();
        Open(host.View, "voice");

        var tiles = Tiles(Grid(host.View, "Reverb"));

        Assert.Single(tiles);
        Assert.True(tiles[0].IsEffectivelyVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void TheLevelsMutesStayRowsBesideTheirChannels()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);

        Open(host.View, "sounds");

        var grids = Page(host.View).GetVisualDescendants().OfType<TileGrid>().Where(g => g.IsEffectivelyVisible).ToList();

        Assert.Single(grids);
        Assert.Equal(2, Tiles(grids[0]).Count);

        host.Close();
    }

    [AvaloniaTheory]
    [InlineData(1280, 860)]
    [InlineData(924, 640)]
    public void TheTiledPagesAreCaptured(double width, double height)
    {
        using var look = AppLook.Put();

        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, width: width, height: height);

        foreach (var placeId in new[] { "voice-input", "voice", "sounds" })
        {
            Open(host.View, placeId);

            if (placeId == "voice")
            {
                ScrollTo(Grid(host.View, "Cylon"));
            }
            else if (placeId == "voice-input")
            {
                ScrollTo(Grid(host.View, "Cancel D47's own voice out of the microphone"));
            }

            var path = Path.Combine(TestSurface.CaptureDirectory, $"toggle-tiles-{placeId}-{width}x{height}.png");

            using (var frame = host.Window.CaptureRenderedFrame()!)
            {
                frame.Save(path, new PngBitmapEncoderOptions());
            }

            Assert.True(File.Exists(path));
        }

        host.Close();
    }

    private static void ScrollTo(Control target)
    {
        target.BringIntoView();
        Jobs();
    }
}
