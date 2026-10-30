using System.Text.Json;
using System.Globalization;
using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Adventures;

/// <summary>A <c>failsOn</c> trigger matched: the beat the chapter was waiting on, when, and why. <paramref name="Live"/> is set when the book found it announcing.</summary>
public sealed record AdventureFailure(int Beat, DateTimeOffset At, string Reason, bool Live = false);

/// <summary>One step of a beat that fired: which, when, and the journal event kind or source that fired it.</summary>
public sealed record StepFire(int Beat, int Step, DateTimeOffset At, string By);

/// <summary>The mission a <see cref="TriggerKind.MissionTaken"/> step bound: its id and where it goes.</summary>
public sealed record MissionBinding(long MissionId, string? DestinationSystem, string? DestinationStation);

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

    /// <summary>Every step that fired so far, oldest first; a beat's last step is the one that fired the beat.</summary>
    public IReadOnlyList<StepFire> StepFires { get; init; } = [];

    /// <summary>When each <c>LoadGame</c> since acceptance was written, oldest first.</summary>
    public IReadOnlyList<DateTimeOffset> Loads { get; init; } = [];

    /// <summary>The system the Commander is in, from the last <c>FSDJump</c>, <c>Location</c> or <c>CarrierJump</c> folded, acceptance or not.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>The market the Commander is docked at, from <c>Docked</c> and <c>Location</c>, or null in flight.</summary>
    public long? MarketId { get; init; }

    /// <summary>The missions the steps taken so far bound, by <see cref="AdventureTrigger.Handle"/>.</summary>
    public IReadOnlyDictionary<string, MissionBinding> Handles { get; init; } = new Dictionary<string, MissionBinding>();

    /// <summary>The running total toward the current step when it is counted: events, or tons sold or refined.</summary>
    public int Counted { get; init; }

    /// <summary>How many of <see cref="Adventure.Events"/>, from the oldest, the fold has applied.</summary>
    public int EventsApplied { get; init; }

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

    /// <summary>The option chosen at each beat that begins with a choice, by beat index.</summary>
    public IReadOnlyDictionary<int, int> Chosen { get; init; } = new Dictionary<int, int>();

    /// <summary>Set by the fold when the steps of an option with an ending of its own are done; the book stores it as the adventure's ending.</summary>
    public AdventureEnding? Reached { get; init; }

    /// <summary>The named ending of an option the chapter ended in, stored or not; never <see cref="AdventureEnding.Failed"/>.</summary>
    public AdventureEnding? Ended => Reached ?? (Adventure.Ending is { Name: not AdventureEnding.Failed } stored ? stored : null);

    /// <summary>The index of the beat the story is waiting on.</summary>
    public int Current => Fired.Count;

    /// <summary>Whether the chapter ran to its last beat or ended in an option's ending.</summary>
    public bool IsDone => Adventure.IsBegun && (Ended is not null || (Fired.Count >= Adventure.Beats.Count && Adventure.Beats.Count > 0));

    /// <summary>The steps of beat <paramref name="beat"/> with the option chosen there, if any.</summary>
    public IReadOnlyList<BeatStep> StepsOf(int beat) =>
        Adventure.Beats.ElementAtOrDefault(beat)?.StepsWith(Chosen.TryGetValue(beat, out var option) ? option : null) ?? [];

    /// <summary>The choice the Commander can make now: the current beat begins with one, none is made, and its offer was said since it became current.</summary>
    public BeatStep? OpenChoice =>
        InChoice
        && CurrentBeat is { } beat
        && CurrentStep == 0
        && Adventure.Offered is { } offer
        && offer.Beat == Current
        && offer.At >= ChoiceSince
            ? beat.Steps[0]
            : null;

    /// <summary>When the current beat became current: the last beat's fire, the acceptance, or a later replacement of it.</summary>
    private DateTimeOffset ChoiceSince
    {
        get
        {
            var since = LastFiredAt ?? Adventure.AcceptedAt ?? DateTimeOffset.MinValue;

            return Adventure is { RewrittenFrom: { } from, RewrittenAt: { } rewritten } && from <= Current && rewritten > since ? rewritten : since;
        }
    }

    /// <summary>Whether a <c>failsOn</c> trigger matched, stored or not.</summary>
    public bool IsFailed => Failure is not null || Adventure.IsFailed;

    public AdventureBeat? CurrentBeat =>
        Adventure.IsBegun && !IsDone && !IsFailed && Current < Adventure.Beats.Count ? Adventure.Beats[Current] : null;

    /// <summary>The index within the current beat of the step the story is waiting on.</summary>
    public int CurrentStep => StepFires.Count(fire => fire.Beat == Current);

    /// <summary>The step the story is waiting on.</summary>
    public BeatStep? CurrentObjective =>
        CurrentBeat is not null && StepsOf(Current) is { Count: > 0 } steps ? steps[Math.Min(CurrentStep, steps.Count - 1)] : null;

    /// <summary>The current beat narrowed to the step the story is waiting on, or null.</summary>
    public AdventureBeat? CurrentStepBeat =>
        CurrentBeat is { } beat && CurrentObjective is { } step ? beat with { Steps = [step] } : null;

    /// <summary>The trigger of the step the story is waiting on.</summary>
    public AdventureTrigger? CurrentTrigger => CurrentObjective?.Trigger;

    /// <summary>Whether the step the story is waiting on is its beat's last.</summary>
    public bool OnLastStep => CurrentBeat is not null && CurrentStep >= StepsOf(Current).Count - 1;

    public AdventureBeat? LastBeat => Fired.Count > 0 ? Adventure.Beats[Fired.Count - 1] : null;

    public DateTimeOffset? LastFiredAt => Fired.Count > 0 ? Fired[^1] : null;

    /// <summary>Derived — the last beat's fire time, or when an option's ending was reached — and never written.</summary>
    public DateTimeOffset? FinishedAt => IsDone ? Ended?.At ?? Fired[^1] : null;

    /// <summary>Whether the turn (the beat whose function says so) has fired, for the spoiler rule.</summary>
    public bool TurnReached => FunctionReached("midpoint", "turn");

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
        else if (IsFailed && !story)
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
        Adventure is { StoryId: not null, IsActive: true } && CurrentTrigger is { Kind: not TriggerKind.Beacon } && !InChoice;

    /// <summary>Whether a begun, unfinished short story is waiting on a beat the Commander may replace.</summary>
    public bool IsShortStoryRefusable =>
        Adventure is { StoryId: null, IsActive: true } && CurrentBeat is not null && !InChoice;

    /// <summary>Whether the story is waiting on a beat that begins with a choice, whose steps cannot be replaced, refused or marked done one by one.</summary>
    public bool InChoice => Adventure.IsActive && CurrentBeat is { OpensWithChoice: true };

    /// <summary>Why an objective of a beat that begins with a choice cannot be changed.</summary>
    public const string ChoiceRefusal = "That objective is part of a choice, so it cannot be changed or refused.";

    /// <summary>How far through, as a count (asked for 2026-08-22).</summary>
    public string? Step()
    {
        if (!Adventure.IsBegun || Adventure.Beats.Count == 0 || IsFailed)
        {
            return null;
        }

        var of = Adventure.Beats.Count;
        var at = Math.Min(Fired.Count + (IsDone && Ended is null ? 0 : 1), of);

        return $"Step {at.ToString(CultureInfo.InvariantCulture)} of {of.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>What the Commander did to reach where they are — the last beat's trigger, in words.</summary>
    public string? LastTrigger() =>
        CurrentBeat is not null && StepsOf(Current) is var steps && CurrentStep > 0 && CurrentStep <= steps.Count
            ? steps[CurrentStep - 1].Trigger.Describe()
            : LastBeat?.Steps.LastOrDefault()?.Trigger.Describe();

    /// <summary>What the story is waiting for the Commander to do next, in words.</summary>
    public string? NextTrigger() =>
        Adventure.IsActive && CurrentTrigger is { } current ? current.Progress(Counted) ?? current.Describe() : null;

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
            return Ended is { } ended ? $"ended: {ended.Name}" : "finished";
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

    private bool FunctionReached(params string[] functions)
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
    public static bool Matches(AdventureTrigger trigger, JournalEvent journalEvent, long? systemAddress = null, AdventureStanding? standing = null)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (!trigger.IsResolved || journalEvent.IsOperationLocation)
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

            // Arriving in the beacon's system proves the ship can make the trip, whatever its range, unless the ship had no laser or railgun.
            TriggerKind.Reach =>
                (trigger.Unarmed != true
                    && (journalEvent.Kind is "CarrierBuy"
                        || (journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump"
                            && string.Equals(raw.String("StarSystem"), trigger.System?.Trim(), StringComparison.OrdinalIgnoreCase))))
                || (journalEvent.Kind is "Loadout" && LoadoutMakesTheTrip(trigger, new Journal.ShipLoadout().Apply(journalEvent))),

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
                AdventureTrigger.GenerationSite =>
                    journalEvent.Kind is "SupercruiseDestinationDrop"
                    && trigger.SiteName is { } ship
                    && string.Equals(raw.String("Type")?.Trim(), ship.Trim(), StringComparison.OrdinalIgnoreCase),
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

            TriggerKind.MissionTaken =>
                journalEvent.Kind is "MissionAccepted"
                && raw.Long("MissionID") is not null
                && SameFaction(trigger.Faction, raw.String("Faction"))
                && MissionFamilies.Counts(trigger.MissionFamily, raw.String("Name"))
                && (trigger.MarketId is null || standing?.MarketId == trigger.MarketId),

            TriggerKind.MissionDestination =>
                Bound(trigger, standing) is { } destination
                && (journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump"
                        && Named(destination.DestinationSystem, raw.String("StarSystem"))
                    || journalEvent.Kind is "Docked" && (Named(destination.DestinationStation, raw.String("StationName"))
                        || Named(destination.DestinationStation, raw.Named("StationName")))
                    || journalEvent.Kind is "ApproachSettlement" && (Named(destination.DestinationStation, raw.String("Name"))
                        || Named(destination.DestinationStation, raw.Named("Name")))),

            TriggerKind.MissionComplete =>
                journalEvent.Kind is "MissionCompleted" && Bound(trigger, standing) is { } done && raw.Long("MissionID") == done.MissionId,

            TriggerKind.MissionAbandon =>
                journalEvent.Kind is "MissionAbandoned" or "MissionFailed"
                && Bound(trigger, standing) is { } dropped
                && raw.Long("MissionID") == dropped.MissionId,

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
                && SameType(trigger.Filter, raw.String("Genus"))
                && AtBody(trigger, raw),

            TriggerKind.Map =>
                journalEvent.Kind is "SAAScanComplete"
                && AtBody(trigger, raw),

            TriggerKind.Signal =>
                journalEvent.Kind is "SAASignalsFound"
                && raw.Items("Signals").Any(signal => SameType(trigger.Filter, signal.String("Type")))
                && AtBody(trigger, raw),

            TriggerKind.Wreck =>
                journalEvent.Kind is "Touchdown"
                && WreckType(raw.String("NearestDestination")) is { } wreck
                && string.Equals(wreck, string.IsNullOrWhiteSpace(trigger.Filter) ? "Unknown" : trigger.Filter.Trim(), StringComparison.OrdinalIgnoreCase),

            TriggerKind.Codex =>
                journalEvent.Kind is "CodexEntry"
                && (SameType(trigger.Filter, raw.String("Category")) || SameType(trigger.Filter, raw.String("SubCategory")))
                && (trigger.New != true || (raw.TryGetProperty("IsNewEntry", out var isNew) && isNew.ValueKind == JsonValueKind.True)),

            TriggerKind.CodexPicture =>
                IsPictureOnRecord(journalEvent),

            TriggerKind.NewCodexPicture =>
                IsPictureOnRecord(journalEvent)
                && standing is not null
                && AdventureWatch.LoggedNewSince(standing.Seen, raw.String("Entry"), raw.String("Region")) is { } logged
                && logged > StepAppeared(standing),

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
                && SameType(trigger.Filter, raw.String("USSType"))
                && (trigger.SystemAddress is null || systemAddress == trigger.SystemAddress),

            TriggerKind.Rescue =>
                journalEvent.Kind is "SearchAndRescue"
                && SameType(trigger.Filter, raw.String("Name")),

            TriggerKind.Engineer =>
                journalEvent.Kind is "EngineerProgress"
                && EngineerReached(trigger, raw),

            TriggerKind.Srv => journalEvent.LaunchesSrv,

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

            TriggerKind.Event =>
                journalEvent.Kind == trigger.Event
                && trigger.Where.All(pair => FieldHolds(raw, pair)),

            _ => false,
        };
    }

    private static bool IsPictureOnRecord(JournalEvent journalEvent) =>
        journalEvent.Kind == D47Event.CodexPicture && journalEvent.Raw.String("OnRecord") == "true";

    /// <summary>When the step the standing waits on became current: the last step fired, the acceptance, or a later replacement.</summary>
    private static DateTimeOffset StepAppeared(AdventureStanding standing)
    {
        var since = standing.StepFires.Count > 0 ? standing.StepFires[^1].At : standing.Adventure.AcceptedAt ?? DateTimeOffset.MinValue;

        return standing.Adventure is { RewrittenFrom: not null, RewrittenAt: { } rewritten } && rewritten > since ? rewritten : since;
    }

    /// <summary>A string field holds when the folded symbols are equal, a boolean when it is present and equal; a missing field never holds.</summary>
    private static bool FieldHolds(JsonElement raw, EventField pair)
    {
        if (raw.ValueKind != JsonValueKind.Object || !raw.TryGetProperty(pair.Field, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => string.Equals(JournalJson.Symbol(value.GetString()), JournalJson.Symbol(pair.Value), StringComparison.OrdinalIgnoreCase),
            JsonValueKind.True => bool.TryParse(pair.Value?.Trim(), out var wanted) && wanted,
            JsonValueKind.False => bool.TryParse(pair.Value?.Trim(), out var wanted) && !wanted,
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

    private static MissionBinding? Bound(AdventureTrigger trigger, AdventureStanding? standing) =>
        trigger.Handle is { } handle && standing?.Handles.TryGetValue(handle.Trim(), out var binding) == true ? binding : null;

    private static bool LoadoutMakesTheTrip(AdventureTrigger trigger, Journal.ShipLoadout ship) =>
        trigger.Carrier == true
            ? Stories.BeaconReach.HasWeapon(ship)
            : Stories.BeaconReach.CanMakeTrip(ship, weaponNeeded: trigger.Unarmed is not null);

    private static bool Named(string? wanted, string? actual) =>
        !string.IsNullOrWhiteSpace(wanted) && string.Equals(wanted.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);

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
            && !journalEvent.IsOperationLocation
            && journalEvent.Raw.Long("SystemAddress") is { } arrived
            && arrived != standing.SystemAddress)
        {
            standing = standing with { SystemAddress = arrived };
        }

        var market = journalEvent.Kind switch
        {
            "Location" when journalEvent.IsOperationLocation => standing.MarketId,
            "Docked" => journalEvent.Raw.Long("MarketID"),
            "Location" => journalEvent.Raw.Bool("Docked") ? journalEvent.Raw.Long("MarketID") : null,
            "Undocked" or "FSDJump" => null,
            _ => standing.MarketId,
        };

        if (market != standing.MarketId)
        {
            standing = standing with { MarketId = market };
        }

        if (world is not null && standing.Parts.Any(part => !part.Ended) && journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump" && !journalEvent.IsOperationLocation)
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

        if (journalEvent.StartsSession)
        {
            return standing with { Loads = [.. standing.Loads, journalEvent.Timestamp] };
        }

        var current = standing.CurrentBeat;

        if (current is null)
        {
            return standing;
        }

        // A replaced beat counts only what happens after the replacement.
        if (BeforeRewrite(standing) && journalEvent.Timestamp < adventure.RewrittenAt)
        {
            return standing;
        }

        var step = standing.CurrentObjective!;

        if ((FailedOn(step, standing, journalEvent, observation, world)
             ?? ImpliedFailure(step.Trigger, standing, journalEvent)
             ?? ChoiceFailure(standing, journalEvent)) is { } failing)
        {
            return standing with
            {
                Failure = new AdventureFailure(standing.Current, journalEvent.Timestamp, $"At \"{current.Title}\" the Commander did what ruled it out: {failing.Describe()}."),
            };
        }

        var trigger = step.Trigger;

        if (trigger.Kind == TriggerKind.Conflict)
        {
            return ApplyConflict(standing, trigger, journalEvent, world);
        }

        var watched = IsWatched(trigger.Kind);
        var gained = Gained(trigger, journalEvent, observation, world);

        if (watched ? gained == 0 || !trigger.IsResolved : !Matches(trigger, journalEvent, standing.SystemAddress, standing))
        {
            return standing;
        }

        if (trigger.IsCounted)
        {
            var total = (int)Math.Min((long)standing.Counted + (watched ? gained : Amount(trigger, journalEvent)), int.MaxValue);

            if (total < trigger.Count)
            {
                return standing with { Counted = total };
            }
        }

        var fired = Fire(standing, journalEvent.Timestamp, journalEvent.Kind);

        return trigger.Kind == TriggerKind.MissionTaken ? Bind(fired, trigger, journalEvent) : fired;
    }

    /// <summary>The standing with the accepted mission bound to the trigger's handle.</summary>
    private static AdventureStanding Bind(AdventureStanding standing, AdventureTrigger trigger, JournalEvent accepted)
    {
        if (Mission.Of(accepted) is not { } mission || trigger.Handle is not { } handle)
        {
            return standing;
        }

        var binding = new MissionBinding(mission.Id, mission.DestinationSystem, mission.DestinationStation);

        return standing with { Handles = new Dictionary<string, MissionBinding>(standing.Handles) { [handle.Trim()] = binding } };
    }

    /// <summary>
    /// The standing with the step it waits on fired at <paramref name="at"/> by <paramref name="by"/>; the beat fires with its last step,
    /// unless the option chosen in it has an ending of its own, which the chapter then ends in.
    /// </summary>
    private static AdventureStanding Fire(AdventureStanding standing, DateTimeOffset at, string by)
    {
        var last = standing.OnLastStep;
        var fire = new StepFire(standing.Current, standing.CurrentStep, at, by);
        var ending = last ? OptionEnding(standing, at) : null;
        var beat = last && ending is null;

        return standing with
        {
            StepFires = [.. standing.StepFires, fire],
            Fired = beat ? [.. standing.Fired, at] : standing.Fired,
            FiredBy = beat ? [.. standing.FiredBy, by] : standing.FiredBy,
            Reached = ending ?? standing.Reached,
            Counted = 0,
        };
    }

    /// <summary>The ending of the option chosen in the current beat, reached at <paramref name="at"/>, or null when it has none.</summary>
    private static AdventureEnding? OptionEnding(AdventureStanding standing, DateTimeOffset at) =>
        standing.CurrentBeat is { OpensWithChoice: true } beat
        && standing.Chosen.TryGetValue(standing.Current, out var chosen)
        && beat.Steps[0].Options.ElementAtOrDefault(chosen) is { Ending: { Length: > 0 } name } option
            ? new AdventureEnding(name.Trim(), at, $"At \"{beat.Title}\" the Commander chose to {option.Label.Trim()} and did it.")
            : null;

    /// <summary>The standing with option <paramref name="option"/> of the choice it waits on chosen at <paramref name="at"/>.</summary>
    public static AdventureStanding Chose(AdventureStanding standing, int option, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(standing);

        var chosen = standing with { Chosen = new Dictionary<int, int>(standing.Chosen) { [standing.Current] = option } };

        return Fire(chosen, at, AdventureChoice.EventName) with { Side = null };
    }

    /// <summary>
    /// A bound mission's outcome that rules the chapter out at a choice: before choosing, any outcome of a mission an option refers to;
    /// while the chosen option's steps run, an outcome only another option asks for.
    /// </summary>
    private static AdventureTrigger? ChoiceFailure(AdventureStanding standing, JournalEvent journalEvent)
    {
        if (standing.CurrentBeat is not { OpensWithChoice: true } beat)
        {
            return null;
        }

        var options = beat.Steps[0].Options;
        IEnumerable<AdventureTrigger> ruledOut;

        if (!standing.Chosen.TryGetValue(standing.Current, out var chosen))
        {
            ruledOut = options
                .SelectMany(option => Outcomes(option))
                .SelectMany(outcome => new[] { outcome with { Kind = TriggerKind.MissionComplete }, outcome with { Kind = TriggerKind.MissionAbandon } });
        }
        else if (options.ElementAtOrDefault(chosen) is { } picked && standing.CurrentStep <= picked.Steps.Count)
        {
            var kept = Outcomes(picked).Select(OutcomeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

            ruledOut = options.Where((_, index) => index != chosen).SelectMany(Outcomes).Where(outcome => !kept.Contains(OutcomeKey(outcome)));
        }
        else
        {
            return null;
        }

        return ruledOut.FirstOrDefault(outcome => Matches(outcome, journalEvent, standing.SystemAddress, standing));

        static IEnumerable<AdventureTrigger> Outcomes(BeatOption option) =>
            option.Steps.Select(step => step.Trigger).Where(trigger => trigger is { Kind: TriggerKind.MissionComplete or TriggerKind.MissionAbandon, Handle.Length: > 0 });

        static string OutcomeKey(AdventureTrigger outcome) => $"{outcome.Kind}\n{outcome.Handle!.Trim()}";
    }

    /// <summary>Whether the step the standing waits on is at or after the position the last replacement began at.</summary>
    private static bool BeforeRewrite(AdventureStanding standing) =>
        standing.Adventure.RewrittenFrom is { } from
        && (standing.Current > from || (standing.Current == from && standing.CurrentStep >= standing.Adventure.RewrittenStep));

    private static bool IsWatched(TriggerKind kind) =>
        kind is TriggerKind.SuitMod or TriggerKind.Livery or TriggerKind.CarrierJump or TriggerKind.Discover or TriggerKind.Footfall or TriggerKind.OnFootBond;

    private static int Gained(AdventureTrigger trigger, JournalEvent journalEvent, AdventureObservation observation, AdventureWorld? world) => trigger.Kind switch
    {
        TriggerKind.Footfall => world?.IsFirstFootfall(journalEvent) == true ? 1 : 0,
        TriggerKind.OnFootBond => journalEvent.Kind is "FactionKillBond" && world?.IsOnFoot == true ? 1 : 0,
        _ => IsWatched(trigger.Kind) ? AdventureWatch.Amount(trigger, observation) : 0,
    };

    /// <summary>The first of the step's <c>failsOn</c> triggers this event would count toward, as evaluated with a count of one.</summary>
    private static AdventureTrigger? FailedOn(BeatStep beat, AdventureStanding standing, JournalEvent journalEvent, AdventureObservation observation, AdventureWorld? world)
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
                : Matches(trigger, journalEvent, standing.SystemAddress, standing))
            {
                return trigger;
            }
        }

        return null;
    }

    /// <summary>The other outcome of a bound mission when this event brings it about while the step waits on one outcome.</summary>
    private static AdventureTrigger? ImpliedFailure(AdventureTrigger waiting, AdventureStanding standing, JournalEvent journalEvent)
    {
        if (waiting.Kind is not (TriggerKind.MissionComplete or TriggerKind.MissionAbandon))
        {
            return null;
        }

        var other = waiting with { Kind = waiting.Kind == TriggerKind.MissionComplete ? TriggerKind.MissionAbandon : TriggerKind.MissionComplete };

        return Matches(other, journalEvent, standing.SystemAddress, standing) ? other : null;
    }

    /// <summary>The steps, choices and d47 events at or before <paramref name="upTo"/> applied in time order; each d47 event is applied once.</summary>
    public static AdventureStanding ApplySteps(AdventureStanding standing, DateTimeOffset upTo)
    {
        var adventure = standing.Adventure;

        if ((adventure.Stepped.Count == 0 && adventure.Choices.Count == 0 && adventure.Events.Count <= standing.EventsApplied) || !adventure.IsActive)
        {
            return standing;
        }

        var events = adventure.Events.Skip(standing.EventsApplied).TakeWhile(made => made.At <= upTo).ToList();

        var due = adventure.Stepped.Where(step => step.At <= upTo).Select(step => (step.At, Step: (AdventureStep?)step, Choice: (AdventureChoice?)null, Event: (D47Event?)null))
            .Concat(adventure.Choices.Where(choice => choice.At <= upTo).Select(choice => (choice.At, Step: (AdventureStep?)null, Choice: (AdventureChoice?)choice, Event: (D47Event?)null)))
            .Concat(events.Select(made => (made.At, Step: (AdventureStep?)null, Choice: (AdventureChoice?)null, Event: (D47Event?)made)))
            .OrderBy(entry => entry.At);

        foreach (var (at, step, choice, d47) in due)
        {
            if (standing.IsDone || standing.IsFailed || (BeforeRewrite(standing) && at < adventure.RewrittenAt))
            {
                continue;
            }

            if (d47 is { } happened)
            {
                standing = Apply(standing, happened.AsJournalEvent());
                continue;
            }

            if (choice is { } made)
            {
                if (made.Beat == standing.Current && standing.CurrentStep == 0 && standing.CurrentObjective is { IsChoice: true } offered && made.Option < offered.Options.Count)
                {
                    standing = Chose(standing, made.Option, made.At);
                }

                continue;
            }

            // A choice is made only by choosing.
            if (step!.Beat != standing.Current || step.Step != standing.CurrentStep || standing.CurrentObjective is { IsChoice: true })
            {
                continue;
            }

            standing = Stepped(standing, step.At, step.By);
        }

        return events.Count > 0 ? standing with { EventsApplied = standing.EventsApplied + events.Count } : standing;
    }

    /// <summary>Whether any step or <c>failsOn</c> of <paramref name="adventure"/>, options included, waits on <paramref name="made"/>.</summary>
    public static bool WaitsOn(Adventure adventure, JournalEvent made)
    {
        ArgumentNullException.ThrowIfNull(adventure);
        ArgumentNullException.ThrowIfNull(made);

        return adventure.Beats
            .SelectMany(beat => beat.Steps)
            .SelectMany(step => step.Options.SelectMany(option => option.Steps).Prepend(step))
            .SelectMany(step => step.FailsOn.Select(AdventureValidation.FailTrigger).Prepend(step.Trigger))
            .Any(trigger => trigger.Kind == TriggerKind.Event && Matches(trigger, made)
                || trigger.Kind is TriggerKind.CodexPicture or TriggerKind.NewCodexPicture && made.Kind == D47Event.CodexPicture);
    }

    /// <summary>The standing with its current step fired at <paramref name="at"/> by <paramref name="by"/>, the bench when null.</summary>
    public static AdventureStanding Stepped(AdventureStanding standing, DateTimeOffset at, string? by = null) =>
        Fire(standing, at, by ?? BenchSource) with { Side = null };

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
            : Fire(standing, journalEvent.Timestamp, journalEvent.Kind) with { Side = null, Parts = parts };
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
