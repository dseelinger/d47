using System.Text.Json;
using System.Globalization;
using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Adventures;

/// <summary>A <c>failsOn</c> trigger matched: the beat the chapter was waiting on, when, and why. <paramref name="Live"/> is set when the book found it announcing.</summary>
public sealed record AdventureFailure(int Beat, DateTimeOffset At, string Reason, bool Live = false);

/// <summary>
/// Where one adventure stands, computed rather than read (Phase 47, "Progress is derived, and an
/// adventure counts forward only").
/// </summary>
public sealed record AdventureStanding
{
    public required Adventure Adventure { get; init; }

    /// <summary>When each beat reached so far fired, oldest first.</summary>
    public IReadOnlyList<DateTimeOffset> Fired { get; init; } = [];

    /// <summary>The journal event kind that fired each beat in <see cref="Fired"/>, in the same order, or the source of a step.</summary>
    public IReadOnlyList<string> FiredBy { get; init; } = [];

    /// <summary>When each <c>LoadGame</c> since acceptance was written, oldest first.</summary>
    public IReadOnlyList<DateTimeOffset> Loads { get; init; } = [];

    /// <summary>The system the Commander is in, from the last <c>FSDJump</c>, <c>Location</c> or <c>CarrierJump</c> folded, acceptance or not.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>The running total toward the current beat when it is counted: events, or tons sold or refined.</summary>
    public int Counted { get; init; }

    /// <summary>The last suit mods, ship cosmetics and fleet carrier system seen for each suit, weapon, ship and carrier, for <see cref="AdventureWatch"/>.</summary>
    public IReadOnlyDictionary<string, string> Seen { get; init; } = AdventureWatch.Nothing;

    /// <summary>The side the current conflict beat's first counted contribution went to; later contributions count only for it.</summary>
    public string? Side { get; init; }

    /// <summary>The conflicts the Commander contributed to for a conflict beat, with how each ended once an arrival showed it had.</summary>
    public IReadOnlyList<ConflictPart> Parts { get; init; } = [];

    /// <summary>The notable things the journal shows the Commander did after acceptance, oldest first.</summary>
    public IReadOnlyList<Notable> Notables { get; init; } = [];

    /// <summary>Set by the fold when a <c>failsOn</c> trigger matched; the book stores it as the adventure's ending.</summary>
    public AdventureFailure? Failure { get; init; }

    /// <summary>The index of the beat the story is waiting on.</summary>
    public int Current => Fired.Count;

    public bool IsDone => Adventure.IsBegun && Fired.Count >= Adventure.Beats.Count && Adventure.Beats.Count > 0;

    /// <summary>Whether a <c>failsOn</c> trigger matched, stored or not.</summary>
    public bool IsFailed => Failure is not null || Adventure.IsFailed;

    public AdventureBeat? CurrentBeat =>
        Adventure.IsBegun && !IsDone && !IsFailed && Current < Adventure.Beats.Count ? Adventure.Beats[Current] : null;

    public AdventureBeat? LastBeat => Fired.Count > 0 ? Adventure.Beats[Fired.Count - 1] : null;

    public DateTimeOffset? LastFiredAt => Fired.Count > 0 ? Fired[^1] : null;

    /// <summary>Derived — the last beat's fire time — and never written.</summary>
    public DateTimeOffset? FinishedAt => IsDone ? Fired[^1] : null;

    /// <summary>Whether the turn (the beat whose function says so) has fired, for the spoiler rule.</summary>
    public bool TurnReached => Reached("midpoint", "turn");

    public bool EndingReached => IsDone;

    /// <summary>The labels of the buttons on the reading page, in order. A stock story's chapter has no Remove or Write the next chapter; a short story under way offers Remove only.</summary>
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
        else if (adventure.IsFailed && !story)
        {
            buttons.Add("Write the next chapter");
        }
        else if (IsDone && !story)
        {
            buttons.Add("Write the next chapter");
        }

        if (!story)
        {
            buttons.Add("Remove");
        }

