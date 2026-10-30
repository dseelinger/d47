using System.Text;

namespace D47.Core.Adventures;

/// <summary>What an objective's trigger confirms when it fires, and what it does not; the story prompts and the fidelity check read it.</summary>
internal static class TriggerConfirms
{
    private static readonly Dictionary<TriggerKind, string> Entries = new()
    {
        [TriggerKind.Arrive] = "the ship arrived in that system — not why, what is there, or what the Commander did on the way",
        [TriggerKind.Dock] = "the Commander docked there after the objective before — nothing about the station's past or the Commander's history with it",
        [TriggerKind.Land] = "the ship touched down on that body — not what is on the surface or what the Commander did there",
        [TriggerKind.Scan] = "that body was scanned — not what the scan found beyond what the game state lists",
        [TriggerKind.Rank] = "a promotion to at least that rank in that career — not how it was earned",
        [TriggerKind.Board] = "the Commander bought or swapped into that hull — not the ship's name, history or previous owner",
        [TriggerKind.Beacon] = "data was scanned from a Guardian beacon in that system — not what the data says",
        [TriggerKind.Bounty] = "bounties were collected, anywhere — not who the targets were, their crimes or their number beyond the count",
        [TriggerKind.Bond] = "kill bonds were earned in a conflict zone, optionally for one faction — not who was killed, who won or whether a fight was broken off",
        [TriggerKind.OnFootBond] = "kill bonds were earned on foot in a ground conflict zone — not who was killed, who won or whether a fight was broken off",
        [TriggerKind.Mission] = "a mission of this family was handed in, wherever it was taken or flown — not its target, the target's standing or how it went",
        [TriggerKind.Sell] = "tons were sold at a market — not the price, the buyer or where the goods came from",
        [TriggerKind.Mine] = "tons were refined from mining — not where they were mined or what they were for",
        [TriggerKind.OnFoot] = "the Commander stepped onto a planet surface — not what they did there",
        [TriggerKind.Collect] = "items were picked up on foot — not where they lay or whose they were",
        [TriggerKind.Organic] = "organic samples were analysed — not the species found or the sample's worth",
        [TriggerKind.Map] = "bodies were mapped with the surface mapper — not what the maps showed",
        [TriggerKind.Signal] = "bodies that show a signal were surveyed — not what makes the signal",
        [TriggerKind.Wreck] = "the ship touched down at crashed ships — not who crashed or what was aboard",
        [TriggerKind.Codex] = "codex entries were logged — not what the entries say",
        [TriggerKind.CodexPicture] = "a picture the Commander chose was saved for a codex entry — not what the picture shows",
        [TriggerKind.NewCodexPicture] = "a new codex entry was logged and the Commander saved a picture for it — not what the entry says or the picture shows",
        [TriggerKind.DataSale] = "credits' worth of exploration or organic data was sold — not who bought it or what it described",
        [TriggerKind.Salvage] = "cargo canisters were picked up in space — not what they held beyond the cargo type named, or whose they were",
        [TriggerKind.Uss] = "the ship dropped into signal sources — not who sent the signal or what was found there",
        [TriggerKind.Rescue] = "rescue items were handed in — not who was saved or how",
        [TriggerKind.Engineer] = "an engineer relationship reached a stage — not what the engineer said or did",
        [TriggerKind.Srv] = "the SRV was launched — not where it drove or what it found",
        [TriggerKind.Crew] = "crew were hired — not who they are",
        [TriggerKind.SuitMod] = "suit or weapon mods appeared in a loadout — not who fitted them or why",
        [TriggerKind.Livery] = "the ship's cosmetic slots changed — not the reason or what the change shows",
        [TriggerKind.CarrierBuy] = "a fleet carrier was bought — not its name, location or what it is used for",
        [TriggerKind.CarrierJump] = "the Commander's fleet carrier moved to a different system — not why it moved",
        [TriggerKind.Wing] = "the Commander joined or added to a wing — not who the wingmates are or what they did",
        [TriggerKind.Multicrew] = "the Commander joined another player's crew — not whose or to what end",
        [TriggerKind.Squadron] = "the Commander joined a squadron — not its aims or members",
        [TriggerKind.SquadronFound] = "the Commander founded a squadron — not its aims or members",
        [TriggerKind.Conflict] = "the Commander took part in a war, civil war or election — not which side won or the Commander's part in the result",
        [TriggerKind.Faction] = "missions for a faction raised its influence — not the missions' content or the faction's standing",
        [TriggerKind.Reach] = "the ship has a fuel scoop and a laser or railgun for the trip — not that the trip was made",
        [TriggerKind.Boost] = "the frame shift drive was supercharged in a jet cone — not where the Commander was headed",
        [TriggerKind.Discover] = "bodies no one had discovered were scanned — not that the discovery was credited or sold",
        [TriggerKind.Footfall] = "the Commander walked on a body no one had walked on — not what was found there",
        [TriggerKind.Contribute] = "tons were delivered to a colonisation site — not who builds it or what it will become",
        [TriggerKind.Claim] = "a system was claimed for colonisation — not what will be built",
        [TriggerKind.Unlock] = "a tech broker row was unlocked — not who supplied the parts",
        [TriggerKind.Pledge] = "the Commander pledged to a Power — not what the Power asked or promised",
        [TriggerKind.Site] = "with the \"guardian\" filter, the ship came to the Guardian site on that body; with the \"tourist\" filter, a tourist beacon in that system was scanned; with the \"generation\" filter, the ship dropped out of supercruise at that generation ship — not what is there or what it holds",
        [TriggerKind.PowerRank] = "a Power rank was reached — not what it was earned for",
        [TriggerKind.MissionTaken] = "a mission of this family was accepted, at the station if one is named — not what it will involve",
        [TriggerKind.MissionDestination] = "the ship came to the mission's destination — not that the mission was done or what waited there",
        [TriggerKind.MissionComplete] = "that mission was handed in as completed — not how it went or what it paid",
        [TriggerKind.MissionAbandon] = "that mission was abandoned, expired or failed — not why",
        [TriggerKind.Choice] = "the Commander said which option they chose — not that they did it",
        [TriggerKind.Event] = "that event happened as many times as the count, with those fields — not why, where, or what followed",
    };

