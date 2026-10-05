using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Callouts;
using D47.Core.Knowledge;
using static D47.Core.Journal.JournalText;

namespace D47.Core.Journal;

/// <summary>Reads one journal event into a headline, labelled rows and every field it carries.</summary>
public static partial class EventReadings
{
    private const string Localised = "_Localised";

    private const double StandardGravity = 9.81;

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private sealed record Curated(List<ReadingRow> Rows, HashSet<string> Used);

    private static readonly Dictionary<string, Func<JsonElement, Curated>> Curations = new(StringComparer.Ordinal)
    {
        ["Docked"] = CurateDocked,
        ["ReceiveText"] = CurateReceiveText,
        ["StartJump"] = raw => Group(raw, "StartJump", ("Jump", ["JumpType"]), ("To", ["StarSystem"]), ("Star class", ["StarClass"])),
        ["FSDTarget"] = raw => Group(raw, "FSDTarget", ("Next jump", ["Name"]), ("Star class", ["StarClass"]), ("Jumps left", ["RemainingJumpsInRoute"])),
        ["FuelScoop"] = raw => Group(raw, "FuelScoop", ("Scooped", ["Scooped"]), ("Fuel now", ["Total"])),
        ["SupercruiseExit"] = raw => Group(raw, "SupercruiseExit", ("Arrived at", ["Body"]), ("Body type", ["BodyType"]), ("System", ["StarSystem"])),
        ["SupercruiseEntry"] = raw => Group(raw, "SupercruiseEntry", ("System", ["StarSystem"])),
        ["SupercruiseDestinationDrop"] = raw => Group(raw, "SupercruiseDestinationDrop", ("Dropped at", ["Type"]), ("Threat level", ["Threat"])),
        ["DockingRequested"] = CurateDockingRequested,
        ["DockingGranted"] = CurateDockingGranted,
        ["Undocked"] = raw => Stationed(raw, []),
        ["RefuelAll"] = raw => Group(raw, "RefuelAll", ("Fuel bought", ["Amount"]), ("Cost", ["Cost"])),
        ["LaunchDrone"] = raw => Group(raw, "LaunchDrone", ("Drone", ["Type"])),
        ["FSSDiscoveryScan"] = raw => Group(raw, "FSSDiscoveryScan", ("System", ["SystemName"]), ("Scanned", ["Progress"]), ("Bodies found", ["BodyCount"]), ("Other objects", ["NonBodyCount"])),
        ["MaterialCollected"] = raw => Group(raw, "MaterialCollected", ("Material", ["Name"]), ("Category", ["Category"]), ("Collected", ["Count"])),
        ["ShipTargeted"] = CurateShipTargeted,
        ["MiningRefined"] = raw => Group(raw, "MiningRefined", ("Refined", ["Type"])),
        ["UnderAttack"] = raw => Group(raw, "UnderAttack", ("Attacked", ["Target"])),
        ["BackpackChange"] = CurateBackpackChange,
        ["PowerplayMerits"] = raw => Group(raw, "PowerplayMerits", ("Power", ["Power"]), ("Merits gained", ["MeritsGained"]), ("Total merits", ["TotalMerits"])),
        ["CollectItems"] = raw => Group(raw, "CollectItems", ("Item", ["Name"]), ("Kind", ["Type"]), ("Taken", ["Count"])),
        ["FSDJump"] = CurateFsdJump,
        ["Scan"] = CurateScan,
        ["Loadout"] = CurateLoadout,
        ["Cargo"] = CurateCargo,
        ["EngineerCraft"] = CurateEngineerCraft,
        ["CommunityGoal"] = CurateCommunityGoal,
        ["ColonisationConstructionDepot"] = CurateConstructionDepot,
    };

