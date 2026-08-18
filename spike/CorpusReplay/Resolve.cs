using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace CorpusReplay;

/// <summary>
/// Every distinct symbol spelling the corpus contains, put through the lookup d47 actually
/// performs on it. A lookup that misses does not throw and does not log — it answers "I do not
/// know that one" about something the Commander is looking at, which is the failure mode this
/// repo's rules are written to catch.
/// </summary>
internal static class Resolve
{
    internal static void Run(IReadOnlyList<string> files)
    {
        // family -> spelling -> count
        var seen = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        void Note(string family, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!seen.TryGetValue(family, out var bucket))
            {
                bucket = new Dictionary<string, int>(StringComparer.Ordinal);
                seen[family] = bucket;
            }

            bucket[value] = bucket.GetValueOrDefault(value) + 1;
        }

        foreach (var file in files)
        {
            // Shared, because the newest journal is open in Elite while a Commander is playing —
            // which is exactly when somebody runs this. JournalReader has always opened this way;
            // this pass did not, so the soak crashed after reporting a clean run.
            using var stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);

            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (document)
                {
                    var root = document.RootElement;
                    var kind = root.String("event");

                    switch (kind)
                    {
                        case "Loadout":
                            Note("ship", root.String("Ship"));
                            foreach (var module in root.Items("Modules"))
                            {
                                Note("module", module.String("Item"));
                                if (module.Object("Engineering") is { } engineering)
                                {
                                    Note("blueprint", engineering.String("BlueprintName"));
                                    Note("experimental", engineering.String("ExperimentalEffect"));
                                }
                            }

                            break;

                        case "LoadGame":
                            Note("ship", root.String("Ship"));
                            break;

                        case "ShipyardSwap" or "ShipyardBuy" or "ShipyardNew" or "ShipyardSell":
                            Note("ship", root.String("ShipType"));
                            break;

                        case "StoredShips":
                            foreach (var ship in root.Items("ShipsHere").Concat(root.Items("ShipsRemote")))
                            {
                                Note("ship", ship.String("ShipType"));
                            }

                            break;

                        case "ModuleBuy":
                            Note("module", root.String("BuyItem"));
                            Note("module", root.String("SellItem"));
                            break;

                        case "ModuleSell":
                            Note("module", root.String("SellItem"));
                            break;

                        case "ModuleRetrieve":
                            Note("module", root.String("RetrievedItem"));
                            break;

                        case "ModuleStore":
                            Note("module", root.String("StoredItem"));
                            break;

                        case "StoredModules":
                            foreach (var item in root.Items("Items"))
                            {
                                Note("module", item.String("Name"));
                            }

                            break;

                        case "EngineerCraft":
                            Note("module", root.String("Module"));
                            Note("blueprint", root.String("BlueprintName"));
                            foreach (var ingredient in root.Items("Ingredients"))
                            {
                                Note("material", ingredient.String("Name"));
                            }

                            break;

                        case "MaterialCollected" or "MaterialDiscarded":
                            Note("material", root.String("Name"));
                            break;

                        case "MaterialTrade":
                            if (root.Object("Paid") is { } paid)
                            {
                                Note("material", paid.String("Material"));
                            }

                            if (root.Object("Received") is { } received)
                            {
                                Note("material", received.String("Material"));
                            }

                            break;

                        case "SuitLoadout" or "SwitchSuitLoadout":
                            Note("onfoot", root.String("SuitName"));
                            foreach (var module in root.Items("Modules"))
                            {
                                Note("onfoot", module.String("ModuleName"));
                            }

                            break;

                        case "BuySuit" or "UpgradeSuit" or "BuyWeapon" or "UpgradeWeapon":
                            Note("onfoot", root.String("Name"));
                            break;

                        case "ColonisationConstructionDepot":
                            foreach (var resource in root.Items("ResourcesRequired"))
                            {
                                Note("commodity", resource.String("Name"));
                            }

                            break;

                        case "ColonisationContribution":
                            foreach (var contribution in root.Items("Contributions"))
                            {
                                Note("commodity", contribution.String("Name"));
                            }

                            break;

                        case "ProspectedAsteroid":
                            foreach (var material in root.Items("Materials"))
                            {
                                Note("commodity", material.String("Name"));
                            }

                            break;
                    }
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== TABLE RESOLUTION, per raw spelling the corpus contains ===");
        Console.WriteLine();

        Report(seen, "ship", s => EliteSpecifications.Ship(s) is not null, "EliteSpecifications.Ship");
        Report(seen, "module", s => EliteSpecifications.Module(s) is not null, "EliteSpecifications.Module");
        Report(seen, "module", s => EliteSpecifications.Module(JournalJson.Symbol(s)) is not null,
            "EliteSpecifications.Module(Symbol(s))");
        Report(seen, "material", s => MaterialCatalogue.Find(s) is not null, "MaterialCatalogue.Find");
        Report(seen, "blueprint", s => BlueprintCatalogue.Named(s).Count > 0, "BlueprintCatalogue.Named");
        Report(seen, "onfoot", s => OnFootCatalogue.Find(s) is not null, "OnFootCatalogue.Find");
        Report(seen, "onfoot", s => OnFootCatalogue.Find(JournalJson.Symbol(s)) is not null,
            "OnFootCatalogue.Find(Symbol(s))");
    }

    private static void Report(
        Dictionary<string, Dictionary<string, int>> seen,
        string family,
        Func<string, bool> resolves,
        string label)
    {
        if (!seen.TryGetValue(family, out var bucket))
        {
            Console.WriteLine($"--- {label}: NOTHING IN CORPUS for '{family}'");
            return;
        }

        var missed = new List<KeyValuePair<string, int>>();
        var hit = 0;

        foreach (var pair in bucket)
        {
            if (resolves(pair.Key))
            {
                hit++;
            }
            else
            {
                missed.Add(pair);
            }
        }

        Console.WriteLine($"--- {label} over '{family}': {hit}/{bucket.Count} spellings resolve, {missed.Count} miss");

        // The interesting miss is the one whose case-folded twin DOES resolve — that is a bug
        // rather than a gap in the table.
        foreach (var pair in missed.OrderByDescending(p => p.Value).Take(25))
        {
            var twin = bucket.Keys.FirstOrDefault(other =>
                !string.Equals(other, pair.Key, StringComparison.Ordinal)
                && string.Equals(other, pair.Key, StringComparison.OrdinalIgnoreCase)
                && resolves(other));

            var note = twin is not null ? $"   <-- but '{twin}' DOES resolve" : "";
            Console.WriteLine($"      {pair.Key,-50} x{pair.Value}{note}");
        }

        if (missed.Count > 25)
        {
            Console.WriteLine($"      … and {missed.Count - 25} more");
        }

        Console.WriteLine();
    }
}
