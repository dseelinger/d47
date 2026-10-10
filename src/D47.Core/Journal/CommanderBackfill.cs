using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>Where a Commander was last seen in the journals.</summary>
public sealed record CommanderSighting(
    string FrontierId,
    string Name,
    DateTimeOffset LastSeen,
    string? StarSystem,
    string? Ship);

/// <summary>Every Commander found in the journal files, one sighting each, newest-first.</summary>
public static class CommanderBackfill
{
    private const string FidKey = "\"FID\":\"";

    /// <summary>
    /// Each Commander's sighting from their newest file in the window <see cref="FleetBackfill"/> uses,
    /// and the number of files in that window.
    /// </summary>
    public static (IReadOnlyDictionary<string, CommanderSighting> Commanders, int FilesExamined) FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var found = new Dictionary<string, CommanderSighting>(StringComparer.Ordinal);
        var floor = Math.Max(0, files.Count - FleetBackfill.MaxLookback);

        for (var i = files.Count - 1; i >= floor; i--)
        {
            cancellation.ThrowIfCancellationRequested();

            if (Owner(fileSystem, files[i], logger) is not { } owner || found.ContainsKey(owner))
            {
                continue;
            }

            if (Read(fileSystem, files[i], owner, logger) is { } sighting)
            {
                found[owner] = sighting;
            }
        }

        return (found, files.Count - floor);
    }

    private static CommanderSighting? Read(IFileSystem fileSystem, string file, string fid, ILogger logger)
    {
        string? name = null;
        string? system = null;
        ShipLoadout ship = ShipLoadout.Unknown;
        var lastSeen = DateTimeOffset.MinValue;

        var reader = new JournalReader(file, fileSystem, logger);

        while (reader.Poll() is { Count: > 0 } batch)
        {
            foreach (var journalEvent in batch)
            {
                lastSeen = journalEvent.Timestamp;

                switch (journalEvent.Kind)
                {
                    case "Commander":
                        name = journalEvent.String("Name") ?? name;
                        break;

                    case "LoadGame":
                        name = journalEvent.String("Commander") ?? name;
                        ship = ship with
                        {
                            Type = journalEvent.String("Ship") ?? ship.Type,
                            TypeName = journalEvent.Named("Ship") ?? ship.TypeName,
                        };
                        break;

                    case "Loadout":
                        ship = ship with
                        {
                            Type = journalEvent.String("Ship") ?? ship.Type,
                            TypeName = journalEvent.Named("Ship") ?? ship.TypeName,
                        };
                        break;

                    case "Location" or "FSDJump" or "CarrierJump" or "Docked":
                        system = journalEvent.String("StarSystem") ?? system;
                        break;
                }
            }
        }

        return name is null ? null : new CommanderSighting(fid, name, lastSeen, system, ship.TypeSaid);
    }

    /// <summary>The FID on the first line that carries one, or null for an unreadable or ownerless file.</summary>
    private static string? Owner(IFileSystem fileSystem, string file, ILogger logger)
    {
        try
        {
            using var stream = fileSystem.OpenRead(file) ?? throw new FileNotFoundException("The journal file is missing.", file);

            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                var at = line.IndexOf(FidKey, StringComparison.Ordinal);

                if (at < 0)
                {
                    continue;
                }

                var start = at + FidKey.Length;
                var end = line.IndexOf('"', start);

                if (end > start)
                {
                    return line[start..end];
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for its Commander", file);
        }

        return null;
    }
}
