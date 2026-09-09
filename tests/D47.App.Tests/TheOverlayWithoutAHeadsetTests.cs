using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Input;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The mini panel without a headset.</summary>
public class TheOverlayWithoutAHeadsetTests
{
    /// <summary>A third instantiation, not a second design.</summary>
    [AvaloniaFact]
    public void ItDrawsTheTranscriptsTailFromTheSharedModel()
    {
        var (overlay, model, _, _) = Open(on: true, eliteInFront: true);

        model.Append("Fixture One, docked. Fuel at 82 percent.");
        var before = Frame(overlay);

        model.Append("\nStill talking, with the window nowhere in it.");
        var after = Frame(overlay);

        Assert.NotEmpty(after);
        Assert.NotEqual(before, after);

        overlay.Close();
    }

    /// <summary>
    /// The transcript's tail readable with d47's own window minimised, which is the phase's stated
    /// acceptance.
    /// </summary>
    [AvaloniaFact]
    public void ItKeepsDrawingWithTheMainWindowMinimised()
    {
        var (overlay, model, _, _) = Open(on: true, eliteInFront: true);

        var window = new MainWindow(host: null);
        window.Show();

        model.Append("Fixture One, docked.");
        var before = Frame(overlay);

        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();

        model.Append("\nStill talking with the window down.");
        var after = Frame(overlay);

        Assert.Equal(WindowState.Minimized, window.WindowState);
        Assert.NotEmpty(after);
        Assert.NotEqual(before, after);

        window.Close();
        overlay.Close();
    }

    /// <summary>A tab nobody furnished is refused, with no special case anywhere.</summary>
    [AvaloniaFact]
    public void AnUnfurnishedTabCostsNoSpecialCase()
    {
        var (overlay, _, _, _) = Open(on: true, eliteInFront: true, stories: true);

        Assert.True(overlay.Nav.Has(PanelTab.Transcript));
        Assert.True(overlay.Nav.Has(PanelTab.Adventures));

        foreach (var tab in new[]
                 {
                     PanelTab.Settings, PanelTab.Loadout, PanelTab.Checklist,
                     PanelTab.Engineers, PanelTab.Utilities, PanelTab.Routing,
                 })
        {
            Assert.False(overlay.Nav.Has(tab), $"The overlay furnished {tab} and was handed nothing for it.");

            overlay.Nav.Select(tab);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(PanelTab.Transcript, overlay.Nav.Tab);
        }

        overlay.Close();
    }

    /// <summary>
    /// And with nothing furnished at all it is still a surface, on the transcript, refusing every tab
    /// including the one the other case accepts.
    /// </summary>
    [AvaloniaFact]
    public void WithoutAStoryItIsTheTranscriptAndNothingElse()
    {
        var (overlay, _, _, _) = Open(on: true, eliteInFront: true);

        Assert.Equal(PanelTab.Transcript, overlay.Nav.Tab);
        Assert.False(overlay.Nav.Has(PanelTab.Adventures));
        Assert.False(overlay.Nav.Select(PanelTab.Adventures));

        overlay.Close();
    }

    /// <summary>Visible when Elite is in front and hidden otherwise.</summary>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]

    // Placing wins over the foreground rule, because a Commander sets the strip up before launching the game
    // as often as after.
    [InlineData(true, false, true, true)]
    public void ItShowsItselfWhenTheGameDoes(bool enabled, bool elite, bool placing, bool shown) =>
        Assert.Equal(shown, OverlayPanel.ShouldShow(enabled, elite, placing));

    /// <summary>The same rule through the real surface, driven by the tick that asks the question.</summary>
    [AvaloniaFact]
    public void TheTickIsWhatPutsItOnScreenAndTakesItOff()
    {
        var (overlay, _, elite, tick) = Open(on: true, eliteInFront: true);

        Assert.True(overlay.IsVisible);

        elite.IsForeground = false;
        Beat(tick);

        Assert.False(overlay.IsVisible);

        elite.IsForeground = true;
        Beat(tick);

        Assert.True(overlay.IsVisible);

        overlay.Close();
    }

    [AvaloniaFact]
    public void ItIsOffOutOfTheBox()
    {
        Assert.False(new D47Settings().Ui.Overlay.Enabled);

        var (overlay, _, _, _) = Open(on: false, eliteInFront: true);

        Assert.False(overlay.IsVisible);

        overlay.Close();
    }

