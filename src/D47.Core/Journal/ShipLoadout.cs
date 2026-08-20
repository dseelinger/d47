using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>
/// One line of a module's engineering, in real units.
/// <para>
/// <b>This is the only place a module's actual roll exists.</b> The blueprint and grade say what
/// was attempted; these say what came out — 52.3 tonnes of optimal mass where the unmodified drive
/// had 45, and so on. Nothing in any table can supply them, because they are facts about this
/// module rather than about the game.
/// </para>
/// </summary>
public sealed record ShipModifier(string Label)
{
    /// <summary>The modified figure, for the modifiers that carry a number.</summary>
    public double? Value { get; init; }

    /// <summary>The same figure before engineering, so the change is arithmetic and not a claim.</summary>
    public double? OriginalValue { get; init; }

    /// <summary>
    /// The value for modifiers that are not numeric at all — a damage type reading "Thermal"
    /// rather than a quantity. Sixteen of 3,384 modifiers measured across the corpus arrive this
    /// way, carrying <c>ValueStr</c> and no <c>Value</c>, <c>OriginalValue</c> or
    /// <c>LessIsGood</c>. Read as a number they vanish silently.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Whether a smaller number is the better one — true for mass, heat and power draw.
    /// <para>
    /// Elite writes this as <c>0</c> or <c>1</c> rather than as a JSON boolean, so reading it as
    /// one answers false every time and reports every improvement backwards.
    /// </para>
    /// </summary>
    public bool LessIsGood { get; init; }

    /// <summary>How far engineering moved it, or null when there is nothing to subtract.</summary>
    public double? Change => Value is { } value && OriginalValue is { } original ? value - original : null;

    /// <summary>
    /// Whether the change is an improvement, or null when it did not move or cannot be measured.
    /// Null rather than false: "unchanged" and "worse" are different things to say out loud.
    /// </summary>
    public bool? IsImprovement => Change switch
    {
        null => null,
        > 0 => !LessIsGood,
        < 0 => LessIsGood,
        _ => null,
    };

    public static ShipModifier From(JsonElement element) => new(element.Named("Label") ?? "unknown")
    {
        Value = element.Double("Value"),
        OriginalValue = element.Double("OriginalValue"),

        // ValueStr for the non-numeric variant, and String("Value") behind it for a Value that
        // arrives as a string — which Double() answers null for, so without this the modifier
        // would read as present and empty rather than as the text it is.
        Text = element.Named("ValueStr") ?? element.String("Value"),

        // Number first, because that is what Elite actually writes. Bool() behind it so a change
        // to a real JSON boolean keeps working rather than silently flipping every verdict.
        LessIsGood = element.Int("LessIsGood") == 1 || element.Bool("LessIsGood"),
    };
}

/// <summary>One fitted module, as the Loadout event describes it.</summary>
public sealed record ShipModule(string Slot, string Item, bool Powered, int? Health, long? Value)
{
    /// <summary>The engineering blueprint applied, or null for an unmodified module.</summary>
    public string? Blueprint { get; init; }

    public int? BlueprintLevel { get; init; }

    /// <summary>The experimental effect, which Elite reports separately from the blueprint.</summary>
    public string? Experimental { get; init; }

    /// <summary>
    /// Progress through the grade, 0 to 1. <b>0.85 and above is finished</b>, not 1.0 — see
    /// <see cref="Knowledge.EngineeringRules.CompleteAt"/>, which carries the evidence.
    /// </summary>
    public double? Quality { get; init; }

    /// <summary>
    /// Who rolled it, where Elite names them.
    /// <para>
    /// Absent on 27 of 772 engineered modules measured, all of them carrying engineer id 399999
    /// and no name — a module that arrived already engineered rather than one somebody rolled.
    /// So a null engineer with an id present is a real state and not a parse failure.
    /// </para>
    /// </summary>
    public string? Engineer { get; init; }

    public long? EngineerId { get; init; }

    /// <summary>What the roll actually did, in real units. Empty for an unmodified module.</summary>
    public IReadOnlyList<ShipModifier> Modifiers { get; init; } = [];

