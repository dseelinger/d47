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
    /// <summary>Capability ids whose settings card is collapsed.</summary>
    public IReadOnlyList<string> CollapsedCards { get; init; } = [];
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
