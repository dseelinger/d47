using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

internal static class JournalHistory
{
    /// <summary>The events of one journal file whose line passes <paramref name="relevant"/>, in file order.</summary>
    public static List<JournalEvent> Parse(
        IFileSystem files, string file, ILogger logger, string purpose, bool warnIfMissing, Func<string, bool> relevant)
    {
        var events = new List<JournalEvent>();

        foreach (var line in JournalLines.Read(files, file, logger, purpose, warnIfMissing))
        {
            if (relevant(line) && JournalEvent.TryParse(line, logger, out var parsed) && parsed is not null)
            {
                events.Add(parsed);
            }
        }

        return events;
    }
}
