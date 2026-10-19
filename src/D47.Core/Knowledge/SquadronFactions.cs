using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>The minor faction each Commander has named for a squadron, kept at <c>data/squadron-factions.json</c>.</summary>
public sealed class SquadronFactions(string path, IFileSystem files, ILogger<SquadronFactions> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private Dictionary<string, Dictionary<string, string>> _byCommander = new(StringComparer.Ordinal);

    public void Load()
    {
        try
        {
            if (files.ReadText(path) is not { } text)
            {
                return;
            }

            var loaded = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            foreach (var commander in JsonSerializer.Deserialize<Document>(text, Json)?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander.FrontierId) || commander.Squadrons is null)
                {
                    continue;
                }

                loaded[commander.FrontierId] = new Dictionary<string, string>(
                    commander.Squadrons.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)),
                    StringComparer.Ordinal);
            }

            lock (_gate)
            {
                _byCommander = loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; no squadron factions are held", path);
        }
    }

    /// <summary>The faction named for the squadron <paramref name="squadron"/> is in, or null outside one or when none is named.</summary>
    public string? For(string? frontierId, SquadronState squadron)
    {
        ArgumentNullException.ThrowIfNull(squadron);

        if (frontierId is not { Length: > 0 } || squadron.Key is not { } key)
        {
            return null;
        }

        lock (_gate)
        {
            var squadrons = _byCommander.GetValueOrDefault(frontierId);

            return squadrons?.GetValueOrDefault(key)
                   ?? (squadron.Name is { } name ? squadrons?.GetValueOrDefault(name) : null);
        }
    }

    /// <summary>Stores <paramref name="faction"/> for the squadron; false when nobody is flying, there is no squadron or the write failed.</summary>
    public bool Set(string? frontierId, SquadronState squadron, string faction)
    {
        ArgumentNullException.ThrowIfNull(squadron);
        ArgumentException.ThrowIfNullOrWhiteSpace(faction);

        if (frontierId is not { Length: > 0 } || squadron.Key is not { } key)
        {
            return false;
        }

        lock (_gate)
        {
            var next = _byCommander.ToDictionary(
                pair => pair.Key, pair => new Dictionary<string, string>(pair.Value, StringComparer.Ordinal), StringComparer.Ordinal);

            if (!next.TryGetValue(frontierId, out var squadrons))
            {
                next[frontierId] = squadrons = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            squadrons[key] = faction.Trim();

            if (squadron.Name is { } name)
            {
                squadrons[name] = squadrons[key];
            }

            if (!Write(next))
            {
                return false;
            }

            _byCommander = next;
            return true;
        }
    }

    private bool Write(Dictionary<string, Dictionary<string, string>> byCommander)
    {
        try
        {
            var document = new Document
            {
                Commanders =
                [
                    .. byCommander
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new CommanderRecord { FrontierId = pair.Key, Squadrons = pair.Value }),
                ],
            };

            files.WriteText(path, JsonSerializer.Serialize(document, Json));
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
            return false;
        }
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord>? Commanders { get; set; }
    }

    private sealed class CommanderRecord
    {
        public string? FrontierId { get; set; }

        public Dictionary<string, string>? Squadrons { get; set; }
    }
}
