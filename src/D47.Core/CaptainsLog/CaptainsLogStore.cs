using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.CaptainsLog;

/// <summary>One thing the Commander said to the log, with whatever the game state knew when they said it.</summary>
public sealed record CaptainsLogEntry(string Id, string Text, DateTimeOffset At)
{
    public string? System { get; init; }

    public string? Body { get; init; }

    public string? Station { get; init; }

    public string? ShipType { get; init; }

    public string? ShipName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}

/// <summary>The Commander's spoken log entries, per Frontier id, in <c>data\captains-log.json</c>.</summary>
public sealed class CaptainsLogStore(string path, IFileSystem files, ILogger<CaptainsLogStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private readonly Lock _writeGate = new();

    private Dictionary<string, IReadOnlyList<CaptainsLogEntry>> _byCommander = new(StringComparer.Ordinal);

    public string Path => path;

    /// <summary>Raised when an entry is added.</summary>
    public event Action? Changed;

    public void Load()
    {
        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return;
            }

            var document = JsonSerializer.Deserialize<Document>(text, Json);
            var loaded = new Dictionary<string, IReadOnlyList<CaptainsLogEntry>>(StringComparer.Ordinal);

            foreach (var commander in document?.Commanders ?? [])
            {
                if (!string.IsNullOrWhiteSpace(commander.FrontierId))
                {
                    loaded[commander.FrontierId] =
                        [.. (commander.Entries ?? []).Where(entry => !string.IsNullOrWhiteSpace(entry.Text))];
                }
            }

            lock (_gate)
            {
                _byCommander = loaded;
            }

            logger.LogInformation("Loaded the captain's log of {Count} Commanders", loaded.Count);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; log entries made before now are lost", path);
        }
    }

    /// <summary>This Commander's entries, oldest first.</summary>
    public IReadOnlyList<CaptainsLogEntry> For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId, []);
        }
    }

    /// <summary>Adds an entry. False when it could not be written.</summary>
    public bool Add(string frontierId, CaptainsLogEntry entry)
    {
        lock (_writeGate)
        {
            var merged = new Dictionary<string, IReadOnlyList<CaptainsLogEntry>>(_byCommander, StringComparer.Ordinal)
            {
                [frontierId] = [.. For(frontierId), entry],
            };

            var document = new Document
            {
                Commanders = [.. merged.Select(pair => new CommanderRecord { FrontierId = pair.Key, Entries = pair.Value })],
            };

            try
            {
                files.WriteText(path, JsonSerializer.Serialize(document, Json));
            }
            catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
            {
                logger.LogWarning(ex, "Could not write {Path}", path);
                return false;
            }

            lock (_gate)
            {
                _byCommander = merged;
            }
        }

        logger.LogInformation("Saved a captain's log entry");
        Changed?.Invoke();

        return true;
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string FrontierId { get; set; } = string.Empty;

        public IReadOnlyList<CaptainsLogEntry>? Entries { get; set; }
    }
}
