namespace D47.Core.Knowledge;

public static class KnowledgeForms
{
    extension(CodexCatalogue)
    {
        public static IReadOnlyList<CodexRegionEntry> InRegion(int regionId) => CodexCatalogue.Table.InRegion(regionId);
    }

    extension(SignalIndex)
    {
        public static SignalIndex Read(Stream tsv) => SignalIndex.Read(new StreamReader(tsv));
    }

    extension(StarterSystemTable)
    {
        public static HashSet<string> Parse(string text)
        {
            using var reader = new StringReader(text);

            return StarterSystemTable.Read(reader);
        }
    }
}
