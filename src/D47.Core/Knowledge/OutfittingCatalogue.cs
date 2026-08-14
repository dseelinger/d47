namespace D47.Core.Knowledge;

/// <summary>
/// What can be bought, by name. The vocabularies the station search honours, read from its own
/// <c>field_values</c> endpoints on 2026-08-14 rather than recalled.
/// <para>
/// <b>These are validated locally but never advertised.</b> A tool parameter carrying 132 module
/// names as an <c>enum</c> would be several kilobytes of schema in prompt position 1, where a
/// single byte's change invalidates the entire cached prefix (architecture.md §6) — and it would
/// be paid for on every turn to answer a question asked once a session. So the parameter is free
/// text, and the name is checked against this list before a request is built, with the near
/// misses offered back when it does not match.
/// </para>
/// <para>
/// Validating at all is worth it for a different reason than the system filters were. The
/// service does honour these values — a module name it does not know returns zero stations
/// rather than being ignored — so a typo produces "nothing sells that" instead of a wrong
/// answer. That is survivable and still bad: "nothing within 100 light years sells a Frame Shift
/// Drve" is a false statement about the galaxy, and d47's first guardrail is not to make those.
/// Catching it here turns it into "I don't know a module called that; did you mean Frame Shift
/// Drive?".
/// </para>
/// </summary>
public static class OutfittingCatalogue
{
    public static IReadOnlyList<string> Ships { get; } =
    [
        "Adder", "Alliance Challenger", "Alliance Chieftain", "Alliance Crusader", "Anaconda",
        "Asp Explorer", "Asp Scout", "Beluga Liner", "Caspian Explorer", "Cobra MkIII",
        "Cobra MkIV", "Cobra MkV", "Corsair", "Diamondback Explorer", "Diamondback Scout",
        "Dolphin", "Eagle", "Federal Assault Ship", "Federal Corvette", "Federal Dropship",
        "Federal Gunship", "Fer-de-Lance", "Hauler", "Imperial Clipper", "Imperial Courier",
        "Imperial Cutter", "Imperial Eagle", "Keelback", "Kestrel Mk II", "Krait MkII",
        "Krait Phantom", "Lynx Highliner", "Mamba", "Mandalay", "Orca", "Panther Clipper MkII",
        "Python", "Python MkII", "Sidewinder", "Type-10 Defender", "Type-11 Prospector",
        "Type-6 Transporter", "Type-7 Transporter", "Type-8 Transporter", "Type-9 Heavy",
        "Viper MkIII", "Viper MkIV", "Vulture",
    ];

