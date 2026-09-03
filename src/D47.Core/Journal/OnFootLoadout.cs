using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>
/// One modification fitted to a suit or a weapon.
/// <para>
/// <b>The symbol is the fact and the name is a join.</b> Elite writes <c>weapon_clipsize</c>; the
/// recipe table writes "Magazine Size"; nothing published carries both, so
/// <see cref="OnFootCatalogue"/> reconciles the 25 of them by hand and anything outside that table
/// arrives here with a null <see cref="Name"/> rather than a guess.
/// </para>
/// </summary>
public sealed record FittedModification(string Symbol)
{
    /// <summary>What it is called, or null when nothing d47 ships joins the spelling.</summary>
    public string? Name => OnFootCatalogue.ModificationName(Symbol);

    /// <summary>The name if there is one, and Frontier's own spelling tidied if there is not.</summary>
    public string Speak() => Name ?? ModuleNames.Readable(Symbol);

    /// <summary>Whether d47 can say what this is, as opposed to only what Elite called it.</summary>
    public bool IsNamed => Name is not null;
}

/// <summary>One hand weapon in one slot of a loadout.</summary>
public sealed record CarriedWeapon(string Slot, string Symbol)
{
    /// <summary>Elite's id for this particular weapon, which survives an upgrade.</summary>
    public long? ModuleId { get; init; }

    /// <summary>1 to 5. Unlike a suit, a weapon's grade is not in its symbol.</summary>
    public int? Grade { get; init; }

    public IReadOnlyList<FittedModification> Modifications { get; init; } = [];

    public OnFootEntry? Entry => OnFootCatalogue.Find(Symbol);

    /// <summary>Slots left, or null where the grade is not known.</summary>
    public int? FreeSlots =>
        Grade is { } grade ? OnFootRules.SlotsAt(grade) - Modifications.Count : null;

    public string Speak()
    {
        var name = Entry?.Name ?? ModuleNames.Readable(Symbol);

        return Grade is { } grade ? $"{name}, grade {grade}" : name;
    }
}

/// <summary>
/// What the Commander is wearing and carrying on foot (Phase 20, "d47 knows what you are
/// wearing").
/// <para>
/// <b>None of this was readable before.</b> `SuitLoadout`, `BuySuit`, `UpgradeSuit`,
/// `UpgradeWeapon`, `BuyWeapon` and `LoadoutEquipModule` were every one of them unread, so
/// "what am I wearing and what grade is it" had no answer at all.
/// </para>
/// <para>
/// <b>Nothing here speaks <c>SuitName_Localised</c>.</b> Frontier's own localisation is broken for
/// every suit above grade 1 and it is not an edge case: 269 of 768 <c>SuitLoadout</c> events in a
/// 912-journal corpus carry an unresolved <c>$UtilitySuit_Class1_Name;</c> token, and every one of
/// them says <b>Class1</b> whatever the real class is. So the symbol plus
/// <see cref="OnFootCatalogue"/> is the only honest route to a name, and the separate
/// <c>Class</c> field is the only honest route to a grade.
/// </para>
/// <para>
/// A snapshot rather than a merge, for the reason <see cref="ModuleStore"/> is: Elite writes the
/// whole loadout on every switch, and a stale weapon left standing in a slot is worse than no
/// answer.
/// </para>
/// </summary>
public sealed record OnFootLoadout
{
    public static readonly OnFootLoadout Unknown = new();

    /// <summary>Elite's id for the suit itself, which survives an upgrade.</summary>
    public long? SuitId { get; init; }

    /// <summary>Frontier's spelling — <c>explorationsuit_class3</c>. Null before anything is seen.</summary>
    public string? SuitSymbol { get; init; }

    /// <summary>What the Commander named this loadout.</summary>
    public string? LoadoutName { get; init; }

    public IReadOnlyList<FittedModification> SuitModifications { get; init; } = [];

    public IReadOnlyList<CarriedWeapon> Weapons { get; init; } = [];

