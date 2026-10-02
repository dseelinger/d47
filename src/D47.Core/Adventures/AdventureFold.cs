using System.Text.Json;
using System.Globalization;
using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Adventures;

/// <summary>
/// Where one adventure stands, computed rather than read (Phase 47, "Progress is derived, and an
/// adventure counts forward only").
/// </summary>
public sealed record AdventureStanding
{
    public required Adventure Adventure { get; init; }

    /// <summary>When each beat reached so far fired, oldest first.</summary>
    public IReadOnlyList<DateTimeOffset> Fired { get; init; } = [];

    /// <summary>When each <c>LoadGame</c> since acceptance was written, oldest first.</summary>
    public IReadOnlyList<DateTimeOffset> Loads { get; init; } = [];

    /// <summary>The system the Commander is in, from the last <c>FSDJump</c>, <c>Location</c> or <c>CarrierJump</c> folded, acceptance or not.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>The running total toward the current beat when it is counted: events, or tons sold or refined.</summary>
    public int Counted { get; init; }

    /// <summary>The index of the beat the story is waiting on.</summary>
    public int Current => Fired.Count;

    public bool IsDone => Adventure.IsBegun && Fired.Count >= Adventure.Beats.Count && Adventure.Beats.Count > 0;

    public AdventureBeat? CurrentBeat =>
        Adventure.IsBegun && !IsDone && Current < Adventure.Beats.Count ? Adventure.Beats[Current] : null;

    public AdventureBeat? LastBeat => Fired.Count > 0 ? Adventure.Beats[Fired.Count - 1] : null;

    public DateTimeOffset? LastFiredAt => Fired.Count > 0 ? Fired[^1] : null;

    /// <summary>Derived — the last beat's fire time — and never written.</summary>
    public DateTimeOffset? FinishedAt => IsDone ? Fired[^1] : null;

    /// <summary>Whether the turn (the beat whose function says so) has fired, for the spoiler rule.</summary>
    public bool TurnReached => Reached("midpoint", "turn");

    public bool EndingReached => IsDone;

    /// <summary>The labels of the buttons on the reading page, in order. A stock story's chapter has no Edit, Remove or Write the next chapter.</summary>
    public IReadOnlyList<string> ReadingButtons()
    {
        var adventure = Adventure;

        if (adventure.IsDraft)
        {
            return ["Accept", "Change something", "Decline"];
        }

        var story = adventure.StoryId is not null;
        List<string> buttons = [];

        if (!adventure.IsBegun)
        {
            buttons.Add("Begin");
        }
        else if (adventure.IsAbandoned)
        {
            buttons.Add("Begin again");
        }
        else if (!IsDone)
        {
            buttons.Add("Abandon");
        }
        else if (!story)
        {
            buttons.Add("Write the next chapter");
        }

        if (!story)
        {
            if (!adventure.IsBegun || adventure.IsAbandoned || !IsDone)
            {
                buttons.Add("Edit");
            }

            buttons.Add("Remove");
        }

        return buttons;
    }

