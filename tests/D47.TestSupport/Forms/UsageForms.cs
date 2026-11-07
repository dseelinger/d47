using D47.Core.Conversation;

namespace D47.Core.Usage;

public static class UsageForms
{
    extension(UsageLedger ledger)
    {
        public string? Report(DateOnly day) => ledger.Counts(day) is { } counts ? UsageLedger.Report(day, counts) : null;
    }
}
