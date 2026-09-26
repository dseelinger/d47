using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Panel;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Vr;
using D47.Vr;

namespace D47.App.Headset;

/// <summary>
/// The panel, as the headset needs it: a second instantiation of <see cref="PanelView"/> bound to the
/// view model the desktop window is already showing, rasterised offscreen.
/// </summary>
public sealed class VrPanelSurface : IVrSurfaceSource, IDisposable
{
    /// <summary>How thick a resize handle is drawn, in pixels, and how thick while a ray is on it.</summary>
    private const double EdgePixels = 6;

    private const double LitEdgePixels = 16;

    private readonly PanelViewModel _model;

    /// <summary>The resize handles, drawn over the content: left, right, top, bottom.</summary>
    private readonly Border _left = Edge(Avalonia.Layout.HorizontalAlignment.Left, Avalonia.Layout.VerticalAlignment.Stretch);
    private readonly Border _right = Edge(Avalonia.Layout.HorizontalAlignment.Right, Avalonia.Layout.VerticalAlignment.Stretch);
    private readonly Border _top = Edge(Avalonia.Layout.HorizontalAlignment.Stretch, Avalonia.Layout.VerticalAlignment.Top);
    private readonly Border _bottom = Edge(Avalonia.Layout.HorizontalAlignment.Stretch, Avalonia.Layout.VerticalAlignment.Bottom);

    private bool _handlesShown;
    private VrHandle _handlesLit;

    /// <summary>The zoom-out, reset, zoom-in and done buttons, shown only while the handles are (#190).</summary>
    private readonly StackPanel? _bar;

    /// <summary>The width and pixels a resize drag has reached, until it is written to settings.</summary>
    private (float WidthMetres, (int Width, int Height) Pixels)? _reshaping;
    private readonly string? _dumpTo;

    private bool _kept;
    private readonly SettingsService _settings;
    private readonly Func<string, (VrPose Placed, VrPose Against)?> _anchor;
    private readonly PanelView _view;
    private readonly ScaleTransform _scale = new();
    private readonly OffscreenSurface _offscreen;

    private bool _dirty = true;
    private int _appliedZoom = ZoomLadder.Default;
    private (int Width, int Height) _appliedPixels = PanelResolution.Default;
    private VrPose _head = VrPose.Origin;