    private const string StockBeacon = "data was scanned from a Guardian beacon in that system, and the story's Guardian cores came aboard with it — not what the data says";

    /// <summary>What a line may state as fact and what is the story's own; the story prompts and the fidelity check carry it verbatim.</summary>
    internal const string Rule =
        "What a line may state as fact: an objective fires when its trigger is seen in the journal, and the trigger sees only what is listed for its kind. "
        + "Three things are checked: what the Commander did; where the Commander has been and their history with a place; and how a game event went "
        + "(a mission's target or outcome, a fight, a trade, what a scan or site held). A line may state one of these as fact only where its objective "
        + "confirms it or the game state given shows it. So a line never states a sum of credits the Commander was paid, a course or jump plotted, "
        + "or a place, beacon or event the Commander has never visited, scanned or seen. The story's own fiction is not checked: who the cast are, what they say, send, offer, remember "
        + "or own, documents and records in the story, and events before the story began. Objectives fire in order, so the ending may say where the "
        + "story stands and recount what any objective confirmed, in that order; it may not say the Commander did something no objective confirms.";

    /// <summary>The kinds that have an entry.</summary>
    internal static IReadOnlyCollection<TriggerKind> Kinds => Entries.Keys;

    /// <summary>The entry for a kind; in a stock story the beacon's entry says the story's cores came aboard.</summary>
    internal static string Of(TriggerKind kind, bool story = false) => story && kind == TriggerKind.Beacon ? StockBeacon : Entries[kind];

    private const string Sightseeing = "a sightseeing mission was handed in, so its passengers were taken to the sights it listed — not what they made of them";

    /// <summary>The entry for an objective of a kind with a filter; a site objective is described by its filter alone, a sightseeing mission by its family.</summary>
    internal static string Of(TriggerKind kind, string? filter, bool story = false, string? system = null, string? family = null) =>
        kind == TriggerKind.Mission && family is not null && family.Trim().StartsWith("Mission_Sightseeing", StringComparison.OrdinalIgnoreCase)
            ? Sightseeing
            : kind == TriggerKind.Uss && !string.IsNullOrWhiteSpace(system)
            ? $"the ship dropped into signal sources in {system.Trim()} — not who sent the signal or what was found there"
            : kind == TriggerKind.Organic && !string.IsNullOrWhiteSpace(system)
            ? $"organic samples were analysed in {system.Trim()} — not the species found or the sample's worth"
            : kind == TriggerKind.Map && !string.IsNullOrWhiteSpace(system)
            ? $"bodies in {system.Trim()} were mapped with the surface mapper — not what the maps showed"
            : kind == TriggerKind.Signal && !string.IsNullOrWhiteSpace(system)
            ? $"bodies in {system.Trim()} that show a signal were surveyed — not what makes the signal"
            : kind == TriggerKind.Site && string.Equals(filter, AdventureTrigger.GuardianSite, StringComparison.OrdinalIgnoreCase)
            ? "the ship came to the Guardian site on that body — not what is there or what it holds"
            : kind == TriggerKind.Site && string.Equals(filter, AdventureTrigger.TouristSite, StringComparison.OrdinalIgnoreCase)
                ? "the tourist beacon in that system was scanned — not what is there or what it holds"
                : kind == TriggerKind.Site && string.Equals(filter, AdventureTrigger.GenerationSite, StringComparison.OrdinalIgnoreCase)
                    ? "the ship dropped out of supercruise at that generation ship — not what is aboard or what it holds"
                    : Of(kind, story);

    /// <summary>The entry for a kind in the words an answer uses, or null when the word is no kind.</summary>
    internal static string? Of(string word, bool story = false) =>
        Enum.TryParse<TriggerKind>(word, ignoreCase: true, out var kind) && Entries.ContainsKey(kind) ? Of(kind, story) : null;

    /// <summary>The rule and the entry for each of <paramref name="kinds"/>, in the words an answer uses.</summary>
    internal static string Section(IEnumerable<string> kinds, bool story = false)
    {
        var text = new StringBuilder();

        text.AppendLine(Rule + " What each kind confirms:");

        foreach (var kind in kinds)
        {
            if (Of(kind, story) is { } entry)
            {
                text.Append("- \"").Append(kind).Append("\": ").AppendLine(entry);
            }
        }

        return text.ToString();
    }
}
