using System.Text.Json;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Watches;

/// <summary>One watch the file was asked to hold, and why it was refused.</summary>
public sealed record WatchProblem(string Where, string Reason);

/// <summary>The Commander's background watches, <c>data\watches.json</c>.</summary>
public sealed class WatchStore(string path, IFileSystem files, ILogger<WatchStore> logger)
{
    private static readonly JsonSerializerOptions Json = StoreJson.OmittingNulls;

    /// <summary>The most watches one file may hold.</summary>
    public const int MaxWatches = 32;

    private readonly Lock _gate = new();

    private IReadOnlyList<Watch> _watches = [];
    private IReadOnlyList<WatchProblem> _problems = [];

    private readonly WatchedTextFile _file = new(files, path, logger, "Could not read the watch file");

    /// <summary>Raised when the set changed, whoever wrote it.</summary>
    public event Action? Changed;

    public IReadOnlyList<Watch> Watches
    {
        get
        {
            lock (_gate)
            {
                return _watches;
            }
        }
    }

    /// <summary>Watches that were refused, and why.</summary>
    public IReadOnlyList<WatchProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll() => _file.Poll(
        () =>
        {
            lock (_gate)
            {
                _watches = [];
                _problems = [];
            }

            Changed?.Invoke();
        },
        Reload);

    /// <summary>Adds a watch; returns why it was refused, or null when it was written.</summary>
    public string? Add(Watch watch)
    {
        ArgumentNullException.ThrowIfNull(watch);

        Poll();

        if (Refusal(watch) is { } reason)
        {
            return reason;
        }

        IReadOnlyList<Watch> next;

        lock (_gate)
        {
            if (_watches.Count >= MaxWatches)
            {
                return $"the file already holds {MaxWatches} watches.";
            }

            if (_watches.Any(existing => existing.Id == watch.Id))
            {
                return "a watch with that id is already held.";
            }

            next = [.. _watches, watch];
        }

        Save(next);
        return null;
    }

    /// <summary>
    /// Replaces the watch with the same id; returns why it was refused, or null when it was written.
    /// The stored firing is kept unless the kind, market, commodity, comparison or threshold changed.
    /// </summary>
    public string? Update(Watch watch)
    {
        ArgumentNullException.ThrowIfNull(watch);

        Poll();

        if (Refusal(watch) is { } reason)
        {
            return reason;
        }

        IReadOnlyList<Watch> next;

        lock (_gate)
        {
            if (_watches.FirstOrDefault(existing => existing.Id == watch.Id) is not { } stored)
            {
                return "no watch has that id.";
            }

            var retriggered = stored.Kind != watch.Kind
                || stored.MarketId != watch.MarketId
                || !string.Equals(stored.Commodity, watch.Commodity, StringComparison.Ordinal)
                || stored.Comparison != watch.Comparison
                || stored.Threshold != watch.Threshold;

            var updated = watch with { FiredForTick = retriggered ? null : stored.FiredForTick };
            next = [.. _watches.Select(existing => existing.Id == watch.Id ? updated : existing)];
        }

        Save(next);
        return null;
    }

    /// <summary>Removes the watch; false when no watch has that id.</summary>
    public bool Delete(Guid id)
    {
        Poll();

        IReadOnlyList<Watch> next;

        lock (_gate)
        {
            if (_watches.All(existing => existing.Id != id))
            {
                return false;
            }

            next = [.. _watches.Where(existing => existing.Id != id)];
        }

        Save(next);
        return true;
    }

    /// <summary>Records the BGS tick the watch notified for; false when no watch has that id.</summary>
    public bool MarkFired(Guid id, DateTimeOffset tick)
    {
        Poll();

        var stamp = BgsTickWatch.Format(tick);
        IReadOnlyList<Watch> next;

        lock (_gate)
        {
            if (_watches.All(existing => existing.Id != id))
            {
                return false;
            }

            next = [.. _watches.Select(existing => existing.Id == id ? existing with { FiredForTick = stamp } : existing)];
        }

        Save(next);
        return true;
    }