    public bool IsEngineered => Blueprint is not null;

    public static ShipModule From(JsonElement element)
    {
        var engineering = element.Object("Engineering");

        return new ShipModule(
            element.String("Slot") ?? "unknown",
            element.Named("Item") ?? "unknown",
            Powered: element.Bool("On"),
            Health: element.Double("Health") is { } health ? (int)Math.Round(health * 100) : null,
            Value: element.Long("Value"))
        {
            // "BlueprintName" is the current spelling. "Blueprint" is what journals written before
            // 3.0 used; there are zero of them in the 912-journal corpus, which starts seven years
            // after that, so this is carried on EDDiscovery's authority rather than on evidence and
            // costs one fallback.
            Blueprint = engineering?.Named("BlueprintName") ?? engineering?.Named("Blueprint"),
            BlueprintLevel = engineering?.Int("Level"),

            // Loadout writes "ExperimentalEffect" and EngineerCraft writes
            // "ApplyExperimentalEffect" for the same thing. Blanked because an effect present and
            // empty is Elite saying there is none, and null is what the rest of this type means
            // by that.
            Experimental = Blank(engineering?.Named("ExperimentalEffect")
                                 ?? engineering?.Named("ApplyExperimentalEffect")),

            Quality = engineering?.Double("Quality"),
            Engineer = Blank(engineering?.String("Engineer")),
            EngineerId = engineering?.Long("EngineerID"),
            Modifiers = engineering is { } block
                ? [.. block.Items("Modifiers").Select(ShipModifier.From)]
                : [],
        };
    }

    /// <summary>Empty means absent everywhere in this file, and the two must not both circulate.</summary>
    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// The ship the Commander is flying, and what the game says about it (list.md Phase 7,
/// "Ship's loadout" and "Ship metrics").
/// <para>
/// <b>Every number here is reported by the Loadout event, not computed from a table of ship
/// specifications.</b> That is the checklist's wording and it is the right constraint: a spec
/// table is a second source of truth that goes stale every balance pass, and it cannot know
/// about engineering, which is precisely what makes one Commander's Anaconda differ from
/// another's. MaxJumpRange out of Loadout already accounts for the modules actually fitted.
/// </para>
/// </summary>
public sealed record ShipLoadout
{
    public static readonly ShipLoadout Unknown = new();

    /// <summary>The internal ship symbol — "Anaconda", "Krait_MkII". Speak <see cref="TypeName"/>.</summary>
    public string? Type { get; init; }

    /// <summary>The player-facing ship name where Elite localises it.</summary>
    public string? TypeName { get; init; }

    /// <summary>What the Commander christened it.</summary>
    public string? Name { get; init; }

    /// <summary>The ship ident painted on the hull.</summary>
    public string? Ident { get; init; }

    public int? ShipId { get; init; }

    public long? HullValue { get; init; }

    public long? ModulesValue { get; init; }

    public long? Rebuy { get; init; }

    /// <summary>Percent. Loadout reports a 0-1 fraction; this is the readable form of it.</summary>
    public int? HullHealth { get; init; }

    public double? UnladenMass { get; init; }

    public int? CargoCapacity { get; init; }

    public double? FuelCapacity { get; init; }

    public double? ReserveCapacity { get; init; }

    /// <summary>
    /// Light years on a full tank with an empty hold, as Loadout reports it. This is the "base
    /// jump range" the checklist asks for: the game has already done the mass-and-FSD
    /// arithmetic, and redoing it here would only be a way to disagree with it.
    /// </summary>
    public double? MaxJumpRange { get; init; }

    public IReadOnlyList<ShipModule> Modules { get; init; } = [];

    public bool IsKnown => Type is not null;

    /// <summary>Modules with a blueprint applied. What a Commander means by "my engineering".</summary>
    public IReadOnlyList<ShipModule> Engineered =>
        [.. Modules.Where(module => module.IsEngineered)];

    /// <summary>
    /// Fitted but unpowered. Worth being able to answer directly, because an unpowered module
    /// is a module the Commander believes they have.
    /// </summary>
    public IReadOnlyList<ShipModule> Unpowered =>
        [.. Modules.Where(module => !module.Powered)];

