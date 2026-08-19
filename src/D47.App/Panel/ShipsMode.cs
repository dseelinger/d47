using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>
/// The Loadout tab's Ships mode: the fleet, a ship, a slot (list.md Phase 26, "Ships").
/// <para>
/// Everything that was in <c>FleetPage</c>, <c>ShipPage</c> and <c>SlotPage</c> that is about
/// <em>ships</em> rather than about drawing, moved behind <see cref="ILoadoutMode"/> when Phase 27
/// needed the same three pages for suits. The pages did not change; what changed is that they no
/// longer know what a hull is.
/// </para>
/// </summary>
public sealed class ShipsMode(
    ShipPlanService ships,
    ChecklistService checklists,
    Func<CommanderGameState?> state) : ILoadoutMode
{
    /// <summary>A row for a ship the journal reports and nothing has planned for yet.</summary>
    private const string Unplanned = "new:";

    public string RootKey => LoadoutPages.FleetRoot;

    public string RootWord => "Ships";

    public string ItemPrefix => LoadoutPages.ShipPrefix;

    public string SlotPrefix => LoadoutPages.SlotPrefix;

    public event Action? Changed
    {
        add => ships.Store.Changed += value;
        remove => ships.Store.Changed -= value;
    }

    public string EmptyIndex =>
        "I have not seen your fleet yet. Dock somewhere with a shipyard and I will read it — or "
        + "plan a ship you do not own, and buying one will point the plan at it.";

    public string EmptySlots =>
        "Nothing is planned, and I cannot see this ship's modules — Elite only reports the loadout "
        + "of the ship you are sitting in. Plan a slot and it will appear here.";

    public string NewLabel => "Plan a ship you do not own";

    public string PromoteLabel => "Put this build on my checklist";

    public string SayAtIndex => "what have I planned";

    public string SayAtItem => "put that on my checklist";

    public string SayAtSlot(string slot) => $"plan grade 5 dirty drives on {slot}";

    public IReadOnlyList<LoadoutRow> Items() =>
    [
        .. ships.Fleet().Select(entry =>
        {
            var planned = entry.Planned;

            return new LoadoutRow(
                Key(entry),
                entry.Name ?? entry.HullName,
                entry.Name is { Length: > 0 } name ? $"{name} ({entry.HullName})" : entry.HullName,

                // Where it is, and how its plans stand. The two questions the fleet page exists to
                // answer before anything is drilled.
                planned > 0
                    ? $"{entry.Where()} · {planned.ToString(CultureInfo.InvariantCulture)} planned"
                    : entry.Where(),
                planned > 0);
        }),
    ];

    /// <summary>
    /// A hull the Commander does not own. Voice first, because a hull is a name and a name is far
    /// easier said than hunted for one key at a time.
    /// </summary>
    public void New(PanelPrompts prompts, Action done) =>
        prompts.Enter(
            new EntryRequest(
                "loadout.intend",
                "Ship",
                "Which ship do you intend to buy?",
                "It is not in your fleet until you own one — acquiring it is the plan's first step.",
                string.Empty,
                EntrySurface.Voice,
                value => EliteSpecifications.Ship(value) is null
                    ? EntryVerdict.No($"I do not know a ship called “{value}”.")
                    : EntryVerdict.Ok),
            hull =>
            {
                ships.Intend(hull);
                done();
            });

    public string? Summary(string item) => Resolve(item) is not { } build
        ? null
        : build.IsOwned
            ? $"{build.Describe()}. One build per ship: a slot holds one plan."
            : $"{build.Describe()}. Buying one will point this plan at it.";

    /// <summary>
    /// What this ship is, where it is, and what it can do (remediation.md 13, item 2).
    /// <para>
    /// <b>Two sources, and they are kept apart.</b> Where it is and what it is worth come from
    /// the journal and are facts about <em>this</em> ship; speed, boost and pad come from the
    /// shipped table and are facts about the hull, true of every one ever built. The figures that
    /// depend on what is fitted — jump range, cargo, rebuy — are only said for the ship being
    /// flown, because those are Loadout's and Elite reports the loadout of one ship at a time.
    /// </para>
    /// <para>
    /// Nothing here is computed from anything else. A jump range for a ship in another dock would
    /// have to be modelled from a loadout d47 cannot see, and a modelled figure that reads like a
    /// measured one is the failure the whole specification table is built the way it is to avoid.
    /// </para>
    /// </summary>
    public IReadOnlyList<LoadoutLine> Details(string item)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        var entry = ships.Fleet().FirstOrDefault(candidate => candidate.Build?.Id == build.Id);
        var lines = new List<LoadoutLine>();

        // Where it is, said in full rather than in the row's one phrase: the row has to fit a
        // line and this does not, so "here" becomes the station it is parked at.
        if (entry?.Stored is { } stored)
        {
            lines.Add(new LoadoutLine(Whereabouts(stored, entry.IsActive), LoadoutTone.Body));

            if (stored.TransferPrice is { } price)
            {
                lines.Add(new LoadoutLine(
                    $"Transferring it here costs {Credits(price)}."));
            }

            if (stored.Value is { } worth)
            {
                lines.Add(new LoadoutLine($"Worth {Credits(worth)}."));
            }
        }
        else if (entry is not null)
        {
            lines.Add(new LoadoutLine("Not bought yet.", LoadoutTone.Body));
        }

        lines.AddRange(Hull(build));
        lines.AddRange(Flying(build));

        return lines;
    }

    /// <summary>Where the ship is, in a sentence rather than in the row's one phrase.</summary>
    private static string Whereabouts(StoredShip stored, bool active)
    {
        if (active)
        {
            return "You are flying it.";
        }

        if (stored.InTransit)
        {
            return $"In transit to {stored.StarSystem}.";
        }

        return stored.StationName is { Length: > 0 } station
            ? $"Parked at {station}, {stored.StarSystem}."
            : $"Parked in {stored.StarSystem}.";
    }

    /// <summary>
    /// What the hull is, from the shipped table. True of every one ever built, which is why it is
    /// said for a ship in another dock as readily as for the one underneath the Commander.
    /// </summary>
    private static IReadOnlyList<LoadoutLine> Hull(ShipBuild build)
    {
        if (EliteSpecifications.Ship(build.Hull) is not { } hull)
        {
            return [new LoadoutLine("I have no figures for this ship.")];
        }

        var lines = new List<LoadoutLine> { new("The ship", LoadoutTone.Heading) };

        var made = hull.Manufacturer is { Length: > 0 } maker ? $"{hull.Name}, by {maker}" : hull.Name;

        lines.Add(new LoadoutLine(
            hull.Pad is { Length: > 0 } pad ? $"{made}. Needs a {pad} pad." : $"{made}."));

        if (hull.Speed is { } speed && hull.Boost is { } boost)
        {
            lines.Add(new LoadoutLine(
                $"{speed.ToString(CultureInfo.InvariantCulture)} m/s, "
                + $"{boost.ToString(CultureInfo.InvariantCulture)} boosting."));
        }

        if (hull.Armour is { } armour && hull.Shields is { } shields)
        {
            lines.Add(new LoadoutLine(
                $"{armour.ToString(CultureInfo.InvariantCulture)} armour and "
                + $"{shields.ToString(CultureInfo.InvariantCulture)} shields before anything is "
                + "fitted."));
        }

        if (hull.Cost is { } cost)
        {
            lines.Add(new LoadoutLine($"Unfitted, it lists at {Credits(cost)}."));
        }

        return lines;
    }

    /// <summary>
    /// The figures that depend on what is fitted, and <b>only for the ship being flown</b>. Elite
    /// reports the loadout of one ship at a time, so these are measured for that one and unknown
    /// for every other — which is a different thing from being zero.
    /// </summary>
    private IReadOnlyList<LoadoutLine> Flying(ShipBuild build)
    {
        var loadout = state()?.Ship;

        if (loadout is not { IsKnown: true } || loadout.ShipId != build.ShipId)
        {
            return
            [
                new LoadoutLine(
                    "Elite reports the loadout of the ship you are sitting in and no other, so its "
                    + "jump range, cargo and rebuy are only known while you are in it."),
            ];
        }

        var lines = new List<LoadoutLine>();

        if (loadout.MaxJumpRange is { } range)
        {
            lines.Add(new LoadoutLine(
                $"{range.ToString("N1", CultureInfo.InvariantCulture)} ly a jump, full tank and "
                + "empty hold."));
        }

        if (loadout.CargoCapacity is { } cargo)
        {
            lines.Add(new LoadoutLine($"{cargo.ToString(CultureInfo.InvariantCulture)} tonnes of hold."));
        }

        if (loadout.UnladenMass is { } mass)
        {
            lines.Add(new LoadoutLine(
                $"{mass.ToString("N1", CultureInfo.InvariantCulture)} tonnes unladen."));
        }

        if (loadout.TotalValue is { } worth)
        {
            lines.Add(new LoadoutLine($"Worth {Credits(worth)}, ship and modules together."));
        }

        if (loadout.Rebuy is { } rebuy)
        {
            // Its own line and its own tone. The rebuy is the one figure here that is a warning
            // rather than a statistic.
            lines.Add(new LoadoutLine($"Rebuy is {Credits(rebuy)}.", LoadoutTone.Danger));
        }

        if (loadout.HullHealth is { } health && health < 100)
        {
            lines.Add(new LoadoutLine(
                $"Hull at {health.ToString(CultureInfo.InvariantCulture)}%.", LoadoutTone.Danger));
        }

        // The heading last, and only if anything is under it. A `Loadout` carrying none of these
        // is unusual and possible, and a heading with nothing beneath it reads as a block that
        // failed to load rather than as one with nothing to say.
        return lines.Count == 0 ? [] : [new LoadoutLine("As it is fitted", LoadoutTone.Heading), .. lines];
    }

    /// <summary>Credits, grouped, because a nine-digit number without separators is unreadable.</summary>
    private static string Credits(long amount) =>
        $"{amount.ToString("N0", CultureInfo.InvariantCulture)} cr";

    /// <summary>
    /// The hull's slots, grouped, whole, and with the cosmetics off them
    /// (remediation.md 12, items 1, 2, 3 and 6).
    /// <para>
    /// <b>The layout leads and the journal fills it in.</b> It used to be the other way round —
    /// the list was whatever the <c>Loadout</c> event happened to mention — which meant an empty
    /// hardpoint did not exist as far as this page was concerned, and a paint job did.
    /// </para>
    /// <para>
    /// A hull the table has no layout for still gets a list, from the journal and the plans as
    /// before; what it does not get is a slot no hull outfits, because that question is answered
    /// by the table as a whole rather than by the one hull's row in it.
    /// </para>
    /// </summary>
    public IReadOnlyList<LoadoutRow> Slots(string item)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        var fitted = Modules(build);
        var layout = EliteSpecifications.Slots(build.Hull);

        var slots = layout.Count > 0
            ? [.. layout.Select(slot => (slot.Name, slot.Kind, Word: slot.Describe()))]
            : Unlaid(build, fitted);

        return
        [
            .. slots.Select(slot =>
            {
                var plan = build.For(slot.Name);
                var module = fitted.FirstOrDefault(candidate =>
                    string.Equals(candidate.Slot, slot.Name, StringComparison.OrdinalIgnoreCase));

                return new LoadoutRow(
                    $"{build.Id}|{slot.Name}",
                    slot.Word,
                    slot.Word,

                    // The plan where there is one, what is fitted where there is not, and the
                    // word for neither — because a row with a blank note reads as a row d47 has
                    // nothing to say about rather than as an empty slot.
                    plan is not null ? plan.Describe() : Describe(module) ?? Vacant(build, fitted),
                    plan is not null)
                {
                    Group = ShipSlot.Heading(slot.Kind),
                };
            }),
        ];
    }

    /// <summary>
    /// The list for a hull with no layout: what the journal mentioned and what is planned, minus
    /// anything no hull outfits.
    /// <para>
    /// Frontier ships hulls faster than the table is rebuilt, so this is the state a brand new
    /// ship is in for a release or two. Ordering follows the same four blocks where the name is
    /// one the table recognises, and anything else falls to the end rather than being dropped —
    /// an unrecognised slot on an unrecognised hull is more likely a new kind of slot than a new
    /// kind of decoration.
    /// </para>
    /// </summary>
    private static List<(string Name, ShipSlotKind Kind, string Word)> Unlaid(
        ShipBuild build, IReadOnlyList<ShipModule> fitted)
    {
        var names = new List<string>();

        names.AddRange(fitted.Select(module => module.Slot));

        foreach (var plan in build.Slots)
        {
            if (!names.Any(name => string.Equals(name, plan.Slot, StringComparison.OrdinalIgnoreCase)))
            {
                names.Add(plan.Slot);
            }
        }

        return
        [
            .. names
                .Select(name => (Name: name, Kind: EliteSpecifications.KindOf(name)))
                .Where(slot => slot.Kind is not null)
                .OrderBy(slot => slot.Kind)
                .Select(slot => (slot.Name, slot.Kind!.Value, Word: slot.Name)),
        ];
    }

    /// <summary>
    /// What an empty slot says. Two answers, and the difference is whether d47 can see the ship:
    /// "empty" is a fact about the slot, and it is only a fact when the Commander is sitting in
    /// the ship that reported it.
    /// </summary>
    private string Vacant(ShipBuild build, IReadOnlyList<ShipModule> fitted) =>
        fitted.Count > 0 || state()?.Ship is { IsKnown: true, ShipId: var id } && id == build.ShipId
            ? "empty"
            : "not seen";

    public string Promote(string item) =>
        Resolve(item) is { } build ? ships.Promote(build.Id) : "That build is not there any more.";

    /// <summary>
    /// A hull the Commander intends to buy can be dropped; one they own cannot
    /// (remediation.md 11, item 7). Owned is derived from the journal and intended is authored,
    /// which is the same rule the checklist draws between a computed line and a written one.
    /// </summary>
    public string? DropLabel(string item) =>
        Resolve(item) is { IsOwned: false } ? "Drop this ship" : null;

    public string Drop(string item) =>
        Resolve(item) is { } build ? ships.Delete(build.Id) : "That build is not there any more.";

    public bool HasPlan(string item, string slot) => Resolve(item)?.For(slot) is not null;

    public void Clear(string item, string slot)
    {
        if (Resolve(item) is { } build)
        {
            ships.Clear(build.Id, slot);
        }
    }

    public IReadOnlyList<LoadoutLine> Fitted(string item, string slot)
    {
        if (Resolve(item) is not { } build)
        {
            return [new LoadoutLine("That build is not there any more.")];
        }

        var loadout = state()?.Ship;

        if (loadout is not { IsKnown: true } || loadout.ShipId != build.ShipId)
        {
            return
            [
                new LoadoutLine(
                    "Elite reports the loadout of the ship you are sitting in and no other, so I "
                    + "cannot say what is in this slot right now."),
            ];
        }

        var module = loadout.Modules.FirstOrDefault(candidate =>
            string.Equals(candidate.Slot, slot, StringComparison.OrdinalIgnoreCase));

        if (module is null)
        {
            return [new LoadoutLine("Nothing.")];
        }

        var lines = new List<LoadoutLine>
        {
            new(EliteSpecifications.ModuleName(module.Item) ?? "Nothing.", LoadoutTone.Body),
        };

        if (module.Blueprint is { Length: > 0 } blueprint)
        {
            var grade = module.BlueprintLevel is { } level
                ? $"grade {level.ToString(CultureInfo.InvariantCulture)} "
                : string.Empty;

            lines.Add(new LoadoutLine(
                $"{grade}{ChecklistNaming.Readable(blueprint)}"
                + (module.Experimental is { Length: > 0 } effect ? $", {effect}" : string.Empty)));
        }

        return lines;
    }

    public IReadOnlyList<LoadoutLine> Planned(string item, string slot)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        if (build.For(slot) is not { } plan)
        {
            return [new LoadoutLine("Nothing planned for this slot.")];
        }

        var lines = new List<LoadoutLine> { new(plan.Describe(), LoadoutTone.Body) };

        if (build.Scope is not { } scope)
        {
            return lines;
        }

        lines.Add(Verdict(build, plan, scope));
        lines.AddRange(Cost(build, plan, scope));

        return lines;
    }

    /// <summary>
    /// What the Commander wants in this slot: the module, then the blueprint, then the grade
    /// (remediation.md 12, item 5).
    /// <para>
    /// <b>Three lists rather than a name to spell.</b> It used to ask for the blueprint by voice
    /// and the grade on the keyboard, which meant a plan for a slot depended on getting a phrase
    /// like "Increased FSD Range" right — and there was no way at all to say what should go in an
    /// empty compartment, only what should be rolled on whatever was already there.
    /// </para>
    /// <para>
    /// Every list is the slot's own. What can go in a size 4 hardpoint is not what can go in a
    /// size 4 compartment, and the modules a blueprint applies to are not every blueprint — so
    /// each step narrows the next rather than offering the whole catalogue three times.
    /// </para>
    /// <para>
    /// <b>Each step can also be skipped</b>, because the three are separate wants. "A shield
    /// generator, I don't care what grade" is a plan; so is "grade 5 dirty drives on whatever is
    /// in there". The first row of each list is the one that declines it.
    /// </para>
    /// </summary>
    public void Ask(string item, string slot, PanelPrompts prompts, Action done)
    {
        if (Resolve(item) is not { } build)
        {
            return;
        }

        var known = EliteSpecifications.Slot(build.Hull, slot);

        if (known is null)
        {
            // A hull the table has no layout for. Nothing can be listed, so the old way in stays
            // rather than the slot becoming unplannable while the table catches up.
            Spell(build, slot, prompts, done);
            return;
        }

        var plan = build.For(slot);

        AskModule(build, known, plan, prompts, module =>
            AskBlueprint(build, known, plan, module, prompts, (blueprint, grade) =>
            {
                ships.Plan(build.Id, new SlotPlan(slot, blueprint, grade, plan?.Engineer)
                {
                    Module = module,
                    Experimental = plan?.Experimental,
                });

                done();
            }));
    }

    /// <summary>The modules this slot takes, by name — the variants are the slot's business.</summary>
    private static void AskModule(
        ShipBuild build,
        ShipSlot slot,
        SlotPlan? plan,
        PanelPrompts prompts,
        Action<string?> chosen)
    {
        // By name rather than one row per class and rating. A size 6 compartment takes over a
        // hundred parts and around forty things; the Commander is choosing the thing, and which
        // 6A of it they buy is what the slot's size already decided.
        // Grouped without regard to case, because the id list spells one weapon two ways:
        // `hpt_atdumbfiremissile_turret_large` is an "AX Missile Rack" and
        // `hpt_atdumbfiremissile_fixed_large` is an "Ax Missile Rack". Grouped exactly, that is
        // one weapon offered twice, which reads as two things a Commander has to choose between.
        // The spelling shown is the first in ordinal order, which is a rule rather than a taste —
        // and it lands on Frontier's own capitals here.
        var modules = EliteSpecifications.ModulesFor(slot)
            .GroupBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ChoiceOption(
                Spelling(group),
                Spelling(group),
                Sizes(group)))
            .ToList();

        if (modules.Count == 0)
        {
            chosen(plan?.Module);
            return;
        }

        prompts.Choose(
            new ChoiceRequest(
                "loadout.module",
                "Module",
                $"What goes in {slot.Describe()}?",
                Context(build, slot),
                [new ChoiceOption(string.Empty, "Anything — I only want the engineering"), .. modules],
                plan?.Module,
                ChoiceSurface.Page)
            {
                CurrentWord = "planned now",
                Searchable = true,
            },
            option => chosen(option.Key.Length == 0 ? null : option.Key));
    }

    /// <summary>
    /// The blueprint, then the grade. Two steps because the grades on offer are the ones that
    /// blueprint actually has — five is not universal, and offering a grade nobody rolls is a
    /// plan that can never be met.
    /// </summary>
    private static void AskBlueprint(
        ShipBuild build,
        ShipSlot slot,
        SlotPlan? plan,
        string? module,
        PanelPrompts prompts,
        Action<string?, int?> chosen)
    {
        // What the blueprints are listed for: the module just chosen, or the one already planned,
        // and otherwise everything — which is a long list and is exactly what the search is for.
        var recipes = BlueprintCatalogue.ForModule(module ?? plan?.Module)
            .Where(recipe => recipe.Kind == BlueprintKind.Modification)
            .ToList();

        if (recipes.Count == 0)
        {
            recipes = [.. BlueprintCatalogue.All.Where(recipe => recipe.Kind == BlueprintKind.Modification)];
        }

        var names = recipes
            .GroupBy(recipe => recipe.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        prompts.Choose(
            new ChoiceRequest(
                "loadout.blueprint",
                "Blueprint",
                module is { Length: > 0 } wanted ? $"What roll on the {wanted}?" : "What roll?",
                Context(build, slot),
                [
                    new ChoiceOption(string.Empty, "No engineering — the module as it comes"),
                    .. names.Select(group => new ChoiceOption(
                        group.Key,
                        group.Key,
                        Grades(group))),
                ],
                plan?.Blueprint,
                ChoiceSurface.Page)
            {
                CurrentWord = "planned now",
                Searchable = true,
            },
            option =>
            {
                var blueprint = option.Key.Length == 0 ? null : option.Key;

                var offered = blueprint is null
                    ? [1, 2, 3, 4, 5]
                    : names.First(group => group.Key == blueprint)
                        .Select(recipe => recipe.Grade)
                        .Where(grade => grade is not null)
                        .Select(grade => grade!.Value)
                        .Distinct()
                        .Order()
                        .ToList();

                AskGrade(build, slot, plan, blueprint, offered, prompts, chosen);
            });
    }

    /// <summary>
    /// The grade, from the ones that blueprint has. <b>Any is a real answer</b> rather than an
    /// unknown, which is why it is the first row rather than a way out of the question.
    /// </summary>
    private static void AskGrade(
        ShipBuild build,
        ShipSlot slot,
        SlotPlan? plan,
        string? blueprint,
        IReadOnlyList<int> offered,
        PanelPrompts prompts,
        Action<string?, int?> chosen)
    {
        prompts.Choose(
            new ChoiceRequest(
                "loadout.grade",
                "Grade",
                blueprint is { Length: > 0 } roll ? $"Which grade of {roll}?" : "Which grade?",
                Context(build, slot),
                [
                    new ChoiceOption(string.Empty, "Any grade"),
                    .. offered.Select(grade => new ChoiceOption(
                        grade.ToString(CultureInfo.InvariantCulture),
                        $"Grade {grade.ToString(CultureInfo.InvariantCulture)}")),
                ],
                plan?.Grade?.ToString(CultureInfo.InvariantCulture),
                ChoiceSurface.Layer)
            {
                CurrentWord = "planned now",
            },
            option => chosen(
                blueprint,
                int.TryParse(option.Key, CultureInfo.InvariantCulture, out var grade) ? grade : null));
    }

    /// <summary>
    /// The header's second line: which slot, how big, and what is in it. The one thing a dropdown
    /// cannot do, and the reason taking the panel is worth it.
    /// </summary>
    private static string Context(ShipBuild build, ShipSlot slot) =>
        $"{build.Describe()} · {slot.Describe()}. It does not reach your checklist until you "
        + "promote the build.";

    /// <summary>One spelling for a name the id list writes more than one way.</summary>
    private static string Spelling(IEnumerable<ModuleSpecification> variants) =>
        variants.Select(module => module.Name).Order(StringComparer.Ordinal).First();

    /// <summary>The sizes a module comes in, so a row says what it would cost the slot.</summary>
    private static string Sizes(IEnumerable<ModuleSpecification> variants)
    {
        var said = variants
            .Select(module => module.Size)
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToList();

        return said.Count == 0 ? string.Empty : string.Join(", ", said);
    }

    /// <summary>The grades a blueprint offers, said as a range rather than as a list of five.</summary>
    private static string Grades(IEnumerable<Blueprint> recipes)
    {
        var grades = recipes
            .Select(recipe => recipe.Grade)
            .Where(grade => grade is not null)
            .Select(grade => grade!.Value)
            .Distinct()
            .Order()
            .ToList();

        return grades.Count switch
        {
            0 => string.Empty,
            1 => $"grade {grades[0].ToString(CultureInfo.InvariantCulture)}",
            _ => $"grades {grades[0].ToString(CultureInfo.InvariantCulture)} to "
                 + grades[^1].ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// The way in for a hull the table has no layout for: a blueprint said, and a grade typed, as
    /// it was for every slot before there was anything to list.
    /// </summary>
    private void Spell(ShipBuild build, string slot, PanelPrompts prompts, Action done) =>
        prompts.Enter(
            new EntryRequest(
                "loadout.blueprint",
                "Blueprint",
                $"What do you want on {slot}?",
                "A blueprint by name. It does not reach your checklist until you promote the build.",
                build.For(slot)?.Blueprint ?? string.Empty,
                EntrySurface.Voice),
            blueprint => prompts.Enter(
                new EntryRequest(
                    "loadout.grade",
                    "Grade",
                    $"Which grade of {blueprint}?",
                    "1 to 5, or leave it empty for any grade — which is a real answer rather than "
                    + "an unknown.",
                    string.Empty,
                    EntrySurface.Keyboard,
                    value => value.Trim().Length == 0
                             || (int.TryParse(value.Trim(), out var grade) && grade is >= 1 and <= 5)
                        ? EntryVerdict.Ok
                        : EntryVerdict.No("A grade is 1 to 5, or nothing at all for any.")),
                grade =>
                {
                    ships.Plan(build.Id, new SlotPlan(
                        slot,
                        string.IsNullOrWhiteSpace(blueprint) ? null : blueprint.Trim(),
                        int.TryParse(grade.Trim(), out var level) ? level : null));

                    done();
                }));

    /// <summary>
    /// The journal's verdict, as of when it was taken. <b>No checkbox</b>: a derived item's
    /// progress is a diff against live state, and a tick here would be undone or left standing
    /// and lying by the next read.
    /// </summary>
    private LoadoutLine Verdict(ShipBuild build, SlotPlan plan, ChecklistScope scope)
    {
        var intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, plan.Slot)
        {
            Detail = plan.Blueprint,
            Grade = plan.Grade,
            Engineer = plan.Engineer,
        };

        var verdict = ChecklistEvaluator.Evaluate(
            new ChecklistItem
            {
                Key = ChecklistKeys.For(intent),
                Scope = scope,
                Kind = ChecklistItemKind.Derived,
                Source = ChecklistSource.EngineeringPlan,
                Text = plan.Describe(),
                Intent = intent,
                Hull = build.Hull,
            },
            state());

        var said = verdict?.Reason
                   ?? "Nothing can be said about this right now — Elite reports the loadout of the "
                      + "ship you are sitting in and no other.";

        return new LoadoutLine(
            said,
            verdict is { } answered && ChecklistNextAction.IsWrong(answered.State)
                ? LoadoutTone.Danger
                : LoadoutTone.Muted);
    }

    /// <summary>
    /// What this plan costs, on the slot. <b>Per plan</b>, because that is the question a
    /// Commander looking at one slot is asking — the arithmetic across every plan at once is what
    /// the gap page is for.
    /// </summary>
    private IReadOnlyList<LoadoutLine> Cost(ShipBuild build, SlotPlan plan, ChecklistScope scope)
    {
        var costing = EngineeringPlan.Cost(
            EngineeringPlan.Items(scope, build.Hull, [plan.ToRequest()], checklists.SlotFor),
            state());

        var lines = new List<LoadoutLine>();

        foreach (var gate in costing.Gates)
        {
            lines.Add(new LoadoutLine(gate, LoadoutTone.Danger));
        }

        if (costing.Ingredients.Count == 0)
        {
            return lines;
        }

        lines.Add(new LoadoutLine("What it costs", LoadoutTone.Heading));

        foreach (var ingredient in costing.Ingredients.OrderByDescending(entry => entry.Short))
        {
            // Held, needed and short, all three. "Short 12" alone is a number a Commander cannot
            // check, and the arithmetic is exact rather than estimated.
            lines.Add(new LoadoutLine(
                $"{ingredient.Material.Name}: {ingredient.Held} of {ingredient.Needed}"
                + (ingredient.Short > 0 ? $", {ingredient.Short} short" : string.Empty)
                + (ingredient.ExceedsCapacity ? " — more than one trip" : string.Empty)));
        }

        return lines;
    }

    private IReadOnlyList<ShipModule> Modules(ShipBuild build)
    {
        var loadout = state()?.Ship;

        return loadout is { IsKnown: true } && loadout.ShipId == build.ShipId
            ? loadout.Modules
            : [];
    }

    private static string Key(FleetEntry entry) =>
        entry.Build?.Id
        ?? (entry.Stored is { } stored
            ? Unplanned + stored.ShipId.ToString(CultureInfo.InvariantCulture)
            : entry.Hull);

    /// <summary>
    /// The build a crumb key means, <b>started if the ship has none yet</b>.
    /// <para>
    /// A ship the journal reports and nothing has planned for has no build id to key a crumb on,
    /// so the row carries its <c>ShipID</c> instead and the build is made on the way in — which
    /// keeps the key stable afterwards, because that ship's build is what the id then resolves to.
    /// </para>
    /// </summary>
    private ShipBuild? Resolve(string key)
    {
        if (ships.Store.Find(key) is { } build)
        {
            return build;
        }

        if (key.StartsWith(Unplanned, StringComparison.Ordinal)
            && int.TryParse(key[Unplanned.Length..], CultureInfo.InvariantCulture, out var shipId))
        {
            var flying = ships.Fleet().FirstOrDefault(entry => entry.Stored?.ShipId == shipId);

            return flying?.Stored is { } stored
                ? ships.BuildFor(stored.ShipId, stored.Type, stored.Name)
                : null;
        }

        // A key that is a hull. The trail Phase 26 wrote for a ship with no build used one, so
        // resolving it keeps a breadcrumb from that shape working rather than dead-ending.
        var byHull = ships.Fleet()
            .Where(entry => string.Equals(entry.Hull, key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return byHull is [{ Stored: { } only }]
            ? ships.BuildFor(only.ShipId, only.Type, only.Name)
            : byHull.Count == 1 ? byHull[0].Build : null;
    }

    /// <summary>
    /// A fitted module, named the way the outfitting screen names it (remediation.md 12, item 4).
    /// The reading that only strips the decoration off the symbol said "int powerplant size6
    /// class5"; the table ships both spellings, so it can say "6A Power Plant".
    /// </summary>
    private static string? Describe(ShipModule? module) =>
        module is null ? null : EliteSpecifications.ModuleName(module.Item);
}
