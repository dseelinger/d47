using Microsoft.Extensions.Logging;
using D47.Core.Storage;

namespace D47.Core.Journal;

/// <summary>How the Commander met another Commander.</summary>
[Flags]
public enum EncounterKind
{
    None = 0,
    Targeted = 1,
    InterdictedBy = 2,
    EscapedInterdictionBy = 4,
    Interdicted = 8,
    Crime = 16,
    KilledBy = 32,
    Killed = 64,
    Winged = 128,
    Messaged = 256,
}

/// <summary>
/// Every meeting with one Commander in one system in one game session. <paramref name="Session"/> is the
/// time of the session's <c>LoadGame</c>.
/// </summary>
public sealed record CommanderEncounter(
    string Name,
    string System,
    string? Ship,
    string? CrimeType,
    EncounterKind Kinds,
    DateTimeOffset First,
    DateTimeOffset Last,
    DateTimeOffset Session);

/// <summary>One Commander met, with the newest meeting and how many meetings there were.</summary>
public sealed record CommanderMet(string Name, string Key, DateTimeOffset LastSeen, int Encounters);

/// <summary>
/// The other Commanders each Commander has met, keyed by Frontier id. Rebuilt from the journals on every
/// start and extended by live events; holds no file and no message text.
/// </summary>
public sealed class CommanderLog(IFileSystem fileSystem, ILogger logger)
{
    private static readonly string[] Events =
    [
        "Commander", "LoadGame", "GameModeChange", "Fileheader", "Shutdown", "Location", "FSDJump", "CarrierJump", "ShipTargeted", "Interdicted",
        "EscapeInterdiction", "Interdiction", "CrimeVictim", "Died", "PVPKill", "WingJoin", "WingAdd", "ReceiveText",
    ];

    private readonly Lock _gate = new();

    private readonly Dictionary<string, Dictionary<(string Key, string System, DateTimeOffset Session), CommanderEncounter>> _byCommander =
        new(StringComparer.Ordinal);

    private string? _commander;

    private string _system = string.Empty;

    private DateTimeOffset _session = DateTimeOffset.MinValue;

    private (string Fid, CommanderEncounter Encounter)? _lastTarget;

    private bool _historyFolded;

    /// <summary>Whether the journals older than this session have been folded in.</summary>
    public bool HistoryFolded
    {
        get
        {
            lock (_gate)
            {
                return _historyFolded;
            }
        }
    }

    /// <summary>The name key every event is compared by: no leading <c>CMDR </c>, case ignored.</summary>
    public static string KeyOf(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.StartsWith("CMDR ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[5..].TrimStart();
        }

        return trimmed.ToUpperInvariant();
    }

    /// <summary>The encounters with one Commander under a Frontier id, newest first.</summary>
    public IReadOnlyList<CommanderEncounter> Encounters(string fid, string name)
    {
        ArgumentNullException.ThrowIfNull(fid);
        ArgumentNullException.ThrowIfNull(name);

        var key = KeyOf(name);

        lock (_gate)
        {
            return _byCommander.TryGetValue(fid, out var book)
                ? [.. book.Where(pair => pair.Key.Key == key).Select(pair => pair.Value).OrderByDescending(e => e.Last)]
                : [];
        }
    }

    /// <summary>The Commanders met under a Frontier id, newest first.</summary>
    public IReadOnlyList<CommanderMet> Met(string fid)
    {
        ArgumentNullException.ThrowIfNull(fid);

        lock (_gate)
        {
            return _byCommander.TryGetValue(fid, out var book)
                ? [.. book.GroupBy(pair => pair.Key.Key, StringComparer.Ordinal)
                    .Select(group =>
                    {
                        var newest = group.Select(pair => pair.Value).MaxBy(e => e.Last)!;
                        return new CommanderMet(newest.Name, group.Key, newest.Last, group.Count());
                    })
                    .OrderByDescending(met => met.LastSeen)]
                : [];
        }
    }

    /// <summary>Whether the encounter ended in an earlier game session than the current one.</summary>
    public bool IsBeforeThisSession(CommanderEncounter encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);