    public static EventReading For(JournalEntry entry)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(entry.Compact);
        }
        catch (JsonException)
        {
            return Unreadable(entry);
        }

        using (document)
        {
            var raw = document.RootElement;

            if (raw.ValueKind != JsonValueKind.Object)
            {
                return Unreadable(entry);
            }

            var curated = Curations.TryGetValue(entry.Kind, out var curate)
                ? curate(raw)
                : new Curated([], []);
            var rows = curated.Rows;

            foreach (var property in raw.EnumerateObject())
            {
                if (curated.Used.Contains(property.Name) || IsPartner(raw, property.Name))
                {
                    continue;
                }

                if (MechanicalRow(raw, property, entry.Kind) is { } row)
                {
                    rows.Add(row);
                }
            }

            return new EventReading(entry.Said, entry.Timestamp, rows, Plumbing(raw));
        }
    }

    /// <summary>How long ago, the way the pane says it; the caller supplies <paramref name="now"/>.</summary>
    public static string Age(DateTimeOffset when, DateTimeOffset now)
    {
        var elapsed = now - when;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return Ago((int)elapsed.TotalMinutes, "minute");
        }

        return elapsed < TimeSpan.FromDays(1)
            ? Ago((int)elapsed.TotalHours, "hour")
            : Ago((int)elapsed.TotalDays, "day");
    }

    private static string Ago(int count, string unit) => $"{count} {unit}{(count == 1 ? string.Empty : "s")} ago";

    private static EventReading Unreadable(JournalEntry entry) =>
        new(entry.Said, entry.Timestamp, [], [new PlumbingField("Raw", entry.Compact)]);

    private static List<PlumbingField> Plumbing(JsonElement raw) =>
    [
        .. raw.EnumerateObject().Select(property => new PlumbingField(
            property.Name,
            property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText())),
    ];

    /// <summary>An <c>X_Localised</c> whose <c>X</c> is also present: drawn with <c>X</c>.</summary>
    private static bool IsPartner(JsonElement raw, string name) =>
        name.EndsWith(Localised, StringComparison.Ordinal)
        && name.Length > Localised.Length
        && raw.TryGetProperty(name[..^Localised.Length], out _);

    // ---- Mechanical rows -------------------------------------------------------------------

    private static ReadingRow? MechanicalRow(JsonElement raw, JsonProperty property, string kind)
    {
        var name = property.Name;

        if (name is "timestamp" or "event")
        {
            return null;
        }

        var field = name.EndsWith(Localised, StringComparison.Ordinal) && name.Length > Localised.Length
            ? JournalFields.Find(name[..^Localised.Length], kind)
            : JournalFields.Find(name, kind);

        if (name.EndsWith("ID", StringComparison.Ordinal)
            || name.EndsWith("Address", StringComparison.Ordinal)
            || field is { Plumbing: true })
        {
            return null;
        }

        var baseName = name.EndsWith(Localised, StringComparison.Ordinal) && name.Length > Localised.Length
            ? name[..^Localised.Length]
            : name;
        var label = field?.Label ?? Spaced(baseName);
        var tone = field?.Tone ?? ReadingTone.Value;
        var value = property.Value;

        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                return field?.TrueSentence is { } sentence
                    ? Row(string.Empty, [new ReadingValue(sentence) { Tone = tone }], name)
                    : Row(Spaced(baseName), [new ReadingValue("Yes")], name);

            case JsonValueKind.String:
                return Value(raw, baseName, value.GetString(), field, tone, kind) is { } text
                    ? Row(label, [text], baseName)
                    : null;

            case JsonValueKind.Number:
                return Row(label, [Number(value, field?.Unit ?? FieldUnit.None, tone)], name);

            case JsonValueKind.Array:
                return ArrayRow(raw, value, baseName, label, field, tone, kind);

            case JsonValueKind.Object:
                return NameOf(value) is { } objectName
                    ? Row(label, [new ReadingValue(objectName) { Tone = tone }], name)
                    : null;

            default:
                return null;
        }
    }

    private static ReadingRow Row(string label, IReadOnlyList<ReadingValue> values, string? field) =>
        new(label, values) { Field = JournalFields.Find(field ?? string.Empty) is null ? null : field };

    private static ReadingRow? ArrayRow(
        JsonElement raw, JsonElement array, string name, string label, JournalField? field, ReadingTone tone, string kind)
    {
        var items = array.EnumerateArray().ToList();

        if (items.Count == 0)
        {
            return null;
        }

        if (items.All(item => item.ValueKind == JsonValueKind.String))
        {
            var values = items
                .Select(item => Value(raw, name, item.GetString(), field, tone, kind, partner: false))
                .OfType<ReadingValue>()
                .ToList();

            return values.Count == 0 ? null : Row(label, values, name);
        }

        var names = items.Select(NameOf).OfType<string>().ToList();

        if (names.Count > 0)
        {
            return Row(label, [.. names.Select(item => new ReadingValue(item) { Tone = tone })], name);
        }

        var noun = items.Count == 1 ? "entry" : "entries";

        return Row(
            label,
            [new ReadingValue($"{items.Count} {noun}")
            {
                Runs = [new ReadingRun(items.Count.ToString(Culture), true), new ReadingRun($" {noun}", false)],
            }],
            name);
    }

    /// <summary>An object's own name: <c>Name_Localised</c>, else <c>Name</c>.</summary>
    private static string? NameOf(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object ? Blank(Clean(item.String("Name_Localised") ?? item.String("Name"))) : null;

    /// <summary>A string with no <c>$</c> key in it.</summary>
    private static string? Clean(string? text)
    {
        if (text is null || !text.StartsWith('$'))
        {
            return text;
        }

        return text.Contains("#name=", StringComparison.Ordinal) ? IncomingMessages.Undecorate(text) : Symbol(text);
    }

    private static ReadingValue? Value(
        JsonElement raw, string name, string? text, JournalField? field, ReadingTone tone, string kind, bool partner = true)
    {
        if (Blank(text) is not { } written)
        {
            return null;
        }

        var localised = partner ? Blank(raw.String(name + Localised)) : null;
        var token = written.StartsWith('$') || (localised is not null && localised != written) ? written : null;
        var plain = localised ?? Clean(written);

        if (plain is null)
        {
            return null;
        }

        switch (field?.Decoder)
        {
            case FieldDecoder.Hull:
                var hull = EliteSpecifications.HullSaid(written);

                return Decoded(hull == written && localised is not null ? localised : hull, written, tone,
                    ShipLink(raw, name));

            case FieldDecoder.Module:
                var module = EliteSpecifications.ModuleName(written) ?? plain;

                return Decoded(module, written, tone, link: null, runs: ModuleRuns(module));

            case FieldDecoder.Material:
                return Decoded(MaterialCatalogue.Find(written)?.Name ?? plain, written, tone, link: null);

            case FieldDecoder.Engineer:
                var engineer = raw.Int("EngineerID") is { } id ? EngineerDirectory.ById(id) : EngineerDirectory.ByName(plain);

                return engineer is null
                    ? new ReadingValue(plain) { Symbol = token, Tone = tone }
                    : new ReadingValue(engineer.Name)
                    {
                        Symbol = token,
                        Tone = tone,
                        Link = new ReadingLink(ReadingLinkKind.Engineer, engineer.Id.ToString(Culture)),
                    };
        }

        return new ReadingValue(plain) { Symbol = token, Tone = tone };
    }

    private static ReadingValue Decoded(
        string text, string symbol, ReadingTone tone, ReadingLink? link, IReadOnlyList<ReadingRun>? runs = null) =>
        new(text)
        {
            Symbol = string.Equals(text, symbol, StringComparison.Ordinal) ? null : symbol,
            Tone = tone,
            Link = link,
            Runs = runs ?? [new ReadingRun(text, false)],
        };

    private static ReadingLink? ShipLink(JsonElement raw, string name) =>
        name is "Ship" or "ShipType" && raw.Long("ShipID") is { } id
            ? new ReadingLink(ReadingLinkKind.Ship, id.ToString(Culture))
            : null;

    /// <summary>A module's size and class at the start of its name, as a number run: <c>4A</c> in <c>4A Thrusters</c>.</summary>
    private static List<ReadingRun> ModuleRuns(string module)
    {
        var size = SizeAndClass().Match(module);

        return size.Success
            ? [new ReadingRun(size.Value, true), new ReadingRun(module[size.Length..], false)]
            : [new ReadingRun(module, false)];
    }

    [GeneratedRegex(@"^\d+[A-I]?(?= )")]
    private static partial Regex SizeAndClass();

    // ---- Numbers ---------------------------------------------------------------------------

    private static ReadingValue Number(JsonElement value, FieldUnit unit, ReadingTone tone)
    {
        var amount = value.GetDouble();
        var (number, rest) = unit switch
        {
            FieldUnit.Fraction => (Grouped(amount * 100), "%"),
            FieldUnit.Percent => (Grouped(amount), "%"),
            FieldUnit.Credits when value.TryGetInt64(out var credits) => CreditParts(credits),
            FieldUnit.LightSeconds => (Grouped(amount), " ls"),
            FieldUnit.LightYears => (Grouped(amount), " ly"),
            FieldUnit.Tonnes => (Grouped(amount), " t"),
            FieldUnit.SolarMasses => (Grouped(amount), " solar masses"),
            FieldUnit.MillionYears => (Grouped(amount), " million years"),
            FieldUnit.MetresPerSecondSquared => (Grouped(amount / StandardGravity), " g"),
            _ => (value.TryGetInt64(out var whole) ? whole.ToString("N0", Culture) : Grouped(amount), string.Empty),
        };

        return Counted(number, rest, tone);
    }

    private static ReadingValue Counted(string number, string rest, ReadingTone tone) =>
        new(number + rest)
        {
            Tone = tone,
            Runs = rest.Length == 0
                ? [new ReadingRun(number, true)]
                : [new ReadingRun(number, true), new ReadingRun(rest, false)],
        };

    private static string Grouped(double amount) => amount.ToString("#,##0.##", Culture);

    /// <summary>A credit figure split into its number and what follows it: <c>362.6</c> and <c> million</c>.</summary>
    private static (string Digits, string After) CreditParts(long credits)
    {
        var banded = SpokenCredits.Band(credits);
        var space = banded.IndexOf(' ');

        return space < 0 ? (banded, " Cr") : (banded[..space], banded[space..]);
    }

    // ---- Curated kinds ---------------------------------------------------------------------

    private static Curated CurateDocked(JsonElement raw)
    {
        var rows = new List<ReadingRow>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var warning in new[] { "Wanted", "ActiveFine" })
        {
            if (raw.Bool(warning) && JournalFields.Find(warning)?.TrueSentence is { } sentence)
            {
                rows.Add(new ReadingRow(string.Empty, [new ReadingValue(sentence) { Tone = ReadingTone.Warning }])
                {
                    Field = warning,
                });
            }

            used.Add(warning);
        }

        var station = new List<ReadingValue>();

        if (Blank(raw.String("StationType")) is { } stationType)
        {
            station.Add(new ReadingValue(Spaced(stationType)) { Symbol = stationType });
        }

        if (raw.Double("DistFromStarLS") is { } distance)
        {
            var whole = Math.Round(distance).ToString("N0", Culture);

            station.Add(new ReadingValue($"{whole} ls from the star")
            {
                Runs = [new ReadingRun(whole, true), new ReadingRun(" ls from the star", false)],
            });
        }

        AddRow(rows, "Station", station);
        used.UnionWith(["StationType", "DistFromStarLS"]);

        var runBy = new List<ReadingValue>();

        if (raw.Object("StationFaction") is { } faction && NameOf(faction) is { } factionName)
        {
            runBy.Add(new ReadingValue(factionName) { Tone = ReadingTone.Name });
        }

        foreach (var property in new[] { "StationGovernment", "StationEconomy" })
        {
            if (Value(raw, property, raw.String(property), null, ReadingTone.Value, "Docked") is { } value)
            {
                runBy.Add(value);
            }
        }

        AddRow(rows, "Run by", runBy);
        used.UnionWith(["StationFaction", "StationGovernment", "StationGovernment_Localised",
            "StationEconomy", "StationEconomy_Localised", "StationEconomies"]);

        AddRow(rows, "Landing pads", Pads(raw));
        used.Add("LandingPads");

        var services = raw.Items("StationServices")
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(token => !StationServices.IsEverywhere(token))
            .Select(token => (Token: token, Described: StationServices.Describe(token)))
            .OrderBy(service => service.Described.Order)
            .Select(service => new ReadingValue(service.Described.Name) { Symbol = service.Token })
            .ToList();

        AddRow(rows, "You can do here", services, "StationServices");
        used.Add("StationServices");

        return new Curated(rows, used);
    }

    private static Curated CurateReceiveText(JsonElement raw)
    {
        var rows = new List<ReadingRow>();
        var sender = Blank(raw.String("From_Localised")) ?? Blank(raw.String("From"));

        if (sender is not null)
        {
            var typed = raw.String("From_Localised") is null && !sender.StartsWith('$');

            AddRow(rows, "From", [typed
                ? new ReadingValue(sender) { Typed = true }
                : new ReadingValue(IncomingMessages.Undecorate(sender)) { Tone = ReadingTone.Name }], "From");
        }

        if (Blank(raw.String("Channel")) is { } channel)
        {
            AddRow(rows, "Channel", [new ReadingValue(JournalJson.Spoken(channel) ?? channel)], "Channel");
        }

        if (raw.String("Message") is { Length: > 0 } message)
        {
            AddRow(rows, "Message", [JournalText.IsFrontiersString(raw)
                ? new ReadingValue(raw.String("Message_Localised")!)
                : new ReadingValue(message) { Typed = true }], "Message");
        }

        return new Curated(rows, ["From", "From_Localised", "Channel", "Message", "Message_Localised"]);
    }

    private static void AddRow(List<ReadingRow> rows, string label, List<ReadingValue> values, string? field = null)
    {
        if (values.Count > 0)
        {
            rows.Add(new ReadingRow(label, values) { Field = field });
        }
    }

    private static List<ReadingValue> Pads(JsonElement raw)
    {
        var pads = new List<ReadingValue>();

        foreach (var size in new[] { "Small", "Medium", "Large" })
        {
            if (raw.Object("LandingPads")?.Int(size) is { } count)
            {
                var label = $" {size.ToLowerInvariant()}";

                pads.Add(new ReadingValue($"{count}{label}")
                {
                    Runs = [new ReadingRun(count.ToString(Culture), true), new ReadingRun(label, false)],
                });
            }
        }

        return pads;
    }

    // ---- Small common kinds ----------------------------------------------------------------

    /// <summary>One row per group, its values read the way the mechanical rows read them.</summary>
    private static Curated Group(JsonElement raw, string kind, params (string Label, string[] Fields)[] groups) =>
        Group(raw, field => Mechanical(raw, kind, field), groups);

    private static Curated Group(
        JsonElement raw, Func<string, IReadOnlyList<ReadingValue>> valuesOf, params (string Label, string[] Fields)[] groups)
    {
        var rows = new List<ReadingRow>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (label, fields) in groups)
        {
            var values = new List<ReadingValue>();

            foreach (var field in fields)
            {
                if (raw.TryGetProperty(field, out _))
                {
                    values.AddRange(valuesOf(field));
                }

                used.Add(field);
            }

            AddRow(rows, label, values, fields[0]);
        }

        return new Curated(rows, used);
    }

    private static IReadOnlyList<ReadingValue> Mechanical(JsonElement raw, string kind, string field)
    {
        foreach (var property in raw.EnumerateObject())
        {
            if (property.Name == field)
            {
                return MechanicalRow(raw, property, kind)?.Values ?? [];
            }
        }

        return [];
    }

    private static Curated Stationed(JsonElement raw, List<ReadingRow> rows)
    {
        var station = new List<ReadingValue>();

        if (Blank(raw.String("StationName")) is { } name)
        {
            station.Add(new ReadingValue(name));
        }

        if (Blank(raw.String("StationType")) is { } type)
        {
            station.Add(new ReadingValue(Spaced(type)) { Symbol = type });
        }

        AddRow(rows, "Station", station, "StationName");

        return new Curated(rows, ["StationName", "StationType"]);
    }

    private static Curated CurateDockingRequested(JsonElement raw)
    {
        var curated = Stationed(raw, []);

        AddRow(curated.Rows, "Landing pads", Pads(raw), "LandingPads");
        curated.Used.Add("LandingPads");

        return curated;
    }

    private static Curated CurateDockingGranted(JsonElement raw)
    {
        var rows = new List<ReadingRow>();

        AddRow(rows, "Pad", [.. Mechanical(raw, "DockingGranted", "LandingPad")], "LandingPad");

        var curated = Stationed(raw, rows);

        curated.Used.Add("LandingPad");

        return curated;
    }

    private static Curated CurateShipTargeted(JsonElement raw) =>
        Group(
            raw,
            field => field switch
            {
                "PilotName" => Pilot(raw),
                "SquadronID" => Blank(raw.String(field)) is { } tag ? [new ReadingValue(tag) { Tone = ReadingTone.Name }] : [],
                "LegalStatus" =>
                [
                    .. Mechanical(raw, "ShipTargeted", field)
                        .Select(value => value.Text == "Wanted" ? value with { Tone = ReadingTone.Warning } : value),
                ],
                _ => Mechanical(raw, "ShipTargeted", field),
            },
            ("Ship", ["Ship"]),
            ("Pilot", ["PilotName"]),
            ("Rank", ["PilotRank"]),
            ("Squadron", ["SquadronID"]),
            ("Faction", ["Faction"]),
            ("Power", ["Power"]),
            ("Legal status", ["LegalStatus"]),
            ("Bounty", ["Bounty"]),
            ("Shields", ["ShieldHealth"]),
            ("Hull", ["HullHealth"]),
            ("Aimed at", ["Subsystem", "SubsystemHealth"]));

    /// <summary>The pilot Elite names, else the player's own typed name.</summary>
    private static IReadOnlyList<ReadingValue> Pilot(JsonElement raw)
    {
        if (Blank(raw.String("PilotName_Localised")) is { } localised)
        {
            return [new ReadingValue(localised) { Tone = ReadingTone.Name }];
        }

        if (Blank(raw.String("PilotName")) is not { } name)
        {
            return [];
        }

        return name.StartsWith('$')
            ? [new ReadingValue(Clean(name) ?? name) { Symbol = name, Tone = ReadingTone.Name }]
            : [new ReadingValue(name) { Typed = true }];
    }

    private static Curated CurateBackpackChange(JsonElement raw)
    {
        var rows = new List<ReadingRow>();

        foreach (var (label, field) in new[] { ("Into the backpack", "Added"), ("Out of the backpack", "Removed") })
        {
            AddRow(rows, label, [.. raw.Items(field).Select(BackpackItem).OfType<ReadingValue>()], field);
        }

        return new Curated(rows, ["Added", "Removed"]);
    }

    private static ReadingValue? BackpackItem(JsonElement item)
    {
        if (NameOf(item) is not { } name)
        {
            return null;
        }

        return Counted(name, item);
    }

    private static ReadingValue Counted(string name, JsonElement item)
    {
        if (item.Int("Count") is not { } count)
        {
            return new ReadingValue(name);
        }

        return new ReadingValue($"{name} ×{count}")
        {
            Runs = [new ReadingRun($"{name} ×", false), new ReadingRun(count.ToString(Culture), true)],
        };
    }
}
