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

    /// <summary>Records where the main window was left.</summary>
    public ViewState With(WindowPlacement placement) => this with { MainWindow = placement };

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
