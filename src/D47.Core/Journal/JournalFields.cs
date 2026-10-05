namespace D47.Core.Journal;

public enum FieldUnit { None, Fraction, Percent, Credits, LightSeconds, LightYears, Tonnes }

public enum FieldDecoder { None, Hull, Module, Engineer, Material }

/// <summary>What the reading knows about one journal field name, optionally only on one kind of event.</summary>
public sealed record JournalField(
    string Name,
    string Label,
    string Meaning,
    string? Kind = null,
    FieldUnit Unit = FieldUnit.None,
    FieldDecoder Decoder = FieldDecoder.None,
    bool Plumbing = false,
    string? TrueSentence = null,
    ReadingTone Tone = ReadingTone.Value);

/// <summary>One entry per field name: its label, tooltip, unit, decoder and tone.</summary>
public static class JournalFields
{
    private static readonly JournalField[] Table =
    [
        new("MarketID", "Market", "the number that joins this station to its market data", Plumbing: true),
        new("SystemAddress", "System address", "the number that identifies this star system", Plumbing: true),
        new("BodyID", "Body", "the number that identifies this body within its system", Plumbing: true),
        new("CarrierID", "Carrier", "the number that identifies this fleet carrier", Plumbing: true),
        new("ShipID", "Ship", "the number that identifies one of your ships", Plumbing: true),
        new("EngineerID", "Engineer", "the number that identifies this engineer", Plumbing: true),
        new("Name", "Name", "what the event calls the thing it is about", Tone: ReadingTone.Name),
        new("StarSystem", "Star system", "the star system the event happened in", Tone: ReadingTone.System),
        new("Body", "Body", "the planet, moon or star the event happened at"),
        new("Count", "Count", "how many of the thing there were"),
        new("Type", "Type", "what kind of thing it was"),
        new("StationName", "Station name", "the station or carrier the event happened at"),
        new("StationType", "Station type", "what kind of station it is"),
        new("Taxi", "Taxi", "whether the ship is an Apex taxi", Plumbing: true),
        new("Multicrew", "Multicrew", "whether the ship is crewed by other players", Plumbing: true),
        new("Ship", "Ship", "the hull of the ship", Decoder: FieldDecoder.Hull),
        new("ShipType", "Ship", "the hull of the ship", Decoder: FieldDecoder.Hull),
        new("Cost", "Cost", "what it cost, in credits", Unit: FieldUnit.Credits),
        new("Amount", "Amount", "how many tonnes were moved", "CarrierDepositFuel", FieldUnit.Tonnes),
        new("Amount", "Fuel bought", "how many tonnes of fuel were bought", "RefuelAll", FieldUnit.Tonnes),
        new("BuyPrice", "Price", "what it cost to buy, in credits", Unit: FieldUnit.Credits),
        new("SellPrice", "Price", "what it sold for, in credits", Unit: FieldUnit.Credits),
        new("TotalEarnings", "Total earnings", "what the sale paid, in credits", Unit: FieldUnit.Credits),
        new("DistFromStarLS", "Distance from the star", "how far the body or station is from the main star, in light seconds", Unit: FieldUnit.LightSeconds),
        new("JumpDist", "Jump distance", "how far the jump went, in light years", Unit: FieldUnit.LightYears),
        new("FuelUsed", "Fuel used", "how much fuel the jump used, in tonnes", Unit: FieldUnit.Tonnes),
        new("FuelLevel", "Fuel level", "how much fuel is in the tank, in tonnes", Unit: FieldUnit.Tonnes),
        new("Health", "Health", "how much of the hull or module is left", Unit: FieldUnit.Fraction),
        new("Progress", "Progress", "how far along it is", Unit: FieldUnit.Fraction),
        new("Level", "Grade", "the grade of the modification or rank"),
        new("Quality", "Quality", "how well the modification rolled", Unit: FieldUnit.Fraction),
        new("Engineer", "Engineer", "the engineer the event involved", Decoder: FieldDecoder.Engineer, Tone: ReadingTone.Name),
        new("Module", "Module", "the module the event involved", Decoder: FieldDecoder.Module),
        new("Material", "Material", "the material the event involved", Decoder: FieldDecoder.Material),
        new("Faction", "Faction", "the minor faction the event involved", Tone: ReadingTone.Name),
        new("Power", "Power", "the Power the event involved", Tone: ReadingTone.Name),
        new("StationFaction", "Run by", "the minor faction that runs the station", Tone: ReadingTone.Name),
        new("StationServices", "You can do here", "the services the station offers"),
        new("LandingPads", "Landing pads", "how many pads of each size the station has"),
        new("Wanted", "Wanted", "whether you are wanted in this jurisdiction", TrueSentence: "You are wanted here.", Tone: ReadingTone.Warning),
        new("ActiveFine", "Active fine", "whether you have an unpaid fine in this jurisdiction", TrueSentence: "You have an unpaid fine here.", Tone: ReadingTone.Warning),
        new("From", "From", "who sent the message", Tone: ReadingTone.Name),
        new("Channel", "Channel", "where the message was sent: local, wing, player, npc and so on"),
        new("Message", "Message", "what the message said"),
        new("JumpType", "Jump", "whether the jump is to another system or within one"),
        new("StarClass", "Star class", "the spectral class of the star"),
        new("RemainingJumpsInRoute", "Jumps left", "how many jumps remain on the plotted route"),
        new("Name", "Next system", "the system the FSD is targeting", "FSDTarget", Tone: ReadingTone.System),
        new("Name", "Material", "the material that was collected", "MaterialCollected", Decoder: FieldDecoder.Material),
        new("Name", "Item", "the item that was collected", "CollectItems", Decoder: FieldDecoder.Material),
        new("Scooped", "Scooped", "how many tonnes of fuel the scoop took in", "FuelScoop", FieldUnit.Tonnes),
        new("Total", "Fuel now", "how much fuel is in the tank, in tonnes", "FuelScoop", FieldUnit.Tonnes),
        new("BodyType", "Body type", "what kind of body the ship dropped out of supercruise at"),
        new("Threat", "Threat level", "how dangerous the destination is, from 0"),
        new("LandingPad", "Pad", "the landing pad the station assigned"),
        new("Category", "Category", "which kind of material it is"),
        new("SystemName", "System", "the star system being scanned", Tone: ReadingTone.System),
        new("BodyCount", "Bodies found", "how many bodies the system has"),
        new("NonBodyCount", "Other objects", "how many signals and other non-body objects the system has"),
        new("Target", "Attacked", "who or what is being attacked"),
        new("MeritsGained", "Merits gained", "the merits this action earned"),
        new("TotalMerits", "Total merits", "the merits held with the Power in total"),
        new("Added", "Into the backpack", "the items that went into the backpack"),
        new("Removed", "Out of the backpack", "the items that left the backpack"),
        new("Stolen", "Stolen", "whether the items were stolen", TrueSentence: "These items are stolen.", Tone: ReadingTone.Warning),
        new("TargetLocked", "Target locked", "whether the target is locked", Plumbing: true),
        new("ScanStage", "Scan stage", "how far the scan of the target has got, from 0 to 3"),
        new("PilotName", "Pilot", "the pilot of the targeted ship", Tone: ReadingTone.Name),
        new("PilotRank", "Rank", "the pilot's combat rank"),
        new("SquadronID", "Squadron", "the squadron tag of the pilot", Tone: ReadingTone.Name),
        new("ShieldHealth", "Shields", "how much of the shield is left", Unit: FieldUnit.Percent),
        new("HullHealth", "Hull", "how much of the hull is left", Unit: FieldUnit.Percent),
        new("LegalStatus", "Legal status", "how the pilot stands with the law"),
        new("Bounty", "Bounty", "the bounty on the pilot, in credits", Unit: FieldUnit.Credits),
        new("Subsystem", "Aimed at", "the subsystem targeted"),
        new("SubsystemHealth", "Subsystem health", "how much of the subsystem is left", Unit: FieldUnit.Percent),
    ];

    /// <summary>The entry for a field on an event kind, or null for a name the table does not hold.</summary>
    public static JournalField? Find(string name, string? kind = null) =>
        Array.Find(Table, field => field.Name == name && field.Kind == kind && kind is not null)
        ?? Array.Find(Table, field => field.Name == name && field.Kind is null);

    /// <summary>The tooltip for a field: its name, then what it means; null where the table has no entry.</summary>
    public static string? Tooltip(string name, string? kind = null) =>
        Find(name, kind) is { } field ? $"{name} — {field.Meaning}" : null;
}
