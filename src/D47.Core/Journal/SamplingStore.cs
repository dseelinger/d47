using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// What has been sampled on each body, kept between sessions (Phase 18, "Exobiology
/// sampling").
/// <para>
/// <b>The repository's first per-body state that has to outlive a session</b>, and the reason it has
/// to is mundane: a Commander logs off on a planet halfway through a run and comes back tomorrow.
/// Everything else d47 derives is refolded from the journal on the next start, but the spine tails
/// the newest file, so a run begun in yesterday's journal is simply gone.
/// </para>
/// <para>
/// <b>Deliberately much smaller than <see cref="Checklists.ChecklistStore"/>, and the difference is
/// what the file is for.</b> That one holds Commander-authored state, so it is polled for hand
/// edits and reports problems rather than dropping lines. This is derived state — every entry came
/// from a journal event — so a file that has gone bad is discarded with a log line and rebuilt by
/// playing, which is a recovery the other one cannot offer.
/// </para>
/// <para>
/// <b>Keyed per Commander with the key inside the document</b>, following the rule the checklist
/// established: the Frontier id comes out of the journal, journal content is untrusted input, and
/// turning it into a filename buys a path-traversal surface for an organisational convenience.
/// </para>
/// </summary>
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

    /// <summary>
    /// Reads the file. A missing file is the normal first run; an unreadable one is logged and
    /// treated as empty, because this is derived state and playing rebuilds it.
    /// </summary>
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
            // Discarded rather than refused. A corrupt checklist would lose the Commander's own
            // words; a corrupt sampling file loses a count they can rebuild by taking a sample.
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

                        // The position is restored too, so a Commander who logs back in beside their
                        // last specimen is told how far they have moved rather than being told
                        // nothing. Null where it was never known.
                        LastAt = genus is { Latitude: { } lat, Longitude: { } lon, Radius: { } radius }
                            ? new SurfaceFix(lat, lon, radius)
                            : null,
                    });
            }
        }

        return sampling;
    }

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
    }
}
