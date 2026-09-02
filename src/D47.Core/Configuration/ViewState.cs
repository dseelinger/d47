using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// How the panel was left, as opposed to how d47 is configured. Which cards are collapsed is
/// not a setting: it has no default worth documenting, no effect on behaviour, and nothing
/// should fail loudly because it could not be read (Phase 4, "Collapse settings cards").
/// </summary>
public sealed record ViewState
{
    /// <summary>Capability ids the Commander collapsed.</summary>
    public IReadOnlyList<string> CollapsedCards { get; init; } = [];

    /// <summary>
    /// Where the main window was left, or null if it has never been moved or resized
    /// (Phase 9, "Open at a size that fits the screen"). Here rather than in settings
    /// for the same reason a collapsed card is: it has no default worth documenting and
    /// nothing should fail loudly because it could not be read.
    /// </summary>
    public WindowPlacement? MainWindow { get; init; }

    /// <summary>
    /// And where it was left <em>in mini</em>, kept apart from the rectangle above
    /// (Phase 51).
    /// <para>
    /// <b>Two records rather than one, and that is the whole of the trap this phase names.</b>
    /// The placement memory samples the window on every resize and move and writes the result down
    /// as a size the Commander chose. A mini toggle is a resize, so one record would have the
    /// window shrink, record 512 pixels as the size that was wanted, and hand that back as the
    /// full window — permanently, and across a restart. It is the same shape as the maximised case
    /// the record already guards and it wants the same treatment: the full rectangle survives the
    /// toggle, and a Commander who widens their mini window keeps that too.
    /// </para>
    /// <para>
    /// Null until the first time mini is entered, when it takes the full window's position and the
    /// measured mini size — so the strip appears where the window already was rather than jumping
    /// across the desk on its first use.
    /// </para>
    /// </summary>
    public WindowPlacement? MainWindowMini { get; init; }

    /// <summary>
    /// Whether the Commander has been asked about a Start Menu entry. Recorded whichever way
    /// they answered, because "no" has to stick — an offer that returns every launch is not an
    /// offer, it is nagging.
    /// <para>
    /// Here rather than in settings for the same reason the window's position is: there is
    /// nothing to configure, no default worth documenting, and being unable to read it should
    /// cost one repeated question rather than a loud failure.
    /// </para>
    /// </summary>
    public bool StartMenuOffered { get; init; }

    /// <summary>
    /// The local day (<c>yyyy-MM-dd</c>) the rival-territory warning last gave its full
    /// explanation, or null (asked for 2026-08-31). Once per day, across sessions and whichever
    /// core is aboard; the shortened form carries every exposure after. Here rather than in
    /// settings for the reason the introductions are: nothing is chosen, and losing it costs one
    /// repeated sentence.
    /// </summary>
    public string? RivalExplainedOn { get; init; }

    /// <summary>
    /// Where the Commander put the flat mini panel, or null if they never moved it
    /// (Phase 48).
    /// <para>
    /// Here rather than in settings, and that is a ruling rather than a filing convenience:
    /// <b>a monitor coordinate is not something a Commander typed</b>, and <c>settings.json</c>
    /// is append-only for anything that ever is. It joins the VR anchors and the main window's
    /// own rectangle for the same reason both of those are here — the overlay's <em>size</em>
    /// falls out of the scale row, so the only thing left to remember is two numbers nobody
    /// would ever write down.
    /// </para>
    /// <para>
    /// Position only. There is no size to keep: the strip is the mini content at the chosen rung
    /// and nothing else can resize it.
    /// </para>
    /// </summary>
    public OverlayPlacement? Overlay { get; init; }

    /// <summary>
    /// Capability ids the Commander expanded. Kept separately from the collapsed list because
    /// a card can start collapsed by default: without this, expanding one would be
    /// indistinguishable from never having touched it, and it would close again next time.
    /// </summary>
    public IReadOnlyList<string> ExpandedCards { get; init; } = [];

