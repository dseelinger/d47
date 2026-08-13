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
    /// Where the settings window was left. Its own slot rather than sharing the main window's:
    /// they are different shapes with different content, and one number for both would mean
    /// resizing either one moved the other.
    /// <para>
    /// One property per window rather than a dictionary, for the same reason the hotkeys are
    /// one property per action — an unknown window name in a hand-edited file should be
    /// rejected like any other unknown key, and a dictionary would take it silently.
    /// </para>
    /// </summary>
    public WindowPlacement? SettingsWindow { get; init; }

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
    /// Capability ids the Commander expanded. Kept separately from the collapsed list because
    /// a card can start collapsed by default: without this, expanding one would be
    /// indistinguishable from never having touched it, and it would close again next time.
    /// </summary>
    public IReadOnlyList<string> ExpandedCards { get; init; } = [];

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

    /// <summary>Records where the main window was left.</summary>
    /// <summary>Records a window's placement in its own slot.</summary>
    public ViewState With(WindowSlot slot, WindowPlacement placement) => slot switch
    {
        WindowSlot.Settings => this with { SettingsWindow = placement },
        _ => this with { MainWindow = placement },
    };

    /// <summary>The placement remembered for one window, or null if it has never been moved.</summary>
    public WindowPlacement? PlacementOf(WindowSlot slot) =>
        slot == WindowSlot.Settings ? SettingsWindow : MainWindow;

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
/// <summary>Which window a remembered placement belongs to.</summary>
public enum WindowSlot
{
    Main,
    Settings,
}

public sealed record WindowPlacement
{
    public double Width { get; init; }

    public double Height { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public bool Maximized { get; init; }
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