    /// <summary>When the loadout was last reported. Null means no on-foot event has arrived.</summary>
    public DateTimeOffset? SeenAt { get; init; }

    public bool IsKnown => SuitSymbol is not null;

    public OnFootEntry? Suit => OnFootCatalogue.Find(SuitSymbol);

    /// <summary>
    /// The suit's grade, from the table rather than from the symbol's spelling. Null for the flight
    /// suit, which has no class at all and cannot be upgraded or modified.
    /// </summary>
    public int? Grade => Suit?.Grade;

    /// <summary>
    /// Modification slots not yet used, or null where the grade is unknown. Zero at grade 1, which
    /// is the ordering constraint rather than a shortage.
    /// </summary>
    public int? FreeSlots =>
        Grade is { } grade ? OnFootRules.SlotsAt(grade) - SuitModifications.Count : null;

    /// <summary>
    /// Whether the suit can be modified at all. False at grade 1 and for the flight suit, and that
    /// is a trip to Pioneer Supplies rather than a trip to an engineer.
    /// </summary>
    public bool CanBeModified => Grade >= OnFootRules.ModifiableFrom;

    public string Speak() =>
        Suit?.Speak() ?? (SuitSymbol is null ? "nothing I have seen" : ModuleNames.Readable(SuitSymbol));

    public CarriedWeapon? InSlot(string slot) =>
        Weapons.FirstOrDefault(weapon => string.Equals(weapon.Slot, slot, StringComparison.OrdinalIgnoreCase));

    public OnFootLoadout Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // The three that carry a whole loadout. Replaced wholesale rather than merged.
        "SuitLoadout" or "SwitchSuitLoadout" or "CreateSuitLoadout" => Snapshot(journalEvent),

        // BuySuit and BuyWeapon are deliberately not here. A suit just bought is by definition not
        // the suit being worn — Elite writes a SwitchSuitLoadout when the Commander puts it on —
        // so folding them would only ever be a branch that never runs.
        "UpgradeSuit" => Upgraded(journalEvent),

        "UpgradeWeapon" => UpgradedWeapon(journalEvent),

        "LoadoutEquipModule" => Equipped(journalEvent),

        "LoadoutRemoveModule" => Removed(journalEvent),

        // Selling the suit being worn leaves the Commander in the flight suit, and Elite writes a
        // fresh SuitLoadout for that. Forgetting rather than guessing which is why this is Unknown.
        "SellSuit" => SameSuit(journalEvent.Long("SuitID")) ? Unknown : this,

        // A weapon sale gets no such correction (#274). Measured on a 944-journal corpus, what
        // follows a SellWeapon is CommunityGoal, Embark, Loadout or another SellWeapon — never a
        // fresh SuitLoadout — so a weapon sold out of the slot the Commander is carrying it in
        // would stand there until the next loadout switch.
        "SellWeapon" => SoldWeapon(journalEvent),