    /// <summary>
    /// <param name="settingsPage"> Builds the settings surface for this copy of the panel, or null to
    /// leave it without one.
    /// </summary>
    /// <param name="settingsPage">
    /// Builds the settings surface for this copy of the panel, or null to leave it without one.
    /// </param>
    public VrPanelSurface(
        PanelViewModel model,
        SettingsService settings,
        Func<string, (VrPose Placed, VrPose Against)?> anchor,
        D47.Core.Interface.AvatarLibrary? avatars = null,
        string? dumpTo = null,
        Func<Control>? settingsPage = null,
        D47.Core.Checklists.ChecklistService? checklists = null,
        D47.Core.Utilities.Timekeeper? timekeeper = null,
        D47.Core.Utilities.AlarmStore? alarmStore = null,
        D47.Core.Ships.ShipPlanService? ships = null,
        Func<D47.Core.Journal.CommanderGameState?>? gameState = null,
        D47.Core.Loadout.OnFootPlanService? onFoot = null,
        D47.Core.Engineers.EngineerPlanService? unlocks = null,

        // The Commander's long arcs, and the button that ages them (Phase 34).
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfillGoals = null,

        // The stories the Commander flies (Phase 47), in the headset from 2026-08-22.
        Panel.AdventureSurface? adventures = null,

        // Where the headset was left, across launches (#276) — null in every test that has no opinion about
        // it, which leaves this surface exactly as it always behaved.
        ViewStateStore? viewState = null,

        // The registry the settings page's learned phrases level reads (#171).
        D47.Core.Capabilities.CapabilityRegistry? capabilities = null,

        // Where the Commander is going (Phase 37), in the headset from 2026-09-09 (#52) — the same record the
        // window built, so neither surface has a list of this tab's needs the other has not got.
        Panel.RoutingSurface? routing = null,

        // The fleet and its builds, what the Commander is wearing and the gap between them (Phases 26-27), in
        // the headset from 2026-09-09 (#53).
        Func<D47.Core.Journal.ModulePower>? modulePower = null,
        Func<bool>? hullArt = null,
        EngineerDirectoryMemory? engineersMemory = null,

        // The clipboard, on the same terms as the window's copy (#157).
        D47.Core.Capabilities.Builtin.IClipboard? clipboard = null,

        // Every system name d47 already holds, on the same terms as the window's copy (#159).
        D47.Core.Knowledge.SystemsInPlay? known = null,

        // What the flying Commander has taught D47 stands for a declared phrase (#171), null unless the
        // caller supplies both this and the registry — the same rule Sourcing above follows.
        D47.Core.Conversation.LearnedPhrasesStore? learnedPhrases = null,

        // Builds one tab's own settings strip by its root key, on the same terms as settingsPage above
        // (#218) — the window's builder, so this surface's copy cannot fall behind it.
        Func<string, Control?>? buildSettingsStrip = null,

        // A ray's own way into and out of resize mode (#190) — null leaves the panel exactly as it
        // behaved before: reachable by voice and the hotkey only.
        Action? enterResize = null,
        Action<string>? stepZoom = null,
        Action? leaveResize = null)
    {
        _dumpTo = dumpTo;

        _model = model;
        _settings = settings;
        _anchor = anchor;

        _view = new PanelView { DataContext = model };

        // Read by PanelView's own style and by its resize mark — this copy is the headset's, whichever
        // mode it is currently drawing (#190).
        _view.Classes.Add("headset");

        if (enterResize is not null)
        {
            _view.EnableResize(enterResize);
        }

        // The Commander's own avatar frames reach the headset copy too.
        _view.Avatar.Library = avatars;

        if (clipboard is not null)
        {
            _view.EnableCopy(clipboard);
        }

        if (known is not null)
        {
            _view.EnableSystemNames(known, gameState is null ? null : () => gameState()?.Location.StarSystem);
        }

        if (gameState is not null)
        {
            _view.EnableCommanderName(() => gameState()?.Identity.Name);
        }

        if (settingsPage is not null)
        {
            Func<Panel.LearnedPhrasesPage>? phrases = capabilities is not null && learnedPhrases is not null
                ? () => new Panel.LearnedPhrasesPage(capabilities, learnedPhrases, gameState ?? (() => null))
                : null;

            _view.EnableSettings(settingsPage, learnedPhrases: phrases);
        }

        if (checklists is not null)
        {
            // What the Commander is working on, back in the headset (Phase 39).
            _view.EnableChecklist(checklists, goals, backfillGoals);
        }

        // The journal's raw reading, in the headset (#231).
        _view.EnableRawJournal();

        // The Log file page's own settings — log levels — in the headset (#283).
        _view.EnableLog(
            buildSettingsStrip is null ? null : () => buildSettingsStrip(PanelView.LogRoot));

        if (routing is not null)
        {
            // Every root, Plan included (#52): a form's boxes are plain text boxes and so reach the offscreen
            // board, which has taken a spelled or dictated value since #51. Settings opens on this surface
            // rather than on the window's, the same as Sourcing above.
            _view.EnableRouting(
                routing with { OpenSettings = () => _view.Tab = PanelTab.Settings },
                settingsStrip: buildSettingsStrip is null
                    ? null
                    : () => buildSettingsStrip(RoutingPages.CommunityGoalRoot));

            if (routing.Plans is { } plans)
            {
                // A plot lands off this surface's own tick — by voice, or from the window's card — so the
                // frame has to be marked dirty on its own account rather than waiting for TickRouting's next
                // journal-driven pass, which would leave the headset showing the old plan until a jump (#212).
                plans.Changed += () => _dirty = true;
            }
        }

        if (adventures is not null)
        {
            // The stories, in the headset (asked for 2026-08-22).
            _view.EnableAdventures(
                adventures,
                settingsStrip: buildSettingsStrip is null
                    ? null
                    : () => buildSettingsStrip(AdventuresPage.RootKey));
        }

        if (ships is not null && checklists is not null && gameState is not null)
        {
            // The fleet and its builds, back in the headset (#53) — withheld until now on the reasoning that a
            // three-level drill ending in a search field was a bigger surface than one list of short rows. Every
            // row it drills to is a button or a switch a ray already presses; the one control that is not,
            // Ctrl-drag of a slot onto another, has no pointer-moved path on this surface to ride on and stays a
            // mouse convenience.
            _view.EnableLoadout(
                ships,
                checklists,
                gameState,
                onFoot,
                modulePower,
                hullArt,
                settingsStrip: buildSettingsStrip is null
                    ? null
                    : () => buildSettingsStrip(LoadoutPages.FleetRoot),
                carrierSettingsStrip: buildSettingsStrip is null
                    ? null
                    : () => buildSettingsStrip(LoadoutPages.CarrierRoot));
        }

        // `ships`, `gameState` and `onFoot` are read again below - Engineers needs all three too.

        if (unlocks is not null && ships is not null && gameState is not null)
        {
            // And who to go and get next (Phase 28).
            _view.EnableEngineers(unlocks, ships, gameState, onFoot, engineersMemory, checklists);
        }

        if (timekeeper is not null && alarmStore is not null)
        {
            // A Commander in a headset is exactly the Commander who cannot glance at a wall clock, which is
            // most of why this page exists at all (Phase 24).
            _view.EnableUtilities(
                timekeeper,
                alarmStore,
                () => D47.Core.SystemWallClock.Instance.UtcNow,
                () => TimeZoneInfo.Local);
        }

        if (viewState is not null)
        {
            // The headset's own tab and roots, back where they were left (#276).
            _view.RememberRoots(new PanelRootMemory(viewState, vr: true));
            _view.RememberTab(new PanelTabMemory(viewState, vr: true));
        }

        // The same scaling host the desktop window zooms with, for the same reason: a render transform would
        // draw the panel larger and let the surface clip it, where a layout transform re-measures so text
        // rewraps and spacing grows with it. "Scale the big panel" and "Zoom the desktop window" are one
        // mechanism seen from two rooms.
        var pixels = settings.Current.Vr.Panel.Resolution;

        var framed = new Avalonia.Controls.Panel();
        framed.Children.Add(new LayoutTransformControl { LayoutTransform = _scale, Child = _view });

        foreach (var edge in new[] { _left, _right, _top, _bottom })
        {
            edge.Bind(Border.BackgroundProperty, edge.GetResourceObservable(Theming.ThemeManager.AKey));
            framed.Children.Add(edge);
        }

        if (stepZoom is not null && leaveResize is not null)
        {
            // Outside the LayoutTransformControl, so it does not grow or shrink with the panel's own zoom
            // — a ray needs it at one size, not the Commander's chosen reading size.
            _bar = BuildBar(stepZoom, leaveResize);
            framed.Children.Add(_bar);
        }

        _offscreen = new OffscreenSurface(framed, new PixelSize(pixels.Width, pixels.Height));

        // Anything the panel shows changing is a reason to redraw, and nothing else is.
        model.PropertyChanged += OnModelChanged;
    }