    /// <summary>Scale is the lever, because there are no metres.</summary>
    [AvaloniaFact]
    public void ItsSizeComesOffTheZoomLadder()
    {
        var (overlay, _, _, _) = Open(on: true, eliteInFront: true);
        var settings = _settings!;

        Assert.Equal(PanelResolution.Mini.Width, overlay.Width);
        Assert.Equal(PanelResolution.Mini.Height, overlay.Height);

        settings.Apply(InterfaceCapability.OverlayScaleKey, "150", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelResolution.Mini.Width * 1.5, overlay.Width);
        Assert.Equal(PanelResolution.Mini.Height * 1.5, overlay.Height);

        overlay.Close();
    }

    /// <summary>
    /// Where it ends up is a view preference and not a setting: a monitor coordinate is not something a
    /// Commander typed, and <c>settings.json</c> is append-only for anything that ever is.
    /// </summary>
    [AvaloniaFact]
    public void WhereItWasLeftIsRememberedAsViewStateAndNotAsASetting()
    {
        var (overlay, _, elite, tick) = Open(on: true, eliteInFront: true);
        var viewState = _viewState!;

        overlay.Position = new PixelPoint(310, 190);

        // Place and let go, which is the whole gesture: the pointer comes back the moment it is done, and
        // where it ended up is what gets written down.
        overlay.Place();
        Assert.True(overlay.IsPlacing);

        overlay.Place();
        Assert.False(overlay.IsPlacing);

        var remembered = viewState.Load().Overlay;

        Assert.NotNull(remembered);
        Assert.Equal(310, remembered.X);
        Assert.Equal(190, remembered.Y);

        // And nothing about it reached settings, which is the half of this claim that a position written to
        // the wrong store would still pass the first half of.
        Assert.Equal(D47Settings.Defaults.Ui.Overlay, _settings!.Current.Ui.Overlay with { Enabled = false });

        overlay.Close();

        // A fresh surface over the same store opens where it was left.
        var again = OverlayPanel.Attach(
            new PanelViewModel(), _settings!, viewState, tick, elite,
            NullLogger<OverlayPanel>.Instance);

        Assert.Equal(new PixelPoint(310, 190), again.Position);

        again.Close();
    }

    /// <summary>Place mode ends itself.</summary>
    [AvaloniaFact]
    public void PlaceModeHandsThePointerBack()
    {
        var (overlay, _, elite, tick) = Open(on: true, eliteInFront: false);

        // Not on screen, because Elite is not in front — and place mode brings it up anyway, since a
        // Commander sets this up before launching the game as often as after.
        Assert.False(overlay.IsVisible);

        overlay.Place();

        Assert.True(overlay.IsVisible);
        Assert.True(overlay.IsPlacing);

        overlay.Place();

        Assert.False(overlay.IsPlacing);

        // And the tick takes it away again, because the reason it was up has gone.
        Beat(tick);
        Assert.False(overlay.IsVisible);

        overlay.Close();
    }

 /// <summary>Which monitor.</summary>
    [AvaloniaFact]
    public void ItAsksWhereTheGameIsEveryTimeItComesUp()
    {
        var (overlay, _, elite, tick) = Open(on: true, eliteInFront: true);

        elite.Bounds = (0, 0, 1920, 1080);
        var asked = elite.BoundsAsked;

        elite.IsForeground = false;
        Beat(tick);

        elite.IsForeground = true;
        Beat(tick);

        Assert.True(
            elite.BoundsAsked > asked,
            "The strip came up without asking where the game was, so it cannot have followed it "
            + "to another monitor.");

        overlay.Close();
    }

    /// <summary>A default may follow the game around, and a choice may not.</summary>
    [AvaloniaFact]
    public void OnceTheCommanderHasPlacedItNothingMovesItAgain()
    {
        var (overlay, _, elite, tick) = Open(on: true, eliteInFront: true);

        overlay.Position = new PixelPoint(240, 160);
        overlay.Place();
        overlay.Place();

        var theirs = overlay.Position;

        elite.Bounds = (0, 0, 1920, 1080);
        elite.IsForeground = false;
        Beat(tick);

        elite.IsForeground = true;
        Beat(tick);

        Assert.Equal(theirs, overlay.Position);

        overlay.Close();
    }

