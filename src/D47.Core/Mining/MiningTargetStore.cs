using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Mining;

/// <summary>Each Commander's mining target, by Frontier id, kept between sessions until cleared.</summary>
public sealed class MiningTargetStore(string path, ILogger<MiningTargetStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    /// <summary>Held across the read that decides a change and the write that applies it.</summary>
    private readonly Lock _writeGate = new();

    private Dictionary<string, MiningTarget> _byCommander = new(StringComparer.Ordinal);

    public string Path => path;

    /// <summary>This Commander's target, or null if none is set.</summary>
    public MiningTarget? For(string? frontierId)
    {
        if (string.IsNullOrEmpty(frontierId))
        {
            return null;
        }

        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId);
        }
    }

    public void Load()
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Json);
            var loaded = new Dictionary<string, MiningTarget>(StringComparer.Ordinal);

            foreach (var commander in document?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander.FrontierId)
                    || MiningTarget.Match(commander.Material ?? string.Empty) is not { } material)
                {
                    continue;
                }

                loaded[commander.FrontierId] = new MiningTarget(material, commander.Percent);
            }

            lock (_gate)
            {
                _byCommander = loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not read {Path}; starting with no mining targets", path);
        }
    }

    public void Set(string frontierId, MiningTarget target) =>
        Change(merged => merged[frontierId] = target);

    /// <summary>False where this Commander had no target to clear.</summary>
    public bool Clear(string frontierId)
    {
        lock (_writeGate)
        {
            if (For(frontierId) is null)
            {
                return false;
            }

            Change(merged => merged.Remove(frontierId));

            return true;
        }
    }

    private void Change(Action<Dictionary<string, MiningTarget>> change)
    {
        lock (_writeGate)
        {
            Dictionary<string, MiningTarget> merged;

            lock (_gate)
            {
                merged = new Dictionary<string, MiningTarget>(_byCommander, StringComparer.Ordinal);
            }

            change(merged);

            lock (_gate)
            {
                _byCommander = merged;
            }

            var document = new Document
            {
                Commanders =
                [
                    .. merged.Select(entry => new CommanderRecord
                    {
                        FrontierId = entry.Key,
                        Material = entry.Value.Material,
                        Percent = entry.Value.Percent,
                    }),
                ],
            };

            try
            {
                AtomicFile.WriteAllText(path, JsonSerializer.Serialize(document, Json));
            }
            catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
            {
                logger.LogWarning(ex, "Could not write {Path}", path);
            }
        }
    }

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string FrontierId { get; set; } = string.Empty;

        public string? Material { get; set; }

        public double? Percent { get; set; }
    }
}