    /// <summary>How far through, as a count (asked for 2026-08-22).</summary>
    public string? Step()
    {
        if (!Adventure.IsBegun || Adventure.Beats.Count == 0)
        {
            return null;
        }

        var of = Adventure.Beats.Count;
        var at = Math.Min(Fired.Count + (IsDone ? 0 : 1), of);

        return $"Step {at.ToString(CultureInfo.InvariantCulture)} of {of.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>What the Commander did to reach where they are — the last beat's trigger, in words.</summary>
    public string? LastTrigger() => LastBeat?.Trigger.Describe();

    /// <summary>What the story is waiting for the Commander to do next, in words.</summary>
    public string? NextTrigger() =>
        Adventure.IsActive && CurrentBeat is { } current ? current.Trigger.Progress(Counted) ?? current.Trigger.Describe() : null;

    /// <summary>The last thing the ship's AI actually said about this story, beat or aside.</summary>
    public AdventureTold? LastSaid() => Adventure.Told.Count > 0 ? Adventure.Told[^1] : null;

    /// <summary>Where the story is, in words a card can show.</summary>
    public string Place()
    {
        if (Adventure.IsAbandoned)
        {
            return CurrentBeatTitle() is { } waiting ? $"abandoned at {waiting}" : "abandoned";
        }

        if (!Adventure.IsBegun)
        {
            return Adventure.IsDraft ? "waiting for your yes" : "not begun";
        }

        if (IsDone)
        {
            return "finished";
        }

        return CurrentBeatTitle() ?? "under way";
    }

    /// <summary>The whole standing in one sentence, for the spoken path and the Technical transcript.</summary>
    public string Describe(DateTimeOffset now)
    {
        var parts = new List<string> { $"{Adventure.Name}: {Place()}." };

        if (Adventure.IsBegun && !IsDone && !Adventure.IsAbandoned && LastFiredAt is { } last)
        {
            parts.Add($"Last beat {Ago(now - last)}.");
        }

        if (Adventure.IsBegun && Adventure.Beats.Count > 0)
        {
            parts.Add(
                $"({Math.Min(Fired.Count, Adventure.Beats.Count).ToString(CultureInfo.InvariantCulture)} of "
                + $"{Adventure.Beats.Count.ToString(CultureInfo.InvariantCulture)} beats)");
        }

        return string.Join(" ", parts);
    }

    private string? CurrentBeatTitle() =>
        Current < Adventure.Beats.Count ? Adventure.Beats[Current].Title : null;

    /// <summary>Whether a beat's function marks the story turning: the same words as <see cref="TurnReached"/>, plus "all is lost".</summary>
    public static bool IsTurning(string? function) =>
        function is not null
        && new[] { "midpoint", "turn", "all is lost" }.Any(word => function.Contains(word, StringComparison.OrdinalIgnoreCase));

    private bool Reached(params string[] functions)
    {
        for (var index = 0; index < Fired.Count && index < Adventure.Beats.Count; index++)
        {
            if (Adventure.Beats[index].Function is { } function
                && functions.Any(wanted => function.Contains(wanted, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string Ago(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "just now",
        < 60 => $"{(int)age.TotalMinutes} minutes ago",
        < 1440 => $"{(int)age.TotalHours} hours ago",
        < 20160 => $"{(int)age.TotalDays} days ago",
        _ => $"{(int)(age.TotalDays / 7)} weeks ago",
    };
}

/// <summary>The one fold, for the live tick and the startup catch-up alike (Phase 47).</summary>
public static class AdventureFold
{
    /// <summary>
    /// Whether one journal event is what this trigger waits for, with the Commander in
    /// <paramref name="systemAddress"/>. For a counted trigger, whether the event counts toward it.
    /// </summary>
    public static bool Matches(AdventureTrigger trigger, JournalEvent journalEvent, long? systemAddress = null)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (!trigger.IsResolved)
        {
            return false;
        }

        var raw = journalEvent.Raw;

        return trigger.Kind switch
        {
            TriggerKind.Arrive =>
                journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump"
                && raw.Long("SystemAddress") == trigger.SystemAddress,

            TriggerKind.Dock =>
                journalEvent.Kind is "Docked"
                && raw.Long("MarketID") == trigger.MarketId,

            TriggerKind.Land =>
                journalEvent.Kind is "Touchdown"
                && raw.Long("SystemAddress") == trigger.SystemAddress
                && raw.Int("BodyID") == trigger.BodyId,

            // A scan beat is satisfied by the scan, or by the Commander going to the body (#77).
            TriggerKind.Scan =>
                journalEvent.Kind is "Scan" or "ApproachBody" or "SupercruiseExit"
                && raw.Long("SystemAddress") == trigger.SystemAddress
                && raw.Int("BodyID") == trigger.BodyId,

            TriggerKind.Rank =>
                journalEvent.Kind is "Promotion"
                && trigger.Career is { } career
                && raw.Int(career) is { } reached
                && reached >= trigger.Rank,

            TriggerKind.Board =>
                journalEvent.Kind is "ShipyardNew" or "ShipyardSwap"
                && string.Equals(raw.String("ShipType"), trigger.ShipType, StringComparison.OrdinalIgnoreCase),

            TriggerKind.Beacon =>
                systemAddress == trigger.SystemAddress
                && GuardianCores.IsBeaconScan(journalEvent, systemAddress),

            TriggerKind.Bounty => journalEvent.Kind is "Bounty",

            TriggerKind.Bond =>
                journalEvent.Kind is "FactionKillBond"
                && SameFaction(trigger.Faction, raw.String("AwardingFaction")),

            TriggerKind.Mission =>
                journalEvent.Kind is "MissionCompleted"
                && SameFaction(trigger.Faction, raw.String("Faction"))
                && MissionFamilies.Counts(trigger.MissionFamily, raw.String("Name")),

            TriggerKind.Sell =>
                journalEvent.Kind is "MarketSell"
                && SameCommodity(trigger.Commodity, raw.String("Type"))
                && (trigger.MarketId is null || raw.Long("MarketID") == trigger.MarketId),

            TriggerKind.Mine =>
                journalEvent.Kind is "MiningRefined"
                && SameCommodity(trigger.Commodity, raw.String("Type")),

            TriggerKind.OnFoot =>
                journalEvent.Kind is "Disembark"
                && raw.Bool("OnPlanet") && !raw.Bool("OnStation")
                && AtBody(trigger, raw),

            TriggerKind.Collect =>
                journalEvent.Kind is "CollectItems"
                && (SameType(trigger.Filter, raw.String("Type")) || SameType(trigger.Filter, raw.String("Name"))),

            TriggerKind.Organic =>
                journalEvent.Kind is "ScanOrganic"
                && string.Equals(raw.String("ScanType"), "Analyse", StringComparison.OrdinalIgnoreCase)
                && SameType(trigger.Filter, raw.String("Genus")),

            TriggerKind.Map =>
                journalEvent.Kind is "SAAScanComplete"
                && AtBody(trigger, raw),

            TriggerKind.Signal =>
                journalEvent.Kind is "SAASignalsFound"
                && raw.Items("Signals").Any(signal => SameType(trigger.Filter, signal.String("Type"))),

            TriggerKind.Wreck =>
                journalEvent.Kind is "Touchdown"
                && WreckType(raw.String("NearestDestination")) is { } wreck
                && string.Equals(wreck, string.IsNullOrWhiteSpace(trigger.Filter) ? "Unknown" : trigger.Filter.Trim(), StringComparison.OrdinalIgnoreCase),

            TriggerKind.Codex =>
                journalEvent.Kind is "CodexEntry"
                && SameType(trigger.Filter, raw.String("Category")),

            TriggerKind.DataSale =>
                journalEvent.Kind switch
                {
                    "SellExplorationData" or "MultiSellExplorationData" => trigger.Organic != true,
                    "SellOrganicData" => trigger.Organic != false,
                    _ => false,
                },

            TriggerKind.Salvage =>
                journalEvent.Kind is "CollectCargo"
                && SameType(trigger.Filter, raw.String("Type")),

            TriggerKind.Uss =>
                journalEvent.Kind is "USSDrop"
                && SameType(trigger.Filter, raw.String("USSType")),

            TriggerKind.Rescue =>
                journalEvent.Kind is "SearchAndRescue"
                && SameType(trigger.Filter, raw.String("Name")),

            TriggerKind.Engineer =>
                journalEvent.Kind is "EngineerProgress"
                && EngineerReached(trigger, raw),

            TriggerKind.Srv => journalEvent.Kind is "LaunchSRV",

            TriggerKind.Crew => journalEvent.Kind is "CrewHire",

            _ => false,
        };
    }

    /// <summary>How much one matching event adds to a counted trigger's total: tons, items or credits where the kind counts those, one otherwise.</summary>
    public static int Amount(AdventureTrigger trigger, JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(journalEvent);

        var raw = journalEvent.Raw;

        return trigger.Kind switch
        {
            TriggerKind.Sell => Math.Max(raw.Int("Count") ?? 0, 0),
            TriggerKind.Collect or TriggerKind.Rescue => Math.Max(raw.Int("Count") ?? 1, 0),
            TriggerKind.DataSale => (int)Math.Clamp(Credits(journalEvent.Kind, raw), 0, int.MaxValue),
            _ => 1,
        };
    }

    private static long Credits(string kind, JsonElement raw) => kind is "SellOrganicData"
        ? raw.Items("BioData").Sum(entry => (entry.Long("Value") ?? 0) + (entry.Long("Bonus") ?? 0))
        : raw.Long("TotalEarnings") ?? 0;

    private static bool SameFaction(string? wanted, string? actual) =>
        string.IsNullOrWhiteSpace(wanted) || string.Equals(wanted.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Folded symbols with spaces removed, so "$platinum_name;", "platinum" and "Platinum" are one commodity.</summary>
    private static bool SameCommodity(string? wanted, string? actual) =>
        string.IsNullOrWhiteSpace(wanted) || (Fold(wanted) is { } folded && folded == Fold(actual));

    private static string? Fold(string? commodity) => JournalJson.Symbol(commodity)?.Replace(" ", string.Empty, StringComparison.Ordinal);

    /// <summary>Equal once both lose any <c>$…;</c> wrapping and case, or the wanted word is one underscore-separated part of the actual, as <c>Thargoid</c> is of <c>$SAA_SignalType_Thargoid;</c>.</summary>
    private static bool SameType(string? wanted, string? actual) =>
        string.IsNullOrWhiteSpace(wanted)
        || (JournalJson.Symbol(wanted) is { } folded
            && JournalJson.Symbol(actual) is { } found
            && (folded == found || found.Split('_').Contains(folded)));

    private static bool AtBody(AdventureTrigger trigger, JsonElement raw) =>
        (trigger.SystemAddress is null || raw.Long("SystemAddress") == trigger.SystemAddress)
        && (trigger.BodyId is null || raw.Int("BodyID") == trigger.BodyId);

    /// <summary>The wreck type in a <c>$Settlement_Unflattened_Wrecked…:</c> destination, or null for any other.</summary>
    private static string? WreckType(string? destination)
    {
        const string prefix = "$Settlement_Unflattened_Wrecked";

        if (destination is null || !destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = destination[prefix.Length..];
        var end = rest.IndexOfAny([':', ';']);

        return end < 0 ? rest : rest[..end];
    }

    /// <summary>The single-engineer form, or the startup list, shows the engineer at the stage or past it.</summary>
    private static bool EngineerReached(AdventureTrigger trigger, JsonElement raw)
    {
        var wanted = EngineerStages.Rank(trigger.Stage);

        bool Reached(JsonElement entry) =>
            string.Equals(entry.String("Engineer")?.Trim(), trigger.Engineer?.Trim(), StringComparison.OrdinalIgnoreCase)
            && EngineerStages.Rank(entry.String("Progress")) >= wanted;

        return Reached(raw) || raw.Items("Engineers").Any(Reached);
    }

    /// <summary>One event against one standing.</summary>
    public static AdventureStanding Apply(AdventureStanding standing, JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(standing);
        ArgumentNullException.ThrowIfNull(journalEvent);

        var adventure = standing.Adventure;

        // Before the acceptance check, so a Commander already in a beat's system when it was accepted is counted.
        if (journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump"
            && journalEvent.Raw.Long("SystemAddress") is { } arrived
            && arrived != standing.SystemAddress)
        {
            standing = standing with { SystemAddress = arrived };
        }

        if (!adventure.IsBegun || standing.IsDone)
        {
            return standing;
        }

        if (journalEvent.Timestamp < adventure.AcceptedAt)
        {
            return standing;
        }

        if (adventure.AbandonedAt is { } abandoned && journalEvent.Timestamp >= abandoned)
        {
            return standing;
        }

        if (journalEvent.Kind is "LoadGame")
        {
            return standing with { Loads = [.. standing.Loads, journalEvent.Timestamp] };
        }

        var current = standing.CurrentBeat;

        if (current is null || !Matches(current.Trigger, journalEvent, standing.SystemAddress))
        {
            return standing;
        }

        if (current.Trigger.IsCounted)
        {
            var total = (int)Math.Min((long)standing.Counted + Amount(current.Trigger, journalEvent), int.MaxValue);

            if (total < current.Trigger.Count)
            {
                return standing with { Counted = total };
            }
        }

        return standing with { Fired = [.. standing.Fired, journalEvent.Timestamp], Counted = 0 };
    }

    /// <summary>A fresh standing: begun or not, nothing fired yet, in <paramref name="systemAddress"/> when it is known.</summary>
    public static AdventureStanding Start(Adventure adventure, long? systemAddress = null) =>
        new() { Adventure = adventure, SystemAddress = systemAddress };
}