    [AvaloniaFact]
    public void AStripClosedFromOutsideStaysDownInsteadOfTakingD47Down()
    {
        var (overlay, _, elite, tick) = Open(on: true, eliteInFront: true);

        Assert.True(overlay.IsVisible);

        overlay.Close();

        // The flip that killed it: away and back, which is every Alt-Tab out of the game.
        elite.IsForeground = false;
        Beat(tick);

        elite.IsForeground = true;
        Beat(tick);

        Assert.False(overlay.IsVisible);

        // And the gesture that would have shown it too, since place mode wins over the foreground test and so
        // reaches Show() by its own path.
        overlay.Place();

        Assert.False(overlay.IsPlacing);
        Assert.False(overlay.IsVisible);

        // The setting is the other way in, and it is the one the issue's smaller fix explicitly
        // declines: touching it re-reads the state and still finds a window that is gone.
        _settings!.Apply(InterfaceCapability.OverlayKey, "False", SettingsCaller.Panel);
        _settings!.Apply(InterfaceCapability.OverlayKey, "True", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();

        Assert.False(overlay.IsVisible);
    }

    /// <summary>A picture of the strip, for looking at rather than for asserting on: a line hanging low or a tail clipped by four pixels is something a test can be written to miss and an eye cannot.</summary>
    [AvaloniaFact]
    public void ItRendersToACapture()
    {
        var (overlay, model, _, _) = Open(on: true, eliteInFront: true);

        model.Append("Fixture One, docked. Fuel at 82 percent.\n");
        model.Append("\n> how far to Shinrarta\n");
        model.Append("Eleven jumps, and you are carrying more than the scoop likes.");

        Dispatcher.UIThread.RunJobs();

        overlay.CaptureRenderedFrame()!.Save(
            Path.Combine(TestSurface.CaptureDirectory, "overlay-mini.png"),
            new PngBitmapEncoderOptions());

        overlay.Close();
    }

    private SettingsService? _settings;
    private ViewStateStore? _viewState;

    private (OverlayPanel Overlay, PanelViewModel Model, StubElite Elite, TickLoop Tick) Open(
        bool on, bool eliteInFront, bool stories = false)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var (settings, viewState, paths) = TestSurface.Create();

        _settings = settings;
        _viewState = viewState;

        if (on)
        {
            settings.Apply(InterfaceCapability.OverlayKey, "True", SettingsCaller.Panel);
        }

        var model = new PanelViewModel();
        var elite = new StubElite { IsForeground = eliteInFront };
        var tick = new TickLoop(NullLogger<TickLoop>.Instance);

        var overlay = OverlayPanel.Attach(
            model, settings, viewState, tick, elite,
            NullLogger<OverlayPanel>.Instance,
            avatars: null,
            adventures: stories ? AdventureFixture.Surface(paths) : null);

        Dispatcher.UIThread.RunJobs();

        return (overlay, model, elite, tick);
    }

    /// <summary>One tick, and the dispatcher pass the surface posts its answer onto.</summary>
    private static void Beat(TickLoop tick)
    {
        tick.Tick(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>What the surface actually drew, so an assertion is about pixels and not about a call.</summary>
    private static byte[] Frame(OverlayPanel overlay)
    {
        Dispatcher.UIThread.RunJobs();

        using var stream = new MemoryStream();

        overlay.CaptureRenderedFrame()!.Save(stream, new PngBitmapEncoderOptions());

        return stream.ToArray();
    }

    /// <summary>Elite's window as the overlay reads it.</summary>
    private sealed class StubElite : IEliteWindow
    {
        public bool IsRunning => IsForeground;

        public bool IsForeground { get; set; }

 /// <summary>Where the game's window is, so the strip can pick the monitor it is on.</summary>
        public (int X, int Y, int Width, int Height)? Bounds
        {
            get
            {
                BoundsAsked++;
                return _bounds;
            }

            set => _bounds = value;
        }

        private (int X, int Y, int Width, int Height)? _bounds;

        /// <summary>How many times the strip has asked where the game is.</summary>
        public int BoundsAsked { get; private set; }

        public FocusResult Raise() => FocusResult.AlreadyThere;
    }
}