    /// <summary>
    /// Hull plus modules. Loadout reports the two separately and never their sum, so this is
    /// arithmetic on reported values rather than a figure from anywhere else.
    /// </summary>
    public long? TotalValue => HullValue is { } hull && ModulesValue is { } modules
        ? hull + modules
        : null;

    /// <summary>
    /// How the Commander refers to the ship: the name they gave it where there is one, and the
    /// type otherwise.
    /// <para>
    /// The type is the journal's own localised name where it wrote one, then the specification
    /// table, and only then the raw symbol. Frontier does not always send `Ship_Localised` — it is
    /// absent for a Type-10 Defender, whose symbol is `type9_military` — so without the middle step
    /// this said *"Oxen, a type9_military is not bound to a core"* while the fleet page one tab
    /// over said "Oxen (Type-10 Defender)" off the same table. Remediation 15 item 14: the name was
    /// in hand and the id was printed anyway.
    /// </para>
    /// </summary>
    public string? Describe() => (Name, TypeSaid) switch
    {
        ({ } name, { } type) => $"{name}, a {type}",
        (null, { } type) => type,
        ({ } name, null) => name,
        _ => null,
    };

    /// <summary>
    /// The hull as it should be said: Frontier's localised name where they sent one, then the
    /// specification table, and only then the raw symbol.
    /// <para>
    /// <b><see cref="TypeName"/> cannot be tested for null here.</b> It is built from
    /// <c>JournalJson.Named</c>, which is <c>Ship_Localised ?? Ship</c> — so it is never null when
    /// the event names a ship at all, and a fallback hung off it would be unreachable code that
    /// looks like a fix. What says Frontier omitted the localised name is that the two are the
    /// <i>same string</i>, which is the test made here.
    /// </para>
    /// <para>
    /// The table last but before the symbol, and never over the journal: where Frontier sends a
    /// localised name it is what the Commander reads in game. Same idiom as
    /// <see cref="Ships.ShipBuild.HullName"/>, which is where the fleet page gets the spelling this
    /// used to disagree with. Remediation 15 item 14.
    /// </para>
    /// </summary>
    private string? TypeSaid =>
        Type is not { } symbol ? TypeName
        : TypeName is { } localised && !string.Equals(localised, symbol, StringComparison.Ordinal)
            ? localised
            : Knowledge.EliteSpecifications.Ship(symbol)?.Name ?? symbol;

    public ShipLoadout Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // The whole picture, rewritten from scratch. Loadout fires on outfitting, module swap and
        // ship swap, and carries the ship entire each time — so folding it rather than replacing
        // it would leave modules from the previous ship in the list. It does **not** fire on
        // engineering, which is why EngineerCraft is folded below rather than waited for here.
        "Loadout" => new ShipLoadout
        {
            Type = journalEvent.String("Ship"),
            TypeName = journalEvent.Named("Ship"),
            Name = Blank(journalEvent.String("ShipName")),
            Ident = Blank(journalEvent.String("ShipIdent")),
            ShipId = journalEvent.Int("ShipID"),
            HullValue = journalEvent.Long("HullValue"),
            ModulesValue = journalEvent.Long("ModulesValue"),
            Rebuy = journalEvent.Long("Rebuy"),
            HullHealth = journalEvent.Double("HullHealth") is { } health
                ? (int)Math.Round(health * 100)
                : null,
            UnladenMass = journalEvent.Double("UnladenMass"),
            CargoCapacity = journalEvent.Int("CargoCapacity"),
            FuelCapacity = journalEvent.Object("FuelCapacity")?.Double("Main"),
            ReserveCapacity = journalEvent.Object("FuelCapacity")?.Double("Reserve"),
            MaxJumpRange = journalEvent.Double("MaxJumpRange"),
            Modules = [.. journalEvent.Items("Modules").Select(ShipModule.From)],
        },

