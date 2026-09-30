using System.Collections.Frozen;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>What a beacon scan woke.</summary>
public enum CoreWaking
{
    /// <summary>The first beacon: every Guardian core but the Heretic.</summary>
    Cores,

    /// <summary>A second beacon, in another system: the Heretic.</summary>
    Heretic,
}

/// <summary>
/// Which Guardian cores are awake — <c>data/guardian-cores.json</c>. A <c>DataScanned</c> in a known beacon
/// system wakes them, matched on the system rather than the scan's <c>Type</c>, and nothing puts them back
/// to sleep.
/// </summary>
public sealed class GuardianCores
{
    /// <summary>The Guardian beacon systems, by id64 from EDSM.</summary>
    public static readonly FrozenDictionary<long, string> Beacons = new Dictionary<long, string>
    {
        [13872878396833] = "IC 2391 Sector MX-T b3-6",
        [4208161886922] = "Synuefe IL-N c23-15",
        [182443805035] = "Synuefe IT-F d12-5",
        [9476442170745] = "Synuefe KU-F b44-4",
        [9476979041665] = "Synuefe QA-E b45-4",
        [13874757182857] = "Synuefe RL-C b46-6",
        [869621795163] = "NGC 2451A Sector LX-U d2-25",
    }.ToFrozenDictionary();

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string? _path;
    private readonly ILogger? _logger;
    private readonly Lock _gate = new();

    private Document _document;
    private JournalLocation _where = JournalLocation.Unknown;

    private GuardianCores(string? path, ILogger? logger, Document document)
    {
        _path = path;
        _logger = logger;
        _document = document;
    }

    /// <summary>Every core awake and nothing written: the host for tests, the designer and the replay harness.</summary>
    public static GuardianCores AllAwake() => new(null, null, new Document { Inherited = true });

    /// <summary>Asleep and held in memory only.</summary>
    public static GuardianCores Asleep() => new(null, null, new Document());

    /// <summary>
    /// Reads the file, or writes it on this version's first run: awake for an install that already had
    /// settings, asleep for a new one.
    /// </summary>
    public static GuardianCores Open(string path, bool installExisted, ILogger<GuardianCores> logger)
    {
        if (Read(path, logger) is { } document)
        {
            return new GuardianCores(path, logger, document);
        }

        var cores = new GuardianCores(path, logger, new Document { Inherited = installExisted });
        cores.Save();

        logger.LogInformation(
            "No {File}; the Guardian cores start {State}",
            System.IO.Path.GetFileName(path),
            installExisted ? "awake, for an existing install" : "asleep");

        return cores;
    }

    /// <summary>Raised on the thread that applied the event, once per waking.</summary>
    public event Action<CoreWaking>? Woke;

    /// <summary>Whether the first beacon has been scanned.</summary>
    public bool CoresAwake
    {
        get
        {
            lock (_gate)
            {
                return _document.Inherited || _document.Scanned.Count >= 1;
            }
        }
    }

    /// <summary>Whether beacons in two different systems have been scanned.</summary>
    public bool HereticAwake
    {
        get
        {
            lock (_gate)
            {
                return _document.Inherited || _document.Scanned.Count >= 2;
            }
        }
    }

    /// <summary>Whether this core can be aboard. Anything that is not a Guardian core always can.</summary>
    public bool IsAwake(Persona persona) =>
        !PersonaCatalog.IsGuardian(persona.Id) || (persona.Unlockable ? HereticAwake : CoresAwake);

    /// <summary>The core itself where it is awake, and the stock core where it is not.</summary>
    public Persona Admit(Persona persona) => IsAwake(persona) ? persona : PersonaCatalog.Covas;

    /// <summary>What is said, once, as the cores wake.</summary>
    public static string Line(CoreWaking waking) => waking switch
    {
        CoreWaking.Heretic =>
            "Data link complete. This beacon held one more core, kept apart from the others. "
            + "The Heretic is awake, and can be chosen in the Persona section.",
        _ =>
            "Data link complete. Something came across with the data: Guardian cores, awake in "
            + "the ship's systems. You can choose one in the Persona section.",
    };

    /// <summary>Folds one event, waking cores on a data-link scan in a beacon system.</summary>
    public void Apply(JournalEvent journalEvent)
    {
        CoreWaking? woke = null;
        string? system = null;

        lock (_gate)
        {
            _where = _where.Apply(journalEvent);

            if (journalEvent.Kind != "DataScanned"
                || _where.SystemAddress is not { } address
                || !Beacons.ContainsKey(address)
                || _document.Scanned.Contains(address))
            {
                return;
            }

            var before = _document.Scanned.Count;
            system = Beacons[address];
            _document = _document with { Scanned = [.. _document.Scanned, address] };

            if (!_document.Inherited)
            {
                woke = before switch
                {
                    0 => CoreWaking.Cores,
                    1 => CoreWaking.Heretic,
                    _ => null,
                };
            }

            Save();
        }

        if (woke is { } waking)
        {
            _logger?.LogInformation("A beacon scan in {System} woke {Waking}", system, waking);
            Woke?.Invoke(waking);
        }
    }

    private void Save()
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(_document, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogError(ex, "Could not write {Path}", _path);
        }
    }

    private static Document? Read(string path, ILogger logger)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Json) ?? new Document();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Unreadable reads as awake, so a damaged file never takes cores away from the Commander.
            logger.LogError(ex, "Could not read {Path}; treating the Guardian cores as awake", path);
            return new Document { Inherited = true };
        }
    }

    /// <summary>The file's shape.</summary>
    private sealed record Document
    {
        /// <summary>Awake before beacons were needed: this install had settings when this version first ran.</summary>
        public bool Inherited { get; init; }

        /// <summary>The beacon systems scanned, in order.</summary>
        public IReadOnlyList<long> Scanned { get; init; } = [];
    }
}