    /// <summary>
    /// Which filter the checklist was left under — the chooser's key, or null for none. Asked for
    /// 2026-08-23, the same day the filter turned out not to be shared between the surfaces.
    /// <para>
    /// Here rather than in settings for the reason the window's position is: no default worth
    /// documenting, nothing behaves differently, and a filter that cannot be read should cost an
    /// unfiltered list rather than a loud failure. The search box is deliberately <em>not</em>
    /// here — a typed query is where a Commander is this minute, and one restored from last week
    /// is a list that looks broken until they find the box.
    /// </para>
    /// </summary>
    public string? ChecklistFilter { get; init; }

    /// <summary>
    /// Whether the engineer filter is also showing work an engineer here can only take part of the
    /// way (change-requests.md 35). Beside the filter because its own request said it should travel
    /// the same road rather than growing a second one.
    /// </summary>
    public bool ChecklistPartialGrades { get; init; }

    /// <summary>
    /// Which way the journal's Raw switch was left
    /// (<a href="https://github.com/dseelinger/d47/issues/267">#267</a>): the file's own JSON, or
    /// sentences. Here rather than in settings for the reason the checklist filter is — no
    /// default worth documenting, nothing behaves differently, and being unable to read it should
    /// cost one flick of a switch rather than a loud failure.
    /// <para>
    /// <b>The switch's position, not a reading to open on.</b> Raw is a root of the Transcript tab
    /// like the journal itself is, and remembering it as a <em>root</em> would open a Commander who
    /// left d47 on raw into a wall of JSON at launch. What is kept is how the journal reading is
    /// drawn when it is next opened; which reading the tab is on is somebody else's fact.
    /// </para>
    /// </summary>
    public bool JournalRaw { get; init; }

    /// <summary>
    /// Which reading each tab was left on, by tab name and root key
    /// (<a href="https://github.com/dseelinger/d47/issues/268">#268</a>) — the Transcript on
    /// the log file, Routing on Course.
    /// <para>
    /// <b>Changing tabs already kept these; a restart did not.</b> <c>PanelNavigator</c> holds one
    /// current root per tab so a tab switch returns to the mode it left, and nothing wrote that
    /// down, so every launch started at the first reading each tab furnished.
    /// </para>
    /// <para>
    /// <b>Keyed by name rather than by position.</b> Both halves are stable strings that already
    /// exist — the tab's enum name and the root's own key — where an index moves the
    /// moment a reading is registered and would quietly restore a different one. A name nothing
    /// answers to is ignored on restore rather than raising, which is what makes a renamed root or
    /// a hand-edited file cost a first reading instead of a failure.
    /// </para>
    /// <para>
    /// <b>Raw Journal is deliberately not storable here.</b> It is a root of the Transcript tab
    /// like the journal itself, so keeping it as one would open a wall of JSON at launch —
    /// the thing <see cref="JournalRaw"/> exists to avoid. The Transcript's entry names the
    /// journal reading and the switch decides how it is drawn; the normalising is the writer's
    /// job, since this record cannot see a root key's meaning.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string> PanelRoots { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Records which reading a tab was left on.</summary>
    public ViewState With(string tab, string root)
    {
        var next = new Dictionary<string, string>(PanelRoots, StringComparer.Ordinal)
        {
            [tab] = root,
        };

        return this with { PanelRoots = next };
    }

    /// <summary>
    /// Which settings section the page was left scrolled to, by capability id, or null
    /// (<a href="https://github.com/dseelinger/d47/issues/268">#268</a>).
    /// <para>
    /// The settings nav is a scroll-spy over one scrolling column, so its "selection" is a scroll
    /// offset rather than a chosen thing — which is why what is kept is the section the spy
    /// last named rather than a pixel count. A capability id survives a card being added above it;
    /// an offset does not.
    /// </para>
    /// </summary>
    public string? SettingsSection { get; init; }

