using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>A plotted route a Commander kept under a name, as it was when it was saved.</summary>
public sealed record SavedRoute
{
    public string Id { get; init; } = string.Empty;

    /// <summary>The Commander who saved it.</summary>
    public string? FrontierId { get; init; }

    public string Name { get; init; } = string.Empty;

    public DateTimeOffset SavedAt { get; init; }

    /// <summary>The plan with no reached stop and nothing bought.</summary>
    public StoredRoutePlan Plan { get; init; } = new();

    [JsonIgnore]
    public RoutePlanKind Kind => Plan.Kind;

    /// <summary>When the plan was plotted.</summary>
    [JsonIgnore]
    public DateTimeOffset PlottedAt => Plan.PlottedAt;
}

/// <summary>
/// The routes each Commander saved, in <c>saved-routes.json</c>. Separate from <see cref="RoutePlanBook"/>:
/// a new plot, an arrival or a purchase never changes a saved route.
/// </summary>
/// <param name="shown">The shown Commander's Frontier ID; used where a caller names no Commander.</param>
public sealed class SavedRouteBook(
    string path,
    IFileSystem files,
    ILogger<SavedRouteBook> logger,
    Func<string?>? shown = null)
{
    private static readonly JsonSerializerOptions Json = StoreJson.KeepingNulls;

    private readonly Lock _gate = new();

    /// <summary>Held for the file write; never taken while holding <see cref="_gate"/>.</summary>
    private readonly Lock _writeGate = new();

    private readonly List<SavedRoute> _routes = [];

    /// <summary>Bumped under <see cref="_gate"/> for each change that needs saving.</summary>
    private long _version;

    /// <summary>The newest version on disk, under <see cref="_writeGate"/>.</summary>
    private long _written;

    /// <summary>Raised when a route is saved or deleted.</summary>
    public event Action? Changed;

    /// <summary>The routes a Commander (the shown one by default) saved, newest plotted first.</summary>
    public IReadOnlyList<SavedRoute> For(string? frontierId = null)
    {
        var owner = frontierId ?? shown?.Invoke();

        lock (_gate)
        {
            return
            [
                .. _routes
                    .Where(route => string.Equals(route.FrontierId, owner, StringComparison.Ordinal))
                    .OrderByDescending(route => route.PlottedAt),
            ];
        }
    }

    /// <summary>One of the shown Commander's saved routes, or null when there is none by that id.</summary>
    public SavedRoute? Get(string id, string? frontierId = null)
    {
        var owner = frontierId ?? shown?.Invoke();

        lock (_gate)
        {
            return _routes.FirstOrDefault(route =>
                string.Equals(route.Id, id, StringComparison.Ordinal)
                && string.Equals(route.FrontierId, owner, StringComparison.Ordinal));
        }
    }

    /// <summary>Keeps a copy of <paramref name="plan"/> under <paramref name="name"/>; a blank name is the plan's headline.</summary>
    public SavedRoute Save(StoredRoutePlan plan, string? name, DateTimeOffset at, string? frontierId = null)
    {
        var owner = frontierId ?? shown?.Invoke() ?? plan.FrontierId;
        var saved = new SavedRoute
        {
            Id = Guid.NewGuid().ToString("N"),
            FrontierId = owner,
            Name = string.IsNullOrWhiteSpace(name) ? plan.Headline : name.Trim(),
            SavedAt = at,
            Plan = plan with { FrontierId = owner, Reached = null, Bought = [] },
        };

        SavedRoute[] snapshot;
        long version;

        lock (_gate)
        {
            _routes.Add(saved);
            snapshot = [.. _routes];
            version = ++_version;
        }

        Write(snapshot, version);
        Changed?.Invoke();

        return saved;
    }

    /// <summary>Removes a saved route of the shown Commander; false when there is none by that id.</summary>
    public bool Delete(string id, string? frontierId = null)
    {
        var owner = frontierId ?? shown?.Invoke();
        SavedRoute[] snapshot;
        long version;

        lock (_gate)
        {
            var index = _routes.FindIndex(route =>
                string.Equals(route.Id, id, StringComparison.Ordinal)
                && string.Equals(route.FrontierId, owner, StringComparison.Ordinal));

            if (index < 0)
            {
                return false;
            }

            _routes.RemoveAt(index);
            snapshot = [.. _routes];
            version = ++_version;
        }

        Write(snapshot, version);
        Changed?.Invoke();

        return true;
    }

    /// <summary>Reads the file, if there is one.</summary>
    public void Load()
    {
        lock (_gate)
        {
            _routes.Clear();

            var text = files.ReadText(path);

            if (text is null)
            {
                return;
            }

            try
            {
                _routes.AddRange(
                    (JsonSerializer.Deserialize<SavedRoute[]>(text, Json) ?? []).Where(route => route.Id.Length > 0));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read {Path}; the next save will rewrite it.", path);
                _routes.Clear();
            }
        }
    }

    /// <summary>Writes <paramref name="snapshot"/> unless a newer version is already on disk. Called outside <see cref="_gate"/>.</summary>
    private void Write(SavedRoute[] snapshot, long version)
    {
        lock (_writeGate)
        {
            if (version <= _written)
            {
                return;
            }

            _written = version;

            try
            {
                files.WriteText(path, JsonSerializer.Serialize(snapshot, Json));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not write {Path}.", path);
            }
        }
    }
}
