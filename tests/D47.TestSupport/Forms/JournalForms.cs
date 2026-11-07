using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

public static class JournalForms
{
    extension(ColonisationBackfill)
    {
        public static IReadOnlyDictionary<string, ColonisationSites> FromHistory(
            IFileSystem fileSystem,
            string directory,
            ILogger logger,
            CancellationToken cancellation = default)
        {
            ArgumentNullException.ThrowIfNull(logger);

            if (fileSystem.FolderWritten(directory) is null)
            {
                logger.LogWarning("No journal folder at {Directory}", directory);
                return new Dictionary<string, ColonisationSites>(StringComparer.Ordinal);
            }

            return ColonisationBackfill.FromHistory(
                fileSystem,
                [.. JournalFolder.Ordered(fileSystem, directory)],
                logger,
                cancellation);
        }
    }

    extension(CommodityLedger ledger)
    {
        public void Apply(JournalEvent journalEvent) => ledger.Apply([journalEvent]);
    }

    extension(FleetRegistry fleet)
    {
        public FleetRegistry Apply(JournalEvent journalEvent) => fleet.Apply(journalEvent, null, null);
    }

    extension(GameStateStore store)
    {
        public FoldReceipt Apply(JournalEvent journalEvent) => store.Apply(journalEvent, null);

        public FoldReceipt Apply(JournalEvent journalEvent, SurfaceFix? at) => store.Apply(journalEvent, at, priming: false);
    }
}