    /// <summary>
    /// Whether the Commander has ever asked d47 anything, by any route. What it retires is the
    /// worked example in the ask box's placeholder, which is an onboarding hint wearing a
    /// placeholder's clothes and was still teaching someone a month in.
    /// <para>
    /// <b>Recorded rather than derived, and that is the departure.</b> The house pattern is
    /// derivation — <c>FirstRun</c> records nothing and decides from live state each time — and
    /// the obvious live signal here is a non-empty conversation history. There is no such
    /// signal: d47 has never persisted conversation history, so every launch looks like a first
    /// one and the hint would come back for ever. A fact about the Commander that no live state
    /// remembers has to be written down.
    /// </para>
    /// <para>
    /// Here rather than in settings for the same reason the window's position is: there is
    /// nothing to configure and no default worth documenting. Losing it costs one worked example
    /// shown once more, which is the cheapest failure on this record.
    /// </para>
    /// </summary>
    public bool HasAsked { get; init; }

    /// <summary>
    /// Which cores have introduced themselves, so a Commander hears each opening line once
    /// rather than once per launch (docs/plans/change-requests.md item 7).
    /// <para>
    /// Remembered state rather than a setting: nothing here is chosen, there is no default worth
    /// documenting, and a core re-introducing itself because this could not be read is a wasted
    /// line rather than a failure. The same reason a collapsed card is here.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> IntroducedCores { get; init; } = [];

    /// <summary>
    /// When each core was last aboard, so a gap reaction can be about a gap that spans launches
    /// (Phase 35).
    /// <para>
    /// <b>Without this the reaction is unreachable.</b> A core remarks on missing time only past
    /// <see cref="Persona.PersonaHost.GapAfter"/>, which is a month, and the elapsed time was
    /// measured from a dictionary that died with the process — so nothing could ever have been
    /// away long enough. Persisting the stamp is what turns a threshold the Commander asked for
    /// into behaviour they will actually see.
    /// </para>
    /// <para>
    /// The time and nothing else. The telemetry delta a returning core reacts to is a comparison
    /// against the session it left, and a session does not survive a restart — so a core coming
    /// back across one reacts to the missing time with nothing to say about the ship, which is
    /// honest, rather than to a delta measured from an empty session, which would be invented.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, DateTimeOffset> CoresLastAboard { get; init; } =
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

    /// <summary>
    /// How the Commander last dragged the rule between panes, keyed by how many panes were
    /// showing at the time (Phase 55).
    /// <para>
    /// <b>Proportions rather than pixels.</b> The window is resizable and a remembered 640 means
    /// something different at 1024 and at 2048, so what is kept is each pane's share of the strip.
    /// The shares for one entry always sum to 1.
    /// </para>
    /// <para>
    /// <b>Keyed by pane count, because a two-pane split and a three-pane split are different
    /// arrangements</b> a Commander will want set differently — and the reflow moves between them
    /// on its own as the window is dragged. One list would have widening the window silently
    /// restate the two-pane choice as a three-pane one.
    /// </para>
    /// <para>
    /// Here rather than in settings for the reason the window's own rectangle is: it is a number
    /// nobody would ever type, and a split that cannot be read should cost equal panes rather than
    /// a loud failure. It is deliberately <em>not</em> keyed by tab or by crumb — the unit is the
    /// surface's split at a given pane count, and a saved layout per subject is a much larger
    /// promise than the one that was asked for.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<double>> PaneShares { get; init; } =
        new Dictionary<int, IReadOnlyList<double>>();

    /// <summary>Records a drag, as each pane's share of the strip at that pane count.</summary>
    public ViewState With(int panes, IReadOnlyList<double> shares)
    {
        var next = new Dictionary<int, IReadOnlyList<double>>(PaneShares)
        {
            [panes] = shares,
        };

        return this with { PaneShares = next };
    }

    /// <summary>
    /// The remembered shares for a pane count, or null for equal panes — which is both the
    /// untouched default and the honest answer when what was stored cannot be trusted.
    /// <para>
    /// Validated on the way out rather than on the way in, because this record is deserialised
    /// from a file a Commander can edit and a wrong-length or non-positive list would otherwise
    /// reach the layout. The count must match, every share must be a positive real, and the
    /// caller normalises — so a hand-edited <c>[1, 3]</c> means the same as <c>[0.25, 0.75]</c>.
    /// </para>
    /// </summary>
    public IReadOnlyList<double>? SharesFor(int panes)
    {
        if (!PaneShares.TryGetValue(panes, out var shares) || shares.Count != panes)
        {
            return null;
        }

        return shares.All(share => share > 0 && double.IsFinite(share)) ? shares : null;
    }

