using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Adventures;

/// <summary>
/// Counts, per Commander, the objectives they replaced with <b>Change objective</b>, by <see cref="RefusedActivities"/> key.
/// An activity replaced <see cref="Threshold"/> times is barred from every later story chapter and short story.
/// </summary>
public sealed class ActivityBars(string? path, IFileSystem fileSystem, ILogger logger)
{
    /// <summary>The replacements of one activity after which it is barred.</summary>
    public const int Threshold = 5;

    private sealed class DataFile
    {
        public Dictionary<string, Dictionary<string, int>> Changed { get; set; } = [];
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Dictionary<string, int>> _changed = new(StringComparer.Ordinal);

    /// <summary>Reads the counts from <c>path</c>.</summary>
    public void Load()
    {
        if (path is null)
        {
            return;
        }

        try
        {
            if (fileSystem.ReadText(path) is not { } text)
            {
                return;
            }

            var file = JsonSerializer.Deserialize<DataFile>(text, Json);

            lock (_gate)
            {
                foreach (var (commander, counts) in file?.Changed ?? [])
                {
                    _changed[commander] = new Dictionary<string, int>(counts, StringComparer.Ordinal);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; no activity is barred", path);
        }
    }

    /// <summary>Adds one replacement of <paramref name="key"/> for the Commander and writes the counts.</summary>
    public void Count(string? commander, string key)
    {
        DataFile file;

        lock (_gate)
        {
            var counts = Open(commander);
            counts[key] = counts.GetValueOrDefault(key) + 1;
            file = new DataFile { Changed = _changed.ToDictionary(pair => pair.Key, pair => new Dictionary<string, int>(pair.Value), StringComparer.Ordinal) };
        }

        if (path is null)
        {
            return;
        }

        try
        {
            fileSystem.WriteText(path, JsonSerializer.Serialize(file, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write {Path}; the counts hold until d47 restarts", path);
        }
    }

    /// <summary>How many times the Commander has replaced an objective for <paramref name="key"/>.</summary>
    public int Changed(string? commander, string key)
    {
        lock (_gate)
        {
            return _changed.GetValueOrDefault(commander ?? string.Empty)?.GetValueOrDefault(key) ?? 0;
        }
    }

    /// <summary>The keys the Commander has replaced <see cref="Threshold"/> times or more.</summary>
    public IReadOnlyList<string> Barred(string? commander)
    {
        lock (_gate)
        {
            return
            [
                .. _changed.GetValueOrDefault(commander ?? string.Empty, [])
                    .Where(pair => pair.Value >= Threshold)
                    .Select(pair => pair.Key)
                    .Order(StringComparer.Ordinal),
            ];
        }
    }

    private Dictionary<string, int> Open(string? commander)
    {
        var id = commander ?? string.Empty;

        if (!_changed.TryGetValue(id, out var counts))
        {
            counts = new Dictionary<string, int>(StringComparer.Ordinal);
            _changed[id] = counts;
        }

        return counts;
    }
}
