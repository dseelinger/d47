using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>The Loadout tab's Ships mode: the fleet, a ship, a slot (Phase 26, "Ships").</summary>
public sealed class ShipsMode(
    ShipPlanService ships,
    ChecklistService checklists,
    Func<CommanderGameState?> state,
    Func<ModulePower>? measured = null,
    ShipsDrawingsMemory? drawings = null) : ILoadoutMode
{
    /// <summary>A row for a ship the journal reports and nothing has planned for yet.</summary>
    private const string Unplanned = "new:";

    /// <summary>The chooser key for "keep the module this plan already names".</summary>
    private const string KeepPlanned = "keep:planned";

    public string RootKey => LoadoutPages.FleetRoot;

    public string RootWord => "Ships";

    public string ItemPrefix => LoadoutPages.ShipPrefix;

    public string SlotPrefix => LoadoutPages.SlotPrefix;

    public string? SlotHelp => D47.Core.Capabilities.Builtin.EngineeringCapability.Id;

    /// <summary>The page behind the module picker's question mark — its own, rather than the slot's.</summary>
    public const string ModuleChoiceHelp =
        D47.Core.Help.HelpLibrary.GeneralPrefix + "choosing-a-module";

    /// <summary>Two things move a ship page, and only one of them was wired (remediation.md 17, item 7).</summary>
    public event Action? Changed
    {
        add
        {
            ships.Store.Changed += value;

            // And a third, added by Phase 38: the tab now carries a question waiting on the Commander, so a
            // question arriving or being answered moves these pages.
            checklists.Proposals.Changed += value;
            _invalidated += value;
        }

        remove
        {
            ships.Store.Changed -= value;
            checklists.Proposals.Changed -= value;
            _invalidated -= value;
        }
    }

    /// <summary>The journal moved under the page.</summary>
    public void Invalidate() => _invalidated?.Invoke();

    private Action? _invalidated;

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

    /// <summary>The fleet, in one alphabet (asked for 2026-09-03).</summary>
    public IReadOnlyList<LoadoutRow> Items() =>
    [
        .. ships.Fleet()
            .OrderBy(entry => entry.Name ?? entry.HullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(entry =>
            {
                var planned = entry.Planned;

                return new LoadoutRow(
                    Key(entry),
                    entry.Name ?? entry.HullName,
                    entry.Name is { Length: > 0 } name ? $"{name} ({entry.HullName})" : entry.HullName,

                    // Where it is, and how its plans stand.
                    planned > 0
                        ? $"{entry.Where()} · {planned.ToString(CultureInfo.InvariantCulture)} planned"
                        : entry.Where(),
                    planned > 0)
                {
                    Standing = entry.IsActive
                        ? LoadoutStanding.Active
                        : entry.IsOwned
                            ? LoadoutStanding.Owned
                            : LoadoutStanding.Wanted,
                    Hull = entry.Hull,
                };
            }),
    ];

    public bool Cards => true;

    /// <summary>The drawings switch, for a fleet that has somewhere to remember it.</summary>
    public LoadoutToggle? IndexToggle =>
        drawings is null
            ? null
            : new LoadoutToggle("Large cards", drawings.Drawings, drawings.Remember);

    /// <summary>A hull to plan a build for before it is bought.</summary>
    public void New(PanelPrompts prompts, Action done) =>
        prompts.Enter(
            new EntryRequest(
                "loadout.intend",
                "Ship",
                "Which ship do you intend to buy?",
                "The build is planned now; buying one will point the plan at it.",
                string.Empty,
                EntrySurface.Voice,
                value => EliteSpecifications.Ship(value) is null
                    ? EntryVerdict.No($"I do not know a ship called “{value}”.")
                    : EntryVerdict.Ok,
                Hulls),
            hull =>
            {
                ships.Intend(hull);
                done();
            });

    /// <summary>
    /// Every hull the specification table can measure, alphabetically — the list the picker offers and,
    /// by construction, exactly the set the validation accepts.
    /// </summary>
    private static IReadOnlyList<string> Hulls { get; } =
    [
        .. EliteSpecifications.Ships
            .Select(ship => ship.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>
    /// The ship, named — "Campaigner (Panther Clipper MkII)", or "Corsair, intended" for a hull nobody
    /// has bought yet (#289).
    /// </summary>
    public string? Title(string item) => Resolve(item)?.Describe();

    /// <summary>What is worth saying under the name, which for an owned ship is nothing.</summary>
    public string? Summary(string item) => Resolve(item) is not { IsOwned: false }
        ? null
        : "Buying one will point this plan at it.";

    /// <summary>What this ship is, where it is, and what it can do (remediation.md 13, item 2).</summary>
    public string? HullOf(string item) => Resolve(item)?.Hull;

    public IReadOnlyList<LoadoutLine> Details(string item)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        var entry = ships.Fleet().FirstOrDefault(candidate => candidate.Build?.Id == build.Id);
        var lines = new List<LoadoutLine>();

        // Where it is, said in full rather than in the row's one phrase: the row has to fit a line and this
        // does not, so "here" becomes the station it is parked at.
        if (entry?.Stored is { } stored)
        {
            lines.Add(new LoadoutLine(Whereabouts(stored, entry.IsActive), LoadoutTone.Body)
            {
                // The system, on the clipboard, for Elite's Galaxy Map search.
                Copy = stored.HasSystem ? new LoadoutCopy(stored.StarSystem) : null,
            });

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
            // The system too, so the copy glyph beside this line has something to be about — the ship under
            // the Commander is as good a reason to want the system name on the clipboard as any other, and
            // often a better one.
            return stored.HasSystem
                ? $"You are flying it, in {stored.StarSystem}."
                : "You are flying it.";
        }

        if (stored.InTransit)
        {
            return $"In transit to {stored.StarSystem}.";
        }

        return stored.StationName is { Length: > 0 } station
            ? $"Parked at {station}, {stored.StarSystem}."
            : $"Parked in {stored.StarSystem}.";
    }

    /// <summary>What the hull is, from the shipped table.</summary>
    private static IReadOnlyList<LoadoutLine> Hull(ShipBuild build)
    {
        if (EliteSpecifications.Ship(build.Hull) is not { } hull)
        {
            return [new LoadoutLine("I have no figures for this ship.")];
        }

        var lines = new List<LoadoutLine> { new("The ship", LoadoutTone.Heading) };

        // One spelling of name-and-maker, shared with the spoken specification (#108).
        var made = hull.Described();

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

    /// <summary>The figures that depend on what is fitted, and only for the ship being flown.</summary>
    private IReadOnlyList<LoadoutLine> Flying(ShipBuild build)
    {
        if (Picture(build) is not { } seen)
        {
            return
            [
                new LoadoutLine(
                    "Elite reports the loadout of the ship you are sitting in and no other, so its "
                    + "jump range, cargo and rebuy are only known once you have been in it."),
            ];
        }

        var loadout = seen.Loadout;
        var lines = new List<LoadoutLine>();

        // Dated where it is remembered, and silent where it is live.
        if (seen.SeenAt is { } when)
        {
            lines.Add(new LoadoutLine($"As you left it, {Age(when)}.", LoadoutTone.Muted));

            // **Said where the doubt is, not only where the fix is** (<a
            // href=".com/dseelinger/d47/issues/128">#128</a>).
            lines.Add(new LoadoutLine(
                "Not look right? Rescan your journals on the Ships card in Settings.",
                LoadoutTone.Muted));
        }

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
            // Its own line and its own tone.
            lines.Add(new LoadoutLine($"Rebuy is {Credits(rebuy)}.", LoadoutTone.Danger));
        }

        if (loadout.HullHealth is { } health && health < 100)
        {
            lines.Add(new LoadoutLine(
                $"Hull at {health.ToString(CultureInfo.InvariantCulture)}%.", LoadoutTone.Danger));
        }

        // The heading last, and only if anything is under it.
        return lines.Count == 0 ? [] : [new LoadoutLine("As it is fitted", LoadoutTone.Heading), .. lines];
    }

    /// <summary>Credits, grouped, because a nine-digit number without separators is unreadable.</summary>
    private static string Credits(long amount) =>
        $"{amount.ToString("N0", CultureInfo.InvariantCulture)} cr";

    /// <summary>
    /// The question left on the tab when one was asked out loud and not answered (Phase 38, "Ask before
    /// the plan and the checklist drift apart").
    /// </summary>
    public LoadoutNotice? Notice()
    {
        var waiting = checklists.Proposals
            .PendingFor(checklists.Document.CommanderFid)
            .FirstOrDefault(proposal => proposal.Kind == ProposalKind.Plan
                                        && proposal.Source == ChecklistSource.EngineeringPlan
                                        && proposal.Scope.Group == ChecklistGroup.Ship);

        if (waiting is null)
        {
            return null;
        }

        var id = waiting.Id;

        return new LoadoutNotice(
            $"{waiting.Summary.TrimEnd('.')}?",
            () => checklists.Accept(id),
            () => checklists.Decline(id));
    }

    /// <summary>Power and jump range, live while the build is edited (Phase 38).</summary>
    public IReadOnlyList<LoadoutGauge> Gauges(string item)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        var seen = Picture(build);

        // Only the ship being flown, and only where the file describes *this* one.
        var live = measured?.Invoke();
        var draw = live is not null && seen is { IsLive: true } && live.Describes(seen.Loadout)
            ? live.Draw
            : null;

        var gauges = ShipGauges.Read(build, seen?.Loadout, draw);

        if (gauges.Silent is { Length: > 0 } why)
        {
            return [new LoadoutGauge("Power", why, 0, LoadoutTone.Muted)];
        }

        var drawn = new List<LoadoutGauge>();

        if (gauges.Power is { } power)
        {
            drawn.Add(Gauge(power));
        }

        if (gauges.Jump is { } jump)
        {
            drawn.Add(Gauge(jump, gauges.Unmodelled));
        }

        return drawn;
    }

    /// <summary>The power bar: filled to the deployed draw, marked where the retracted draw sits.</summary>
    private static LoadoutGauge Gauge(PowerGauge power)
    {
        var modelled = power.Kind == FigureKind.Modelled;

        if (power.Capacity is not { } made || made <= 0)
        {
            // A build with no plant d47 can see.
            return new LoadoutGauge("Power", $"{Megawatts(power.Deployed)} drawn", 0, LoadoutTone.Muted)
            {
                Note = "No power plant I can see, so there is nothing to weigh that against.",
                Modelled = modelled,
            };
        }

        var reading =
            $"{Megawatts(power.Deployed)} of {Megawatts(made)} · {Percent(power.DeployedShare)} deployed";

        return new LoadoutGauge(
            "Power",
            reading,
            power.Deployed / made,
            power.Fits ? LoadoutTone.Body : LoadoutTone.Danger)
        {
            Marks = [new LoadoutMark(power.Retracted / made, $"{Percent(power.RetractedShare)} retracted")],

            // **Three figures under the points they belong to** (the Commander's instruction, 2026-09-01).
            Scale =
            [
                new LoadoutMark(power.Retracted / made, Percent(power.RetractedShare)),
                new LoadoutMark(1, "100%"),
                new LoadoutMark(power.Deployed / made, Percent(power.DeployedShare)),
            ],
            Note = power.Overage is { } over
                ? $"{Megawatts(over)} over with the hardpoints out."
                : null,
            Modelled = modelled,
        };
    }

    /// <summary>The jump bar, worst to best, with the game's own figure at the far end.</summary>
    private static LoadoutGauge Gauge(JumpGauge jump, int unmodelled)
    {
        var span = jump.Best > 0 ? jump.Best : 1;

        return new LoadoutGauge(
            "Jump range",
            $"{LightYears(jump.Worst)} – {LightYears(jump.Best)} ly",
            jump.Worst / span,
            LoadoutTone.Body)
        {
            Marks =
            [
                new LoadoutMark(jump.Middle / span, $"{LightYears(jump.Middle)} full tank"),
                new LoadoutMark(1, $"{LightYears(jump.Best)} one jump's fuel"),
            ],

            // Counted rather than swallowed: a plan naming a kind of module and not a size has no mass to
            // add, so those slots weigh what is fitted and the gauge says how many did.
            Note = unmodelled > 0
                ? $"{unmodelled.ToString(CultureInfo.InvariantCulture)} planned "
                  + $"{(unmodelled == 1 ? "slot names a module" : "slots name modules")} without a "
                  + "size, so this weighs what is fitted there."
                : null,
            Modelled = jump.Kind == FigureKind.Modelled,
        };
    }

    private static string Megawatts(double value) =>
        $"{value.ToString("0.00", CultureInfo.InvariantCulture)} MW";

    private static string LightYears(double value) =>
        value.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Percent(double? share) =>
        share is { } fraction
            ? $"{(fraction * 100).ToString("0", CultureInfo.InvariantCulture)}%"
            : "—";

    /// <summary>
    /// The hull's slots, grouped, whole, and with the cosmetics off them (remediation.md 12, items 1,
    /// 2, 3 and 6).
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
            ? [.. layout.Select(slot => (slot.Name, slot.Kind, Word: slot.Describe(), Brief: slot.Short(), slot.Size))]
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

                    // The plan where there is one, what is fitted where there is not, and the word for
                    // neither — because a row with a blank note reads as a row d47 has nothing to say about
                    // rather than as an empty slot.
                    plan is not null ? plan.Describe() : Describe(module) ?? Vacant(build, fitted),
                    Outstanding(plan, module))
                {
                    Group = ShipSlot.Heading(slot.Kind),

                    // Read off the fitted module rather than off the plan, because they answer different
                    // questions (remediation.md 15, item 10): the dot beside this says there is work left to
                    // do, and the gear says a roll has already been done.
                    Engineered = module?.Blueprint is { Length: > 0 },

                    Parts = Parted(slot, plan, module, build, fitted),
                };
            }),
        ];
    }

    /// <summary>
    /// One slot row broken into the two sides it is drawn from — what is in the slot, and what the plan
    /// asks for (docs/plans/change-requests.md 38; the row itself asked for 2026-08-20).
    /// </summary>
    private LoadoutParts Parted(
        (string Name, ShipSlotKind Kind, string Word, string Brief, int Size) slot,
        SlotPlan? plan,
        ShipModule? module,
        ShipBuild build,
        IReadOnlyList<ShipModule> fitted)
    {
        var planned = plan is { IsEmpty: false } ? plan : null;

        return new LoadoutParts(
            // No size on a utility mount: they are 0 by definition, so a 0 on all eight of them is noise in
            // the first column the eye lands on.
            slot.Kind == ShipSlotKind.Utility ? null : slot.Size,
            slot.Brief,
            Fitting(
                slot.Kind,
                slot.Brief,
                Side(
                // The full name — "3E Pulse Laser, gimballed" — beside the slot's own size, and the two are
                // not the same fact: a size 3 slot can hold a class 2 module, and seeing a 2 beside a 3 is
                // how a Commander spots one.
                module is not null
                    ? EliteSpecifications.ModuleName(module.Item) ?? module.Item
                    : null,
                Bare(module?.Item),

                // The Commander's words either way.
                Readable(module?.Blueprint),
                module?.BlueprintLevel,
                module?.Experimental,
                Effects(module),
                module?.Item)),
            planned is null
                ? null
                : Fitting(
                    slot.Kind,
                    slot.Brief,
                    Side(
                        Planned(planned),
                        Bare(planned.Variant) ?? planned.Module,
                        planned.Blueprint,
                        planned.Grade,
                        planned.Experimental,
                        [],
                        planned.Variant)),
            Vacant(build, fitted))
        {
            // The plan is carried out, so the second column has nothing left to say.
            Met = planned is not null && !Outstanding(planned, module),
        };
    }

    /// <summary>
    /// The two words a column does not need to carry, because the slot beside it already does
    /// (docs/plans/change-requests.md 38).
    /// </summary>
    private static LoadoutSide Fitting(ShipSlotKind kind, string brief, LoadoutSide side)
    {
        if (side.Module is not { Length: > 0 } module)
        {
            return side;
        }

        var said = module;

        if (kind == ShipSlotKind.Utility && said.StartsWith('0'))
        {
            said = said[1..];
        }

        if (kind == ShipSlotKind.Core && said.EndsWith(brief, StringComparison.OrdinalIgnoreCase))
        {
            said = said[..^brief.Length].TrimEnd();
        }

        // Nothing left means the slot's name was the whole of the module's, and a blank cell says less than a
        // repeated word does.
        return said.Length == 0 || said.Length == module.Length ? side : side with { Module = said };
    }

    /// <summary>One side of a slot row, short-named and with the module struck off its blueprint.</summary>
    /// <param name="bare">
    /// The module in Frontier's words with the size, the rating and the mount off it, which is what a
    /// blueprint's tail has to be compared against — <c>6D Hull Reinforcement Package</c> never ends a
    /// blueprint name and <c>Hull Reinforcement Package</c> does.
    /// </param>
    private static LoadoutSide Side(
        string? name,
        string? bare,
        string? blueprint,
        int? grade,
        string? experimental,
        IReadOnlyList<string> effects,
        string? symbol)
    {
        var brief = ShortNames.Of(name);

        return new LoadoutSide(
            brief.Length > 0 ? brief : null,
            name is { Length: > 0 } && !string.Equals(brief, name, StringComparison.Ordinal)
                ? name
                : null,
            ShortNames.Bare(blueprint, bare),
            grade,
            experimental,
            effects)
        {
            // Whether the module this side names is one a pledge is needed to buy (Phase 38).
            Gated = EliteSpecifications.Module(symbol)?.NeedsPledge ?? false,
        };
    }

    /// <summary>A module's name with the size, the rating and the mount off it, for the blueprint join.</summary>
    private static string? Bare(string? symbol) => EliteSpecifications.Module(symbol)?.Name;

    /// <summary>Whether this slot still has work in it (GitHub issue 38).</summary>
    private static bool Outstanding(SlotPlan? plan, ShipModule? module)
    {
        if (plan is null || plan.IsEmpty)
        {
            return false;
        }

        // Nothing there yet, so everything the plan asks for is still to do.
        if (module is null)
        {
            return true;
        }

        if (!IsWhatWasWanted(plan, module))
        {
            // Something else is in the slot.
            return true;
        }

        if (plan.Blueprint is not { Length: > 0 })
        {
            // The plan wanted a module and not a roll, and the module is here.
            return false;
        }

        // **Both spellings, because a plan and the journal do not use the same one.** A plan stores the
        // readable name the Commander picked — "System Focused" — and Elite stores a symbol —
        // `PowerDistributor_PrioritySystems`.
        if (!string.Equals(plan.Blueprint, module.Blueprint, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(plan.Blueprint, Readable(module.Blueprint), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (plan.Grade > 0 && module.BlueprintLevel < plan.Grade)
        {
            return true;
        }

        // An experimental the plan asks for and the roll has not got.
        return plan.Experimental is { Length: > 0 } wanted
               && !string.Equals(wanted, module.Experimental, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(wanted, Readable(module.Experimental), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The blueprint in the Commander's words rather than the journal's (GitHub issue 39).</summary>
    private static string? Readable(string? blueprint) =>
        blueprint is not { Length: > 0 }
            ? null
            : BlueprintCatalogue.NameOf(blueprint) ?? blueprint;

    /// <summary>
    /// The roll that is on the module, where the plan asked for a different one (GitHub issue 42).
    /// </summary>
    private static string? RolledInstead(SlotPlan? plan, ShipModule? module)
    {
        if (plan?.Blueprint is not { Length: > 0 } wanted || module?.Blueprint is not { Length: > 0 } rolled)
        {
            return null;
        }

        var here = Readable(rolled);

        return here is { Length: > 0 } && !string.Equals(here, wanted, StringComparison.OrdinalIgnoreCase)
            ? here
            : null;
    }

    /// <summary>Whether the fitted module is the one the plan asked for (GitHub issue 38).</summary>
    private static bool IsWhatWasWanted(SlotPlan plan, ShipModule module)
    {
        if (plan.Variant is { Length: > 0 } variant)
        {
            return string.Equals(variant, module.Item, StringComparison.OrdinalIgnoreCase);
        }

        if (plan.Module is not { Length: > 0 } wanted)
        {
            // A plan asking only for a roll is about whatever is in the slot.
            return true;
        }

        var here = EliteSpecifications.Module(module.Item)?.Name;

        return here is { Length: > 0 } && string.Equals(here, wanted, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The fitted module's name, where a plan names a different one and both are worth saying.</summary>
    private static string? Standing(SlotPlan? plan, ShipModule? module)
    {
        if (module is null || Planned(plan) is not { Length: > 0 } wanted)
        {
            return null;
        }

        var here = EliteSpecifications.ModuleName(module.Item) ?? module.Item;

        return string.Equals(here, wanted, StringComparison.OrdinalIgnoreCase) ? null : here;
    }

    /// <summary>What the roll actually did, biggest change first, in the shortest form that stays true.</summary>
    private static IReadOnlyList<string> Effects(ShipModule? module)
    {
        if (module is null)
        {
            return [];
        }

        return
        [
            .. module.Modifiers
                .Where(Worth)
                .OrderByDescending(Magnitude)
                .Select(Describe)
        ];
    }

    /// <summary>Whether this modifier says anything.</summary>
    private static bool Worth(ShipModifier modifier) =>
        modifier.IsPercentage
            ? modifier.Value is not null
            : modifier.Change is not null && modifier.OriginalValue is not 0;

    /// <summary>How loud this one is, for the ordering.</summary>
    private static double Magnitude(ShipModifier modifier) =>
        modifier.IsPercentage
            ? Math.Abs(modifier.Change ?? 0)
            : Math.Abs(Proportion(modifier));

    private static string Describe(ShipModifier modifier) =>
        modifier.IsPercentage
            ? $"{modifier.Value!.Value.ToString("0.#", CultureInfo.InvariantCulture)}% {modifier.Label}"
            : $"{Signed(Proportion(modifier))} {modifier.Label}";

    private static double Proportion(ShipModifier modifier) =>
        modifier.Change!.Value / Math.Abs(modifier.OriginalValue!.Value) * 100;

    private static string Signed(double percent) =>
        percent >= 0
            ? $"+{percent.ToString("0.#", CultureInfo.InvariantCulture)}%"
            : $"{percent.ToString("0.#", CultureInfo.InvariantCulture)}%";

    /// <summary>
    /// The list for a hull with no layout: what the journal mentioned and what is planned, minus
    /// anything no hull outfits.
    /// </summary>
    private static List<(string Name, ShipSlotKind Kind, string Word, string Brief, int Size)> Unlaid(
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
                .Select(slot => (slot.Name, slot.Kind!.Value, Word: slot.Name, Brief: slot.Name, Size: 0)),
        ];
    }

    /// <summary>What an empty slot says.</summary>
    private string Vacant(ShipBuild build, IReadOnlyList<ShipModule> fitted) =>
        fitted.Count > 0 || Picture(build) is not null
            ? "empty"
            : "Use ship to refresh";

    /// <summary>
    /// Whether this drag is allowed, asked while the mouse is still down (remediation.md 15, item 1).
    /// </summary>
    public bool CanCopy(string item, string from, string to) => Moved(item, from, to) is not null;

    /// <summary>
    /// Copies a slot's plan onto another, overwriting whatever was there — which is what dragging
    /// means, and why nothing asks first.
    /// </summary>
    public string Copy(string item, string from, string to)
    {
        if (Resolve(item) is not { } build)
        {
            return "That build is not there any more.";
        }

        if (Moved(item, from, to) is not { } moved)
        {
            return WhyNot(build, from, to);
        }

        ships.Plan(build.Id, moved);

        return $"Copied to {moved.Slot}: {moved.Describe()}.";
    }

    /// <summary>Why a drop was turned down, in the Commander's terms (remediation.md 17, item 8).</summary>
    private string WhyNot(ShipBuild build, string from, string to)
    {
        if (build.For(from) is null)
        {
            return $"Nothing is planned in {from}, so there is nothing to copy. "
                + "Plan that slot first, and then it can be dragged.";
        }

        var layout = EliteSpecifications.Slots(build.Hull);

        var source = layout.FirstOrDefault(slot =>
            string.Equals(slot.Name, from, StringComparison.OrdinalIgnoreCase));

        var target = layout.FirstOrDefault(slot =>
            string.Equals(slot.Name, to, StringComparison.OrdinalIgnoreCase));

        if (source is null || target is null)
        {
            return "I do not know that ship's slot layout, so I cannot work out whether that fits.";
        }

        if (source.Kind == ShipSlotKind.Core || target.Kind == ShipSlotKind.Core)
        {
            return "Core internals are not copied between — a power plant socket and a thruster "
                + "socket have nothing to say to each other.";
        }

        if (source.Kind != target.Kind)
        {
            return $"{from} and {to} are different kinds of slot, so a plan for one is not a plan "
                + "for the other.";
        }

        // Said before the size, because it is the more surprising refusal of the two and the Commander cannot
        // work it out by looking: the slot is the right kind and the right size, and what is in it takes no
        // engineering at all.
        if (build.For(from) is { Blueprint: { Length: > 0 } wanted } dragged
            && SlotCopy.Resolve(dragged, source, target) is { } would
            && !Rollable(build, target, would))
        {
            var what = FittedIn(build, target) is { } inTheWay
                ? EliteSpecifications.ModuleName(inTheWay.Item) ?? inTheWay.Item
                : to;

            return $"{what} cannot take {wanted} — Frontier offers it no engineering under that "
                   + "name. Plan the module for that slot first and the engineering will follow it.";
        }

        // Same kind, so the module itself is what does not fit — it does not come small enough for the
        // target, which is the one real failure SlotCopy documents.
        return build.For(from)?.Module is { Length: > 0 } module
            ? $"A {module} does not come small enough for {to}."
            : $"That plan does not fit {to}.";
    }

    /// <summary>What the dragged plan would become in the target slot, or null where it may not go.</summary>
    private SlotPlan? Moved(string item, string from, string to)
    {
        if (Resolve(item) is not { } build || build.For(from) is not { } plan)
        {
            return null;
        }

        var layout = EliteSpecifications.Slots(build.Hull);

        var source = layout.FirstOrDefault(slot =>
            string.Equals(slot.Name, from, StringComparison.OrdinalIgnoreCase));

        var target = layout.FirstOrDefault(slot =>
            string.Equals(slot.Name, to, StringComparison.OrdinalIgnoreCase));

        if (source is null || target is null || SlotCopy.Resolve(plan, source, target) is not { } moved)
        {
            return null;
        }

        // **A drag carries what the row was showing** (reported 2026-08-20: dragging a hull reinforcement
        // onto two empty compartments left them reading *"empty · Heavy Duty Hull Reinforcement (G5), Deep
        // Plating"* — a roll on nothing).
        if (moved.Variant is not { Length: > 0 } && FittedIn(build, source) is { } shown)
        {
            if (SlotCopy.Resize(shown.Item, target) is not { } resized)
            {
                return null;
            }

            moved = moved with { Variant = resized.Symbol, Module = resized.Name };
        }

        return Rollable(build, target, moved) ? moved : null;
    }

    /// <summary>
    /// Whether the roll being dragged is one the target slot's module can actually take (reported
    /// 2026-08-20: "Module Reinforcement packages can't be engineered").
    /// </summary>
    private bool Rollable(ShipBuild build, ShipSlot target, SlotPlan moved)
    {
        if (moved.Blueprint is not { Length: > 0 } blueprint)
        {
            return true;
        }

        // The plan's own module decides where it names one — that is what the copy will fit — and what is in
        // the slot only where it does not.
        var offered = moved.Variant is { Length: > 0 } || moved.Module is { Length: > 0 }
            ? Offered(moved.Module, moved.Variant)
            : FittedIn(build, target) is { } module
                ? Offered(EliteSpecifications.ModuleName(module.Item), module.Item)
                : null;

        return offered is null
               || offered.Any(recipe => string.Equals(recipe.Name, blueprint, StringComparison.OrdinalIgnoreCase));
    }

    public string Promote(string item) =>
        Resolve(item) is { } build ? ships.Promote(build.Id) : "That build is not there any more.";

    /// <summary>
    /// A hull the Commander intends to buy can be dropped; one they own cannot (remediation.md 11, item
    /// 7).
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

        if (Picture(build) is not { } seen)
        {
            return
            [
                new LoadoutLine(
                    "I have never seen inside this ship — Elite reports the loadout of the one you "
                    + "are sitting in and no other. Board it once and I will remember it."),
            ];
        }

        var module = seen.Loadout.Modules.FirstOrDefault(candidate =>
            string.Equals(candidate.Slot, slot, StringComparison.OrdinalIgnoreCase));

        // Said before the module rather than after it, because it qualifies everything below — and left off
        // entirely for the ship being flown, where "now" is the accurate tense.
        var asOf = seen.SeenAt is { } when
            ? new LoadoutLine($"As you left it, {Age(when)}.", LoadoutTone.Muted)
            : null;

        if (module is null)
        {
            return asOf is null ? [new LoadoutLine("Nothing.")] : [asOf, new LoadoutLine("Nothing.")];
        }

        var lines = new List<LoadoutLine>();

        if (asOf is not null)
        {
            lines.Add(asOf);
        }

        lines.Add(new LoadoutLine(EliteSpecifications.ModuleName(module.Item) ?? "Nothing.", LoadoutTone.Body));

        if (module.Blueprint is { Length: > 0 } blueprint)
        {
            var grade = module.BlueprintLevel is { } level
                ? $"grade {level.ToString(CultureInfo.InvariantCulture)} "
                : string.Empty;

            // Its own tone, and now a name rather than a symbol.
            lines.Add(new LoadoutLine(
                $"{grade}{ChecklistNaming.Readable(blueprint)}"
                + (module.Experimental is { Length: > 0 } effect ? $", {effect}" : string.Empty),
                LoadoutTone.Engineered));
        }

        // And whether that is the roll the plan asked for (GitHub issue 42). **The case a plan exists to
        // catch, and the one nothing said out loud.** Both facts were already on this page — the plan in its
        // own block, the roll in this one — and a Commander had to notice that two blueprint names differed.
        if (RolledInstead(build.For(slot), module) is { Length: > 0 } instead)
        {
            lines.Add(new LoadoutLine(
                $"Your plan asks for {build.For(slot)!.Blueprint}, and this is rolled {instead}.",
                LoadoutTone.Danger));
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

        // The grade, steppable in place (remediation.md 15, item 4).
        var offered = BlueprintCatalogue.GradesFor(plan.Blueprint, plan.Module);

        var step = offered.Count > 1
            ? new LoadoutStep(plan.Grade, offered, grade =>
                ships.Plan(build.Id, plan with { Grade = grade }))
            : null;

        var lines = new List<LoadoutLine>
        {
            // Without the grade where a stepper is going to carry it, and with it where there is no stepper
            // (remediation.md 17, item 11).
            new(plan.Describe(withGrade: step is null), LoadoutTone.Body) { Step = step },
        };

        // What the engineering does, where something has been chosen.
        var rolled = BlueprintCatalogue.Named(plan.Blueprint, plan.Module)
            .Where(recipe => recipe.Kind == BlueprintKind.Modification)
            .Where(recipe => recipe.Grade == plan.Grade || recipe.Grade is null)
            .FirstOrDefault(recipe => recipe.Effects.Count > 0)?.Describe();

        // **And what the experimental does, which was the half nobody drew** (asked for 2026-08-20).
        // `Blueprints.tsv` has carried an `effects` column for its 154 experimental rows all along — "Hull
        // Boost -3%, at the cost of Kinetic Resistance +8%" is derived by the same `Describe` from the same
        // three fields — and the block above only ever asked about the blueprint.
        var special = BlueprintCatalogue.Named(plan.Experimental, plan.Module)
            .Where(recipe => recipe.Kind == BlueprintKind.Experimental)
            .FirstOrDefault(recipe => recipe.Effects.Count > 0)?.Describe();

        if (rolled is { Length: > 0 } || special is { Length: > 0 })
        {
            lines.Add(new LoadoutLine("Effect", LoadoutTone.Heading));
        }

        if (rolled is { Length: > 0 } does)
        {
            lines.Add(new LoadoutLine(does, LoadoutTone.Engineered));
        }

        // Named, and only where the blueprint is drawn beside it: two sentences under one heading are two
        // claims about the same slot, and which one is the experimental's is the whole thing the Commander is
        // trying to read.
        if (special is { Length: > 0 } alsoDoes)
        {
            lines.Add(new LoadoutLine(
                rolled is { Length: > 0 } ? $"{plan.Experimental}: {alsoDoes}" : alsoDoes,
                LoadoutTone.Engineered));
        }

        if (build.Scope is not { } scope)
        {
            return lines;
        }

        lines.Add(Verdict(build, plan, scope));
        lines.AddRange(Cost(build, plan, scope));

        return lines;
    }

    /// <summary>
    /// What the Commander wants in this slot: the module, which one of it, the blueprint, the grade,
    /// and the experimental effect (remediation.md 12, item 5; 13, items 8, 9 and 10).
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
            // A hull the table has no layout for.
            Spell(build, slot, prompts, done);
            return;
        }

        var plan = build.For(slot);

        AskModule(build, known, plan, prompts, (module, variant) =>
            AskBlueprint(build, known, plan, module, variant, prompts, (blueprint, grade, experimental) =>
            {
                var wanted = new SlotPlan(slot, blueprint, grade, plan?.Engineer)
                {
                    Module = module,
                    Variant = variant,
                    Experimental = experimental,
                };

                // **An empty plan is not a plan** and must not be stored.
                if (!wanted.IsEmpty)
                {
                    ships.Plan(build.Id, wanted);
                }

                done();
            }));
    }

    /// <summary>
    /// Whether the ship has room for another of this module's limited group (asked for 2026-08-20:
    /// "remove the option to add a module that is already present and only allows 1 of that type").
    /// </summary>
    private bool Room(ShipBuild build, ShipSlot slot, ModuleSpecification module)
    {
        if (module.Limit is not { Length: > 0 } group
            || EliteSpecifications.MostOf(group) is not { } most)
        {
            return true;
        }

        var held = 0;

        foreach (var other in EliteSpecifications.Slots(build.Hull))
        {
            if (string.Equals(other.Name, slot.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // The plan where the Commander has one for that slot, and what is in it otherwise: a plan is what
            // the slot is going to hold, and it outranks what is there now.
            var symbol = build.For(other.Name)?.Variant
                         ?? (build.For(other.Name) is null ? FittedIn(build, other)?.Item : null);

            if (EliteSpecifications.Module(symbol)?.Limit is { } occupied
                && string.Equals(occupied, group, StringComparison.OrdinalIgnoreCase))
            {
                held++;
            }
        }

        return held < most;
    }

    /// <summary>
    /// What Frontier offers this module that d47 holds no recipe for, by name where the symbol resolves
    /// to one and empty where nothing is offered at all.
    /// </summary>
    private static IReadOnlyList<string> Unrecipied(string? variant)
    {
        if (EliteSpecifications.Module(variant) is not { } specification
            || BlueprintCatalogue.OfferedTo(specification.Type) is not { Count: > 0 } offered)
        {
            return [];
        }

        return
        [
            .. offered
                .Select(BlueprintCatalogue.NameOf)
                .Where(name => name is { Length: > 0 })
                .Select(name => name!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>The modules this slot takes, by name — then which one of it.</summary>
    private void AskModule(
        ShipBuild build,
        ShipSlot slot,
        SlotPlan? plan,
        PanelPrompts prompts,
        Action<string?, string?> chosen)
    {
        // By name rather than one row per class and rating.
        var offered = EliteSpecifications.ModulesFor(slot)
            .Where(module => Room(build, slot, module))
            .GroupBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (offered.Count == 0)
        {
            chosen(plan?.Module, plan?.Variant);
            return;
        }

        if (offered.Count == 1)
        {
            // One name, so there is nothing to choose.
            var only = offered[0];

            AskVariant(
                build,
                slot,
                plan,
                Spelling(only),
                only,
                prompts,
                variant => chosen(Spelling(only), variant));

            return;
        }

        prompts.Choose(
            new ChoiceRequest(
                "loadout.module",
                "Module",
                $"What goes in {slot.Describe()}?",
                Context(build, slot),
                [
                    // Only where there is something to engineer (remediation.md 15, item 15).
                    .. Keeping(build, slot, plan),
                    // Badged where every variant of the name is gated, which is how the nineteen fall: a
                    // Prismatic Shield Generator is its own name in all eight sizes, and an ordinary Shield
                    // Generator is a different name entirely.
                    .. offered.Select(group => new ChoiceOption(
                        Spelling(group),
                        group.All(module => module.NeedsPledge)
                            ? $"{Coin} {Spelling(group)}"
                            : Spelling(group),
                        Detail(
                            Detail(Sizes(group), About(group)),
                            (group.All(module => module.NeedsPledge)
                             ? Gate(group.First())
                             : null) ?? string.Empty))),
                ],
                plan?.Module,
                ChoiceSurface.Page)
            {
                CurrentWord = "planned now",
                Searchable = true,

                // Its own page, asked for 2026-08-23 ("there's no help for this page").
                Help = ModuleChoiceHelp,
            },
            option =>
            {
                if (option.Key.Length == 0)
                {
                    chosen(null, null);
                    return;
                }

                // Straight past the variant question: the plan already answers it, and asking a Commander who
                // said "keep this module" which one they meant is asking twice.
                if (option.Key == KeepPlanned)
                {
                    chosen(plan?.Module, plan?.Variant);
                    return;
                }

                AskVariant(
                    build,
                    slot,
                    plan,
                    option.Key,
                    offered.First(group => group.Key.Equals(option.Key, StringComparison.OrdinalIgnoreCase)),
                    prompts,
                    variant => chosen(option.Key, variant));
            });
    }

    /// <summary>Which one of it: the size and the mount (remediation.md 13, item 8).</summary>
    private void AskVariant(
        ShipBuild build,
        ShipSlot slot,
        SlotPlan? plan,
        string module,
        IEnumerable<ModuleSpecification> variants,
        PanelPrompts prompts,
        Action<string?> chosen)
    {
        var offered = variants
            .OrderByDescending(variant => variant.Class)
            .ThenBy(variant => variant.Mount, StringComparer.Ordinal)
            .ThenBy(variant => variant.Rating, StringComparer.Ordinal)
            .ToList();

        if (offered.Count <= 1)
        {
            // One of it, so there is nothing to choose.
            chosen(offered.Count == 1 ? offered[0].Symbol : null);
            return;
        }

        // **The one already in the slot, named and first** (reported 2026-08-20: *"no option to keep the Life
        // Support I already have"*).
        var here = FittedIn(build, slot)?.Item;

        var kept = here is { Length: > 0 }
            ? offered.FirstOrDefault(variant =>
                string.Equals(variant.Symbol, here, StringComparison.OrdinalIgnoreCase))
            : null;

        var rest = kept is null
            ? offered
            : [.. offered.Where(variant => !string.Equals(variant.Symbol, kept.Symbol, StringComparison.OrdinalIgnoreCase))];

        List<ChoiceOption> rows =
        [
            // **What the Commander means, not a claim about the game.** This row read "size and mount do not
            // matter", and that is false about every module in Elite: an FSD's size is most of a jump range,
            // and a weapon's mount is the whole of how it tracks.
            new ChoiceOption(string.Empty, $"Any {module} — I do not mind which"),
        ];

        if (kept is not null)
        {
            rows.Add(new ChoiceOption(
                kept.Symbol,
                $"Keep the {EliteSpecifications.ModuleName(kept.Symbol) ?? Wording(kept)} — the one fitted now",
                Figures(kept)));
        }

        // The coin again, per variant this time — the level a Commander is actually choosing at.
        rows.AddRange(rest.Select(variant => new ChoiceOption(
            variant.Symbol,
            Badged(Wording(variant), variant),
            Detail(Figures(variant), Gate(variant) ?? string.Empty))));

        prompts.Choose(
            new ChoiceRequest(
                "loadout.variant",
                "Which one",
                $"Which {module}?",
                Context(build, slot),
                rows,
                plan?.Variant,
                ChoiceSurface.Page)
            {
                CurrentWord = "planned now",
                Searchable = true,
            },
            option => chosen(option.Key.Length == 0 ? null : option.Key));
    }

    /// <summary>The blueprint, then the grade, then the effect.</summary>
    private void AskBlueprint(
        ShipBuild build,
        ShipSlot slot,
        SlotPlan? plan,
        string? module,
        string? variant,
        PanelPrompts prompts,
        Action<string?, int, string?> chosen)
    {
        // What the blueprints are listed for: the module just chosen, or the one already planned, or — where
        // the Commander said to keep what is in the slot — the module that is in it. **That last one is
        // remediation.md 17, item 6's whole fix.** "Keep the 5D Hull Reinforcement Package — I only want the
        // engineering" answers the module question with null, deliberately: the plan must not name a module,
        // or keeping becomes wanting.
        var fitted = FittedIn(build, slot);

        var wanted = module
            ?? plan?.Module
            ?? (fitted is not null ? EliteSpecifications.ModuleName(fitted.Item) : null);

        // The symbol answers exactly — size, rating and mount — where the name answers for every size of the
        // thing.
        var offer = Offered(wanted, variant ?? plan?.Variant ?? fitted?.Item);

        // Three states, where there used to be two (remediation.md 15, item 6).
        if (offer is { Count: 0 })
        {
            chosen(null, 0, null);
            return;
        }

        // Everything, for a slot with no module decided yet or a module whose type d47 does not know.
        var recipes = offer is { Count: > 0 }
            ? offer.Where(recipe => recipe.Kind == BlueprintKind.Modification).ToList()
            : [.. BlueprintCatalogue.All.Where(recipe => recipe.Kind == BlueprintKind.Modification)];

        var names = recipes
            .GroupBy(recipe => recipe.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        prompts.Choose(
            new ChoiceRequest(
                "loadout.blueprint",
                "Blueprint",
                wanted is { Length: > 0 } named ? $"What are we crafting on the {named}?" : "What are we crafting?",
                Context(build, slot),
                [
                    new ChoiceOption(string.Empty, "No engineering — the module as it comes"),
                    .. names.Select(group => new ChoiceOption(
                        group.Key,
                        group.Key,
                        Detail(Grades(group), Does(group)))),
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

                if (blueprint is null)
                {
                    // No roll means no grade and no effect: both are properties of a roll, and asking about
                    // them anyway is two questions with one answer.
                    chosen(null, 0, null);
                    return;
                }

                var offered = names.First(group => group.Key == blueprint)
                    .Select(recipe => recipe.Grade)
                    .Where(grade => grade is not null)
                    .Select(grade => grade!.Value)
                    .Distinct()
                    .OrderDescending()
                    .ToList();

                // Straight past the grade, which is decided rather than asked.
                var grade = Grade(plan, offered);

                // The same offer the blueprint list was built from, rather than the module's name again.
                AskEffect(build, slot, plan, wanted, offer, prompts, effect =>
                    chosen(blueprint, grade, effect));
            });
    }

    /// <summary>
    /// The grade, from the ones that blueprint has, best first and grade 5 the default (remediation.md
    /// 13, item 9).
    /// </summary>
    private static int Grade(SlotPlan? plan, IReadOnlyList<int> offered)
    {
        if (offered.Count == 0)
        {
            return 0;
        }

        // What was planned, where it is still on offer.
        return plan is { Grade: > 0 } wanted && offered.Contains(wanted.Grade)
            ? wanted.Grade
            : offered[0];
    }

    /// <summary>The experimental effect, on the modules that support one (remediation.md 13, item 10).</summary>
    /// <param name="offer">
    /// What this module can take, already resolved by <see cref="AskBlueprint"/>.
    /// </param>
    private void AskEffect(
        ShipBuild build,
        ShipSlot slot,
        SlotPlan? plan,
        string? module,
        IReadOnlyList<Blueprint>? offer,
        PanelPrompts prompts,
        Action<string?> chosen)
    {
        // Grouped rather than projected to a bare name (reported 2026-08-20: *"details on experimental effect
        // should be somewhere"*).
        var effects = (offer ?? BlueprintCatalogue.ForModule(module))
            .Where(recipe => recipe.Kind == BlueprintKind.Experimental)
            .GroupBy(recipe => recipe.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (effects.Count == 0)
        {
            chosen(plan?.Experimental);
            return;
        }

        // Deliberately no "one option, take it" here, though remediation 15 item 7 groups this step with the
        // others.

        prompts.Choose(
            new ChoiceRequest(
                "loadout.experimental",
                "Effect",
                module is { Length: > 0 } named
                    ? $"An experimental effect on the {named}?"
                    : "An experimental effect?",
                Context(build, slot),
                [
                    new ChoiceOption(string.Empty, "No effect"),
                    .. effects.Select(group => new ChoiceOption(group.Key, group.Key, Does(group))),
                ],
                plan?.Experimental,
                ChoiceSurface.Page)
            {
                CurrentWord = "planned now",
                Searchable = true,
            },
            option => chosen(option.Key.Length == 0 ? null : option.Key));
    }

    /// <summary>One variant, in the words a Commander uses for it.</summary>
    private static string Wording(ModuleSpecification variant)
    {
        var size = variant.Class switch
        {
            1 => "Small",
            2 => "Medium",
            3 => "Large",
            4 => "Huge",
            _ => null,
        };

        if (variant.Mount is { Length: > 0 } mount && size is not null)
        {
            return $"{size}, {mount}";
        }

        return variant.Class is { } cls && variant.Rating is { Length: > 0 } rating
            ? $"Size {cls.ToString(CultureInfo.InvariantCulture)}, rating {rating}"
            : variant.Name;
    }

    /// <summary>The coin that marks a module a Powerplay pledge is needed to buy (Phase 38).</summary>
    internal const string Coin = "¤";

    /// <summary>The label for a module the Commander may not be able to buy, and the sentence under it.</summary>
    private string? Gate(ModuleSpecification? module)
    {
        if (module?.NeedsPledge is not true)
        {
            return null;
        }

        return state()?.Pledge.IsPledged == true
            ? "Needs a Powerplay pledge — one Power's, and I cannot tell you which."
            : "Needs a Powerplay pledge, and you have none.";
    }

    /// <summary>The coin in front of a chooser row's label, for a module behind a pledge.</summary>
    private static string Badged(string label, ModuleSpecification? module) =>
        module?.NeedsPledge is true ? $"{Coin} {label}" : label;

    /// <summary>The code underneath the words, and what it costs the ship to carry.</summary>
    private static string Figures(ModuleSpecification variant)
    {
        var parts = new List<string> { variant.Size };

        if (variant.Mass is { } mass)
        {
            parts.Add($"{mass.ToString("N1", CultureInfo.InvariantCulture)} t");
        }

        if (variant.Power is { } power)
        {
            parts.Add($"{power.ToString("N2", CultureInfo.InvariantCulture)} MW");
        }

        if (variant.Cost is { } cost)
        {
            parts.Add(Credits(cost));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The header's second line: which slot, how big, and what is in it.</summary>
    private IReadOnlyList<ChoiceOption> Keeping(ShipBuild build, ShipSlot slot, SlotPlan? plan)
    {
        // **The plan first, where it names a module** (reported 2026-08-20).
        if (plan?.Module is { Length: > 0 } planned)
        {
            var carried = Planned(plan) ?? planned;

            return Offered(planned, plan.Variant) is { Count: 0 }
                ? [new ChoiceOption(
                    KeepPlanned,
                    $"Keep the {carried} — I have no engineering for this module. Click to close.")]
                : [new ChoiceOption(KeepPlanned, $"Keep the {carried} — I only want the engineering")];
        }

        if (FittedIn(build, slot) is not { } module)
        {
            return [];
        }

        var named = EliteSpecifications.ModuleName(module.Item);
        var what = named is { Length: > 0 } ? $"the {named}" : "what is fitted";

        // **Whether there is any engineering to want** (asked for 2026-08-20, reported against an Auto
        // Field-Maintenance Unit).
        if (Offered(named, module.Item) is not { Count: 0 })
        {
            return [new ChoiceOption(string.Empty, $"Keep {what} — I only want the engineering")];
        }

        // **Three states where there were two** (reported 2026-08-20 against a Guardian Gauss Cannon: *"it
        // does have 1 engineering option — Anti-Guardian Zone Resistance"*, and it does). "No recipes I hold"
        // and "no engineering exists" are different claims, and d47 was making the second while entitled only
        // to the first.
        var withheld = EliteSpecifications.Module(module.Item) is { } specification
            ? BlueprintCatalogue.DisputedFor(specification.Type)
            : [];

        return (Unrecipied(module.Item), withheld) switch
        {
            ([], []) => [new ChoiceOption(
                string.Empty,
                $"Keep {what} — I have no engineering for this module. Click to close.")],

            ([], var disputed) => [new ChoiceOption(
                string.Empty,
                $"Keep {what} — Frontier engineers this ({string.Join(", ", disputed)}), my two "
                + "sources disagree about what it costs, and I will not guess. Click to close.")],

            (var missing, []) => [new ChoiceOption(
                string.Empty,
                $"Keep {what} — Frontier engineers this ({string.Join(", ", missing)}) and I have "
                + "no recipe for it. Click to close.")],

            var (missing, disputed) => [new ChoiceOption(
                string.Empty,
                $"Keep {what} — Frontier engineers this. I have no recipe for "
                + $"{string.Join(", ", missing)}, and my sources disagree about "
                + $"{string.Join(", ", disputed)}. Click to close.")],
        };
    }

    /// <summary>The module fitted in this slot right now, or null where nothing is or nothing can see.</summary>
    private ShipModule? FittedIn(ShipBuild build, ShipSlot slot) =>
        Picture(build)?.Loadout.Modules.FirstOrDefault(candidate =>
            string.Equals(candidate.Slot, slot.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether this slot is known to be empty — as opposed to holding something, or being on a ship
    /// nothing can see into.
    /// </summary>
    private bool KnownEmpty(ShipBuild build, ShipSlot slot) =>
        Picture(build) is not null && FittedIn(build, slot) is null;

    /// <summary>What a module can be engineered with, or null where d47 does not know its type.</summary>
    private static IReadOnlyList<Blueprint>? Offered(string? module, string? variant)
    {
        if (variant is { Length: > 0 } symbol
            && BlueprintCatalogue.For(EliteSpecifications.Module(symbol)) is { } exact)
        {
            return exact;
        }

        if (module is not { Length: > 0 } named)
        {
            return null;
        }

        return EliteSpecifications.ModulesNamed(named, null, null) is [var first, ..]
            ? BlueprintCatalogue.For(first)
            : null;
    }

    /// <summary>The chooser's second line: what is being chosen for, and what is in it now.</summary>
    private string Context(ShipBuild build, ShipSlot slot)
    {
        // A utility mount is size 0, which is not a size a Commander says — and a compartment has already
        // said its own, because `ShipSlot.Describe` reads it out of `Slot04_Size4`.
        var size = slot.Size > 0 && slot.Kind is ShipSlotKind.Core or ShipSlotKind.Hardpoint
            ? $" (size {slot.Size.ToString(CultureInfo.InvariantCulture)})"
            : string.Empty;

        var fitted = FittedIn(build, slot) is { } module
            ? EliteSpecifications.ModuleName(module.Item) ?? module.Item
            : KnownEmpty(build, slot) ? "empty" : null;

        var now = fitted is null
            ? "I cannot see into this ship from here"
            : $"currently {fitted}";

        return $"{build.Describe()} · {slot.Describe()}{size}, {now}. "
               + "It does not reach your checklist until you promote the build.";
    }

    /// <summary>Two facts on a row's second line, with the separator only where both are there.</summary>
    private static string Detail(string first, string second) =>
        first.Length == 0 ? second
        : second.Length == 0 ? first
        : $"{first} · {second}";

    /// <summary>One spelling for a name the id list writes more than one way.</summary>
    private static string Spelling(IEnumerable<ModuleSpecification> variants) =>
        variants.Select(module => module.Name).Order(StringComparer.Ordinal).First();

    /// <summary>The few figures that answer "why is this one better", under a module's name.</summary>
    private static string About(IEnumerable<ModuleSpecification> variants)
    {
        var best = variants.FirstOrDefault(module => module.Figures.Count > 0)
                   ?? variants.FirstOrDefault();

        if (best is null)
        {
            return string.Empty;
        }

        var said = string.Join(
            " · ",
            best.Figures.Take(3).Select(pair => $"{pair.Name} {pair.Value}"));

        return best.About is { Length: > 0 } described
            ? said.Length > 0 ? $"{said}. {described}" : described
            : said;
    }

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

    /// <summary>What a blueprint does in general, from its highest grade (remediation.md 15, item 8).</summary>
    private static string Does(IEnumerable<Blueprint> recipes) =>
        recipes
            .Where(recipe => recipe.Effects.Count > 0)
            .OrderByDescending(recipe => recipe.Grade ?? 0)
            .FirstOrDefault()
            ?.Describe() ?? string.Empty;

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
    /// The way in for a hull the table has no layout for: a blueprint said, and a grade typed, as it
    /// was for every slot before there was anything to list.
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
                        : EntryVerdict.No("A grade is 1 to 5.")),
                grade =>
                {
                    ships.Plan(build.Id, new SlotPlan(
                        slot,
                        string.IsNullOrWhiteSpace(blueprint) ? null : blueprint.Trim(),
                        int.TryParse(grade.Trim(), out var level) ? level : 0));

                    done();
                }));

    /// <summary>The journal's verdict, as of when it was taken.</summary>
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

        var said = verdict?.Says
                   ?? "Nothing can be said about this right now — Elite reports the loadout of the "
                      + "ship you are sitting in and no other.";

        return new LoadoutLine(
            said,
            verdict is { } answered && ChecklistNextAction.IsWrong(answered.State)
                ? LoadoutTone.Danger
                : LoadoutTone.Muted);
    }

    /// <summary>What this plan costs, on the slot.</summary>
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
            // Held, needed and short, all three. "Short 12" alone is a number a Commander cannot check, and
            // the arithmetic is exact rather than estimated.
            lines.Add(new LoadoutLine(
                $"{ingredient.Material.Name}: {ingredient.Held} of {ingredient.Needed}"
                + (ingredient.Short > 0 ? $", {ingredient.Short} short" : string.Empty)
                + (ingredient.ExceedsCapacity ? " — more than one trip" : string.Empty)));
        }

        return lines;
    }

    /// <summary>
    /// The picture of this ship to read, live where the Commander is in it and remembered where they
    /// are not (asked for 2026-08-20: "Don't get amnesia").
    /// </summary>
    private sealed record Seen(ShipLoadout Loadout, DateTimeOffset? SeenAt)
    {
        public bool IsLive => SeenAt is null;
    }

    private Seen? Picture(ShipBuild build)
    {
        var active = state();

        if (active?.Ship is { IsKnown: true } loadout && loadout.ShipId == build.ShipId)
        {
            return new Seen(loadout, null);
        }

        return active?.Loadouts.For(build.ShipId) is { } remembered
            ? new Seen(remembered.Loadout, remembered.SeenAt)
            : null;
    }

    /// <summary>
    /// What a planned module is called, with its class and rating (reported 2026-08-20: "Size five is
    /// missing the 5D designation").
    /// </summary>
    private static string? Planned(SlotPlan? plan) =>
        plan?.Variant is { Length: > 0 } variant
            ? EliteSpecifications.ModuleName(variant) ?? plan.Module
            : plan?.Module;

    /// <summary>How long ago a remembered picture was taken, in the Commander's words.</summary>
    private static string Age(DateTimeOffset seenAt) =>
        $"{D47.Core.Persona.TelemetryDelta.Spoken(DateTimeOffset.Now - seenAt)} ago";

    private IReadOnlyList<ShipModule> Modules(ShipBuild build) => Picture(build)?.Loadout.Modules ?? [];

    private static string Key(FleetEntry entry) =>
        entry.Build?.Id
        ?? (entry.Stored is { } stored
            ? Unplanned + stored.ShipId.ToString(CultureInfo.InvariantCulture)
            : entry.Hull);

    /// <summary>The build a crumb key means, started if the ship has none yet.</summary>
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

        // A key that is a hull.
        var byHull = ships.Fleet()
            .Where(entry => string.Equals(entry.Hull, key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return byHull is [{ Stored: { } only }]
            ? ships.BuildFor(only.ShipId, only.Type, only.Name)
            : byHull.Count == 1 ? byHull[0].Build : null;
    }

    /// <summary>
    /// A fitted module, named the way the outfitting screen names it (remediation.md 12, item 4).
    /// </summary>
    private static string? Describe(ShipModule? module) =>
        module is null ? null : EliteSpecifications.ModuleName(module.Item);
}
