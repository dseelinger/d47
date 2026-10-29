namespace D47.Core.Journal;

internal static class LedgerBooks
{
    /// <summary>The Commander's book, added empty where there is none.</summary>
    public static TBook Open<TBook>(Dictionary<string, TBook> books, string commander)
        where TBook : new()
    {
        if (!books.TryGetValue(commander, out var book))
        {
            book = new TBook();
            books[commander] = book;
        }

        return book;
    }
}
