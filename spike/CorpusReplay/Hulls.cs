using D47.Core.Knowledge;

namespace CorpusReplay;

/// <summary>
/// The four hulls the corpus flies that the specification table has no figures for, put through
/// the exact match SpecificationCapability.Unknown performs — which is over display names while
/// the value arriving from the journal is a symbol.
/// </summary>
internal static class Hulls
{
    internal static void Run()
    {
        Console.WriteLine();
        Console.WriteLine("=== KNOWN-BUT-UNMEASURED, matched the way the capability matches it ===");
        Console.WriteLine();

        Console.WriteLine($"table lists: [{string.Join(", ", EliteSpecifications.KnownButUnmeasured)}]");
        Console.WriteLine();

        // Every spelling of a hull the corpus contains that Ship() cannot resolve.
        string[] symbols =
        [
            "corsair", "Corsair",
            "explorer_nx", "Explorer_NX",
            "smallcombat01_nx", "SmallCombat01_NX",
            "mediumtransport01", "MediumTransport01",
        ];

        foreach (var symbol in symbols)
        {
            var resolved = EliteSpecifications.Ship(symbol);

            var known = EliteSpecifications.KnownButUnmeasured
                .FirstOrDefault(name => Catalogue.Match([name], symbol) is not null);

            var near = EliteSpecifications.NearShips(symbol);

            var branch = resolved is not null
                ? $"RESOLVES to {resolved.Name}"
                : known is not null
                    ? $"'newer than my table' — matched {known}"
                    : near.Count > 0
                        ? $"'did you mean' — {string.Join(" / ", near)}"
                        : "raw symbol read out: \"flying a '" + symbol + "'\"";

            Console.WriteLine($"  {symbol,-22} -> {branch}");
        }

        Console.WriteLine();
        Console.WriteLine("  (armour rows for all four hulls ARE in the table; only the hull row is absent)");
    }
}
