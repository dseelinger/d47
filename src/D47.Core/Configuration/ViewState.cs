using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>How the panel was left, as opposed to how d47 is configured.</summary>
public sealed record ViewState
{
    /// <summary>Capability ids the Commander collapsed.</summary>
    public IReadOnlyList<string> CollapsedCards { get; init; } = [];

    /// <summary>
    /// Where the main window was left, or null if it has never been moved or resized (Phase 9, "Open at
    /// a size that fits the screen").
    /// </summary>
    public WindowPlacement? MainWindow { get; init; }

    /// <summary>And where it was left in mini, kept apart from the rectangle above (Phase 51).</summary>
    public WindowPlacement? MainWindowMini { get; init; }

    /// <summary>Whether the Commander has been asked about a Start Menu entry.</summary>
    public bool StartMenuOffered { get; init; }

    /// <summary>
    /// The local day (<c>yyyy-MM-dd</c>) the rival-territory warning last gave its full explanation, or
    /// null (asked for 2026-08-31).
    /// </summary>
    public string? RivalExplainedOn { get; init; }

    /// <summary>Where the Commander put the flat mini panel, or null if they never moved it (Phase 48).</summary>
    public OverlayPlacement? Overlay { get; init; }

    /// <summary>Capability ids the Commander expanded.</summary>
    public IReadOnlyList<string> ExpandedCards { get; init; } = [];

    /// <summary>Which filter the checklist was left under — the chooser's key, or null for none.</summary>
    public string? ChecklistFilter { get; init; }

    /// <summary>
    /// Whether the engineer filter is also showing work an engineer here can only take part of the way
    /// (change-requests.md 35).
    /// </summary>
    public bool ChecklistPartialGrades { get; init; }

    /// <summary>Which way the journal's Raw switch was left (#267): the file's own JSON, or sentences.</summary>
    public bool JournalRaw { get; init; }

    /// <summary>Whether the Ships index draws its hull artwork, or packs the cards down to their names.</summary>
    public bool ShipsDrawingsOff { get; init; }

    /// <summary>Whether the Engineers tab has taken the Colonia eight off its lists (#132).</summary>
    public bool EngineersColoniaHidden { get; init; }

    /// <summary>Whether the Engineers tab has taken the on-foot engineers off its lists (#132).</summary>
    public bool EngineersOnFootHidden { get; init; }

    /// <summary>
    /// Which reading each tab was left on, by tab name and root key (#268) — the Transcript on the log
    /// file, Routing on Course.
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
    /// Which reading each tab was left on in the headset, kept apart from <see cref="PanelRoots"/> for
    /// the reason <see cref="LastTabVr"/> is (#276): a root is per-surface by design apart from the
    /// Transcript's, and a shared key would have a move in one surface overwrite what the other was
    /// left on.
    /// </summary>
    public IReadOnlyDictionary<string, string> PanelRootsVr { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Records which reading a tab was left on in the headset.</summary>
    public ViewState WithVr(string tab, string root)
    {
        var next = new Dictionary<string, string>(PanelRootsVr, StringComparer.Ordinal)
        {
            [tab] = root,
        };

        return this with { PanelRootsVr = next };
    }

    /// <summary>Which tab the desktop window was left on, or null for never left Transcript (#276).</summary>
    public string? LastTab { get; init; }

    /// <summary>
    /// Which tab the headset was left on, kept apart from <see cref="LastTab"/> because the two
    /// surfaces move independently — the window can be on Settings while the headset reads the
    /// conversation — so one key would have whichever surface changed tabs last decide where both
    /// reopen.
    /// </summary>
    public string? LastTabVr { get; init; }

    /// <summary>Which settings section the page was left scrolled to, by capability id, or null (#268).</summary>
    public string? SettingsSection { get; init; }

    /// <summary>Whether the Commander has ever asked d47 anything, by any route.</summary>
    public bool HasAsked { get; init; }

    /// <summary>
    /// Which cores have introduced themselves, so a Commander hears each opening line once rather than
    /// once per launch (docs/plans/change-requests.md item 7).
    /// </summary>
    public IReadOnlyList<string> IntroducedCores { get; init; } = [];

    /// <summary>
    /// When each core was last aboard, so a gap reaction can be about a gap that spans launches (Phase
    /// 35).
    /// </summary>
    public IReadOnlyDictionary<string, DateTimeOffset> CoresLastAboard { get; init; } =
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

    /// <summary>
    /// How the Commander last dragged the rule between panes, keyed by how many panes were showing at
    /// the time (Phase 55).
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
    /// The remembered shares for a pane count, or null for equal panes — which is both the untouched
    /// default and the correct answer when what was stored cannot be trusted.
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
    /// Whether a card should be open, given what the capability asked for and what the Commander has
    /// since said.
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
    /// Where each world-locked headset surface was put down, keyed by surface slot, and where the head
    /// was when it was put there (Phase 9, "Re-anchor the panels").
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

    /// <summary>Records where the main window was left.</summary>
    public ViewState With(WindowPlacement placement) => this with { MainWindow = placement };

    /// <summary>Records where the window was left, in whichever of its two shapes it was in (Phase 51).</summary>
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
    /// Forgets what the Commander said about one card, so <see
    /// cref="CapabilityDisplay.StartCollapsed"/> decides again (#223).
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

/// <summary>A surface that has been put down: where it went, and where the head was when it did.</summary>
public sealed record SurfaceAnchor
{
    public required PoseSettings Placed { get; init; }

    public required PoseSettings PlacedAgainst { get; init; }
}

/// <summary>
/// How the window was left: size and position in device-independent pixels, plus whether it was
/// maximised.
/// </summary>
public sealed record WindowPlacement
{
    public double Width { get; init; }

    public double Height { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public bool Maximized { get; init; }

    /// <summary>
    /// The origin of the working area of the screen the window was maximised on, or null when it was
    /// not maximised.
    /// </summary>
    public double? MaximizedOnX { get; init; }

    public double? MaximizedOnY { get; init; }
}

/// <summary>
/// Where the flat mini panel was left, in device-independent pixels on the virtual desktop (Phase 48).
/// </summary>
public sealed record OverlayPlacement
{
    public double X { get; init; }

    public double Y { get; init; }

    /// <summary>The strip's own size, or null when the Commander has never dragged a handle (#89).</summary>
    public double? Width { get; init; }

    public double? Height { get; init; }
}

/// <summary>The third store, and the one that shrugs hardest.</summary>
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
