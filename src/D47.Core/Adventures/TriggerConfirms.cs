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
        [TriggerKind.Reach] = "the ship has the jump range and a fuel scoop for the trip — not that the trip was made",
        [TriggerKind.Boost] = "the frame shift drive was supercharged in a jet cone — not where the Commander was headed",
        [TriggerKind.Discover] = "bodies no one had discovered were scanned — not that the discovery was credited or sold",
        [TriggerKind.Footfall] = "the Commander walked on a body no one had walked on — not what was found there",
        [TriggerKind.Contribute] = "tons were delivered to a colonisation site — not who builds it or what it will become",
        [TriggerKind.Claim] = "a system was claimed for colonisation — not what will be built",
        [TriggerKind.Unlock] = "a tech broker row was unlocked — not who supplied the parts",
        [TriggerKind.Pledge] = "the Commander pledged to a Power — not what the Power asked or promised",
        [TriggerKind.Site] = "the ship came to a Guardian site, or a tourist beacon was scanned in that system — not what is there or what it holds",
        [TriggerKind.PowerRank] = "a Power rank was reached — not what it was earned for",
    };

    /// <summary>The kinds that have an entry.</summary>
    internal static IReadOnlyCollection<TriggerKind> Kinds => Entries.Keys;

    internal static string Of(TriggerKind kind) => Entries[kind];

    /// <summary>The entry for a kind in the words an answer uses, or null when the word is no kind.</summary>
    internal static string? Of(string word) =>
        Enum.TryParse<TriggerKind>(word, ignoreCase: true, out var kind) && Entries.TryGetValue(kind, out var entry) ? entry : null;

    /// <summary>The rule for lines and the entry for each of <paramref name="kinds"/>, in the words an answer uses.</summary>
    internal static string Section(IEnumerable<string> kinds)
    {
        var text = new StringBuilder();

        text.AppendLine(
            "What a line may say as fact: an objective fires when its trigger is seen in the journal, and the trigger sees only what is listed below. "
            + "A line may state only what its objective confirms, what the game state given shows, or what a character claims in their own voice "
            + "(\"the patron calls it a rescue\"). The ending must be something the last objective confirms. What each kind confirms:");

        foreach (var kind in kinds)
        {
            if (Of(kind) is { } entry)
            {
                text.Append("- \"").Append(kind).Append("\": ").AppendLine(entry);
            }
        }

        return text.ToString();
    }
}
