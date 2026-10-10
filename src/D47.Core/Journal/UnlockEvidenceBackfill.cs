using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The readings an engineer unlock is decided from: reputation, engineer contributions and tallies.</summary>
public sealed record UnlockEvidence(
    ReputationState Reputation,
    EngineerContributions Contributions,
    EngineerTallies Tallies)
{
    public static readonly UnlockEvidence Empty =
        new(ReputationState.Empty, EngineerContributions.Empty, EngineerTallies.Empty);

    public bool IsKnown => Reputation.IsKnown || Contributions.IsKnown || Tallies.IsKnown;

    /// <summary>Folds one event as <see cref="CommanderGameState"/> does, <c>NewCommander</c> included.</summary>
    public UnlockEvidence Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind == "NewCommander"
            ? new UnlockEvidence(Reputation.WithoutFactions(), EngineerContributions.Empty, EngineerTallies.Empty)
            : new UnlockEvidence(
                Reputation.Apply(journalEvent),
                Contributions.Apply(journalEvent),
                Tallies.Apply(journalEvent));
    }
}

/// <summary>Reputation, engineer contributions and tallies recovered from journals d47 was not running for (#182).</summary>
public static class UnlockEvidenceBackfill
{
    /// <summary>Every Commander's readings and contributions as their journals last wrote them, keyed by Frontier id.</summary>
    public static IReadOnlyDictionary<string, UnlockEvidence> FromHistory(
        IFileSystem fileSystem,
        string directory,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (fileSystem.FolderWritten(directory) is null)
        {
            logger.LogWarning("No journal folder at {Directory}", directory);
            return new Dictionary<string, UnlockEvidence>(StringComparer.Ordinal);
        }

        return FromHistory(
            fileSystem,
            [.. fileSystem.Enumerate(directory, JournalFolder.FilePattern)
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)],
            logger,
            cancellation);
    }

    /// <summary>The same, over an explicit list oldest-first.</summary>
    public static IReadOnlyDictionary<string, UnlockEvidence> FromHistory(
        IFileSystem fileSystem,
        IReadOnlyList<string> files,
        ILogger logger,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(logger);

        var folded = new Dictionary<string, UnlockEvidence>(StringComparer.Ordinal);
        var commander = string.Empty;

        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();

            foreach (var line in Lines(fileSystem, file, logger))
            {
                if (!Relevant(line) || !JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                // Goes to the Commander it names, and does not make them the active one.
                if (parsed.Kind == "NewCommander")
                {
                    if (parsed.String("FID") is { Length: > 0 } created)
                    {
                        folded[created] = Held(folded, created).Apply(parsed);
                    }

                    continue;
                }

                if (parsed.Kind is "Commander" or "LoadGame")
                {
                    if (parsed.String("FID") is { Length: > 0 } fid)
                    {
                        commander = fid;
                    }

                    continue;
                }

                if (commander.Length == 0)
                {
                    continue;
                }

                folded[commander] = Held(folded, commander).Apply(parsed);
            }
        }

        var evidence = new Dictionary<string, UnlockEvidence>(StringComparer.Ordinal);

        foreach (var (fid, found) in folded)
        {
            if (found.IsKnown)
            {
                evidence[fid] = found;
            }
        }

        logger.LogInformation("Unlock evidence recovered from history for {Count} Commanders", evidence.Count);

        return evidence;
    }

    /// <summary>A text test before any JSON is parsed: the few event kinds, and jumps that carry a reputation.</summary>
    private static bool Relevant(string line) =>
        line.Contains("\"MyReputation\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"EngineerContribution\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"EngineerProgress\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Docked\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Location\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"ApproachSettlement\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"MissionCompleted\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"SellMicroResources\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Disembark\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Reputation\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"NewCommander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"Commander\"", StringComparison.Ordinal)
        || line.Contains("\"event\":\"LoadGame\"", StringComparison.Ordinal);

    private static UnlockEvidence Held(Dictionary<string, UnlockEvidence> folded, string fid) =>
        folded.TryGetValue(fid, out var held) ? held : UnlockEvidence.Empty;

    /// <summary>One journal's lines.</summary>
    private static IEnumerable<string> Lines(IFileSystem fileSystem, string file, ILogger logger)
    {
        Stream stream;

        try
        {
            stream = fileSystem.OpenRead(file) ?? throw new FileNotFoundException("The journal file is missing.", file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not read {File} for unlock evidence", file);
            yield break;
        }

        using (stream)
        {
            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                yield return line;
            }
        }
    }
}
