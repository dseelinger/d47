using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using D47.App.Panel;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Vr;
using D47.Vr;

namespace D47.App.Headset;

/// <summary>
/// The panel, as the headset needs it: a second instantiation of <see cref="PanelView"/> bound
/// to the view model the desktop window is already showing, rasterised offscreen.
/// <para>
/// This is where "one widget tree renders to both surfaces" actually lands. There is no second
/// view definition and no screenshot of the window — both surfaces read one model, so the
/// windowed one cannot be more functional than the headset one by construction rather than by
/// anybody remembering (Phase 9).
/// </para>
/// </summary>
public sealed class VrPanelSurface : IVrSurfaceSource, IDisposable
{
    /// <summary>
    /// Mini, and it is 512 wide rather than 640 because that is the lever on apparent text size.
    /// <para>
    /// Apparent size is the pixel count and the quad's width in metres together, so the same
    /// 14-point text across 512 pixels of the same 0.34 m is a quarter larger than across 640.
    /// Reported as a tad too small (remediation.md 9, "bump up the mini-panel font"). The zoom row
    /// would do it too and does not reach a Commander whose settings file already records 100;
    /// this is the default that does.
    /// </para>
    /// <para>
    /// <b>The height does not shrink with it.</b> Holding the aspect — 512x224, which is exactly
    /// 640x280 scaled — was tried first and left the transcript pane with nothing: mini is "the
    /// tail and the provenance line", the chrome around it does not get smaller, and at 224 there
    /// was no room left for the tail. Caught by the minimise-safety test, which renders this
    /// surface and asserts that an appended line changes what it draws. So the pixel budget down
    /// the panel is unchanged and only the width moves; the quad is proportionally taller in the
    /// room as a result, which is what "bigger" looks like.
    /// </para>
    /// <para>
    /// Mini alone is fixed. The big panel is a setting since Phase 25 — see
    /// <see cref="PanelResolution"/> — and mini is not on that ladder for the reason above: the
    /// numbers here are not an aspect, they are a floor under a reduced content set.
    /// </para>
    /// </summary>
    private static readonly PixelSize Mini =
        new(PanelResolution.Mini.Width, PanelResolution.Mini.Height);

    private readonly PanelViewModel _model;
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

    /// <param name="settingsPage">
    /// Builds the settings surface for this copy of the panel, or null to leave it without one.
    /// <para>
    /// It used to be structurally absent here, on the reasoning that a nav column beside a
    /// 700-pixel minimum has no business on a quad a metre away. The quad is 1024 wide and the
    /// surface already collapses its nav below 900, so what it renders at is the arrangement a
    /// Commander sees in the desktop window at its default size — and every reason to reach a
    /// setting applies at least as much with a headset on, where there is no other way to reach
    /// one (remediation.md, "The VR big panel should carry the Settings tab").
    /// </para>
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

        // The Commander's long arcs, and the button that ages them (Phase 34). They ride
        // the checklist tab, so they reach the headset exactly when the checklist does.
        D47.Core.Goals.GoalBook? goals = null,
        Action? backfillGoals = null,

