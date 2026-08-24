using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// How the panel was left, as opposed to how d47 is configured. Which cards are collapsed is
/// not a setting: it has no default worth documenting, no effect on behaviour, and nothing
/// should fail loudly because it could not be read (list.md Phase 4, "Collapse settings cards").
/// </summary>
public sealed record ViewState
{
    /// <summary>Capability ids the Commander collapsed.</summary>
    public IReadOnlyList<string> CollapsedCards { get; init; } = [];

    /// <summary>
    /// Where the main window was left, or null if it has never been moved or resized
    /// (list.md Phase 9, "Open at a size that fits the screen"). Here rather than in settings
    /// for the same reason a collapsed card is: it has no default worth documenting and
    /// nothing should fail loudly because it could not be read.
    /// </summary>
    public WindowPlacement? MainWindow { get; init; }

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
    /// Where the Commander put the flat mini panel, or null if they never moved it
    /// (list.md Phase 48).
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
    /// (list.md Phase 35).
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
    /// the head was when it was put there (list.md Phase 9, "Re-anchor the panels").
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

    /// <summary>Records where the flat mini panel was dragged to (list.md Phase 48).</summary>
    public ViewState With(OverlayPlacement placement) => this with { Overlay = placement };

    /// <summary>Records a card's new state as an explicit choice.</summary>
    public ViewState With(string capabilityId, bool expanded) => this with
    {
        CollapsedCards = Without(CollapsedCards, capabilityId, add: !expanded),
        ExpandedCards = Without(ExpandedCards, capabilityId, add: expanded),
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
/// (list.md Phase 48).
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
