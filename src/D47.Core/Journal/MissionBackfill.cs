using System.Globalization;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Journal;

/// <summary>
/// The live missions and their detail, recovered from journals d47 was not running for. The latest
/// <c>Missions</c> snapshot on disk says what is live; older files are searched newest-first for each live
/// mission's accept, and the search stops once every one is found.
/// </summary>
public static class MissionBackfill
{
    private static readonly string[] Relevant = ["\"event\":\"Mission", "\"event\":\"CargoDepot\"", "\"event\":\"Commander\"", "\"event\":\"LoadGame\""];

    public static IReadOnlyDictionary<string, MissionBoard> FromHistory(
        IFileSystem files,
        string directory,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        if (files.FolderWritten(directory) is null)
        {
            logger.LogWarning("No journal folder at {Directory}", directory);
            return new Dictionary<string, MissionBoard>(StringComparer.Ordinal);
        }

        var journals = files.Enumerate(directory, JournalFolder.FilePattern)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();

        return FromHistory(journals, logger, path => ReadAll(files, path), cancellation);
    }

    /// <summary>The same, over an explicit list oldest-first.</summary>
    /// <param name="read">Reads one file's text, or returns null where it cannot be read.</param>
    public static IReadOnlyDictionary<string, MissionBoard> FromHistory(
        IReadOnlyList<string> files,
        ILogger logger,
        Func<string, string?> read,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(read);

        // Newest first; folded oldest-first once the search ends.
        var kept = new List<IReadOnlyList<JournalEvent>>();

        string? commander = null;
        HashSet<long>? wanted = null;
        var live = 0;
        var opened = 0;

        for (var i = files.Count - 1; i >= 0 && wanted is not { Count: 0 }; i--)
        {
            cancellation.ThrowIfCancellationRequested();

            if (read(files[i]) is not { } text)
            {
                continue;
            }

            opened++;

            if (wanted is null)
            {
                // Down to and including the file holding the latest snapshot, every mission event is kept.
                var events = Parse(text, _ => true, out var snapshot, out var fid);
                kept.Add(events);

                if (snapshot is null)
                {
                    continue;
                }

                commander = fid;

                var accepted = kept
                    .SelectMany(file => file)
                    .Where(journalEvent => journalEvent.Kind == "MissionAccepted")
                    .Select(journalEvent => journalEvent.Long("MissionID"))
                    .ToHashSet();

                var ids = snapshot.Items("Active").Select(entry => entry.Long("MissionID")).OfType<long>().ToList();
                live = ids.Count;
                wanted = [.. ids.Where(id => !accepted.Contains(id))];

                continue;
            }

            // Searched as text before any line is parsed, since most files name none of the missions.
            if (!wanted.Any(id => Mentions(text, id)))
            {
                continue;
            }

            var found = Parse(
                text,
                journalEvent => journalEvent.Long("MissionID") is { } id && wanted.Contains(id),
                out _,
                out _);

            kept.Add(found);

            foreach (var journalEvent in found)
            {
                if (journalEvent.Kind == "MissionAccepted" && journalEvent.Long("MissionID") is { } id)
                {
                    _ = wanted.Remove(id);
                }
            }
        }

        var boards = new Dictionary<string, MissionBoard>(StringComparer.Ordinal);

        if (commander is null)
        {
            logger.LogInformation("No Missions snapshot with a Commander in {Files} journal files", opened);
            return boards;
        }

        var board = MissionBoard.Empty;

        for (var i = kept.Count - 1; i >= 0; i--)
        {
            foreach (var journalEvent in kept[i])
            {
                board = board.Apply(journalEvent);
            }
        }

        logger.LogInformation(
            "Missions recovered from {Files} journal files for {Commander}: {Live} live, {Missing} with no accept on disk",
            opened,
            commander,
            live,
            wanted?.Count ?? 0);

        boards[commander] = board;
        return boards;
    }

    /// <summary>
    /// The mission events in one file that pass <paramref name="keep"/>, with the file's last snapshot and
    /// the Commander logged in before it.
    /// </summary>
    private static List<JournalEvent> Parse(
        string text,
        Func<JournalEvent, bool> keep,
        out JournalEvent? snapshot,
        out string? commander)
    {
        var events = new List<JournalEvent>();
        string? current = null;

        snapshot = null;
        commander = null;

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Tested as text first, since most lines are none of these.
            if (!Relevant.Any(kind => line.Contains(kind, StringComparison.Ordinal)))
            {
                continue;
            }

            if (!JournalEvent.TryParse(line, NullLogger.Instance, out var journalEvent) || journalEvent is null)
            {
                continue;
            }

            if (journalEvent.Kind is "Commander" or "LoadGame" && journalEvent.String("FID") is { Length: > 0 } fid)
            {
                current = fid;
                continue;
            }

            if (journalEvent.Kind is not ("MissionAccepted" or "MissionRedirected" or "CargoDepot"
                or "MissionCompleted" or "MissionFailed" or "MissionAbandoned" or "Missions"))
            {
                continue;
            }

            if (journalEvent.Kind == "Missions")
            {
                snapshot = journalEvent;
                commander = current;
            }

            if (keep(journalEvent))
            {
                events.Add(journalEvent);
            }
        }

        return events;
    }

    /// <summary>Whether the text carries this id as a <c>MissionID</c> value.</summary>
    private static bool Mentions(string text, long id)
    {
        var needle = "\"MissionID\":" + id.ToString(CultureInfo.InvariantCulture);

        for (var at = text.IndexOf(needle, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            var after = at + needle.Length;

            if (after >= text.Length || !char.IsAsciiDigit(text[after]))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadAll(IFileSystem files, string path)
    {
        try
        {
            return files.ReadText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
