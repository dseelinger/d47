using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A humor level row's bar is pressed and dragged like the Guardian effect bars (#1498).</summary>
[Trait("Category", "Integration")]
public sealed class PressingAHumorSegmentSetsThatLevelTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static (SettingsView View, Window Window, SettingsService Settings) Open(string key)
    {
        var (settings, viewState, _) = TestSurface.Create();
        settings.Apply(InterfaceCapability.ShowEverySettingKey, "true", SettingsCaller.Panel);
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var onCarrierTab = key == PersonaCapability.CarrierHumorKey;

        if (onCarrierTab)
        {
            viewState.Save(viewState.Load().With("fleet-carrier", expanded: true));
        }

        var view = new SettingsView();
        view.Attach(settings, viewState, tabPlaceId: onCarrierTab ? "fleet-carrier" : null);

        var window = new Window { Content = view, Width = 1180, Height = 900 };
        window.Show();
        Jobs();

        return (view, window, settings);
    }

    private static ScaleTrack TrackOn(SettingsView view, string key, string label)
    {
        view.ShowPlaceOf(key);
        view.RevealRow(key);
        Jobs();

        return view.GetVisualDescendants().OfType<ScaleTrack>()
            .First(track => track.IsEffectivelyVisible
                && Avalonia.Automation.AutomationProperties.GetName(track) == $"{label} level");
    }

    private static Point Segment(Window window, ScaleTrack track, int segment)
    {
        var bar = track.GetVisualDescendants().OfType<Grid>().First(grid => grid.ColumnDefinitions.Count == 10);
        var x = bar.Bounds.Width / 10 * (segment - 0.5);

        return bar.TranslatePoint(new Point(x, bar.Bounds.Height / 2), window)!.Value;
    }

    private static void Key(Window window, ScaleTrack track, Key key, PhysicalKey physical)
    {
        track.GetVisualDescendants().OfType<Level>().First().Focus();
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        Jobs();
    }

    [AvaloniaTheory]
    [InlineData(PersonaCapability.CoreHumorKey, "Core humor")]
    [InlineData(PersonaCapability.NpcHumorKey, "NPC humor")]
    [InlineData(PersonaCapability.CarrierHumorKey, "Carrier crew humor")]
    public void PressingASegmentStoresThatNumberAndTheKeysReachTheEnds(string key, string label)
    {
        using var look = AppLook.ControlKit();
        var (view, window, settings) = Open(key);
        var track = TrackOn(view, key, label);

        var point = Segment(window, track, 4);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Jobs();
        Assert.Equal("4", settings.Read(key));

        point = Segment(window, track, 2);
        window.MouseDown(point, MouseButton.Left);
        window.MouseMove(Segment(window, track, 6), RawInputModifiers.LeftMouseButton);
        window.MouseUp(Segment(window, track, 6), MouseButton.Left);
        Jobs();
        Assert.Equal("6", settings.Read(key));

        Key(window, track, Avalonia.Input.Key.Right, PhysicalKey.ArrowRight);
        Assert.Equal("7", settings.Read(key));

        point = Segment(window, track, 1);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Jobs();
        Assert.Equal("1", settings.Read(key));

        Key(window, track, Avalonia.Input.Key.Left, PhysicalKey.ArrowLeft);
        Assert.Equal("0", settings.Read(key));
        Assert.Equal(0, track.Value);

        window.Close();
    }

    [AvaloniaFact]
    public void TheStepperMovesTheBarWithoutWritingTheRowTwice()
    {
        using var look = AppLook.ControlKit();
        var key = PersonaCapability.CoreHumorKey;
        var (view, window, settings) = Open(key);
        var track = TrackOn(view, key, "Core humor");
        var writes = 0;
        settings.Changed += _ => writes++;

        var increase = view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.IsEffectivelyVisible
                && Avalonia.Automation.AutomationProperties.GetName(button) == "Increase")
            .First(button => button.GetVisualAncestors().OfType<Grid>().Any(grid => grid.Children.Contains(track)));
        increase.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Jobs();

        Assert.Equal(1, writes);
        Assert.Equal(track.Value, int.Parse(settings.Read(key)!, System.Globalization.CultureInfo.InvariantCulture));

        window.Close();
    }
}