        _ => this,
    };

    private bool SameSuit(long? suitId) => suitId is not null && suitId == SuitId;

    private OnFootLoadout Snapshot(JournalEvent journalEvent)
    {
        if (journalEvent.String("SuitName") is not { } symbol)
        {
            return this;
        }

        return new OnFootLoadout
        {
            SuitId = journalEvent.Long("SuitID"),
            SuitSymbol = JournalJson.Symbol(symbol),
            LoadoutName = journalEvent.String("LoadoutName"),
            SuitModifications = Modifications(journalEvent.Raw, "SuitMods"),
            Weapons = [.. journalEvent.Items("Modules")
                .Where(module => module.String("SlotName") is not null
                                 && module.String("ModuleName") is not null)
                .Select(module => new CarriedWeapon(
                    module.String("SlotName")!,
                    JournalJson.Symbol(module.String("ModuleName"))!)
                {
                    ModuleId = module.Long("SuitModuleID"),
                    Grade = module.Int("Class"),
                    Modifications = Modifications(module, "WeaponMods"),
                })],
            SeenAt = journalEvent.Timestamp,
        };
    }

    /// <summary>
    /// A grade upgrade to the suit being worn.
    /// <para>
    /// <b><c>Name</c> and <c>Class</c> disagree by design on this one event</b>: it carries the
    /// suit's <em>old</em> symbol beside its <em>new</em> class, so reading the grade off the
    /// symbol here is off by one. The new symbol is built from the family and the new class, which
    /// is what makes the table lookup land on the suit the Commander now owns.
    /// </para>
    /// </summary>
    private OnFootLoadout Upgraded(JournalEvent journalEvent)
    {
        if (!SameSuit(journalEvent.Long("SuitID")) || journalEvent.Int("Class") is not { } grade)
        {
            return this;
        }

        var family = (JournalJson.Symbol(journalEvent.String("Name")) ?? SuitSymbol)?.Split("_class")[0];

        return family is null
            ? this
            : this with
            {
                SuitSymbol = $"{family}_class{grade}",
                SeenAt = journalEvent.Timestamp,
            };
    }

    private OnFootLoadout UpgradedWeapon(JournalEvent journalEvent)
    {
        var moduleId = journalEvent.Long("SuitModuleID");

        return moduleId is null || journalEvent.Int("Class") is not { } grade
            ? this
            : this with
            {
                Weapons = [.. Weapons.Select(weapon =>
                    weapon.ModuleId == moduleId ? weapon with { Grade = grade } : weapon)],
                SeenAt = journalEvent.Timestamp,
            };
    }

    /// <summary>
    /// A weapon sold. Matched on <c>SuitModuleID</c> alone, because <c>SellWeapon</c> carries no
    /// <c>SuitID</c> to check the suit by — and it does not need one: the id is that weapon's own
    /// and no other suit can be carrying it. A weapon that is not in this loadout is not news.
    /// </summary>
    private OnFootLoadout SoldWeapon(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SuitModuleID") is not { } moduleId
            || !Weapons.Any(weapon => weapon.ModuleId == moduleId))
        {
            return this;
        }

        return this with
        {
            Weapons = [.. Weapons.Where(weapon => weapon.ModuleId != moduleId)],
            SeenAt = journalEvent.Timestamp,
        };
    }

    private OnFootLoadout Equipped(JournalEvent journalEvent)
    {
        if (!SameSuit(journalEvent.Long("SuitID"))
            || journalEvent.String("SlotName") is not { } slot
            || JournalJson.Symbol(journalEvent.String("ModuleName")) is not { } symbol)
        {
            return this;
        }

        var fitted = new CarriedWeapon(slot, symbol)
        {
            ModuleId = journalEvent.Long("SuitModuleID"),
            Grade = journalEvent.Int("Class"),
            Modifications = Modifications(journalEvent.Raw, "WeaponMods"),
        };

        return this with
        {
            Weapons = [.. Weapons.Where(weapon => !string.Equals(
                weapon.Slot, slot, StringComparison.OrdinalIgnoreCase)), fitted],
            SeenAt = journalEvent.Timestamp,
        };
    }

    private OnFootLoadout Removed(JournalEvent journalEvent)
    {
        if (!SameSuit(journalEvent.Long("SuitID")) || journalEvent.String("SlotName") is not { } slot)
        {
            return this;
        }

        return this with
        {
            Weapons = [.. Weapons.Where(weapon => !string.Equals(
                weapon.Slot, slot, StringComparison.OrdinalIgnoreCase))],
            SeenAt = journalEvent.Timestamp,
        };
    }

    private static IReadOnlyList<FittedModification> Modifications(
        System.Text.Json.JsonElement element, string property) =>
        [.. element.Items(property)
            .Select(entry => entry.GetString())
            .Select(JournalJson.Symbol)
            .Where(symbol => symbol is not null)
            .Select(symbol => new FittedModification(symbol!))];
}