    /// <summary>
    /// Whether a card should be open, given what the capability asked for and what the
    /// Commander has since said. Their choice wins in both directions.
    /// </summary>
    public bool IsExpanded(string capabilityId, bool startCollapsed)
    {
        if (CollapsedCards.Contains(capabilityId, StringComparer.Ordinal))
        {
            return false;
        }

        if (ExpandedCards.Contains(capabilityId, StringComparer.Ordinal))
        {
            return true;
        }

        return !startCollapsed;
    }

    /// <summary>
    /// Where each world-locked headset surface was put down, keyed by surface slot, and where
    /// the head was when it was put there (Phase 9, "Re-anchor the panels").
    /// <para>
    /// Here rather than in settings for the same reason the window's position is. <em>Choosing</em>
    /// to have a surface world-locked is a setting - it is a preference, it has a default worth
    /// documenting, and it belongs on a row. Where the Commander's hand happened to leave it is
    /// not; it is seven numbers nobody would ever type, and nothing should fail loudly because
    /// they could not be read.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, SurfaceAnchor> VrAnchors { get; init; } =
        new Dictionary<string, SurfaceAnchor>(StringComparer.Ordinal);

    /// <summary>Records a surface having been put somewhere.</summary>
    public ViewState With(string slot, SurfaceAnchor anchor)
    {
        var next = new Dictionary<string, SurfaceAnchor>(VrAnchors, StringComparer.Ordinal)
        {
            [slot] = anchor,
        };

        return this with { VrAnchors = next };
    }

    /// <summary>
    /// Records where the main window was left.
    /// <para>
    /// One placement rather than a slot per window, since Phase 12 made settings a page of this
    /// window instead of a second one. There is one window to remember, so there is one number
    /// to remember it with.
    /// </para>
    /// </summary>
    public ViewState With(WindowPlacement placement) => this with { MainWindow = placement };

    /// <summary>
    /// Records where the window was left, in whichever of its two shapes it was in
    /// (Phase 51). The caller says which; nothing here reads a mode, because
    /// <c>PanelMode</c> is the app's word and Core has never heard it.
    /// </summary>
    public ViewState With(WindowPlacement placement, bool mini) => mini
        ? this with { MainWindowMini = placement }
        : this with { MainWindow = placement };

    /// <summary>Records where the flat mini panel was dragged to (Phase 48).</summary>
    public ViewState With(OverlayPlacement placement) => this with { Overlay = placement };

    /// <summary>Records a card's new state as an explicit choice.</summary>
    public ViewState With(string capabilityId, bool expanded) => this with
    {
        CollapsedCards = Without(CollapsedCards, capabilityId, add: !expanded),
        ExpandedCards = Without(ExpandedCards, capabilityId, add: expanded),
    };

    /// <summary>
    /// Forgets what the Commander said about one card, so
    /// <see cref="CapabilityDisplay.StartCollapsed"/> decides again
    /// (<a href="https://github.com/dseelinger/d47/issues/223">#223</a>).
    /// <para>
    /// <b>Because Collapse all buries that default otherwise.</b> Every card whose state has been
    /// written stops falling back to it — permanently, and one press of a bulk control writes all
    /// of them at once. Resetting a card is the Commander saying they want its defaults, and this
    /// is one of them.
    /// </para>
    /// <para>
    /// It does not touch what is on screen. The card stays as it is until the next launch, which
    /// is what makes this a restored default rather than a card that shuts itself while being
    /// reset.
    /// </para>
    /// </summary>
    public ViewState Forgetting(string capabilityId) => this with
    {
        CollapsedCards = Without(CollapsedCards, capabilityId, add: false),
        ExpandedCards = Without(ExpandedCards, capabilityId, add: false),
    };