    public bool Enabled { get; set; }

    /// <summary>This surface's own prompts (Phase 25).</summary>
    public Panel.PanelPrompts Prompts => _view.Prompts;

    /// <summary>
    /// The keyboard a ray press on a plain text box opens, which takes speech while it is up (#51).
    /// </summary>
    public Panel.OffscreenSurface Board => _offscreen;

    /// <summary>
    /// Back one level on this surface, and whether there was anything to go back from — so the
    /// controller button stays available to whatever else wants it at a root (Phase 25).
    /// </summary>
    public bool Back() => _view.GoBack();

    /// <summary>Redraws the clocks, from the headset's own tick.</summary>
    public void TickClocks() => _dirty |= _view.TickClocks();

    /// <summary>
    /// Redraws the engineer pages when the Commander has moved, re-fitted or unlocked somebody (Phase
    /// 28).
    /// </summary>
    public void TickEngineers() => _dirty |= _view.TickEngineers();

    /// <summary>Redraws the Fleet tab when the journal says the ship changed, from the headset's own tick.</summary>
    public void TickLoadout() => _dirty |= _view.TickLoadout();

    /// <summary>Redraws an open Ships page after the Hull pictures setting changes (#247).</summary>
    public void InvalidateLoadout()
    {
        _view.InvalidateLoadout();
        _dirty = true;
    }