        // Renaming does not re-emit Loadout, so without this the ship would keep answering to
        // the name it had when it was last outfitted.
        "SetUserShipName" => this with
        {
            Name = Blank(journalEvent.String("UserShipName")) ?? Name,
            // Both spellings. Elite is not consistent about the casing of this field, and it
            // uses "ShipID" for the numeric id in the same event — so a single guess here is a
            // rename that silently does not take.
            Ident = Blank(journalEvent.String("UserShipId"))
                    ?? Blank(journalEvent.String("UserShipID"))
                    ?? Ident,
        },

        // Engineering is the change that matters and does not re-emit Loadout. Measured over the
        // 917-journal corpus: of 6,485 EngineerCraft events, not one is followed by a Loadout
        // within five seconds, 79 within a minute, and 1,378 are never followed by one at all in
        // the same file. Without this a plan is diffed against the loadout from when the
        // Commander last boarded, so the roll they have just finished reads as not started — and
        // the moment worth speaking to, standing at the console, passes in silence.
        "EngineerCraft" => Rolled(journalEvent),

        _ => this,
    };

    /// <summary>
    /// One slot, rewritten from the roll that just landed.
    /// <para>
    /// <b>EngineerCraft carries no ShipID</b>, and needs none: a Commander engineers the ship they
    /// are sitting in and no other. What it does carry — measured across all 6,485 of them — is
    /// <c>Slot</c>, <c>BlueprintName</c>, <c>Level</c>, <c>Quality</c> and a non-empty
    /// <c>Modifiers</c> list, every time, with the engineer named. That is the whole
    /// <c>Engineering</c> block, so nothing here is inferred.
    /// </para>
    /// <para>
    /// <b>One slot rather than the whole picture</b>, which is the opposite of what <c>Loadout</c>
    /// gets and for the same reason: this event describes one module and says nothing whatever
    /// about the others.
    /// </para>
    /// </summary>
    private ShipLoadout Rolled(JournalEvent journalEvent)
    {
        if (Blank(journalEvent.String("Slot")) is not { } slot)
        {
            return this;
        }

        var index = Modules.ToList()
            .FindIndex(module => string.Equals(module.Slot, slot, StringComparison.OrdinalIgnoreCase));

        // A slot this loadout has never listed. Adding one would mean inventing its item, health
        // and value to go with the roll, and putting that phantom in the fleet report; ignoring it
        // costs nothing, because the next Loadout carries the truth. The same branch covers d47
        // starting mid-session, with no picture of the ship to update at all.
        if (index < 0)
        {
            return this;
        }

        var existing = Modules[index];
        var modifiers = journalEvent.Items("Modifiers").Select(ShipModifier.From).ToList();

        var rolled = existing with
        {
            Blueprint = journalEvent.Named("BlueprintName") ?? existing.Blueprint,
            BlueprintLevel = journalEvent.Int("Level") ?? existing.BlueprintLevel,
            Quality = journalEvent.Double("Quality") ?? existing.Quality,

            // Localised first, exactly as the Loadout path does it — a plan names "Fast Charge"
            // and Elite localises this one field. Falling back to the raw symbol keeps the
            // ApplyExperimentalEffect spelling readable if the localised pair ever goes missing.
            //
            // **Kept where the event is silent.** A grade roll writes no experimental field at
            // all, which is not Elite saying there is none — reading it that way would strip the
            // effect off the moment the Commander improved the grade underneath it.
            Experimental = Blank(journalEvent.Named("ExperimentalEffect")
                                 ?? journalEvent.Named("ApplyExperimentalEffect"))
                           ?? existing.Experimental,

            Engineer = Blank(journalEvent.String("Engineer")) ?? existing.Engineer,
            EngineerId = journalEvent.Long("EngineerID") ?? existing.EngineerId,
            Modifiers = modifiers.Count > 0 ? modifiers : existing.Modifiers,
        };

        return this with { Modules = [.. Modules.Select((module, at) => at == index ? rolled : module)] };
    }

    /// <summary>
    /// Elite writes an empty string for a ship that has never been named. Null is what the rest
    /// of this type means by "not named", and the two must not both be in circulation.
    /// </summary>
    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
