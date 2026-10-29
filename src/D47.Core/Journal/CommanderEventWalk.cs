using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

internal static class CommanderEventWalk
{
    private static readonly JournalLineFilter Always = JournalLineFilter.Events("NewCommander", "Commander", "LoadGame");

    /// <summary>
    /// Every line the filter matches, or that names a Commander, parsed in file order and
    /// handed to <paramref name="fold"/> with the Commander it belongs to. The Commander is empty until a
    /// <c>Commander</c> or <c>LoadGame</c> names one and carries across files. <c>NewCommander</c> is passed
    /// on with the Commander unchanged.
    /// </summary>
    public static void Each(
        IFileSystem files,
        IReadOnlyList<string> journals,
        ILogger logger,
        string purpose,
        JournalLineFilter filter,
        CancellationToken cancellationToken,
        Action<JournalEvent, string> fold)
    {
        var commander = string.Empty;

        foreach (var journal in journals)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var line in JournalLines.Read(files, journal, logger, purpose, warnIfMissing: true))
            {
                if (!Always.Matches(line) && !filter.Matches(line))
                {
                    continue;
                }

                if (!JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                Track(parsed, ref commander);
                fold(parsed, commander);
            }
        }
    }

    /// <summary>The same over a <see cref="JournalReader"/>, with <paramref name="commander"/> kept by the caller across readers.</summary>
    public static void Poll(JournalReader reader, ref string commander, Action<JournalEvent, string> fold)
    {
        while (reader.Poll() is { Count: > 0 } batch)
        {
            foreach (var journalEvent in batch)
            {
                Track(journalEvent, ref commander);
                fold(journalEvent, commander);
            }
        }
    }

    private static void Track(JournalEvent journalEvent, ref string commander)
    {
        if (journalEvent.Kind is "Commander" or "LoadGame" && journalEvent.String("FID") is { Length: > 0 } fid)
        {
            commander = fid;
        }
    }
}
