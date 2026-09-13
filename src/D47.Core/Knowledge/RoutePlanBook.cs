using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>Which planner produced a plan.</summary>
public enum RoutePlanKind
{
    /// <summary>A jump route between two systems.</summary>
    Jump,

    /// <summary>A Road to Riches loop.</summary>
    Riches,

    /// <summary>A chain of buy-and-sell stops.</summary>
    Trade,
}

/// <summary>The last plan of one kind, as it was answered (Phase 37, "One last plan").</summary>
public sealed record StoredRoutePlan
{
    public RoutePlanKind Kind { get; init; }

    /// <summary>When it was worked out.</summary>
    public DateTimeOffset PlottedAt { get; init; }

    /// <summary>
    /// What the Commander asked for, in their terms — "Sol to Colonia", "50,000,000 credits over 5
    /// hops".
    /// </summary>
    public string Headline { get; init; } = string.Empty;

    public PlottedRoute? Jump { get; init; }

    public RichesRoute? Riches { get; init; }

    public TradeRoute? Trade { get; init; }

    /// <summary>The furthest stop the Commander has reached, as an index into the plan's own stop list.</summary>
    public int? Reached { get; init; }
}

/// <summary>
/// The last plan each planner produced, shared by every path that can ask for one (Phase 37, "One last
/// plan, shared by the voice and the panel").
/// </summary>
public sealed class RoutePlanBook(string path, ILogger<RoutePlanBook> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lock _gate = new();

    private readonly Dictionary<RoutePlanKind, StoredRoutePlan> _plans = [];

    /// <summary>Raised when a plan is recorded, whoever recorded it — the model's path or a panel.</summary>
    public event Action? Changed;

    /// <summary>The last plan of a kind, or null if none has been made.</summary>
    public StoredRoutePlan? Last(RoutePlanKind kind)
    {
        lock (_gate)
        {
            return _plans.GetValueOrDefault(kind);
        }
    }

    public void Record(PlottedRoute route, string headline, DateTimeOffset at) =>
        Keep(new StoredRoutePlan
        {
            Kind = RoutePlanKind.Jump,
            PlottedAt = at,
            Headline = headline,
            Jump = route,
        });

    public void Record(RichesRoute route, string headline, DateTimeOffset at) =>
        Keep(new StoredRoutePlan
        {
            Kind = RoutePlanKind.Riches,
            PlottedAt = at,
            Headline = headline,
            Riches = route,
        });

    /// <summary>
    /// <paramref name="currentSystem"/> is the Commander's system as the plan was made: the first stop of a
    /// trade route is the station they were already standing on, so no arrival event follows it (#199).
    /// </summary>
    public void Record(TradeRoute route, string headline, DateTimeOffset at, string? currentSystem = null) =>
        Keep(new StoredRoutePlan
        {
            Kind = RoutePlanKind.Trade,
            PlottedAt = at,
            Headline = headline,
            Trade = route,
            Reached = route.Stops.Count > 0 &&
                      string.Equals(route.Stops[0].System, currentSystem, StringComparison.OrdinalIgnoreCase)
                ? 0
                : null,
        });

    /// <summary>
    /// Moves a stored plan's reached stop forward on arrival, one plan and one kind at a time (#199).
    /// </summary>
    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        var moved = false;

        lock (_gate)
        {
            foreach (var journalEvent in events)
            {
                if (journalEvent.Kind is not ("FSDJump" or "CarrierJump" or "Location"))
                {
                    continue;
                }

                if (journalEvent.String("StarSystem") is not { Length: > 0 } system)
                {
                    continue;
                }

                foreach (var kind in _plans.Keys.ToArray())
                {
                    var plan = _plans[kind];

                    if (journalEvent.Timestamp < plan.PlottedAt)
                    {
                        continue;
                    }

                    if (Stops(plan) is not { } stops)
                    {
                        continue;
                    }

                    var from = plan.Reached is { } reached ? reached + 1 : 0;
                    var found = -1;

                    for (var i = from; i < stops.Count; i++)
                    {
                        if (string.Equals(stops[i], system, StringComparison.OrdinalIgnoreCase))
                        {
                            found = i;
                            break;
                        }
                    }

                    if (found < 0)
                    {
                        continue;
                    }

                    _plans[kind] = plan with { Reached = found };
                    moved = true;
                }
            }

            if (moved)
            {
                Save();
            }
        }

        if (moved)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>The systems a plan visits in order, or null for a kind with no plan recorded.</summary>
    private static IReadOnlyList<string>? Stops(StoredRoutePlan plan) => plan.Kind switch
    {
        RoutePlanKind.Jump => plan.Jump?.Waypoints.Select(waypoint => waypoint.System).ToArray(),
        RoutePlanKind.Riches => plan.Riches?.Stops.Select(stop => stop.System).ToArray(),
        RoutePlanKind.Trade => plan.Trade?.Stops.Select(stop => stop.System).ToArray(),
        _ => null,
    };

    private void Keep(StoredRoutePlan plan)
    {
        lock (_gate)
        {
            _plans[plan.Kind] = plan;
            Save();
        }

        Changed?.Invoke();
    }

    /// <summary>Reads the file, if there is one.</summary>
    public void Load()
    {
        lock (_gate)
        {
            _plans.Clear();

            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var stored = JsonSerializer.Deserialize<StoredRoutePlan[]>(File.ReadAllText(path), Json) ?? [];

                foreach (var plan in stored)
                {
                    _plans[plan.Kind] = plan;
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read {Path}; the next plot will rewrite it.", path);
                _plans.Clear();
            }
        }
    }

    /// <summary>Called under the lock.</summary>
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(_plans.Values.ToArray(), Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A plan that cannot be written is still a plan that was answered.
            logger.LogWarning(ex, "Could not write {Path}.", path);
        }
    }
}
