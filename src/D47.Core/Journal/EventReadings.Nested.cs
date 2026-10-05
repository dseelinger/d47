using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Knowledge;
using static D47.Core.Journal.JournalText;

namespace D47.Core.Journal;

public static partial class EventReadings
{
    // ---- Nested common kinds ---------------------------------------------------------------

    private static readonly JournalField? ModuleField = JournalFields.Find("Module");

    /// <summary>Only the curated rows: every other field of the event stays in the plumbing.</summary>
    private static Curated Quiet(JsonElement raw, Curated curated) =>
        curated with { Used = [.. raw.EnumerateObject().Select(property => property.Name)] };

    private static Curated CurateFsdJump(JsonElement raw) =>
        Quiet(
            raw,
            Group(
                raw,
                "FSDJump",
                ("System", ["StarSystem"]),
                ("Jump distance", ["JumpDist"]),
                ("Fuel used", ["FuelUsed"]),
                ("Fuel left", ["FuelLevel"]),
                ("Controlled by", ["SystemFaction"]),
                ("Government", ["SystemGovernment"]),
                ("Security", ["SystemSecurity"]),
                ("Economy", ["SystemEconomy"]),
                ("Population", ["Population"]),
                ("Controlling power", ["ControllingPower"])));

    private static Curated CurateScan(JsonElement raw)
    {
        (string, string[])[] common =
        [
            ("Body", ["BodyName"]),
            ("Distance from arrival", ["DistanceFromArrivalLS"]),
            ("Discovered", ["WasDiscovered"]),
            ("Mapped", ["WasMapped"]),
        ];
        (string, string[])[] kind = raw.String("StarType") is null
            ?
            [
                ("Planet class", ["PlanetClass"]),
                ("Landing", ["Landable"]),
                ("Gravity", ["SurfaceGravity"]),
                ("Atmosphere", ["Atmosphere"]),
                ("Volcanism", ["Volcanism"]),
                ("Terraform state", ["TerraformState"]),
            ]
            :
            [
                ("Star class", ["StarType"]),
                ("Age", ["Age_MY"]),
                ("Mass", ["StellarMass"]),
            ];

        return Quiet(
            raw,
            Group(
                raw,
                field => field switch
                {
                    "WasDiscovered" => [new ReadingValue(raw.Bool(field) ? "Already discovered" : "Not yet discovered")],
                    "WasMapped" => [new ReadingValue(raw.Bool(field) ? "Already mapped" : "Not yet mapped")],
                    "StarType" => [StarClass(raw)],
                    "Landable" => [new ReadingValue(raw.Bool(field) ? "Landable" : "Not landable")],
                    "Atmosphere" => [new ReadingValue(Atmosphere(raw))],
                    "Volcanism" => [new ReadingValue(Blank(raw.String(field)) ?? "None")],
                    "TerraformState" => [new ReadingValue(Blank(raw.String(field)) ?? "Not terraformable")],
                    _ => Mechanical(raw, "Scan", field),
                },
                [.. common, .. kind]));
    }

    /// <summary>A star's class and subclass: <c>M7</c>, the subclass its own number run.</summary>
    private static ReadingValue StarClass(JsonElement raw)
    {
        var type = raw.String("StarType")!;

        return raw.Int("Subclass") is { } subclass
            ? new ReadingValue($"{type}{subclass}")
            {
                Runs = [new ReadingRun(type, false), new ReadingRun(subclass.ToString(Culture), true)],
            }
            : new ReadingValue(type);
    }

    private static string Atmosphere(JsonElement raw) =>
        Blank(raw.String("Atmosphere"))
        ?? (Blank(raw.String("AtmosphereType")) is { } type ? Spaced(type) : "None");

    private static Curated CurateLoadout(JsonElement raw) =>
        Quiet(
            raw,
            Group(
                raw,
                field => field switch
                {
                    "ShipName" or "ShipIdent" => Blank(raw.String(field)) is { } text ? [new ReadingValue(text.Trim()) { Typed = true }] : [],
                    "Modules" =>
                    [
                        .. raw.Items("Modules")
                            .Select(module => Value(raw, "Item", module.String("Item"), ModuleField, ReadingTone.Value, "Loadout", partner: false))
                            .OfType<ReadingValue>(),
                    ],
                    _ => Mechanical(raw, "Loadout", field),
                },
                ("Ship", ["Ship"]),
                ("Name", ["ShipName"]),
                ("Ident", ["ShipIdent"]),
                ("Jump range", ["MaxJumpRange"]),
                ("Cargo", ["CargoCapacity"]),
                ("Rebuy", ["Rebuy"]),
                ("Modules", ["Modules"])));

    private static Curated CurateCargo(JsonElement raw) =>
        Quiet(
            raw,
            Group(
                raw,
                field => field == "Inventory"
                    ? [.. raw.Items(field).Select(BackpackItem).OfType<ReadingValue>()]
                    : Mechanical(raw, "Cargo", field),
                ("Vessel", ["Vessel"]),
                ("Total", ["Count"]),
                ("Items", ["Inventory"])));