        lock (_gate)
        {
            return encounter.Session < _session;
        }
    }

    /// <summary>The Commander targeted most recently in the current session, for the Commander flying.</summary>
    public CommanderEncounter? LastTargeted()
    {
        lock (_gate)
        {
            return _lastTarget is { } target && target.Fid == _commander && target.Encounter.Session == _session
                ? target.Encounter
                : null;
        }
    }

    /// <summary>Folds live events; a synchronous append with no I/O.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            foreach (var journalEvent in events)
            {
                Fold(journalEvent);
            }
        }
    }

    /// <summary>Folds journal files oldest first, on the calling thread.</summary>
    public void FoldHistory(IReadOnlyList<string> files, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            Stream? stream;

            try
            {
                stream = fileSystem.OpenRead(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read {File} for the Commander log", file);
                continue;
            }

            if (stream is null)
            {
                continue;
            }

            using (stream)
            {
                Fold(ReadLines(stream));
            }
        }

        lock (_gate)
        {
            _historyFolded = true;
        }
    }

    /// <summary>Folds journal lines in order.</summary>
    public void Fold(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var events = new List<JournalEvent>();

        foreach (var line in lines)
        {
            if (Relevant(line) && JournalEvent.TryParse(line, logger, out var parsed) && parsed is not null)
            {
                events.Add(parsed);
            }
        }

        Apply([.. OperationMarker.Mark(events)]);
    }

    private static IEnumerable<string> ReadLines(Stream stream)
    {
        using var reader = new StreamReader(stream);

        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static bool Relevant(string line)
    {
        foreach (var name in Events)
        {
            if (line.Contains("\"event\":\"" + name + "\"", StringComparison.Ordinal))
            {
                return name != "ShipTargeted" || line.Contains("cmdr_decorate", StringComparison.Ordinal);
            }
        }

        return false;
    }

    private void Fold(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "Commander" or "LoadGame":
                if (journalEvent.String("FID") is { Length: > 0 } fid)
                {
                    _commander = fid;
                }

                if (journalEvent.Kind == "LoadGame")
                {
                    _system = string.Empty;
                }

                if (journalEvent.StartsSession)
                {
                    _session = journalEvent.Timestamp;
                }

                break;

            case "Location" or "FSDJump" or "CarrierJump":
                if (!journalEvent.InOperation && journalEvent.String("StarSystem") is { } system)
                {
                    _system = system;
                }

                break;

            case "ShipTargeted":
                if (journalEvent.String("PilotName") is { } pilot && PilotNameOf(pilot) is { } targeted)
                {
                    Meet(journalEvent, targeted, EncounterKind.Targeted, ship: journalEvent.String("Ship_Localised"));
                }

                break;

            case "Interdicted":
                MeetPlayer(journalEvent, "Interdictor", EncounterKind.InterdictedBy);
                break;

            case "EscapeInterdiction":
                MeetPlayer(journalEvent, "Interdictor", EncounterKind.EscapedInterdictionBy);
                break;

            case "Interdiction":
                MeetPlayer(journalEvent, "Interdicted", EncounterKind.Interdicted);
                break;

            case "CrimeVictim":
                Meet(journalEvent, journalEvent.String("Offender"), EncounterKind.Crime, crime: journalEvent.String("CrimeType"));
                break;

            case "Died":
                MeetKiller(journalEvent, journalEvent.String("KillerName"));

                foreach (var killer in journalEvent.Items("Killers"))
                {
                    MeetKiller(journalEvent, killer.String("Name"));
                }

                break;

            case "PVPKill":
                Meet(journalEvent, journalEvent.String("Victim"), EncounterKind.Killed);
                break;

            case "WingJoin":
                foreach (var other in journalEvent.Items("Others"))
                {
                    Meet(journalEvent, other.ValueKind == System.Text.Json.JsonValueKind.String ? other.GetString() : null, EncounterKind.Winged);
                }

                break;

            case "WingAdd":
                Meet(journalEvent, journalEvent.String("Name"), EncounterKind.Winged);
                break;

            case "ReceiveText" when journalEvent.String("Channel") is "player" or "wing":
                Meet(journalEvent, journalEvent.String("From"), EncounterKind.Messaged);
                break;
        }
    }

    /// <summary>The Commander named in a <c>ShipTargeted</c> pilot name, or null for an NPC.</summary>
    public static string? PilotNameOf(string pilotName)
    {
        if (!pilotName.Contains("$cmdr_decorate", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        const string Marker = "#name=";
        var start = pilotName.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);

        if (start < 0)
        {
            return null;
        }

        start += Marker.Length;
        var end = pilotName.IndexOf(';', start);
        return end < 0 ? pilotName[start..] : pilotName[start..end];
    }

    private void MeetPlayer(JournalEvent journalEvent, string nameField, EncounterKind kind)
    {
        if (journalEvent.Bool("IsPlayer"))
        {
            Meet(journalEvent, journalEvent.String(nameField), kind);
        }
    }

    private void MeetKiller(JournalEvent journalEvent, string? name)
    {
        if (name is not null && name.TrimStart().StartsWith("Cmdr ", StringComparison.OrdinalIgnoreCase))
        {
            Meet(journalEvent, name, EncounterKind.KilledBy);
        }
    }

    private void Meet(JournalEvent journalEvent, string? rawName, EncounterKind kind, string? ship = null, string? crime = null)
    {
        if (_commander is not { } fid || rawName is null)
        {
            return;
        }

        var key = KeyOf(rawName);

        if (key.Length == 0)
        {
            return;
        }

        var name = rawName.Trim();

        if (name.StartsWith("CMDR ", StringComparison.OrdinalIgnoreCase))
        {
            name = name[5..].TrimStart();
        }

        if (!_byCommander.TryGetValue(fid, out var book))
        {
            book = [];
            _byCommander[fid] = book;
        }

        var at = journalEvent.Timestamp;
        var slot = (key, _system, _session);

        var encounter = book.TryGetValue(slot, out var held)
            ? held with
            {
                Ship = held.Ship ?? ship,
                CrimeType = held.CrimeType ?? crime,
                Kinds = held.Kinds | kind,
                First = at < held.First ? at : held.First,
                Last = at > held.Last ? at : held.Last,
            }
            : new CommanderEncounter(name, _system, ship, crime, kind, at, at, _session);

        book[slot] = encounter;

        if (kind == EncounterKind.Targeted
            && (_lastTarget is not { } target || target.Encounter.Session < _session || target.Encounter.Last <= at))
        {
            _lastTarget = (fid, encounter);
        }
    }
}