    /// <summary>Why a watch cannot be held, or null when it can.</summary>
    public static string? Refusal(Watch watch)
    {
        ArgumentNullException.ThrowIfNull(watch);

        if (watch.Kind != WatchKind.Stock)
        {
            return null;
        }

        if (watch.MarketId is not > 0)
        {
            return "it has no market ID.";
        }

        if (string.IsNullOrWhiteSpace(watch.Commodity))
        {
            return "it names no commodity.";
        }

        if (watch.Comparison is null)
        {
            return "it says neither above nor below.";
        }

        if (watch.Threshold is not { } threshold)
        {
            return "it has no threshold.";
        }

        return threshold < 0 ? "the threshold is negative." : null;
    }

    private void Save(IReadOnlyList<Watch> watches)
    {
        var file = new WatchFile
        {
            Watches = [.. watches.Select(watch => new WatchLine
            {
                Id = watch.Id,
                Name = watch.Name,
                Enabled = watch.Enabled,
                Kind = watch.Kind,
                MarketId = watch.MarketId,
                Station = watch.Station,
                System = watch.System,
                Commodity = watch.Commodity,
                Comparison = watch.Comparison,
                Threshold = watch.Threshold,
                FiredForTick = watch.FiredForTick,
            })],
        };

        var text = JsonSerializer.Serialize(file, Json);

        try
        {
            files.WriteText(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write the watch file");
            return;
        }

        Reload(text);
    }

    private bool Reload(string text)
    {
        WatchFile? file;

        try
        {
            file = JsonSerializer.Deserialize<WatchFile>(text, Json);
        }
        catch (JsonException ex)
        {
            lock (_gate)
            {
                _watches = [];
                _problems = [new WatchProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _file.Seen(text);
            }

            logger.LogWarning(ex, "The watch file could not be read");
            Changed?.Invoke();
            return true;
        }

        var watches = new List<Watch>();
        var problems = new List<WatchProblem>();

        foreach (var line in file?.Watches ?? [])
        {
            var name = (line.Name ?? string.Empty).Trim();
            var where = name.Length > 0 ? name : "a watch";

            if (line.Kind is not { } kind)
            {
                problems.Add(new WatchProblem(where, "it has no kind."));
                continue;
            }

            var id = line.Id is { } given && given != Guid.Empty ? given : Guid.NewGuid();

            if (watches.Any(existing => existing.Id == id))
            {
                problems.Add(new WatchProblem(where, "another watch has the same id."));
                continue;
            }

            var watch = new Watch(
                id,
                name,
                kind,
                line.Enabled ?? true,
                line.MarketId,
                line.Station,
                line.System,
                line.Commodity?.Trim(),
                line.Comparison,
                line.Threshold,
                line.FiredForTick);

            if (Refusal(watch) is { } reason)
            {
                problems.Add(new WatchProblem(where, reason));
                continue;
            }

            if (watches.Count >= MaxWatches)
            {
                problems.Add(new WatchProblem(where, $"the file already holds {MaxWatches} watches."));
                continue;
            }

            watches.Add(watch);
        }

        lock (_gate)
        {
            _watches = watches;
            _problems = problems;
            _file.Seen(text);
        }

        Changed?.Invoke();
        return true;
    }

    private sealed record WatchFile
    {
        public IReadOnlyList<WatchLine> Watches { get; init; } = [];
    }

    private sealed record WatchLine
    {
        public Guid? Id { get; init; }

        public string? Name { get; init; }

        public bool? Enabled { get; init; }

        public WatchKind? Kind { get; init; }

        public long? MarketId { get; init; }

        public string? Station { get; init; }

        public string? System { get; init; }

        public string? Commodity { get; init; }

        public StockComparison? Comparison { get; init; }

        public int? Threshold { get; init; }

        public string? FiredForTick { get; init; }
    }
}