    public static IReadOnlyList<string> Modules { get; } =
    [
        "Abrasion Blaster", "Advanced Docking Computer", "Advanced Missile Rack",
        "Advanced Multi-Cannon", "Advanced Planetary Approach Suite", "Advanced Plasma Accelerator",
        "Auto Field-Maintenance Unit", "AX Missile Rack", "AX Multi-Cannon", "Beam Laser",
        "Bi-Weave Shield Generator", "Burst Laser", "Business Class Passenger Cabin", "Cannon",
        "Cargo Rack", "Cargo Scanner", "Caustic Sink Launcher", "Chaff Launcher",
        "Collector Limpet Controller", "Concord Cannon", "Corrosion Resistant Cargo Rack",
        "Cytoscrambler Burst Laser", "Decontamination Limpet Controller", "Detailed Surface Scanner",
        "Economy Class Passenger Cabin", "Electronic Countermeasure", "Enforcer Cannon",
        "Enhanced AX Missile Rack", "Enhanced AX Multi-Cannon", "Enhanced Performance Thrusters",
        "Enhanced Xeno Scanner", "Enzyme Missile Rack", "Experimental Weapon Stabiliser",
        "Fighter Hangar", "First Class Passenger Cabin", "Fragment Cannon", "Frame Shift Drive",
        "Frame Shift Drive (SCO)", "Frame Shift Drive Interdictor", "Frame Shift Wake Scanner",
        "Fuel Scoop", "Fuel Tank", "Fuel Transfer Limpet Controller", "Guardian FSD Booster",
        "Guardian Gauss Cannon", "Guardian Hull Reinforcement", "Guardian Hybrid Power Distributor",
        "Guardian Hybrid Power Plant", "Guardian Module Reinforcement", "Guardian Nanite Torpedo Pylon",
        "Guardian Plasma Charger", "Guardian Shard Cannon", "Guardian Shield Reinforcement",
        "Hatch Breaker Limpet Controller", "Heat Sink Launcher", "Hull Reinforcement Package",
        "Imperial Hammer Rail Gun", "Kill Warrant Scanner", "Life Support", "Lightweight Alloy",
        "Luxury Class Passenger Cabin", "Meta Alloy Hull Reinforcement", "Military Grade Composite",
        "Mine Launcher", "Mining Lance", "Mining Laser", "Mining Multi Limpet Controller",
        "Mining Volley Repeater", "Mirrored Surface Composite", "Missile Rack",
        "Mk II Ablative Lightweight Alloys", "Mk II Ablative Military Grade Composite",
        "Mk II Ablative Mirrored Surface Composite", "Mk II Ablative Reactive Surface Composite",
        "Mk II Ablative Reinforced Alloys", "Mk II Agile Boost Thrusters",
        "Mk II Business Class Passenger Cabin", "Mk II Cargo Rack",
        "Mk II Economy Class Passenger Cabin", "Mk II Gravity Optimised Thrusters",
        "Mk II Mining Multi-Limpet Controller", "Mk II Plasma Shock Accelerator",
        "Mk II Supercharge Optimised Frame Shift Drive (SCO)", "Mk II Vessel Hangar",
        "Module Reinforcement Package", "Multi-Cannon", "Operations Multi Limpet Controller",
        "Pacifier Frag-Cannon", "Pack-Hound Missile Rack", "Planetary Approach Suite",
        "Planetary Vehicle Hangar", "Plasma Accelerator", "Point Defence", "Power Distributor",
        "Power Plant", "Prismatic Shield Generator", "Prospector Limpet Controller",
        "Pulse Disruptor Laser", "Pulse Laser", "Pulse Wave Analyser", "Pulse Wave Xeno Scanner",
        "Rail Gun", "Reactive Surface Composite", "Recon Limpet Controller", "Refinery",
        "Reinforced Alloy", "Remote Release Flak Launcher", "Remote Release Flechette Launcher",
        "Repair Limpet Controller", "Rescue Multi Limpet Controller", "Research Limpet Controller",
        "Retributor Beam Laser", "Rocket Propelled FSD Disruptor", "Seeker Missile Rack",
        "Seismic Charge Launcher", "Sensors", "Shield Booster", "Shield Cell Bank",
        "Shield Generator", "Shock Cannon", "Shock Mine Launcher", "Shutdown Field Neutraliser",
        "Standard Docking Computer", "Sub-Surface Displacement Missile",
        "Sub-Surface Extraction Missile", "Supercruise Assist", "Thargoid Pulse Neutraliser",
        "Thrusters", "Torpedo Pylon", "Universal Multi Limpet Controller",
        "Xeno Multi Limpet Controller", "Xeno Scanner",
    ];

    /// <summary>Module sizes, as the service spells them.</summary>
    public static IReadOnlyList<string> Classes { get; } = ["0", "1", "2", "3", "4", "5", "6", "7", "8"];

    /// <summary>Module ratings. Wider than the familiar A-E: some modules go to I.</summary>
    public static IReadOnlyList<string> Ratings { get; } = ["A", "B", "C", "D", "E", "F", "G", "H", "I"];

    public static string? MatchShip(string spoken) => Catalogue.Match(Ships, spoken);

    public static string? MatchModule(string spoken) => Catalogue.Match(Modules, spoken);

    /// <summary>
    /// Names close enough to be worth offering back. Delegates to <see cref="Catalogue"/>, which
    /// is where these rules moved when the body search needed the same three of them.
    /// </summary>
    public static IReadOnlyList<string> Near(IReadOnlyList<string> catalogue, string spoken) =>
        Catalogue.Near(catalogue, spoken);
}