    private static IReadOnlyList<string> Without(IReadOnlyList<string> ids, string id, bool add)
    {
        var next = ids.Where(existing => !string.Equals(existing, id, StringComparison.Ordinal)).ToList();

        if (add)
        {
            next.Add(id);
        }

        return next;
    }
}

/// <summary>
/// A surface that has been put down: where it went, and where the head was when it did.
/// <para>
/// Both together, because one without the other is a placement re-anchor cannot undo. The
/// second pose is the whole reason re-anchoring can preserve a layout rather than stacking
/// every panel in the same place.
/// </para>
/// </summary>
public sealed record SurfaceAnchor
{
    public required PoseSettings Placed { get; init; }

    public required PoseSettings PlacedAgainst { get; init; }
}

/// <summary>
/// How the window was left: size and position in device-independent pixels, plus whether it
/// was maximised. Restoring a maximised window at its restored size is a papercut nobody
/// reports and everybody notices, so the flag travels with the numbers.
/// <para>
/// Position is nullable because "never moved" and "moved to 0,0" are different states, and
/// only the second one should override the platform centring the window.
/// </para>
/// </summary>
public sealed record WindowPlacement
{
    public double Width { get; init; }

    public double Height { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public bool Maximized { get; init; }

    /// <summary>
    /// The origin of the working area of the screen the window was maximised on, or null when it
    /// was not maximised.
    /// <para>
    /// Its own pair rather than reusing <see cref="X"/> and <see cref="Y"/>, which hold the
    /// restored rectangle and have to keep holding it: a Commander who maximises once must still
    /// be able to un-maximise back to a window they chose. A maximised window on the second
    /// monitor and a restored window on the first is an ordinary arrangement, and one pair of
    /// numbers cannot describe it.
    /// </para>
    /// <para>
    /// A screen origin rather than the window's own position, because a maximised window on
    /// Windows sits at roughly minus the border thickness — which on the leftmost monitor is a
    /// point on no screen at all, and next to another monitor is a point on the wrong one.
    /// </para>
    /// </summary>
    public double? MaximizedOnX { get; init; }

    public double? MaximizedOnY { get; init; }
}

/// <summary>
/// Where the flat mini panel was left, in device-independent pixels on the virtual desktop
/// (Phase 48).
/// <para>
/// Its own record rather than a <see cref="WindowPlacement"/> with two fields left at zero: the
/// overlay has no size to remember and cannot be maximised, and a record whose meaningful half is
/// unused is a record the next reader has to be told about.
/// </para>
/// </summary>
public sealed record OverlayPlacement
{
    public double X { get; init; }

    public double Y { get; init; }
}

/// <summary>
/// The third store, and the one that shrugs hardest. Settings shout on a bad file and secrets
/// shrug into "capability off"; this one shrugs into "everything expanded", which is exactly
/// what a first run looks like anyway (architecture.md §5 D6).
/// <para>
/// Read synchronously at startup so the panel can apply it while building rather than after
/// painting — a card that flashes open and then collapses is worse than one that never
/// remembered.
/// </para>
/// </summary>
public sealed class ViewStateStore(AppPaths paths, ILogger<ViewStateStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ViewState Load()
    {
        if (!File.Exists(paths.ViewStateFile))
        {
            return new ViewState();
        }

        try
        {
            return JsonSerializer.Deserialize<ViewState>(File.ReadAllText(paths.ViewStateFile), Json)
                   ?? new ViewState();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogInformation(ex, "View state at {Path} is unreadable; starting with everything expanded",
                paths.ViewStateFile);
            return new ViewState();
        }
    }

    public void Save(ViewState state)
    {
        try
        {
            AtomicFile.WriteAllText(paths.ViewStateFile, JsonSerializer.Serialize(state, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a collapse state is not worth a dialog, or a log line above Debug.
            logger.LogDebug(ex, "Could not write view state to {Path}", paths.ViewStateFile);
        }
    }
}