        return buttons;
    }

    /// <summary>Whether a story chapter is waiting on a beat the Commander may refuse: any but the beacon scan that ends act one.</summary>
    public bool IsRefusable =>
        Adventure is { StoryId: not null, IsActive: true } && CurrentBeat is { Trigger.Kind: not TriggerKind.Beacon };

    /// <summary>Whether a begun, unfinished short story is waiting on a beat the Commander may replace.</summary>
    public bool IsShortStoryRefusable =>
        Adventure is { StoryId: null, IsActive: true } && CurrentBeat is not null;

    /// <summary>How far through, as a count (asked for 2026-08-22).</summary>
    public string? Step()
    {
        if (!Adventure.IsBegun || Adventure.Beats.Count == 0 || IsFailed)
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
        if (IsFailed)
        {
            return "failed";
        }

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

        if (IsFailed)
        {
            parts.Add(Adventure.Ending?.Reason ?? Failure!.Reason);
            return string.Join(" ", parts);
        }

        if (Adventure.IsBegun && !IsDone && !Adventure.IsAbandoned && LastFiredAt is { } last)
        {
            parts.Add($"Last objective {Ago(now - last)}.");
        }

        if (Adventure.IsBegun && Adventure.Beats.Count > 0)
        {
            parts.Add(
                $"({Math.Min(Fired.Count, Adventure.Beats.Count).ToString(CultureInfo.InvariantCulture)} of "
                + $"{Adventure.Beats.Count.ToString(CultureInfo.InvariantCulture)} objectives)");
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

            // Arriving in the beacon's system proves the ship can make the trip, whatever its range.
            TriggerKind.Reach =>
                journalEvent.Kind is "CarrierBuy"
                || (journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump"
                    && string.Equals(raw.String("StarSystem"), trigger.System?.Trim(), StringComparison.OrdinalIgnoreCase))
                || (journalEvent.Kind is "Loadout"
                    && trigger.JumpRange is { } needed
                    && Stories.BeaconReach.CanMakeTrip(new Journal.ShipLoadout().Apply(journalEvent), needed)),

            TriggerKind.Beacon =>
                systemAddress == trigger.SystemAddress
                && GuardianCores.IsBeaconScan(journalEvent, systemAddress),

            TriggerKind.Site => trigger.Filter switch
            {
                AdventureTrigger.GuardianSite =>
                    journalEvent.Kind is "ApproachSettlement"
                    && raw.String("Name") is { } name && name.StartsWith("$Ancient", StringComparison.Ordinal)
                    && raw.Long("SystemAddress") == trigger.SystemAddress
                    && raw.Int("BodyID") == trigger.BodyId,
                AdventureTrigger.TouristSite =>
                    GuardianCores.IsTouristBeaconScan(journalEvent)
                    && systemAddress == trigger.SystemAddress,
                _ => false,
            },

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
                && (SameType(trigger.Filter, raw.String("Category")) || SameType(trigger.Filter, raw.String("SubCategory")))
                && (trigger.New != true || (raw.TryGetProperty("IsNewEntry", out var isNew) && isNew.ValueKind == JsonValueKind.True)),

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

            TriggerKind.CarrierBuy =>
                journalEvent.Kind is "CarrierBuy"
                && !string.Equals(raw.String("CarrierType"), "SquadronCarrier", StringComparison.OrdinalIgnoreCase),

            TriggerKind.Wing => journalEvent.Kind is "WingJoin" or "WingAdd",

            TriggerKind.Multicrew => journalEvent.Kind is "JoinACrew",

            TriggerKind.Squadron => journalEvent.Kind is "JoinedSquadron",

            TriggerKind.SquadronFound => journalEvent.Kind is "SquadronCreated",

            TriggerKind.Faction =>
                journalEvent.Kind is "MissionCompleted"
                && InfluenceLedger.UpMarks(journalEvent, trigger.Faction!, trigger.SystemAddress) > 0,

            TriggerKind.Boost =>
                journalEvent.Kind is "JetConeBoost"
                && BoostStar(trigger.Filter, raw.Double("BoostValue")),

            TriggerKind.Contribute =>
                journalEvent.Kind is "ColonisationContribution"
                && Delivered(trigger, raw) > 0,

            TriggerKind.Claim => journalEvent.Kind is "ColonisationSystemClaim",

            TriggerKind.Unlock =>
                journalEvent.Kind is "TechnologyBroker"
                && Knowledge.TechBrokerCatalogue.ForName(trigger.Filter) is { } row
                && raw.Items("ItemsUnlocked").Any(item => Knowledge.TechBrokerCatalogue.ForSymbol(item.String("Name"))?.Name == row.Name),

            TriggerKind.Pledge =>
                journalEvent.Kind is "PowerplayJoin"
                && (string.IsNullOrWhiteSpace(trigger.Filter) || string.Equals(trigger.Filter.Trim(), raw.String("Power"), StringComparison.OrdinalIgnoreCase)),

            TriggerKind.PowerRank =>
                journalEvent.Kind is "PowerplayRank"
                && raw.Int("Rank") >= trigger.Rank,

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
            TriggerKind.Contribute => Delivered(trigger, raw),
            TriggerKind.Collect or TriggerKind.Rescue => Math.Max(raw.Int("Count") ?? 1, 0),
            TriggerKind.DataSale => (int)Math.Clamp(Credits(journalEvent.Kind, raw), 0, int.MaxValue),
            TriggerKind.Faction => InfluenceLedger.UpMarks(journalEvent, trigger.Faction ?? string.Empty, trigger.SystemAddress),
            _ => 1,
        };
    }

    /// <summary>The tons in a <c>ColonisationContribution</c> whose commodity the trigger accepts.</summary>
    private static int Delivered(AdventureTrigger trigger, JsonElement raw) =>
        raw.Items("Contributions")
            .Where(entry => SameCommodity(trigger.Commodity, entry.String("Name")))
            .Sum(entry => Math.Max(entry.Int("Amount") ?? 0, 0));

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

    /// <summary>A boost of 4 or more is a neutron star's; less is a white dwarf's.</summary>
    private static bool BoostStar(string? wanted, double? value) =>
        JournalJson.Symbol(wanted) switch
        {
            null or "" => true,
            "neutron" => value >= 4,
            "whitedwarf" => value < 4,
            _ => false,
        };

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

    /// <summary>One event against one standing, with no conflict beat counting.</summary>
    public static AdventureStanding Apply(AdventureStanding standing, JournalEvent journalEvent) =>
        Apply(standing, journalEvent, null);

    /// <summary>
    /// One event against one standing. <paramref name="world"/> is what the Commander has seen once this event is folded into it;
    /// without it no conflict beat counts.
    /// </summary>
    public static AdventureStanding Apply(AdventureStanding standing, JournalEvent journalEvent, AdventureWorld? world)
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

        if (world is not null && standing.Parts.Any(part => !part.Ended) && journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump")
        {
            standing = standing with { Parts = [.. standing.Parts.Select(part => Ended(part, world))] };
        }

        var observation = AdventureWatch.Observe(standing.Seen, journalEvent);

        if (!ReferenceEquals(observation.Seen, standing.Seen))
        {
            standing = standing with { Seen = observation.Seen };
        }

        if (!adventure.IsBegun || standing.IsDone || standing.IsFailed)
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

        if (adventure.Ending is { } ended && journalEvent.Timestamp >= ended.At)
        {
            return standing;
        }

        var notables = ChapterNotables.Fold(standing.Notables, journalEvent);

        if (!ReferenceEquals(notables, standing.Notables))
        {
            standing = standing with { Notables = notables };
        }

        if (journalEvent.Kind is "LoadGame")
        {
            return standing with { Loads = [.. standing.Loads, journalEvent.Timestamp] };
        }

        var current = standing.CurrentBeat;

        if (current is null)
        {
            return standing;
        }

        // A replaced beat counts only what happens after the replacement.
        if (adventure.RewrittenFrom is { } from && standing.Current >= from && journalEvent.Timestamp < adventure.RewrittenAt)
        {
            return standing;
        }

        if (FailedOn(current, standing, journalEvent, observation, world) is { } failing)
        {
            return standing with
            {
                Failure = new AdventureFailure(standing.Current, journalEvent.Timestamp, $"At \"{current.Title}\" the Commander did what ruled it out: {failing.Describe()}."),
            };
        }

        if (current.Trigger.Kind == TriggerKind.Conflict)
        {
            return ApplyConflict(standing, current.Trigger, journalEvent, world);
        }

        var watched = IsWatched(current.Trigger.Kind);
        var gained = Gained(current.Trigger, journalEvent, observation, world);

        if (watched ? gained == 0 || !current.Trigger.IsResolved : !Matches(current.Trigger, journalEvent, standing.SystemAddress))
        {
            return standing;
        }

        if (current.Trigger.IsCounted)
        {
            var total = (int)Math.Min((long)standing.Counted + (watched ? gained : Amount(current.Trigger, journalEvent)), int.MaxValue);

            if (total < current.Trigger.Count)
            {
                return standing with { Counted = total };
            }
        }

        return standing with { Fired = [.. standing.Fired, journalEvent.Timestamp], FiredBy = [.. standing.FiredBy, journalEvent.Kind], Counted = 0 };
    }

    private static bool IsWatched(TriggerKind kind) =>
        kind is TriggerKind.SuitMod or TriggerKind.Livery or TriggerKind.CarrierJump or TriggerKind.Discover or TriggerKind.Footfall;

    private static int Gained(AdventureTrigger trigger, JournalEvent journalEvent, AdventureObservation observation, AdventureWorld? world) =>
        trigger.Kind == TriggerKind.Footfall
            ? world?.IsFirstFootfall(journalEvent) == true ? 1 : 0
            : IsWatched(trigger.Kind) ? AdventureWatch.Amount(trigger, observation) : 0;

    /// <summary>The first of the beat's <c>failsOn</c> triggers this event would count toward, as evaluated with a count of one.</summary>
    private static AdventureTrigger? FailedOn(AdventureBeat beat, AdventureStanding standing, JournalEvent journalEvent, AdventureObservation observation, AdventureWorld? world)
    {
        foreach (var fail in beat.FailsOn)
        {
            var trigger = AdventureValidation.FailTrigger(fail);

            if (trigger.Kind == TriggerKind.Conflict)
            {
                continue;
            }

            if (IsWatched(trigger.Kind)
                ? trigger.IsResolved && Gained(trigger, journalEvent, observation, world) > 0
                : Matches(trigger, journalEvent, standing.SystemAddress))
            {
                return trigger;
            }
        }

        return null;
    }

    /// <summary>The steps at or before <paramref name="upTo"/> applied, each to the beat it names when the standing is waiting on it.</summary>
    public static AdventureStanding ApplySteps(AdventureStanding standing, DateTimeOffset upTo)
    {
        var adventure = standing.Adventure;

        if (adventure.Stepped.Count == 0 || !adventure.IsActive)
        {
            return standing;
        }

        foreach (var step in adventure.Stepped.Where(step => step.At <= upTo).OrderBy(step => step.At))
        {
            if (standing.IsDone || step.Beat != standing.Current)
            {
                continue;
            }

            if (adventure.RewrittenFrom is { } from && step.Beat >= from && step.At < adventure.RewrittenAt)
            {
                continue;
            }

            standing = Stepped(standing, step.At, step.By);
        }

        return standing;
    }

    /// <summary>The standing with its current beat fired at <paramref name="at"/> by <paramref name="by"/>, the bench when null.</summary>
    public static AdventureStanding Stepped(AdventureStanding standing, DateTimeOffset at, string? by = null) =>
        standing with { Fired = [.. standing.Fired, at], FiredBy = [.. standing.FiredBy, by ?? BenchSource], Counted = 0, Side = null };

    /// <summary>The <see cref="AdventureStanding.FiredBy"/> of a beat the bench fired.</summary>
    public const string BenchSource = "Bench";

    /// <summary>The <see cref="AdventureStanding.FiredBy"/> of a beat the Commander marked done.</summary>
    public const string CommanderSource = "Marked done by the Commander";

    private static ConflictPart Ended(ConflictPart part, AdventureWorld world) =>
        !part.Ended && world.Of(part) is { Ended: true } conflict ? part with { Ended = true, Winner = conflict.Winner } : part;

    private static AdventureStanding ApplyConflict(AdventureStanding standing, AdventureTrigger trigger, JournalEvent journalEvent, AdventureWorld? world)
    {
        if (!trigger.IsResolved || world is null || Contribution(trigger, journalEvent, world, standing.Side) is not { } contribution)
        {
            return standing;
        }

        var parts = standing.Parts.Any(part => SamePart(part, contribution))
            ? standing.Parts
            : [.. standing.Parts, contribution];
        var total = standing.Counted + 1;

        return total < trigger.Count
            ? standing with { Counted = total, Side = contribution.Side, Parts = parts }
            : standing with { Fired = [.. standing.Fired, journalEvent.Timestamp], FiredBy = [.. standing.FiredBy, journalEvent.Kind], Counted = 0, Side = null, Parts = parts };
    }

    private static bool SamePart(ConflictPart a, ConflictPart b) =>
        string.Equals(a.System, b.System, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.WarType, b.WarType, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Side, b.Side, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Against, b.Against, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The conflict one event contributes to, or null. A kill bond counts for one side of a war or civil war in the system the Commander is
    /// in; a mission counts for one side of an election when it marks influence in the election's system.
    /// </summary>
    private static ConflictPart? Contribution(AdventureTrigger trigger, JournalEvent journalEvent, AdventureWorld world, string? side)
    {
        var raw = journalEvent.Raw;

        bool Wanted(SystemConflict conflict, string? faction) =>
            (string.IsNullOrWhiteSpace(trigger.Filter) || string.Equals(JournalJson.Symbol(trigger.Filter), JournalJson.Symbol(conflict.WarType), StringComparison.Ordinal))
            && AdventureWorld.Takes(conflict, faction)
            && SameFaction(trigger.Faction, faction)
            && SameFaction(side, faction);

        if (journalEvent.Kind is "FactionKillBond" && raw.String("AwardingFaction") is { } awarding && world.System is { } here)
        {
            return world.Here()
                .Where(conflict => JournalJson.Symbol(conflict.WarType) is "war" or "civilwar" && Wanted(conflict, awarding))
                .Select(conflict => new ConflictPart(here, conflict.WarType, awarding.Trim(), AdventureWorld.Against(conflict, awarding)))
                .FirstOrDefault();
        }

        if (journalEvent.Kind is "MissionCompleted" && raw.String("Faction") is { } faction)
        {
            foreach (var system in world.Standings.Systems)
            {
                foreach (var conflict in world.Open(system))
                {
                    if (JournalJson.Symbol(conflict.WarType) == "election"
                        && Wanted(conflict, faction)
                        && world.AddressOf(system) is { } address
                        && InfluenceLedger.UpMarksIn(journalEvent, address) > 0)
                    {
                        return new ConflictPart(system, conflict.WarType, faction.Trim(), AdventureWorld.Against(conflict, faction));
                    }
                }
            }
        }

        return null;
    }

    /// <summary>A fresh standing: begun or not, nothing fired yet, in <paramref name="systemAddress"/> when it is known.</summary>
    public static AdventureStanding Start(Adventure adventure, long? systemAddress = null, IReadOnlyDictionary<string, string>? seen = null) =>
        new() { Adventure = adventure, SystemAddress = systemAddress, Seen = seen ?? AdventureWatch.Nothing };
}