    /// <summary>One frame of the d47 is composing animation (asked for 2026-08-22).</summary>
    public void TickAdventures() => _dirty |= _view.TickAdventures();

    /// <summary>Redraws the route being flown, from the headset's own tick (#52).</summary>
    public void TickRouting() => _dirty |= _view.TickRouting();

    /// <summary>Where this surface currently is, for a spoken phrase to move.</summary>
    public D47.Core.Interface.PanelNavigator Nav => _view.Nav;

    /// <summary>Moves the page this surface is showing, for a spoken scroll (#34).</summary>
    public D47.Core.Interface.PanelScrollOutcome Scroll(D47.Core.Interface.PanelScrollStep step)
    {
        var outcome = _view.Scroll(step);

        // Dirty only for a move, which is the same bargain as before and is now said precisely: "already at
        // the bottom" changes no pixel, and re-rendering for it would hand SteamVR an identical image (#263).
        _dirty |= outcome == D47.Core.Interface.PanelScrollOutcome.Moved;

        return outcome;
    }

    /// <summary>Which mode the Commander has the panel in.</summary>
    public PanelMode Mode =>
        string.Equals(_settings.Current.Vr.Mode, "mini", StringComparison.OrdinalIgnoreCase)
            ? PanelMode.Mini
            : PanelMode.Full;

    public VrSurface Surface => Mode == PanelMode.Mini ? VrSurface.PanelMini : VrSurface.PanelFull;

    public bool Visible => Enabled;

    /// <summary>
    /// The panel is the surface a hand can press or carry, so it is the one d47's own ray is cast at.
    /// </summary>
    public bool TakesPointer => true;

    /// <summary>Where this surface goes and what it looks like.</summary>
    public SurfacePlacement Placement
    {
        get
        {
            var placement = Settings().ToPlacement(_settings.Current.Vr.Opacity);

            if (_reshaping is { } live)
            {
                placement = (placement with { WidthMetres = live.WidthMetres }).Sane();
            }

            return _anchor(Slot) is { } anchor
                ? placement with { Placed = anchor.Placed, PlacedAgainst = anchor.Against }
                : placement;
        }
    }

    /// <summary>Which settings slot this mode reads from.</summary>
    public string Slot => Mode == PanelMode.Mini
        ? D47.Core.Capabilities.Builtin.VrCapability.MiniSlot
        : D47.Core.Capabilities.Builtin.VrCapability.PanelSlot;

    /// <summary>How many pixels to render: a resize drag's while one is running, otherwise the slot's own.</summary>
    public (int Width, int Height) Size =>
        _reshaping?.Pixels
        ?? (Mode == PanelMode.Mini ? Settings().ResolutionOr(PanelResolution.Mini) : Settings().Resolution);

    /// <summary>Renders at a width and pixel size a resize drag has reached, ahead of settings (#107).</summary>
    public void Reshape(float widthMetres, (int Width, int Height) pixels)
    {
        if (_reshaping == (widthMetres, pixels))
        {
            return;
        }

        _reshaping = (widthMetres, pixels);
        _dirty = true;
    }

    /// <summary>Goes back to reading the slot's settings, once a drag has been written to them.</summary>
    public void Reshaped()
    {
        _reshaping = null;
        _dirty = true;
    }

    /// <summary>Draws the resize handles, with the edges a ray is on lit (#107).</summary>
    public void ShowHandles(bool shown, VrHandle lit)
    {
        lit = shown ? lit : VrHandle.None;

        // Kept outside the early return below: the density behind this inset can move between one call
        // and the next — a drag in progress, a resolution change — while the handle a ray sits on does
        // not, and only the second of those trips the guard.
        if (_bar is { } bar && shown)
        {
            bar.Margin = new Thickness(BarMarginPixels());
        }

        if (shown == _handlesShown && lit == _handlesLit)
        {
            return;
        }

        _handlesShown = shown;
        _handlesLit = lit;

        Light(_left, lit.HasFlag(VrHandle.Left), across: false);
        Light(_right, lit.HasFlag(VrHandle.Right), across: false);
        Light(_top, lit.HasFlag(VrHandle.Top), across: true);
        Light(_bottom, lit.HasFlag(VrHandle.Bottom), across: true);

        if (_bar is not null)
        {
            _bar.IsVisible = shown;
        }

        _dirty = true;

        void Light(Border edge, bool on, bool across)
        {
            edge.IsVisible = shown;
            edge.Opacity = on ? 1 : 0.55;

            if (across)
            {
                edge.Height = on ? LitEdgePixels : EdgePixels;
            }
            else
            {
                edge.Width = on ? LitEdgePixels : EdgePixels;
            }
        }
    }