    private static Curated CurateEngineerCraft(JsonElement raw) =>
        Quiet(
            raw,
            Group(
                raw,
                field => field switch
                {
                    "BlueprintName" => Blueprint(raw),
                    "Ingredients" => [.. raw.Items(field).Select(Ingredient).OfType<ReadingValue>()],
                    _ => Mechanical(raw, "EngineerCraft", field),
                },
                ("Engineer", ["Engineer"]),
                ("Module", ["Module"]),
                ("Blueprint", ["BlueprintName"]),
                ("Effect", ["ExperimentalEffect"]),
                ("Ingredients", ["Ingredients"])));

    /// <summary>The blueprint spaced, with its grade.</summary>
    private static IReadOnlyList<ReadingValue> Blueprint(JsonElement raw)
    {
        if (Blank(raw.String("BlueprintName")) is not { } blueprint)
        {
            return [];
        }

        var name = Spaced(blueprint.Replace('_', ' ')).Trim();

        return raw.Int("Level") is { } level
            ? [new ReadingValue($"{name}, grade {level}")
            {
                Runs = [new ReadingRun($"{name}, grade ", false), new ReadingRun(level.ToString(Culture), true)],
            }]
            : [new ReadingValue(name)];
    }

    private static ReadingValue? Ingredient(JsonElement item)
    {
        var name = MaterialCatalogue.Find(item.String("Name"))?.Name ?? NameOf(item);

        return name is null ? null : Counted(name, item);
    }

    private static Curated CurateCommunityGoal(JsonElement raw) =>
        Quiet(
            raw,
            Group(
                raw,
                _ => [.. raw.Items("CurrentGoals").Select(Goal).OfType<ReadingValue>()],
                ("Goals", ["CurrentGoals"])));

    /// <summary>A goal's title, the tier reached where it has one and the Commander's contribution where it is above zero.</summary>
    private static ReadingValue? Goal(JsonElement goal)
    {
        if (Blank(goal.String("Title")) is not { } title)
        {
            return null;
        }

        var runs = new List<ReadingRun> { new(title, false) };

        if (Blank(goal.String("TierReached")) is { } tier)
        {
            runs.Add(new ReadingRun(" — ", false));
            runs.AddRange(NumberRuns(tier));
        }

        if (goal.Long("PlayerContribution") is > 0 and var contribution)
        {
            runs.Add(new ReadingRun(" — you gave ", false));
            runs.Add(new ReadingRun(contribution.ToString("N0", Culture), true));
        }

        return new ReadingValue(string.Concat(runs.Select(run => run.Text))) { Runs = runs };
    }

    private static List<ReadingRun> NumberRuns(string text)
    {
        var runs = new List<ReadingRun>();
        var at = 0;

        foreach (var number in Digits().EnumerateMatches(text))
        {
            if (number.Index > at)
            {
                runs.Add(new ReadingRun(text[at..number.Index], false));
            }

            runs.Add(new ReadingRun(text.Substring(number.Index, number.Length), true));
            at = number.Index + number.Length;
        }

        if (at < text.Length)
        {
            runs.Add(new ReadingRun(text[at..], false));
        }

        return runs;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    private static Curated CurateConstructionDepot(JsonElement raw) =>
        Quiet(
            raw,
            Group(
                raw,
                field => field switch
                {
                    "ConstructionComplete" => [ConstructionState(raw)],
                    "ConstructionFailed" => [],
                    "ResourcesRequired" => [ResourcesShort(raw)],
                    _ => Mechanical(raw, "ColonisationConstructionDepot", field),
                },
                ("Progress", ["ConstructionProgress"]),
                ("Status", ["ConstructionComplete", "ConstructionFailed"]),
                ("Resources short", ["ResourcesRequired"])));

    private static ReadingValue ConstructionState(JsonElement raw)
    {
        if (raw.Bool("ConstructionFailed"))
        {
            return new ReadingValue("Failed") { Tone = ReadingTone.Warning };
        }

        return new ReadingValue(raw.Bool("ConstructionComplete") ? "Complete" : "In progress");
    }

    private static ReadingValue ResourcesShort(JsonElement raw)
    {
        var required = raw.Items("ResourcesRequired").ToList();
        var lacking = required.Count(item => (item.Int("ProvidedAmount") ?? 0) < (item.Int("RequiredAmount") ?? 0));

        return new ReadingValue($"{lacking} of {required.Count} resources")
        {
            Runs =
            [
                new ReadingRun(lacking.ToString(Culture), true),
                new ReadingRun(" of ", false),
                new ReadingRun(required.Count.ToString(Culture), true),
                new ReadingRun(" resources", false),
            ],
        };
    }
}
