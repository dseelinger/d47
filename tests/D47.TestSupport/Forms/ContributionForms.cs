using D47.Core.Journal;

namespace D47.Core.Contribution;

public static class ContributionForms
{
    extension(EddnComposer composer)
    {
        public EddnEnvelope? Compose(JournalEvent journalEvent) =>
            composer.ComposeAll(journalEvent, DateTimeOffset.UtcNow).SingleOrDefault();
    }
}