        // The stories the Commander flies (Phase 47), in the headset from 2026-08-22.
        Panel.AdventureSurface? adventures = null)
    {
        _dumpTo = dumpTo;

        _model = model;
        _settings = settings;
        _anchor = anchor;

        _view = new PanelView { DataContext = model };

        // The Commander's own avatar frames reach the headset copy too. One widget tree renders
        // to both surfaces, and a face the window has and the headset does not would be exactly
        // the parity the one-widget-tree item exists to protect.
        _view.Avatar.Library = avatars;

        if (settingsPage is not null)
        {
            _view.EnableSettings(settingsPage);
        }

        if (checklists is not null)
        {
            // What the Commander is working on, back in the headset (Phase 39). Phase 25
            // made the checklist reachable there at all, which a `Window` cannot be, and both it
            // and Loadout were then <b>withdrawn on the Commander's instruction</b> during the
            // panel redesign - the Commander overruling two built phases, not a discovery that
            // either tab had never worked. That call was real, which is why this comment is
            // rewritten rather than deleted along with the withdrawal it used to record.
            //
            // <b>This reverses half of it.</b> Loadout is still not furnished, and the asymmetry
            // is a decision rather than an oversight: the fleet is a three-level drill ending in
            // a search field, which is a bigger surface and its own day's work. Bringing one tab
            // back is not a step toward parity, which stays a someday-maybe (CLAUDE.md, "Feature
            // parity between the two surfaces is a nice-to-have, not a constraint").
            //
            // The withdrawal was done by <b>not calling</b> this rather than by hiding anything,
            // because absent is the default: a tab nobody furnishes has no builder, registers no
            // root, and `PanelView.Tab` already refuses to select one - so the spoken route and
            // the drawn one agree without either being taught a special case. That is the rule
            // Settings has followed since Phase 12, and it is why reversing it is this one call
            // and nothing else: the constructor has gone on taking `checklists`, `goals` and
            // `backfillGoals` from `AppHost` against exactly this day.
            //
            // `goals` and `backfillGoals` ride the tab rather than sitting beside it (Phase 34
            // ), so the arcs and the button that ages them reach the headset on exactly
            // the same terms the list does.
            _view.EnableChecklist(checklists, goals, backfillGoals);
        }

        // The journal's raw reading, in the headset (#231). It shipped desktop-only on the
        // reasoning that a wall of JSON is not readable at a metre and exists to be pasted into a
        // bug report — an act with no meaning in mid-air. The Commander overruled that: it is one
        // toggle beside the reading it belongs to, and "if they don't like it they can toggle it
        // off". A reading nobody opens costs a Commander nothing; a reading they wanted and could
        // not reach costs them the flight.
        _view.EnableRawJournal();

        if (adventures is not null)
        {
            // The stories, in the headset (asked for 2026-08-22). Phase 47 made this tab
            // desktop-only, on the reasoning that its editor and its ask form want a keyboard.
            // That weighed the wrong half: the reading level is where a Commander who has just
            // arrived somewhere finds out what the story made of it, and arriving somewhere is
            // what a Commander in a headset has just done. The forms come along because they are
            // the same tab and the prompts have taken a spoken value since Phase 25 — a surface
            // that shows a story but cannot be asked for one is a surface with a hole in it.
            //
            // The mini panel gets its own short reading of the same story, which is the other
            // half of the instruction and is furnished by this same call — see AdventureMini.
            _view.EnableAdventures(adventures);
        }

        // `ships`, `gameState` and `onFoot` are still read below - Engineers needs all three.

        if (unlocks is not null && ships is not null && gameState is not null)
        {
            // And who to go and get next (Phase 28). The distances are arithmetic over a
            // shipped table, so this page is exactly as useful in a headset in flight as it is at
            // a desk with a browser open — which is the whole reason the coordinates ship.
            _view.EnableEngineers(unlocks, ships, gameState, onFoot);
        }

        if (timekeeper is not null && alarmStore is not null)
        {
            // A Commander in a headset is exactly the Commander who cannot glance at a wall
            // clock, which is most of why this page exists at all (Phase 24).
            _view.EnableUtilities(
                timekeeper,
                alarmStore,
                () => D47.Core.SystemWallClock.Instance.UtcNow,
                () => TimeZoneInfo.Local);
        }

        // The same scaling host the desktop window zooms with, for the same reason: a render
        // transform would draw the panel larger and let the surface clip it, where a layout
        // transform re-measures so text rewraps and spacing grows with it. "Scale the big
        // panel" and "Zoom the desktop window" are one mechanism seen from two rooms.
        var pixels = settings.Current.Vr.Panel.Resolution;

        _offscreen = new OffscreenSurface(
            new LayoutTransformControl { LayoutTransform = _scale, Child = _view },
            new PixelSize(pixels.Width, pixels.Height));

        // Anything the panel shows changing is a reason to redraw, and nothing else is. This
        // is D1's second Phase 9 instruction in one line: the measured 4-10 Hz cost is the
        // worst case, and a panel with nothing new costs a boolean.
        model.PropertyChanged += OnModelChanged;
    }

    public bool Enabled { get; set; }

    /// <summary>
    /// This surface's own prompts (Phase 25). Its own, and not the window's: a chooser is
    /// a level of one navigator's stack, and the Commander can be picking a module in the headset
    /// while the window goes on showing the transcript.
    /// </summary>
    public Panel.PanelPrompts Prompts => _view.Prompts;

    /// <summary>
    /// Back one level on this surface, and whether there was anything to go back from — so the
    /// controller button stays available to whatever else wants it at a root (Phase 25).
    /// </summary>
    public bool Back() => _view.GoBack();

    /// <summary>
    /// Redraws the clocks, from the headset's own tick. Marks the surface dirty only when a digit
    /// actually changed — everything on that page reads to the minute, so at 10 Hz it was being
    /// re-rasterised six hundred times per change and 599 of those images were identical to the
    /// one before (#23). The tab check is not change detection and never was: it only stops a
    /// clock behind a transcript costing pixels.
    /// </summary>
    public void TickClocks() => _dirty |= _view.TickClocks();

    /// <summary>
    /// Redraws the engineer pages when the Commander has moved, re-fitted or unlocked somebody
    /// (Phase 28). Marks the surface dirty only when something actually moved: unlike a
    /// clock, a ranking with nothing behind it has not changed.
    /// <para>
    /// This sentence was true of the intent and not of the code until #23 — the stamp comparison
    /// that answers it has been in <see cref="PanelView.TickEngineers"/> all along, returning
    /// <c>void</c>, so the flag went up on the strength of the call rather than the answer.
    /// </para>
    /// </summary>
    public void TickEngineers() => _dirty |= _view.TickEngineers();

    /// <summary>
    /// One frame of the <em>d47 is composing</em> animation (asked for 2026-08-22). Marks the
    /// surface dirty only when the drawing actually moved, which is the same bargain
    /// <see cref="Aim"/> struck for the same reason: a flag held true every frame re-rasterises the
    /// whole widget tree and hands SteamVR an identical image, and that is what made the panel
    /// flicker the last time something set it unconditionally.
    /// </summary>
    public void TickAdventures() => _dirty |= _view.TickAdventures();

    /// <summary>Where this surface currently is, for a spoken phrase to move.</summary>
    public D47.Core.Interface.PanelNavigator Nav => _view.Nav;

    /// <summary>
    /// Moves the page this surface is showing, for a spoken scroll
    /// (<a href="https://github.com/dseelinger/d47/issues/34">#34</a>).
    /// <para>
    /// <b>This is the surface the phrase was asked for.</b> A ray on a twelve-pixel bar is the only
    /// way to scroll in a headset — the thumbsticks are unbound and stay that way — and a Commander
    /// with their hands on a stick cannot use it. Dragging is not replaced; this is a second way in.
    /// </para>
    /// <para>
    /// Dirty only when something moved, which is the bargain every other tick on this surface
    /// strikes: a flag held true for a scroll that did nothing re-renders the whole widget tree and
    /// hands SteamVR an identical image.
    /// </para>
    /// </summary>
    public D47.Core.Interface.PanelScrollOutcome Scroll(D47.Core.Interface.PanelScrollStep step)
    {
        var outcome = _view.Scroll(step);

        // Dirty only for a move, which is the same bargain as before and is now said precisely:
        // "already at the bottom" changes no pixel, and re-rendering for it would hand SteamVR an
        // identical image (#263).
        _dirty |= outcome == D47.Core.Interface.PanelScrollOutcome.Moved;

        return outcome;
    }

    /// <summary>Which mode the Commander has the panel in. Read from settings, never held here.</summary>
    public PanelMode Mode =>
        string.Equals(_settings.Current.Vr.Mode, "mini", StringComparison.OrdinalIgnoreCase)
            ? PanelMode.Mini
            : PanelMode.Full;

    public VrSurface Surface => Mode == PanelMode.Mini ? VrSurface.PanelMini : VrSurface.PanelFull;

    public bool Visible => Enabled;

    /// <summary>
    /// The panel is the surface a hand can do something to — it is grab-to-move — so it is the
    /// one that asks SteamVR for a laser and the mouse events that come back with it.
    /// </summary>
    public bool TakesPointer => true;

    /// <summary>
    /// Where this surface goes and what it looks like. The look comes from settings and the
    /// anchor from view state, and they are joined here rather than in either store: choosing
    /// to be world-locked is a preference, and where a hand happened to leave it is not.
    /// </summary>
    public SurfacePlacement Placement
    {
        get
        {
            var placement = Settings().ToPlacement(_settings.Current.Vr.Opacity);

            return _anchor(Slot) is { } anchor
                ? placement with { Placed = anchor.Placed, PlacedAgainst = anchor.Against }
                : placement;
        }
    }

    /// <summary>Which settings slot this mode reads from.</summary>
    public string Slot => Mode == PanelMode.Mini
        ? D47.Core.Capabilities.Builtin.VrCapability.MiniSlot
        : D47.Core.Capabilities.Builtin.VrCapability.PanelSlot;

    /// <summary>
    /// How many pixels to render, which is the Commander's since Phase 25 for the big panel and
    /// fixed for mini. Read fresh on every serve rather than held, so a resize takes on the next
    /// frame — <see cref="Draw"/> resizes the offscreen surface from this, and
    /// <see cref="Configure"/> is what marks it dirty when the setting moves.
    /// </summary>
    public (int Width, int Height) Size =>
        Mode == PanelMode.Mini ? (Mini.Width, Mini.Height) : Settings().Resolution;

    public bool IsDirty => _dirty;

    /// <summary>Where the head was when this surface was last served. Re-anchor reads it.</summary>
    public VrPose Head => _head;

    public void Observe(VrPose head) => _head = head;

    /// <summary>
    /// Which way <c>output-only</c> was last set, so the class is touched when the mode changes
    /// and not once a frame. Null until the first draw.
    /// </summary>
    private bool? _outputOnly;

    /// <summary>
    /// Mini carries no buttons, the same as the flat overlay (change-requests.md 42).
    /// <para>
    /// <c>PanelView</c> already declares the rule — <c>output-only</c> hides an exact
    /// <c>Button</c> — and <see cref="Windowing.OverlayPanel"/> already applies it. Its argument
    /// carries over unchanged: nothing there can be clicked, and the room is better spent on the
    /// data. Mini is <b>512 pixels wide</b>, chosen for apparent text size rather than for
    /// comfort, so every control on it is space taken from the transcript tail.
    /// </para>
    /// <para>
    /// <b>Toggled rather than set once, because this surface is one <c>PanelView</c> with a
    /// mode.</b> Adding the class at construction would strip the buttons from the big headset
    /// panel too — and that is the one surface where they are genuinely pressable, since the ray
    /// reaches them through the geometric hit test. The negative half is the half worth testing.
    /// </para>
    /// <para>
    /// Called from <see cref="Draw"/> before the render rather than on a settings change, so the
    /// frame that goes to the headset cannot disagree with the mode it was drawn for — and only
    /// on a change, so a mode that has not moved costs nothing and cannot restyle the tree
    /// underneath a frame.
    /// </para>
    /// </summary>
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
        // Following is re-asserted between the layout and the rasterise, because that is the one
        // moment this tree has a real extent to scroll to the end of.
        var rendered = _offscreen.Render(_view.KeepUp);
        _offscreen.CopyInto(destination, rowBytes);
        _dirty = false;

        Keep(rendered);
    }

    /// <summary>
    /// Writes the first frame of a session to <c>data/</c>, so what the headset was handed can
    /// be looked at rather than reasoned about.
    /// <para>
    /// The rasterise is covered by a test, but that test runs on Avalonia's headless platform
    /// and this runs on Win32 against a window that is never shown. That difference is the last
    /// thing between the two that has never been observed in a real build, and every other
    /// explanation for an overlay SteamVR reports as visible has been wrong.
    /// </para>
    /// <para>
    /// Once per session and overwritten each time, so it stays one small file rather than a
    /// stream of them, and it never touches the frame path after the first.
    /// </para>
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

    /// <summary>
    /// Re-reads the settings that change what is drawn rather than where it goes. Placement is
    /// read fresh on every serve and needs nothing; the content scale changes the render, so it
    /// has to invalidate.
    /// </summary>
    public void Configure()
    {
        // Two levers, both read here and both only marking dirty when they actually moved: this
        // runs on every tick of a live session, and a surface held dirty for a setting nobody
        // touched re-renders the whole widget tree at frame rate for pixels that did not change.
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

    /// <summary>
    /// The mode this surface shows, pushed onto its own view rather than onto the model. The
    /// desktop window is always the full panel and the headset follows the setting; a mode held
    /// on the shared model would put the window into mini the moment the headset went into it.
    /// </summary>
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
    /// A press at a point on the quad's face, in the 0..1 the ray already answers in, and whether
    /// there was anything there to press.
    /// <para>
    /// The conversion to pixels is the whole of what this adds: <see cref="VrHit"/> is a fraction
    /// across and down the face in raster order, and the view is laid out at whatever
    /// <see cref="Size"/> the current mode asks for. Zoom needs no part in it — the scale is a
    /// layout transform inside the surface, so the view's own coordinate space is the pixel space
    /// either way.
    /// </para>
    /// <para>
    /// The frame after a press is a different frame, so it is marked dirty unconditionally: a tab
    /// that has been selected and not redrawn is a tab a Commander pressed twice.
    /// </para>
    /// </summary>
    public bool Press(float u, float v)
    {
        var (width, height) = Size;
        var landed = _offscreen.Click(new Point(u * width, v * height));

        _dirty = true;

        return landed;
    }

    /// <summary>
    /// Takes hold of the scrollbar a ray is aiming at, if there is one within reach, and says
    /// whether it did.
    /// <para>
    /// Asked before a carry is allowed to begin, so a hand that came down on a scrollbar scrolls
    /// rather than picking the whole panel up. The two gestures are the same button and cannot
    /// both run.
    /// </para>
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

    /// <summary>Lets go. The bar stays where it was left.</summary>
    public void ReleaseScroll() => _scrolling = null;

    /// <summary>
    /// Lights whatever the ray is resting on, so the Commander can see they have found it.
    /// Called every frame a ray is on the panel, and with null when it leaves.
    /// <para>
    /// <b>Dirty only when the light actually moved.</b> This is called on every tick of a live
    /// session — including the ordinary case of no ray anywhere near the panel, which asks for
    /// null over and over — so setting the flag unconditionally holds the surface dirty forever.
    /// That re-renders the widget tree, converts it and hands the whole image back to SteamVR
    /// every frame for pixels that did not change, which is the exact condition that made the
    /// panel flicker while it was being carried; done on every frame instead of only while
    /// carrying, it makes the panel flicker all the time. <see cref="OffscreenSurface.Illuminate"/>
    /// already knows whether anything moved and now says so.
    /// </para>
    /// <para>
    /// Or-assigned rather than assigned: a frame where the light did not move may still be dirty
    /// for a reason that has nothing to do with aiming, and this is not the place that clears it.
    /// <see cref="Draw"/> is.
    /// </para>
    /// <para>
    /// <b>A bar being dragged stays lit wherever the ray goes</b> (<a
    /// href="https://github.com/dseelinger/d47/issues/29">#29</a>). Reported as the half of that
    /// issue the hysteresis did not fix: the highlight flashes <em>while scrolling</em>, on every
    /// tab that scrolls. Dragging is the only way to scroll in the headset, so "while scrolling"
    /// and "while the trigger is held on a bar" are the same interval.
    /// </para>
    /// <para>
    /// The cause is that this asked <see cref="OffscreenSurface.ScrollbarNear"/> the question from
    /// scratch every tick, with no idea a bar was already held — so a hand that wandered past the
    /// release radius, or off the panel entirely, put the light out and lit it again on the way
    /// back. Widening a radius cannot fix that, which is why the hysteresis did not: the radius is
    /// the wrong question for the duration of a drag. <b>Capture is what every scrollbar outside
    /// VR already does</b>, and <see cref="VrHost"/> two lines from where it starts a drag says the
    /// same thing about the scroll itself — <em>"a drag that wanders off the bar keeps scrolling
    /// rather than suddenly picking the panel up"</em>. The scroll survived the wander and the
    /// highlight did not.
    /// </para>
    /// </summary>
    public void Aim(float? u, float? v)
    {
        // Held beats aimed. Before the ray is consulted at all, because the whole point is that
        // where the ray is does not decide this while a bar is captured.
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
