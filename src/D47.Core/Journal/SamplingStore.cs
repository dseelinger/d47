using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>What has been sampled on each body, kept between sessions (Phase 18, "Exobiology sampling").</summary>
public sealed class SamplingStore(string path, ILogger<SamplingStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _gate = new();

    private Dictionary<string, OrganicSampling> _byCommander = new(StringComparer.Ordinal);

    public string Path => path;

    /// <summary>What was on disk for this Commander, or null if nothing was.</summary>
    public OrganicSampling? For(string frontierId)
    {
        lock (_gate)
        {
            return _byCommander.GetValueOrDefault(frontierId);
        }
    }

    /// <summary>Reads the file.</summary>
    public void Load()
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Json);

            var loaded = new Dictionary<string, OrganicSampling>(StringComparer.Ordinal);

            foreach (var commander in document?.Commanders ?? [])
            {
                if (string.IsNullOrWhiteSpace(commander.FrontierId))
                {
                    continue;
                }

                loaded[commander.FrontierId] = Rehydrate(commander);
            }

            lock (_gate)
            {
                _byCommander = loaded;
            }

            logger.LogInformation("Loaded sampling history for {Count} Commanders", loaded.Count);
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            // Discarded rather than refused.
            logger.LogWarning(ex, "Could not read {Path}; starting with no sampling history", path);
        }
    }

    /// <summary>Writes every Commander's state through <see cref="AtomicFile"/>.</summary>
    public void Save(IEnumerable<CommanderGameState> commanders)
    {
        var document = new Document
        {
            Commanders =
            [
                .. commanders
                    .Where(commander => commander.Sampling.IsKnown)
                    .Select(commander => Dehydrate(commander.Identity.FrontierId, commander.Sampling)),
            ],
        };

        if (document.Commanders.Count == 0)
        {
            return;
        }

        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(document, Json));
        }
        catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(ex, "Could not write {Path}", path);
        }
    }

    private static OrganicSampling Rehydrate(CommanderRecord record)
    {
        var sampling = OrganicSampling.Empty;

        foreach (var body in record.Bodies ?? [])
        {
            foreach (var genus in body.Genera ?? [])
            {
                sampling = sampling.Restore(
                    body.SystemAddress,
                    body.BodyId,
                    new GenusProgress(genus.Genus)
                    {
                        Species = genus.Species,
                        Taken = genus.Taken,
                        Complete = genus.Complete,
                        SeenAt = genus.SeenAt,

                        // The position is restored too, so a Commander who logs back in beside their last
                        // specimen is told how far they have moved rather than being told nothing.
                        LastAt = Fix(genus.Latitude, genus.Longitude, genus.Radius),

                        // A save older than the per-specimen positions holds only the last one.
                        Specimens = genus.Specimens is { } specimens
                            ? [.. specimens.Select(point => Fix(point.Latitude, point.Longitude, point.Radius)).OfType<SurfaceFix>()]
                            : Fix(genus.Latitude, genus.Longitude, genus.Radius) is { } last ? [last] : [],
                    });
            }
        }

        return sampling;
    }

    private static SurfaceFix? Fix(double? latitude, double? longitude, double? radius) =>
        latitude is { } lat && longitude is { } lon && radius is { } r ? new SurfaceFix(lat, lon, r) : null;

    private static CommanderRecord Dehydrate(string frontierId, OrganicSampling sampling) => new()
    {
        FrontierId = frontierId,
        Bodies =
        [
            .. sampling.All.Select(body => new BodyRecord
            {
                SystemAddress = body.SystemAddress,
                BodyId = body.BodyId,
                Genera =
                [
                    .. body.Genera.Values.Select(genus => new GenusRecord
                    {
                        Genus = genus.Genus,
                        Species = genus.Species,
                        Taken = genus.Taken,
                        Complete = genus.Complete,
                        SeenAt = genus.SeenAt,
                        Latitude = genus.LastAt?.Latitude,
                        Longitude = genus.LastAt?.Longitude,
                        Radius = genus.LastAt?.RadiusMetres,
                        Specimens =
                        [
                            .. genus.Specimens.Select(point => new PointRecord
                            {
                                Latitude = point.Latitude,
                                Longitude = point.Longitude,
                                Radius = point.RadiusMetres,
                            }),
                        ],
                    }),
                ],
            }),
        ],
    };

    private sealed class Document
    {
        public IReadOnlyList<CommanderRecord> Commanders { get; set; } = [];
    }

    private sealed class CommanderRecord
    {
        public string FrontierId { get; set; } = string.Empty;

        public IReadOnlyList<BodyRecord>? Bodies { get; set; }
    }

    private sealed class BodyRecord
    {
        public long SystemAddress { get; set; }

        public int BodyId { get; set; }

        public IReadOnlyList<GenusRecord>? Genera { get; set; }
    }

    private sealed class GenusRecord
    {
        public string Genus { get; set; } = string.Empty;

        public string? Species { get; set; }

        public int Taken { get; set; }

        public bool Complete { get; set; }

        public DateTimeOffset SeenAt { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double? Radius { get; set; }

        public IReadOnlyList<PointRecord>? Specimens { get; set; }
    }

    private sealed class PointRecord
    {
        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double? Radius { get; set; }
    }
}
