using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Listening;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>"Show that the microphone is open".</summary>
public class MicrophoneIsVisibleTests
{
    /// <summary>Always drawn on the transcript tab, including Off.</summary>
    [AvaloniaTheory]
    [InlineData(MicrophoneState.Off, true)]
    [InlineData(MicrophoneState.Idle, true)]
    [InlineData(MicrophoneState.Armed, true)]
    [InlineData(MicrophoneState.Open, true)]
    public void TheIndicatorIsAlwaysDrawnOnTheTranscriptTab(MicrophoneState state, bool shown)
    {
        var model = new PanelViewModel { Microphone = state };
        var view = Bind(model);

        Render(view, 1024, 640);

        Assert.Equal(shown, Named(view, "MicrophoneRow").IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(MicrophoneState.Off, "MIC OFF")]
    [InlineData(MicrophoneState.Idle, "PTT READY")]
    [InlineData(MicrophoneState.Armed, "LISTENING")]
    [InlineData(MicrophoneState.Open, "MIC ON")]
    public void EachStateSaysWhichOneItIs(MicrophoneState state, string expected)
    {
        var model = new PanelViewModel { Microphone = state };
        var view = Bind(model);

        Render(view, 1024, 640);

        Assert.Contains(
            expected,
            ((TextBlock)Named(view, "MicrophoneLabel")).Text ?? string.Empty,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A model still loading says so rather than claiming the microphone is ready, except where the gate
    /// is already open and what is being said will be held for it (#147).
    /// </summary>
    [AvaloniaTheory]
    [InlineData(MicrophoneState.Idle, "LOADING MODEL")]
    [InlineData(MicrophoneState.Armed, "LOADING MODEL")]
    [InlineData(MicrophoneState.Open, "MIC ON")]
    public void ALoadingModelSaysSoInsteadOfReady(MicrophoneState state, string expected)
    {
        var model = new PanelViewModel { Microphone = state, ModelLoading = true };
        var view = Bind(model);

        Render(view, 1024, 640);

        Assert.Equal(expected, ((TextBlock)Named(view, "MicrophoneLabel")).Text);
    }

    /// <summary>The shape, not just the colour — hollow for Off, filled once a device is open.</summary>
    [AvaloniaFact]
    public void OnlyAnOpenDeviceIsFilledIn()
    {
        var model = new PanelViewModel { Microphone = MicrophoneState.Off };
        var view = Bind(model);

        // One surface across both renders.
        using var surface = new OffscreenSurface(view, new PixelSize(1024, 640));
        surface.Render();

        var glyph = (Avalonia.Controls.Shapes.Ellipse)Named(view, "MicrophoneGlyph");
        Assert.Null(glyph.Fill);

        model.Microphone = MicrophoneState.Open;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        surface.Render();

        Assert.NotNull(glyph.Fill);
    }

    /// <summary>The requirement in the checklist's own words: visible on the panel and the VR surface.</summary>
    [AvaloniaFact]
    public void BothSurfacesShowIt()
    {
        var model = new PanelViewModel { Microphone = MicrophoneState.Armed };

        var window = new Window { Width = 820, Height = 640, Content = Bind(model) };
        window.Show();

        var headset = Bind(model);
        Render(headset, 1024, 640);

        Assert.True(Named((Control)window.Content!, "MicrophoneRow").IsVisible);
        Assert.True(Named(headset, "MicrophoneRow").IsVisible);

        window.Close();
    }

    /// <summary>
    /// Mini drops content on purpose — the header, the ask line and the banners all go — and this is
    /// the one thing that must not go with them.
    /// </summary>
    [AvaloniaFact]
    public void ItSurvivesMiniMode()
    {
        var model = new PanelViewModel
        {
            Microphone = MicrophoneState.Armed,
            MicrophoneDetail = "say D47 and it will listen",
        };

        model.Append("Fixture Anchorage, 12.4 ly.");

        var view = Bind(model);
        view.Mode = PanelMode.Mini;

        using var surface = new OffscreenSurface(view, new PixelSize(640, 280));
        var frame = surface.Render();

        Assert.False(Named(view, "Header").IsVisible);

        // Both halves: the region mini could have taken away with the rest of the chrome, and the indicator
        // inside it.
        Assert.True(Named(view, "StatusRow").IsVisible);
        Assert.True(Named(view, "MicrophoneRow").IsVisible);

        Assert.NotNull(frame);
        frame.Save(
            Path.Combine(TestSurface.CaptureDirectory, "vr-panel-mini-listening.png"),
            new PngBitmapEncoderOptions());
    }

    /// <summary>The state arrives from the gate, which is driven by the audio thread and the tick loop.</summary>
    [AvaloniaFact]
    public void TheStateCanBeSetFromAnotherThread()
    {
        var model = new PanelViewModel();
        var view = Bind(model);
        Render(view, 1024, 640);

        Task.Run(() => model.Microphone = MicrophoneState.Open).Wait();

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Render(view, 1024, 640);

        Assert.Equal("MIC ON", ((TextBlock)Named(view, "MicrophoneLabel")).Text);
    }

    [AvaloniaFact]
    public void TheSettingsPageShowsNeitherTheIndicatorNorTheProvenanceLine()
    {
        var model = new PanelViewModel { Microphone = MicrophoneState.Open };
        var view = Bind(model);
        view.EnableSettings(() => new Border());
        view.Tab = PanelTab.Settings;

        Render(view, 1024, 640);

        // Both hidden by ApplyChrome off the transcript tab; the indicator goes with its footer.
        Assert.False(Named(view, "StatusRow").IsVisible);
        Assert.False(Named(view, "Footer").IsVisible);
    }

    /// <summary>Only the dot says the state by colour: Good when ready, Warn listening, Danger when off.</summary>
    [AvaloniaTheory]
    [InlineData(MicrophoneState.Idle, Theming.ThemeManager.CyanKey)]
    [InlineData(MicrophoneState.Armed, Theming.ThemeManager.AKey)]
    [InlineData(MicrophoneState.Open, Theming.ThemeManager.AKey)]
    [InlineData(MicrophoneState.Off, Theming.ThemeManager.RedKey)]
    public void OnlyTheDotChangesColour(MicrophoneState state, string dotKey)
    {
        var model = new PanelViewModel { Microphone = state };
        var view = Bind(model);
        var window = new Window { Width = 924, Height = 640, Content = view };
        window.Show();

        var dot = (Avalonia.Controls.Shapes.Ellipse)Named(view, "MicrophoneGlyph");
        var label = (TextBlock)Named(view, "MicrophoneLabel");

        Assert.Equal(Colour(view, dotKey), (dot.Stroke as Avalonia.Media.ISolidColorBrush)?.Color);
        Assert.Equal(
            Colour(view, Theming.ThemeManager.Grey2Key),
            (label.Foreground as Avalonia.Media.ISolidColorBrush)?.Color);

        window.Close();
    }

    /// <summary>The status band is a 36px strip on the footer's ground, with no border of its own.</summary>
    [AvaloniaFact]
    public void TheStatusBandHasNoBorder()
    {
        var view = Bind(new PanelViewModel());

        Render(view, 924, 640);

        var band = (Border)Named(view, "MicrophoneRow");
        var footer = (Border)Named(view, "Footer");

        Assert.Equal(36, band.Bounds.Height);
        Assert.Equal(default, band.BorderThickness);
        Assert.Equal(new Thickness(0, 1, 0, 0), footer.BorderThickness);
    }

    private static Avalonia.Media.Color? Colour(Control near, string key) =>
        (near.FindResource(key) as Avalonia.Media.ISolidColorBrush)?.Color
        ?? throw new InvalidOperationException($"{key} is not reachable from {near}");

    private static Control Named(Control view, string name) =>
        view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == name);

    private static PanelView Bind(PanelViewModel model)
    {
        new Theming.ThemeManager(Application.Current!, NullLogger<Theming.ThemeManager>.Instance)
            .Apply(D47.Core.Interface.ThemeCatalog.Elite);

        return new PanelView { DataContext = model };
    }

    private static RenderTargetBitmap Render(Control view, int width, int height)
    {
        using var surface = new OffscreenSurface(view, new PixelSize(width, height));
        return surface.Render();
    }
}
