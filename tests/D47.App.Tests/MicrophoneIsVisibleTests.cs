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

/// <summary>
/// "Show that the microphone is open" (list.md Phase 13).
/// <para>
/// The item is specific about what it wants and why: continuous capture with no visible state is
/// the thing a Commander is right to distrust, and the indicator is a property of the gate policy
/// rather than of any one capability. Which makes the load-bearing assertions here structural
/// rather than cosmetic — it has to be on <em>both</em> surfaces, and it has to survive mini,
/// because mini is the headset's normal state and a headset is where a Commander cannot see a
/// desktop window to check.
/// </para>
/// </summary>
public class MicrophoneIsVisibleTests
{
    /// <summary>
    /// Off is the only state that draws nothing. Every other one is a claim the Commander is
    /// entitled to see made — including the idle one, which is push-to-talk saying out loud what
    /// it has always quietly done.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(MicrophoneState.Off, false)]
    [InlineData(MicrophoneState.Idle, true)]
    [InlineData(MicrophoneState.Armed, true)]
    [InlineData(MicrophoneState.Open, true)]
    public void TheIndicatorIsDrawnWheneverADeviceIsOpen(MicrophoneState state, bool shown)
    {
        var model = new PanelViewModel { Microphone = state };
        var view = Bind(model);

        Render(view, 1024, 640);

        Assert.Equal(shown, Named(view, "MicrophoneRow").IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(MicrophoneState.Idle, "PTT Ready")]
    [InlineData(MicrophoneState.Armed, "Listening...")]
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
    /// The shape, not just the colour. A glance reads a filled shape before it reads a hue, and
    /// a difference that is only colour is not a difference for everybody.
    /// </summary>
    [AvaloniaFact]
    public void OnlyTheOpenStateIsFilledIn()
    {
        var model = new PanelViewModel { Microphone = MicrophoneState.Armed };
        var view = Bind(model);

        // One surface across both renders. The theme brushes are taken as resource observables
        // so a marker drawn under one theme is still the accent under the next, and a resource
        // observable resolves nothing at all on a control that is not in a tree — so a helper
        // that built and disposed a surface per render would be asserting about a detached view.
        using var surface = new OffscreenSurface(view, new PixelSize(1024, 640));
        surface.Render();

        var glyph = (Avalonia.Controls.Shapes.Path)Named(view, "MicrophoneGlyph");
        Assert.Null(glyph.Fill);

        model.Microphone = MicrophoneState.Open;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        surface.Render();

        Assert.NotNull(glyph.Fill);
    }

    /// <summary>
    /// The requirement in the checklist's own words: visible on the panel <em>and</em> the VR
    /// surface. Both instantiate the same view against one model, so what this really asserts is
    /// that neither surface was given a way to leave it out.
    /// </summary>
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
    /// Mini drops content on purpose — the header, the ask line and the banners all go — and this
    /// is the one thing that must not go with them. Mini is what the headset shows, and a
    /// Commander in a headset has no window to go and check.
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

        // Both halves: the region mini could have taken away with the rest of the chrome, and
        // the indicator inside it.
        Assert.True(Named(view, "StatusRow").IsVisible);
        Assert.True(Named(view, "MicrophoneRow").IsVisible);

        Assert.NotNull(frame);
        frame.Save(
            Path.Combine(TestSurface.CaptureDirectory, "vr-panel-mini-listening.png"),
            new PngBitmapEncoderOptions());
    }

    /// <summary>
    /// The state arrives from the gate, which is driven by the audio thread and the tick loop.
    /// Neither owns these controls, and the view is where thread affinity is answered — the same
    /// rule the transcript scroll and the avatar already follow.
    /// </summary>
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

    /// <summary>The settings page has no turns on it and no provenance line, so neither shows.</summary>
    [AvaloniaFact]
    public void TheSettingsPageShowsNeitherTheIndicatorNorTheProvenanceLine()
    {
        var model = new PanelViewModel { Microphone = MicrophoneState.Open };
        var view = Bind(model);
        view.EnableSettings(() => new Border());
        view.Tab = PanelTab.Settings;

        Render(view, 1024, 640);

        // The row that carries both, which is what ApplyChrome hides. Asserted on the region
        // rather than through IsEffectivelyVisible, because the offscreen path hosts the view in
        // a window that is deliberately never shown.
        Assert.False(Named(view, "StatusRow").IsVisible);
    }

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