    /// <summary>Whether the resize handles are drawn.</summary>
    public bool HandlesShown => _handlesShown;

    private static Border Edge(Avalonia.Layout.HorizontalAlignment across, Avalonia.Layout.VerticalAlignment down) => new()
    {
        HorizontalAlignment = across,
        VerticalAlignment = down,
        Width = across == Avalonia.Layout.HorizontalAlignment.Stretch ? double.NaN : EdgePixels,
        Height = down == Avalonia.Layout.VerticalAlignment.Stretch ? double.NaN : EdgePixels,
        IsHitTestVisible = false,
        IsVisible = false,
    };

    /// <summary>Zoom out, reset, zoom in, done — the four things a ray can already reach by hotkey or voice.</summary>
    private static StackPanel BuildBar(Action<string> stepZoom, Action leaveResize) => new()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
        Spacing = 8,
        IsVisible = false,
        Children =
        {
            Grip("ZOOM OUT", "Zoom the panel out", () => stepZoom("out")),
            Grip(Controls.Glyphs.ResetText, "Reset the panel's zoom", () => stepZoom("reset")),
            Grip("ZOOM IN", "Zoom the panel in", () => stepZoom("in")),
            Grip("DONE", "Done resizing", leaveResize),
        },
    };

    /// <summary>One button of the bar, sized for a ray rather than a mouse.</summary>
    private static Button Grip(string word, string says, Action click)
    {
        var button = new Button
        {
            Content = word,
            MinWidth = 44,
            MinHeight = 44,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };

        ToolTip.SetTip(button, says);
        Avalonia.Automation.AutomationProperties.SetName(button, says);

        button.Click += (_, _) => click();

        return button;
    }

    /// <summary>
    /// How far the bar sits from every edge of <c>framed</c>: more than the handle band gets at the
    /// panel's own density, so no point on it ever answers <see cref="VrResize.HandleAt"/> with
    /// anything but <see cref="VrHandle.None"/>.
    /// </summary>
    private double BarMarginPixels()
    {
        var (pixelsWide, _) = Size;
        var widthMetres = Placement.WidthMetres;

        return widthMetres <= 0f
            ? LitEdgePixels
            : (VrResize.HandleMetres * pixelsWide / widthMetres) + LitEdgePixels;
    }

    public bool IsDirty => _dirty;

    /// <summary>Where the head was when this surface was last served.</summary>
    public VrPose Head => _head;

    public void Observe(VrPose head) => _head = head;

    /// <summary>
    /// Which way <c>output-only</c> was last set, so the class is touched when the mode changes and not
    /// once a frame.
    /// </summary>
    private bool? _outputOnly;

    /// <summary>Mini carries no buttons, the same as the flat overlay (change-requests.md 42).</summary>
    private void KeepChromeInStep()
    {
        var outputOnly = Mode == PanelMode.Mini;

        if (_outputOnly == outputOnly)
        {
            return;
        }

        _outputOnly = outputOnly;
        _view.Classes.Set("output-only", outputOnly);
    }

    public void Draw(IntPtr destination, int rowBytes)
    {
        KeepChromeInStep();

        var (width, height) = Size;
        _offscreen.Resize(new PixelSize(width, height));
        // Following is re-asserted between the layout and the rasterise, because that is the one moment this
        // tree has a real extent to scroll to the end of.
        var rendered = _offscreen.Render(_view.KeepUp);
        _offscreen.CopyInto(destination, rowBytes);
        _dirty = false;

        Keep(rendered);
    }

    /// <summary>
    /// Writes the first frame of a session to <c>data/</c>, so what the headset was handed can be
    /// looked at rather than reasoned about.
    /// </summary>
    private void Keep(RenderTargetBitmap rendered)
    {
        if (_kept || _dumpTo is not { } folder)
        {
            return;
        }

        _kept = true;

        try
        {
            rendered.Save(
                Path.Combine(folder, $"vr-{Surface}.png"),
                new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A diagnostic must never be why a frame fails.
            _ = ex;
        }
    }

    /// <summary>Re-reads the settings that change what is drawn rather than where it goes.</summary>
    public void Configure()
    {
        // Whether the resize mark beside Help can do anything, read fresh every tick because the
        // Commander can switch the controllers off with no other way to tell this surface (#190). A
        // no-op once it agrees with what is already shown.
        _view.SetControllersOn(_settings.Current.Vr.Controllers);

        if (_view.SetTabsDownTheLeft(_settings.Current.Ui.Tabs == InterfaceCapability.TabsLeft))
        {
            _dirty = true;
        }

        // Two levers, both read here and both only marking dirty when they actually moved: this runs on every
        // tick of a live session, and a surface held dirty for a setting nobody touched re-renders the whole
        // widget tree at frame rate for pixels that did not change.
        var pixels = Size;

        if (pixels != _appliedPixels)
        {
            _appliedPixels = pixels;
            _dirty = true;
        }

        var zoom = ZoomLadder.Snap(Settings().Zoom);

        if (zoom == _appliedZoom)
        {
            return;
        }

        _appliedZoom = zoom;
        _scale.ScaleX = ZoomLadder.ScaleOf(zoom);
        _scale.ScaleY = ZoomLadder.ScaleOf(zoom);
        _dirty = true;
    }

    /// <summary>The mode this surface shows, pushed onto its own view rather than onto the model.</summary>
    public void ApplyMode()
    {
        if (_view.Mode == Mode)
        {
            return;
        }

        _view.Mode = Mode;
        _dirty = true;
    }

    /// <summary>Forces the next serve to redraw — after a reconnect, or a mode change.</summary>
    public void Invalidate() => _dirty = true;

    /// <summary>
    /// A press at a point on the quad's face, in the 0..1 the ray already answers in, and whether there
    /// was anything there to press.
    /// </summary>
    public bool Press(float u, float v)
    {
        var (width, height) = Size;
        var landed = _offscreen.Click(new Point(u * width, v * height));

        _dirty = true;

        return landed;
    }

    /// <summary>
    /// Takes hold of the scrollbar a ray is aiming at, if there is one within reach, and says whether
    /// it did.
    /// </summary>
    public bool GrabsScroll(float u, float v)
    {
        var (width, height) = Size;
        var at = new Point(u * width, v * height);

        _scrolling = _offscreen.ScrollbarNear(at);

        if (_scrolling is null)
        {
            return false;
        }

        Scroll(u, v);
        return true;
    }

    /// <summary>Moves the held bar to where the ray is now.</summary>
    public void Scroll(float u, float v)
    {
        if (_scrolling is null)
        {
            return;
        }

        var (width, height) = Size;

        OffscreenSurface.Aim(_scrolling, _offscreen.View, new Point(u * width, v * height));
        _dirty = true;
    }

    /// <summary>Lets go.</summary>
    public void ReleaseScroll() => _scrolling = null;

    /// <summary>Lights whatever the ray is resting on, so the Commander can see they have found it.</summary>
    public void Aim(float? u, float? v)
    {
        // Held beats aimed.
        if (_scrolling is not null)
        {
            _dirty |= _offscreen.Illuminate(_scrolling);
            return;
        }

        if (u is not { } across || v is not { } down)
        {
            _dirty |= _offscreen.Illuminate(null);
            return;
        }

        var (width, height) = Size;
        var lit = _offscreen.ScrollbarNear(new Point(across * width, down * height));

        _dirty |= _offscreen.Illuminate(lit);
    }

    private Avalonia.Controls.Primitives.ScrollBar? _scrolling;

    public void Dispose()
    {
        _model.PropertyChanged -= OnModelChanged;
        _offscreen.Dispose();
    }

    private VrSurfaceSettings Settings() =>
        Mode == PanelMode.Mini ? _settings.Current.Vr.Mini : _settings.Current.Vr.Panel;

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;
}
