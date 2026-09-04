using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Ships;

namespace D47.App.Panel;

/// <summary>
/// The Loadout tab's Ships mode: the fleet, a ship, a slot (Phase 26, "Ships").
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
    Func<CommanderGameState?> state,
    Func<ModulePower>? measured = null,
    ShipsDrawingsMemory? drawings = null) : ILoadoutMode
{
    /// <summary>A row for a ship the journal reports and nothing has planned for yet.</summary>
    private const string Unplanned = "new:";

    /// <summary>
    /// The chooser key for "keep the module this plan already names". Its own sentinel rather
    /// than the empty string the fitted row uses, because the two answer differently: empty
    /// plans no module at all, and this one carries the planned module through untouched. No
    /// module name can collide with it — a colon is not in one.
    /// </summary>
    private const string KeepPlanned = "keep:planned";

    public string RootKey => LoadoutPages.FleetRoot;

    public string RootWord => "Ships";

    public string ItemPrefix => LoadoutPages.ShipPrefix;

    public string SlotPrefix => LoadoutPages.SlotPrefix;

    public string? SlotHelp => D47.Core.Capabilities.Builtin.EngineeringCapability.Id;

    /// <summary>
    /// The page behind the module picker's question mark — its own, rather than the slot's.
    /// <para>
    /// A slot's page is about engineering a module; this one is the list of every module that
    /// fits, with grades, damage types, a Powerplay badge and a "keep what is fitted" line at the
    /// top of it. Different question, different page.
    /// </para>
    /// </summary>
    public const string ModuleChoiceHelp =
        D47.Core.Help.HelpLibrary.GeneralPrefix + "choosing-a-module";

    /// <summary>
    /// Two things move a ship page, and only one of them was wired (remediation.md 17, item 7).
    /// <para>
    /// The plans file changing is the obvious one and was the only one: <c>ShipBuildStore.Changed</c>
    /// is raised when a build is saved or reloaded. But half of what these pages show is the
    /// <em>journal's</em> — what is fitted, which slots are known, whether the Commander is flying
    /// this ship at all — and no journal event raised anything. So a page open across a ship swap
    /// kept its first answer for the rest of the session, and the report was a fleet whose modules
    /// read *not seen* while the Commander sat in the ship.
    /// </para>
    /// <para>
    /// <see cref="Invalidate"/> is the second source, driven by <c>PanelView.TickLoadout</c> — the
    /// same shape as the Engineers tab, which had this from the day it shipped.
    /// </para>
    /// </summary>
    public event Action? Changed
    {
        add
        {
            ships.Store.Changed += value;

            // And a third, added by Phase 38: the tab now carries a question waiting on
            // the Commander, so a question arriving or being answered moves these pages. Without
            // it the banner is drawn on whichever pane was redrawn last and left standing on the
            // other — a Commander who answers on the ship page finds the fleet page still asking.
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

    /// <summary>The journal moved under the page. Raised by the host's tick, never from in here.</summary>
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

    /// <summary>
    /// The fleet, in one alphabet (asked for 2026-09-03).
    /// <para>
    /// <b>Flat and alphabetical, rather than active-then-owned-then-wanted.</b> The old order was
    /// three groups deep and unlabelled, so it read as arbitrary unless you already knew the rule —
    /// and the one thing a Commander does on this page is find a named ship they are thinking of.
    /// An alphabet is the only order you can search without reading. Whether a ship is owned, and
    /// whether you are sitting in it, is carried by <see cref="LoadoutStanding"/> and drawn on the
    /// card, which was the trade asked for in as many words: <i>"rather than groups beginning on a
    /// new row, some sort of visual indicator is the better choice"</i>.
    /// </para>
    /// </summary>
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

                    // Where it is, and how its plans stand. The two questions the fleet page exists
                    // to answer before anything is drilled.
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

    /// <summary>
    /// The drawings switch, for a fleet that has somewhere to remember it.
    /// <para>
    /// Null without the memory, which is what a test constructing this mode by hand gets — and it
    /// means the switch is simply absent rather than present and forgetful.
    /// </para>
    /// </summary>
    public LoadoutToggle? IndexToggle =>
        drawings is null
            ? null
            : new LoadoutToggle("Drawings", drawings.Drawings, drawings.Remember);

    /// <summary>
    /// A hull to plan a build for before it is bought. Every hull is offered and the Commander
    /// picks one; voice still answers it, because a hull is a name and a name is far easier said
    /// than hunted for one key at a time (#282).
    /// <para>
    /// <b>The second line does not claim anything about the fleet.</b> It used to read "it is not
    /// in your fleet until you own one", which is false the moment the hull picked is one the
    /// Commander already flies — owning a Python and planning a second one is an ordinary thing
    /// to do, and the page was telling them otherwise. What is true either way is what buying one
    /// does to the plan, which is what the summary of an unowned build already says.
    /// </para>
    /// </summary>
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
    /// Every hull the specification table can measure, alphabetically — the list the picker
    /// offers and, by construction, exactly the set the validation accepts.
    /// </summary>
    private static IReadOnlyList<string> Hulls { get; } =
    [
        .. EliteSpecifications.Ships
            .Select(ship => ship.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

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
    /// <summary>
    /// Which hull this ship is, for the picture on its page
    /// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>). Through the same
    /// <see cref="Resolve"/> the details go through, so a ship the journal reported and nothing
    /// has planned for has a hull here too.
    /// </summary>
    public string? HullOf(string item) => Resolve(item)?.Hull;

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
            lines.Add(new LoadoutLine(Whereabouts(stored, entry.IsActive), LoadoutTone.Body)
            {
                // The system, on the clipboard, for Elite's Galaxy Map search. The sentence names
                // the station too, and the station is not what the Galaxy Map takes.
                Copy = stored.HasSystem
                    ? new LoadoutCopy(
                        stored.StarSystem,
                        $"Copy “{stored.StarSystem}” — paste it into the Galaxy Map")
                    : null,
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
            // The system too, so the copy glyph beside this line has something to be about — the
            // ship under the Commander is as good a reason to want the system name on the
            // clipboard as any other, and often a better one.
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

    /// <summary>
    /// The figures that depend on what is fitted, and <b>only for the ship being flown</b>. Elite
    /// reports the loadout of one ship at a time, so these are measured for that one and unknown
    /// for every other — which is a different thing from being zero.
    /// </summary>
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

        // Dated where it is remembered, and silent where it is live. These are figures a
        // Commander will act on, and a re-outfit d47 never watched would change every one of them.
        if (seen.SeenAt is { } when)
        {
            lines.Add(new LoadoutLine($"As you left it, {Age(when)}.", LoadoutTone.Muted));

            // **Said where the doubt is, not only where the fix is**
            // (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>). A Commander
            // looking at a figure that does not match the ship in front of them has no way to know
            // that anything can be done about it — and this is the one line on the page that
            // already admits the figures may be out of date, so the offer belongs beside it.
            //
            // Only on a remembered ship: the one being flown reports no age at all, because "now"
            // is the honest tense there and a rescan would answer a question nobody has.
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
    /// The question left on the tab when one was asked out loud and not answered
    /// (Phase 38, "Ask before the plan and the checklist drift apart").
    /// <para>
    /// <b>Read off the proposal itself rather than kept beside it.</b> The question <em>is</em> a
    /// checklist proposal — the same accept-or-decline boundary the promote button uses — so the
    /// banner cannot come to disagree with what is actually waiting, and answering it here and
    /// answering it out loud are the same act.
    /// </para>
    /// <para>
    /// <b>Narrowed to a ship's own list.</b> An engineer-unlock chain proposes plan items from the
    /// same source and scopes them to the universal list, and those are the Engineers tab's
    /// business rather than this one's — a question about a Krait belongs on the tab showing the
    /// Krait.
    /// </para>
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

    /// <summary>
    /// Power and jump range, live while the build is edited (Phase 38).
    /// <para>
    /// <b>The two numbers a build is designed against</b>, and neither was visible until after the
    /// credits were spent: a Commander answered both by alt-tabbing to Coriolis. Both are
    /// panel-side and neither spends a byte of the tool surface — a gauge is not a tool.
    /// </para>
    /// <para>
    /// <b>Silent rather than approximate for a ship d47 has never been inside.</b> Elite reports
    /// the loadout of the ship the Commander is sitting in and no other, so a hull that has never
    /// been boarded has nothing to total and says so where the gauges would be.
    /// </para>
    /// </summary>
    public IReadOnlyList<LoadoutGauge> Gauges(string item)
    {
        if (Resolve(item) is not { } build)
        {
            return [];
        }

        var seen = Picture(build);

        // Only the ship being flown, and only where the file describes *this* one. A Commander who
        // swaps ships without re-outfitting leaves the previous ship's figures on disk, and those
        // would put one hull's draw under another hull's plant.
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

    /// <summary>
    /// The power bar: filled to the deployed draw, marked where the retracted draw sits.
    /// <para>
    /// <b>Deployed is the fill</b> because it is the figure a build has to fit inside — a build
    /// that fits until the guns come out does not fit. The retracted draw is the mark, so the
    /// distance between them is the cost of the hardpoints, read at a glance.
    /// </para>
    /// <para>
    /// <b>A percentage and, when it is over, the megawatts</b> (the Commander's call, 2026-08-20):
    /// the percentage says whether there is a problem and the overage says how big a plant fixes
    /// it.
    /// </para>
    /// </summary>
    private static LoadoutGauge Gauge(PowerGauge power)
    {
        var modelled = power.Kind == FigureKind.Modelled;

        if (power.Capacity is not { } made || made <= 0)
        {
            // A build with no plant d47 can see. The draw is still a real total and is worth
            // saying; what it cannot be is a share of anything.
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

            // **Three figures under the points they belong to** (the Commander's instruction,
            // 2026-09-01). What the tick means was unguessable from a legend at the far left of
            // the line beneath — so the retracted share is written under the tick, the plant's own
            // limit under the end of the track, and the total draw under wherever it lands.
            //
            // **The draw is clamped for its position and not for its words.** A build 18% over its
            // plant has nowhere further along the bar to be written, and the figure is the whole
            // point of the gauge, so it sits at the end and says 118%.
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

    /// <summary>
    /// The jump bar, worst to best, with the game's own figure at the far end.
    /// <para>
    /// <b>The bar is filled to the laden range and marked at the other two</b>, because the filled
    /// part is what the Commander gets whatever they are carrying and the marks are what they get
    /// as they burn fuel and sell cargo.
    /// </para>
    /// </summary>
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

            // Counted rather than swallowed: a plan naming a kind of module and not a size has no
            // mass to add, so those slots weigh what is fitted and the gauge says how many did.
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

                    // The plan where there is one, what is fitted where there is not, and the
                    // word for neither — because a row with a blank note reads as a row d47 has
                    // nothing to say about rather than as an empty slot.
                    plan is not null ? plan.Describe() : Describe(module) ?? Vacant(build, fitted),
                    Outstanding(plan, module))
                {
                    Group = ShipSlot.Heading(slot.Kind),

                    // Read off the fitted module rather than off the plan, because they answer
                    // different questions (remediation.md 15, item 10): the dot beside this says
                    // there is work left to do, and the gear says a roll has already been done.
                    Engineered = module?.Blueprint is { Length: > 0 },

                    Parts = Parted(slot, plan, module, build, fitted),
                };
            }),
        ];
    }

    /// <summary>
    /// One slot row broken into the two sides it is drawn from — what is in the slot, and what the
    /// plan asks for (docs/plans/change-requests.md 38; the row itself asked for 2026-08-20).
    /// <para>
    /// <b>Current is the journal and Plan is <c>ships.json</c>, and neither ever borrows from the
    /// other.</b> This method used to blend them and the blend was the defect: it took the plan's
    /// module where there was one and the fitted module otherwise, and the plan's blueprint where
    /// there was one and the rolled blueprint otherwise, so a row could name a module that is not
    /// there wearing a roll that has not been done. Both halves were reported on 2026-08-24 —
    /// <i>"something IS fitted on oxen utility mount 8"</i> against an empty mount, and a power
    /// distributor planned Weapon Focused and rolled Priority Systems reading as finished.
    /// </para>
    /// <para>
    /// <b>The argument that produced the blend was sound and is now unnecessary.</b> It ran: the
    /// row already carries the plan's roll and the plan's mark, so naming the fitted module beside
    /// them describes a thing that does not exist — which is true, and was equally true the other
    /// way round. Both readings are right and neither survives one column. Two columns is the
    /// answer that was unavailable while the row was an index, and the three flags that were
    /// carrying the difference — the standing module, the empty slot and the roll that disagrees —
    /// are not needed any more, because each of them is now just what one of the two sides says.
    /// </para>
    /// <para>
    /// <b>The effects are the fitted module's own</b> and are never a plan's. Elite reports what a
    /// roll actually landed — 3,384 modifiers across the corpus, each with the figure before it —
    /// and a planned roll has no figures at all. Showing modelled ones beside measured ones is the
    /// failure the specification table is built the way it is to avoid.
    /// </para>
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
            // No size on a utility mount: they are 0 by definition, so a 0 on all eight of them is
            // noise in the first column the eye lands on.
            slot.Kind == ShipSlotKind.Utility ? null : slot.Size,
            slot.Brief,
            Fitting(
                slot.Kind,
                slot.Brief,
                Side(
                // The full name — "3E Pulse Laser, gimballed" — beside the slot's own size, and
                // the two are not the same fact: a size 3 slot can hold a class 2 module, and
                // seeing a 2 beside a 3 is how a Commander spots one. The rating and the mount are
                // in it for the same reason, and the mount is the half that decides how a weapon
                // behaves.
                module is not null
                    ? EliteSpecifications.ModuleName(module.Item) ?? module.Item
                    : null,
                Bare(module?.Item),

                // The Commander's words either way. A plan stores the readable name they picked
                // and the journal stores a symbol, so taking whichever is there put two spellings
                // of one blueprint on one page — "Heavy Duty Hull Reinforcement" on the planned
                // slots and "HullReinforcement_HeavyDuty" on the fitted-but-unplanned one (GitHub
                // issue 39). BlueprintCatalogue.NameOf is the join and nothing here was reading it.
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
            // The plan is carried out, so the second column has nothing left to say. Read off the
            // same function as the marker rather than worked out again here: two mechanisms
            // answering "is there work left" differently is how the marker came to mean something
            // else in the first place.
            Met = planned is not null && !Outstanding(planned, module),
        };
    }

    /// <summary>
    /// The two words a column does not need to carry, because the slot beside it already does
    /// (docs/plans/change-requests.md 38).
    /// <para>
    /// <b>A core internal is named by its slot.</b> The Power Distributor sits in the slot called
    /// Power Distributor, so a cell reading <i>7A Power Dist.</i> beside a column reading <i>Power
    /// Dist.</i> says one thing twice and spends the width that the roll needs. What is left —
    /// <b>7A</b> — is the whole of what the row does not already say.
    /// </para>
    /// <para>
    /// <b>And a utility mount is size 0 by definition</b>, so <i>0C Shield Booster</i> leads with a
    /// digit that is true of all eight of them. The rating is the half that differs, which is the
    /// same argument that keeps the size column off a utility row.
    /// </para>
    /// <para>
    /// The long form is untouched either way: it is what the tooltip and the drill say, and this
    /// only ever shortens what is drawn.
    /// </para>
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

        // Nothing left means the slot's name was the whole of the module's, and a blank cell says
        // less than a repeated word does.
        return said.Length == 0 || said.Length == module.Length ? side : side with { Module = said };
    }

    /// <summary>
    /// One side of a slot row, short-named and with the module struck off its blueprint.
    /// <para>
    /// <b>The short name and the long one are both kept</b>, because a short name is never the
    /// only name (the Commander's ruling, 2026-08-25): the row draws the short form and hangs the
    /// long one on the tooltip, and the slot drill below is untouched and still says everything in
    /// full. Where they are the same string the long one is null, so nothing hangs a tooltip
    /// repeating the word underneath it.
    /// </para>
    /// </summary>
    /// <param name="bare">
    /// The module in Frontier's words with the size, the rating and the mount off it, which is
    /// what a blueprint's tail has to be compared against — <c>6D Hull Reinforcement Package</c>
    /// never ends a blueprint name and <c>Hull Reinforcement Package</c> does.
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
            // Per side, because planning one you have not got is exactly when the gate is
            // worth knowing about.
            Gated = EliteSpecifications.Module(symbol)?.NeedsPledge ?? false,
        };
    }

    /// <summary>
    /// A module's name with the size, the rating and the mount off it, for the blueprint join.
    /// <see cref="EliteSpecifications.ModuleName"/> composes those on and this is the part
    /// underneath, so a symbol the table has never heard of has no bare name rather than a
    /// guessed one.
    /// </summary>
    private static string? Bare(string? symbol) => EliteSpecifications.Module(symbol)?.Name;

    /// <summary>
    /// Whether this slot still has work in it (GitHub issue 38).
    /// <para>
    /// <b>The marker used to mean "a plan exists", and that is why it never cleared.</b> Reported
    /// against <c>Military01</c> and <c>Military02</c>, rolled <c>HullReinforcement_HeavyDuty</c> G5
    /// with Deep Plating exactly as planned and still carrying a dot that reads as outstanding
    /// work — <i>"these have been engineered, the orange circles should be gone, right?"</i> Yes.
    /// A plan that has been carried out is a plan with nothing left in it, and a mark that cannot
    /// go out is not a mark, it is decoration.
    /// </para>
    /// <para>
    /// So it means <em>the hull does not match the plan yet</em>: nothing fitted, a different
    /// module fitted, or the right module without the roll the plan asks for. Where the plan names
    /// no engineering, having the module is the whole of it.
    /// </para>
    /// </summary>
    private static bool Outstanding(SlotPlan? plan, ShipModule? module)
    {
        if (plan is null || plan.IsEmpty)
        {
            return false;
        }

        // Nothing there yet, so everything the plan asks for is still to do. This is also the case
        // that used to draw as though the planned module were fitted (GitHub issue 38).
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

        // **Both spellings, because a plan and the journal do not use the same one.** A plan
        // stores the readable name the Commander picked — "System Focused" — and Elite stores a
        // symbol — `PowerDistributor_PrioritySystems`. Comparing one to the other found them
        // different every time, so a slot rolled exactly as planned kept its dot: the same fault
        // as GitHub issue 38 arriving by the other road, and invisible until the plan column had
        // to decide whether to collapse. `BlueprintCatalogue.NameOf` is the join, and the raw
        // symbols are still compared for a blueprint the catalogue has never heard of.
        if (!string.Equals(plan.Blueprint, module.Blueprint, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(plan.Blueprint, Readable(module.Blueprint), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (plan.Grade > 0 && module.BlueprintLevel < plan.Grade)
        {
            return true;
        }

        // An experimental the plan asks for and the roll has not got. The other way round is not
        // outstanding work — a Commander who took a better effect than they planned has finished.
        //
        // **Both spellings here too, and this line is why the dot came back** (GitHub issue 86).
        // The blueprint comparison above learned the join; this one was left comparing a plan's
        // readable name to the journal's symbol, so a plan asking for "Deep Plating" never matched
        // a roll carrying `special_hullreinforcement_chunky` — which is the same effect. Reported
        // against a Kestrel whose four hull reinforcements were all rolled Heavy Duty G5 with Deep
        // Plating exactly as planned, and three of them kept a dot that reads as outstanding work.
        //
        // Three times now the same fault has arrived by a different road: the marker meaning "a
        // plan exists" (issue 38), the blueprint compared symbol-to-name (issue 39), and now the
        // experimental. A plan stores what the Commander picked and Elite stores a symbol, so
        // **every comparison between the two sides has to go through the catalogue**, and the ones
        // that do not are the ones nobody has looked at yet.
        return plan.Experimental is { Length: > 0 } wanted
               && !string.Equals(wanted, module.Experimental, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(wanted, Readable(module.Experimental), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The blueprint in the Commander's words rather than the journal's (GitHub issue 39).
    /// <para>
    /// A symbol nothing knows is passed through rather than dropped: an unknown blueprint spelled
    /// badly is still information, and hiding it would turn a cosmetic fault into a missing one.
    /// </para>
    /// </summary>
    private static string? Readable(string? blueprint) =>
        blueprint is not { Length: > 0 }
            ? null
            : BlueprintCatalogue.NameOf(blueprint) ?? blueprint;

    /// <summary>
    /// The roll that is on the module, where the plan asked for a different one (GitHub issue 42).
    /// <para>
    /// <b>The disagreement a plan exists to catch, and the one d47 was silent about.</b> A slot
    /// with nothing in it is reported by two different paths already; a slot carrying the right
    /// module engineered the <em>wrong way</em> was reported by none, because the row printed
    /// <c>plan?.Blueprint ?? module?.Blueprint</c> and the plan always won. Measured on the
    /// Commander's own Type-10: a power distributor planned Weapon Focused, rolled Priority
    /// Systems, grade 5 with Super Conduits, reading as finished on the checklist and on this row.
    /// </para>
    /// <para>
    /// Null where they agree, where either side is silent, and where nothing is fitted — that last
    /// one is <see cref="LoadoutParts.NotFitted"/>'s to say, and two marks for one fact would be
    /// worse than the one that was missing.
    /// </para>
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

    /// <summary>
    /// Whether the fitted module is the one the plan asked for (GitHub issue 38).
    /// <para>
    /// <b>How exact the plan is decides how exact the match has to be</b>, and that is
    /// <see cref="SlotPlan.Variant"/>'s whole reason for existing: <em>"a pulse laser, I do not
    /// mind which"</em> is a real plan and is the name alone, and a large gimballed one is that
    /// plan made exact. So a plan naming only a module is met by any module of that kind, and one
    /// naming a variant is met by that symbol and nothing else.
    /// </para>
    /// <para>
    /// Not <see cref="Standing"/>, which compares the <em>printed</em> names and would read an 8E
    /// Cargo Rack as failing to satisfy a plan for "a Cargo Rack". That comparison is right for
    /// deciding whether a row has two things worth saying and wrong for deciding whether there is
    /// work left, which is how one function serving both would have gone quietly wrong.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The fitted module's name, where a plan names a <em>different</em> one and both are worth
    /// saying. Null wherever a second name would be noise: no plan, no module fitted, or the two
    /// naming the same thing.
    /// </summary>
    private static string? Standing(SlotPlan? plan, ShipModule? module)
    {
        if (module is null || Planned(plan) is not { Length: > 0 } wanted)
        {
            return null;
        }

        var here = EliteSpecifications.ModuleName(module.Item) ?? module.Item;

        return string.Equals(here, wanted, StringComparison.OrdinalIgnoreCase) ? null : here;
    }

    /// <summary>
    /// What the roll actually did, biggest change first, in the shortest form that stays true.
    /// <para>
    /// Ordered by how far each figure moved <em>proportionally</em>, because a +300% mass and a
    /// +38% shield are not comparable as raw numbers and the Commander is scanning for the
    /// headline. Modifiers Elite reports without a before-figure carry no change to state, so they
    /// are left out rather than shown as zero.
    /// </para>
    /// <para>
    /// <b>An attribute that is already a percentage is shown as its value instead</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/53">#53</a>). A resistance that moved
    /// from 1% to 15.8% is <c>15.8% KineticResistance</c> — which is the figure the game's own
    /// ring shows and the one a Commander compares against — where the proportional reading made
    /// it <c>+1485%</c>. Such a modifier needs no before-figure to be worth showing, so it is kept
    /// where the guard below would have dropped it for having a base of zero.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Whether this modifier says anything. A percentage attribute needs only its own figure;
    /// everything else needs a before-figure to be a change at all, and a base of zero is one
    /// nothing can be divided by.
    /// </summary>
    private static bool Worth(ShipModifier modifier) =>
        modifier.IsPercentage
            ? modifier.Value is not null
            : modifier.Change is not null && modifier.OriginalValue is not 0;

    /// <summary>
    /// How loud this one is, for the ordering. <b>Points for a percentage attribute and a
    /// proportion for everything else</b>, which is the only way the two are comparable at all:
    /// ranking a resistance by its proportional change put it above every real change on the
    /// module for ever, because the number was a hundred times too big.
    /// </summary>
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
    /// <para>
    /// Frontier ships hulls faster than the table is rebuilt, so this is the state a brand new
    /// ship is in for a release or two. Ordering follows the same four blocks where the name is
    /// one the table recognises, and anything else falls to the end rather than being dropped —
    /// an unrecognised slot on an unrecognised hull is more likely a new kind of slot than a new
    /// kind of decoration.
    /// </para>
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

    /// <summary>
    /// What an empty slot says. Two answers, and the difference is whether d47 can see the ship:
    /// "empty" is a fact about the slot, and it is only a fact when the Commander is sitting in
    /// the ship that reported it.
    /// <para>
    /// <b>The other answer names the fix rather than the gap.</b> It read "not seen", which is
    /// true and leaves the Commander holding a state with no action in it; boarding the ship is
    /// what makes Elite write the <c>Loadout</c> this page is waiting for, so the row says so.
    /// </para>
    /// <para>
    /// <b>And it is now rare rather than usual.</b> It used to be every slot of every ship the
    /// Commander was not sitting in; since <see cref="ShipLoadouts"/> remembers what has been
    /// watched, a remembered ship's empty slot is empty — it was seen to be — and this answer is
    /// left for a ship genuinely never seen.
    /// </para>
    /// </summary>
    private string Vacant(ShipBuild build, IReadOnlyList<ShipModule> fitted) =>
        fitted.Count > 0 || Picture(build) is not null
            ? "empty"
            : "Use ship to refresh";

    /// <summary>
    /// Whether this drag is allowed, asked while the mouse is still down (remediation.md 15,
    /// item 1). The rules are <see cref="SlotCopy"/>'s and are tested without a window.
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

    /// <summary>
    /// Why a drop was turned down, in the Commander's terms (remediation.md 17, item 8).
    /// <para>
    /// It used to be *"That cannot go there"*, and worse, the page never even asked for that
    /// sentence — the release handler returned early on a refusal and said nothing at all. Every
    /// refused drag therefore looked identical to a feature that did not work, which is what was
    /// reported.
    /// </para>
    /// <para>
    /// <b>The most likely refusal is first and is not about the target at all:</b> dragging a slot
    /// that has nothing planned in it. A row shows what is fitted as well as what is planned, so
    /// the obvious thing to try is dragging a module — and a module is not a plan.
    /// </para>
    /// </summary>
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

        // Said before the size, because it is the more surprising refusal of the two and the
        // Commander cannot work it out by looking: the slot is the right kind and the right size,
        // and what is in it takes no engineering at all.
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

        // Same kind, so the module itself is what does not fit — it does not come small enough for
        // the target, which is the one real failure SlotCopy documents.
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

        // **A drag carries what the row was showing** (reported 2026-08-20: dragging a hull
        // reinforcement onto two empty compartments left them reading *"empty · Heavy Duty Hull
        // Reinforcement (G5), Deep Plating"* — a roll on nothing).
        //
        // The source plan names no module, and that is not a defect: *keep what is fitted, I only
        // want the engineering* deliberately stores the roll and no module, and the row then draws
        // the fitted module beside it. So the Commander drags a line reading "2D Hull
        // Reinforcement Package" and the plan underneath it says only "this roll" — which lands on
        // an empty slot as a roll with nothing to roll on.
        //
        // Resolved through `SlotCopy.Resize` rather than copied flat, so the module arrives at the
        // size the target actually takes; a module that does not come small enough refuses the
        // whole drag, which is the rule that was already there.
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
    /// 2026-08-20: <i>"Module Reinforcement packages can't be engineered"</i>).
    /// <para>
    /// <b>The gap the copy rules could not see.</b> <see cref="SlotCopy"/> resizes the module and
    /// carries the blueprint, which is sound while the plan names a module — downsizing keeps the
    /// name and a blueprint belongs to a name. A plan that names <em>no</em> module is the hole:
    /// "grade 5 Heavy Duty Hull Reinforcement, I do not mind which module" dropped on a
    /// compartment holding a Module Reinforcement Package produced a row reading
    /// <c>4D Module Reinforcement · Heavy Duty Hull Reinforcement (G5)</c>, which is a roll that
    /// module cannot have. Frontier offers it none at all.
    /// </para>
    /// <para>
    /// <b>Checked here rather than in <see cref="SlotCopy"/></b>, because the thing that decides
    /// is what is <em>fitted</em> in the target, and Core's copy rules are given two slots and a
    /// plan. This is the one caller that can see the ship.
    /// </para>
    /// <para>
    /// Silent where nothing can be checked: a plan with no blueprint, a slot d47 cannot see into,
    /// and a module whose type the table does not carry all pass. A refusal has to be a fact.
    /// </para>
    /// </summary>
    private bool Rollable(ShipBuild build, ShipSlot target, SlotPlan moved)
    {
        if (moved.Blueprint is not { Length: > 0 } blueprint)
        {
            return true;
        }

        // The plan's own module decides where it names one — that is what the copy will fit — and
        // what is in the slot only where it does not.
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

        // Said before the module rather than after it, because it qualifies everything below —
        // and left off entirely for the ship being flown, where "now" is the honest tense.
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

            // Its own tone, and now a name rather than a symbol. The colour was asked for on the
            // strength of the line being worth noticing; until the blueprint join existed it would
            // have made "grade 5 PowerDistributor PrioritySystems" more prominent, which is worse
            // than leaving it grey. It reads "grade 5 System Focused" now.
            lines.Add(new LoadoutLine(
                $"{grade}{ChecklistNaming.Readable(blueprint)}"
                + (module.Experimental is { Length: > 0 } effect ? $", {effect}" : string.Empty),
                LoadoutTone.Engineered));
        }

        // And whether that is the roll the plan asked for (GitHub issue 42).
        //
        // **The case a plan exists to catch, and the one nothing said out loud.** Both facts were
        // already on this page — the plan in its own block, the roll in this one — and a Commander
        // had to notice that two blueprint names differed. A slot never rolled is obvious the
        // moment you look at it; a slot rolled the wrong way looks exactly like one rolled right.
        // Measured on the Commander's own Type-10: a power distributor planned Weapon Focused,
        // rolled Priority Systems to grade 5 with Super Conduits, reading as finished everywhere.
        //
        // Said here rather than on the row because the row's roll text is never trimmed and has to
        // fit at 620 pixels; naming both blueprints measured 395 in a 368 row. The row carries the
        // marker and this page carries the sentence, which is the division it exists for.
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

        // The grade, steppable in place (remediation.md 15, item 4). Absent where there is no
        // blueprint, and absent where the blueprint offers one grade — there is nothing to step,
        // and the line does not print the grade either.
        var offered = BlueprintCatalogue.GradesFor(plan.Blueprint, plan.Module);

        var step = offered.Count > 1
            ? new LoadoutStep(plan.Grade, offered, grade =>
                ships.Plan(build.Id, plan with { Grade = grade }))
            : null;

        var lines = new List<LoadoutLine>
        {
            // Without the grade where a stepper is going to carry it, and with it where there is
            // no stepper (remediation.md 17, item 11). The two are never both true, so the grade
            // is said exactly once either way.
            new(plan.Describe(withGrade: step is null), LoadoutTone.Body) { Step = step },
        };

        // What the engineering does, where something has been chosen. Asked for as an area
        // describing the effects of the particular engineering choices (item 8), and it moves with
        // the stepper because the figures are the top grade's.
        var rolled = BlueprintCatalogue.Named(plan.Blueprint, plan.Module)
            .Where(recipe => recipe.Kind == BlueprintKind.Modification)
            .Where(recipe => recipe.Grade == plan.Grade || recipe.Grade is null)
            .FirstOrDefault(recipe => recipe.Effects.Count > 0)?.Describe();

        // **And what the experimental does, which was the half nobody drew** (asked for
        // 2026-08-20). `Blueprints.tsv` has carried an `effects` column for its 154 experimental
        // rows all along — "Hull Boost -3%, at the cost of Kinetic Resistance +8%" is derived by
        // the same `Describe` from the same three fields — and the block above only ever asked
        // about the blueprint. So a Commander choosing between Auto Loader and Corrosive Shell
        // was reading a sentence about the *roll underneath them*, which is the same for both.
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

        // Named, and only where the blueprint is drawn beside it: two sentences under one heading
        // are two claims about the same slot, and which one is the experimental's is the whole
        // thing the Commander is trying to read. Alone, the heading has already said it.
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
    /// What the Commander wants in this slot: the module, which one of it, the blueprint, the
    /// grade, and the experimental effect (remediation.md 12, item 5; 13, items 8, 9 and 10).
    /// <para>
    /// <b>Lists rather than a name to spell.</b> It used to ask for the blueprint by voice and the
    /// grade on the keyboard, so a plan depended on getting a phrase like "Increased FSD Range"
    /// right — and there was no way at all to say what should go in an empty compartment, only
    /// what should be rolled on whatever was already there.
    /// </para>
    /// <para>
    /// Every list is the slot's own. What can go in a size 4 hardpoint is not what can go in a
    /// size 4 compartment, the rolls offered are the ones that module can take, the grades are the
    /// ones that blueprint has, and the experimentals are the ones that module supports — so each
    /// step narrows the next rather than offering the whole catalogue five times.
    /// </para>
    /// <para>
    /// <b>Each step can be declined</b>, because they are separate wants. "A shield generator, I
    /// do not mind which grade" is a plan; so is "grade 5 dirty drives on whatever is in there".
    /// The first row of each list is the one that declines it, and a step with nothing to offer is
    /// not shown at all.
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

        AskModule(build, known, plan, prompts, (module, variant) =>
            AskBlueprint(build, known, plan, module, variant, prompts, (blueprint, grade, experimental) =>
            {
                var wanted = new SlotPlan(slot, blueprint, grade, plan?.Engineer)
                {
                    Module = module,
                    Variant = variant,
                    Experimental = experimental,
                };

                // **An empty plan is not a plan** and must not be stored. Keeping a module that
                // takes no engineering answers every question with nothing, and the row for it
                // says so and says pressing it only closes — which would be a lie if it left a
                // plan behind, marking the slot with a dot for a want nobody expressed.
                if (!wanted.IsEmpty)
                {
                    ships.Plan(build.Id, wanted);
                }

                done();
            }));
    }

    /// <summary>
    /// Whether the ship has room for another of this module's limited group (asked for
    /// 2026-08-20: <i>"remove the option to add a module that is already present and only allows
    /// 1 of that type"</i>).
    /// <para>
    /// <b>The group is what is limited, not the module.</b> A Standard and an Advanced Docking
    /// Computer share one, so fitting either rules out the other, and all three shield generator
    /// families share another — so a list that only checked for the same module by name would
    /// still offer a Bi-Weave beside the Prismatic already fitted.
    /// </para>
    /// <para>
    /// <b>And it is a count.</b> Sixteen of the seventeen groups allow one; the AX and Guardian
    /// weapons allow four. Treating it as a flag would have hidden three quarters of an anti-xeno
    /// loadout.
    /// </para>
    /// <para>
    /// <b>This slot never counts against itself</b>, which is the Commander's own condition:
    /// replacing the fuel scoop that is already here must still offer fuel scoops. Everything
    /// else on the ship counts, planned as well as fitted, because a plan for a second one is the
    /// same mistake made a step earlier.
    /// </para>
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

            // The plan where the Commander has one for that slot, and what is in it otherwise:
            // a plan is what the slot is going to hold, and it outranks what is there now.
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
    /// What Frontier offers this module that d47 holds no recipe for, by name where the symbol
    /// resolves to one and empty where nothing is offered at all.
    /// <para>
    /// <b>Two shipped tables disagreeing is the signal</b>, and the disagreement is information
    /// rather than a fault: the offer list comes from EDSY, which says which blueprints a module
    /// <em>type</em> may take, and the recipes come from EDEngineer, which says what each costs.
    /// EDEngineer carries no Guardian weapon recipes, so every Guardian hardpoint has offers and
    /// no rows. Reading that as "no engineering exists" was a claim about Elite; reading it as
    /// "I have no recipe" is a claim about d47, and only the second is true.
    /// </para>
    /// <para>
    /// A symbol with no row anywhere cannot be named, so it is dropped rather than de-underscored
    /// into a plausible-looking invention. The sentence still says the engineering is there, which
    /// is the part that was wrong.
    /// </para>
    /// <para>
    /// <b><c>GuardianModule_Sturdy</c> used to be the example and is no longer offered at all</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/127">#127</a>, ruled 2026-09-01): the
    /// only two sources describing Anti-Guardian Zone Resistance disagree about what it costs, so
    /// d47 withholds it rather than offering something it cannot state. What still reaches this
    /// method is engineering d47 knows is real and cannot price — <c>Weapon_RapidFire</c> on that
    /// same cannon among them — which is a different thing from engineering it cannot describe.
    /// </para>
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
        // By name rather than one row per class and rating. A size 6 compartment takes over a
        // hundred parts and around forty things; the Commander is choosing the thing, and which
        // one of it is the next question rather than part of this one.
        //
        // Grouped without regard to case, because the id list spells one weapon two ways:
        // `hpt_atdumbfiremissile_turret_large` is an "AX Missile Rack" and
        // `hpt_atdumbfiremissile_fixed_large` is an "Ax Missile Rack". Grouped exactly, that is
        // one weapon offered twice, which reads as two things a Commander has to choose between.
        // The spelling shown is the first in ordinal order, which is a rule rather than a taste —
        // and it lands on Frontier's own capitals here.
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
            // One name, so there is nothing to choose. Reported against Life Support: "there is
            // only one choice, it can't be anything else" — and the page drew two rows anyway,
            // "Anything — I only want the engineering" and the single answer. Remediation 15
            // item 7. Skipping to the variant lands on the 5A-5E question, which is a real choice,
            // so nothing is lost.
            //
            // Taken rather than declined: this records Module = "Life Support" where "Anything"
            // would leave it null, and for a socket that accepts one type those are the same want.
            // The plan line then reads properly instead of opening with a bare grade.
            //
            // Deliberately not "core internals do not ask". The Frame Shift Drive socket is a core
            // internal offering three names — Frame Shift Drive, Frame Shift Drive (SCO) and the
            // Mk II — and SCO is a real decision. The rule is one option, take it, wherever it is.
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
                    // Reported as "I went to an optional slot with nothing in it and it said the
                    // same thing. You can't engineer an empty slot" — and it was offered
                    // unconditionally, so taking it on an empty slot planned engineering for a
                    // module that is not there. Named where there is one, because "if so, fine,
                    // but say so": keeping what is fitted is a real want and was left unsaid.
                    .. Keeping(build, slot, plan),
                    // Badged where every variant of the name is gated, which is how the nineteen
                    // fall: a Prismatic Shield Generator is its own name in all eight sizes, and
                    // an ordinary Shield Generator is a different name entirely.
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

                // Its own page, asked for 2026-08-23 ("there's no help for this page"). It had
                // none twice over: the mark inherited the slot's engineering page rather than
                // naming this one, and would not have drawn even that, because help refused to
                // open over a chooser at all.
                Help = ModuleChoiceHelp,
            },
            option =>
            {
                if (option.Key.Length == 0)
                {
                    chosen(null, null);
                    return;
                }

                // Straight past the variant question: the plan already answers it, and asking a
                // Commander who said "keep this module" which one they meant is asking twice.
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

    /// <summary>
    /// Which one of it: the size and the mount (remediation.md 13, item 8).
    /// <para>
    /// <b>Said the way a Commander says it.</b> "Large, gimballed" is how somebody asks for a
    /// weapon and "3F" is how the outfitting screen files it — the second is derivable from the
    /// first and nobody remembers it, so the row leads with the words and carries the code
    /// underneath. A module with no mount says its size and rating instead, because that is what
    /// distinguishes one shield generator from another.
    /// </para>
    /// <para>
    /// Declinable, and the decline is a real plan rather than a way out: "a pulse laser, I do not
    /// mind which" is a slot the Commander has an opinion about, and it is the opinion the fleet
    /// page draws.
    /// </para>
    /// </summary>
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
            // One of it, so there is nothing to choose. Taken rather than asked: a chooser with a
            // single row is a press that teaches the Commander the panel wastes their time.
            chosen(offered.Count == 1 ? offered[0].Symbol : null);
            return;
        }

        // **The one already in the slot, named and first** (reported 2026-08-20: *"no option to
        // keep the Life Support I already have"*). The module page carries a *keep what is fitted*
        // row — and for a socket that offers one module name, remediation 15 item 7 skips that
        // page entirely and lands here, so the row was unreachable exactly where it was most
        // wanted. A core internal is the common case: nobody replaces their life support, they
        // engineer it.
        //
        // Lifted out of the list rather than added beside it, so the same module is not offered
        // twice under two names — and it keeps its own figures, because "keep this one" and "what
        // does it weigh" are the same question at this point.
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
            // **What the Commander means, not a claim about the game.** This row read
            // "size and mount do not matter", and that is false about every module in
            // Elite: an FSD's size is most of a jump range, and a weapon's mount is the
            // whole of how it tracks. The row is the *decline* — it says the plan is not
            // specifying which one — so it says that, in the words this method's own
            // documentation already uses for it.
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

    /// <summary>
    /// The blueprint, then the grade, then the effect. Three steps because the grades on offer are
    /// the ones that blueprint actually has — five is not universal, and offering a grade nobody
    /// rolls is a plan that can never be met.
    /// </summary>
    private void AskBlueprint(
        ShipBuild build,
        ShipSlot slot,
        SlotPlan? plan,
        string? module,
        string? variant,
        PanelPrompts prompts,
        Action<string?, int, string?> chosen)
    {
        // What the blueprints are listed for: the module just chosen, or the one already planned,
        // or — where the Commander said to keep what is in the slot — the module that is in it.
        //
        // **That last one is remediation.md 17, item 6's whole fix.** "Keep the 5D Hull
        // Reinforcement Package — I only want the engineering" answers the module question with
        // null, deliberately: the plan must not name a module, or keeping becomes wanting. But
        // the *question* still has a subject, and dropping it here sent the list through the
        // "d47 does not know this module" fallback below and offered every blueprint in the game.
        // Reported against Oxen's Military 02: a hull reinforcement offered Ammo Capacity.
        //
        // Asked of the slot rather than remembered from the chooser, because `Keeping` only
        // offers that row when something is fitted and visible — so where this fallback can be
        // reached at all, it has an answer, and where it cannot there was no row to take.
        var fitted = FittedIn(build, slot);

        var wanted = module
            ?? plan?.Module
            ?? (fitted is not null ? EliteSpecifications.ModuleName(fitted.Item) : null);

        // The symbol answers exactly — size, rating and mount — where the name answers for every
        // size of the thing. Same order of preference, so the two cannot disagree.
        var offer = Offered(wanted, variant ?? plan?.Variant ?? fitted?.Item);

        // Three states, where there used to be two (remediation.md 15, item 6). A module that
        // genuinely takes no engineering is not asked about at all — a fuel tank is the reported
        // case, and it is still a plannable slot, just not an engineerable one. Before, that was
        // indistinguishable from a name that failed to match, and both fell through to offering
        // every blueprint in the game: a Type-10's armour was offered Dirty Drive Tuning.
        if (offer is { Count: 0 })
        {
            chosen(null, 0, null);
            return;
        }

        // Everything, for a slot with no module decided yet or a module whose type d47 does not
        // know. The first is a real want — "I only care about the engineering" — and the second is
        // a hole in the table rather than a claim that nothing can be rolled.
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
                    // No roll means no grade and no effect: both are properties of a roll, and
                    // asking about them anyway is two questions with one answer.
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

                // The same offer the blueprint list was built from, rather than the module's name
                // again. See AskEffect for why the name is not enough.
                AskEffect(build, slot, plan, wanted, offer, prompts, effect =>
                    chosen(blueprint, grade, effect));
            });
    }

    /// <summary>
    /// The grade, from the ones that blueprint has, <b>best first and grade 5 the default</b>
    /// (remediation.md 13, item 9).
    /// <para>
    /// Ascending order put grade 1 at the top, which is the grade nobody plans: a Commander
    /// writing down what they want writes down the finished thing. So the list runs 5 down to 1
    /// and 5 is marked as the usual answer — marked rather than pre-selected, because a chooser
    /// that opens with a row already chosen invites a press that changes nothing and the
    /// Commander cannot tell whether it took.
    /// </para>
    /// <para>
    /// <b>Any is still a real answer</b> rather than a way out of the question, which is why it
    /// keeps its row.
    /// </para>
    /// </summary>
    /// <summary>
    /// The grade, <b>decided rather than asked</b> (remediation.md 15, item 4).
    /// <para>
    /// Reported as "I should be able to skip choose an engineering grade — 999 times out of 1000
    /// it will be 5". Measured: of 160 module-and-blueprint pairs, 155 reach grade 5, and the whole
    /// exception set is five. So the rule is <b>the highest grade the blueprint offers</b>, never
    /// the number five — landing on "any" when 5 was not offered was worse than landing on the top
    /// of what is.
    /// </para>
    /// <para>
    /// <b>The page is gone entirely.</b> The grade becomes a stepper on the slot page, where
    /// changing it changes what is underneath it: <see cref="EngineeringRules.RollsFor"/> turns
    /// grade and engineer rank into a roll count, which drives the "What it costs" block, so a
    /// stepper answers "what would grade 4 cost me instead" in place. A link that reopened a
    /// chooser got you back where you started.
    /// </para>
    /// <para>
    /// <b>Any grade is not a thing</b>, on the Commander's ruling, which is why nothing here offers
    /// it and why <see cref="SlotPlan.Grade"/> is an <c>int</c>.
    /// </para>
    /// </summary>
    private static int Grade(SlotPlan? plan, IReadOnlyList<int> offered)
    {
        if (offered.Count == 0)
        {
            return 0;
        }

        // What was planned, where it is still on offer. A recipe that lost a grade drops to the
        // top of what remains rather than keeping a number nobody can roll.
        //
        // **`offered` is highest-first**, so `[0]` is already the top grade and a fresh plan
        // already opens at 5. Checked on 2026-08-20 by changing it to `[^1]` and watching
        // `PlanningASlotOffersWhatFitsIt` and `TheGradeReadsLastAndTheStepperFollowsIt` both fail
        // on a grade of 1 — the two tests encode this and are the reason the change was one line
        // long and lived for two minutes.
        return plan is { Grade: > 0 } wanted && offered.Contains(wanted.Grade)
            ? wanted.Grade
            : offered[0];
    }

    /// <summary>
    /// The experimental effect, on the modules that support one (remediation.md 13, item 10).
    /// <para>
    /// <b>Per module kind, because that is how the game works</b>: the recipe for Double Braced on
    /// a beam laser is not the recipe on a power plant, and the shipped table carries them
    /// separately for that reason. A module with none is not asked about at all.
    /// </para>
    /// <para>
    /// After the blueprint rather than beside it, because it is a second thing bought at the same
    /// bench and it is its own checklist item on the same slot.
    /// </para>
    /// <para>
    /// <b>From the offer the blueprint list was built from, not from the module's name.</b>
    /// Reported 2026-08-20: keeping a fitted Shield Booster and rolling Heavy Duty went straight
    /// past this step. The two lists were joined by different means — <see cref="Offered"/> tries
    /// the module's <em>symbol</em> first, which is exact, and only falls back to the name, while
    /// this asked <see cref="BlueprintCatalogue.ExperimentalsFor"/>, which matches on the name
    /// alone. That is fine until the name is one nothing can match: "keep what is fitted" answers
    /// the module question with null on purpose, so the subject comes from
    /// <c>EliteSpecifications.ModuleName</c>, which returns <c>"0A Shield Booster"</c> — the size
    /// and rating are in it. Measured: <c>ForModule("Shield Booster")</c> finds 31 recipes and six
    /// experimentals, <c>ForModule("0A Shield Booster")</c> finds none.
    /// </para>
    /// <para>
    /// So the blueprint list was right and this one was empty, and a step with nothing to offer is
    /// not shown — which is correct behaviour reached from a wrong answer, and therefore silent.
    /// <b>It was not a Shield Booster fault</b>: the prefix is on every fitted module's name, so
    /// keeping <em>any</em> module skipped this step.
    /// </para>
    /// </summary>
    /// <param name="offer">
    /// What this module can take, already resolved by <see cref="AskBlueprint"/>. Null where the
    /// module is unknown or undecided, and the name is then the only thing left to ask by.
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
        // Grouped rather than projected to a bare name (reported 2026-08-20: *"details on
        // experimental effect should be somewhere"*). The page listed nine names — Double Braced,
        // Fast Charge, Hi-cap, Lo-draw, Thermo Block — and nothing else, so choosing between them
        // meant already knowing what they do. The blueprint page beside it has carried a `Does`
        // line since remediation 15 item 8, off the same `effects` column, and an experimental
        // row carries one too; taking the name off the recipe was what threw it away.
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

        // Deliberately no "one option, take it" here, though remediation 15 item 7 groups this
        // step with the others. The rule there rests on "anything" and "the only thing it takes"
        // being the same want, which is true of a module socket and false of an experimental: the
        // decline on this page is "No effect", which the single effect does not satisfy. Taking it
        // would put an experimental on the plan that nobody asked for — the opposite of the step
        // it would be copying.

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

    /// <summary>
    /// One variant, in the words a Commander uses for it. The size and the mount for anything
    /// mounted, and the size and the rating for everything else.
    /// </summary>
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

    /// <summary>
    /// The coin that marks a module a Powerplay pledge is needed to buy (Phase 38).
    /// <para>
    /// <b>U+00A4, the currency sign</b>, and the character is a decision rather than a taste: it
    /// is in every Latin font there is, so it cannot come out as tofu — which is the one failure
    /// only an eye catches, and which Phase 37 already recorded costing time. A glyph nobody can
    /// see is worse than no badge, because the row then reads as unrestricted.
    /// </para>
    /// </summary>
    internal const string Coin = "¤";

    /// <summary>
    /// The label for a module the Commander may not be able to buy, and the sentence under it.
    /// <para>
    /// <b>Two sentences, and which one depends on the pledge d47 already tracks.</b> An unpledged
    /// Commander cannot buy any of the nineteen and is told so flatly. A pledged one is told a
    /// pledge is needed and that d47 cannot say whose — see
    /// <see cref="ModuleSpecification.NeedsPledge"/> for why no source can.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The header's second line: which slot, how big, and what is in it. The one thing a dropdown
    /// cannot do, and the reason taking the panel is worth it.
    /// </summary>
    /// <summary>
    /// The "keep what is there, I only want the engineering" row, where there is anything to keep.
    /// <para>
    /// Empty on a slot that is known to be empty: engineering nothing is not a plan. Empty as well
    /// where d47 cannot see into the ship, because the row would then be an offer to keep a module
    /// nobody can name.
    /// </para>
    /// </summary>
    private IReadOnlyList<ChoiceOption> Keeping(ShipBuild build, ShipSlot slot, SlotPlan? plan)
    {
        // **The plan first, where it names a module** (reported 2026-08-20). Reported as dragging
        // a planned multi-cannon onto another slot, pressing "Change the plan" to alter only the
        // experimental, and finding no way to say so. This row was keyed on what is *fitted*, so a
        // slot holding a plan and nothing else — which is every slot on a ship being designed, and
        // every slot on a ship the Commander is not sitting in — offered no way to keep the module
        // and change the roll. The only route left was to pick the same module again from a list
        // of forty and answer the variant question a second time.
        //
        // Keeping a *planned* module carries it through rather than clearing it, which is the
        // other half: the fitted row means "do not plan a module at all" and answers null, and
        // answering null here would silently drop the module the Commander had just copied.
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

        // **Whether there is any engineering to want** (asked for 2026-08-20, reported against an
        // Auto Field-Maintenance Unit). The row promised "I only want the engineering" on modules
        // that take none — a fuel tank, an AFMU — and pressing it walked the Commander into a
        // blueprint question with no answers, which closed the whole prompt without a word.
        //
        // The same call AskBlueprint makes, and it has to be: a row that offers something the next
        // step will refuse is worse than a row that says so. Null means the module is unknown
        // rather than unengineerable, and an unknown one is still offered the catalogue.
        // **"I have none" rather than "there is none."** The asked-for wording was "engineering is
        // not available for this module", and that is a claim about Elite which d47 is not
        // currently entitled to make: 369 of 1,243 rows in Blueprints.tsv carry no `mtypes`, and
        // the join in BlueprintCatalogue.For needs one — so the Auto Field-Maintenance Unit this
        // was reported against reads as unengineerable while its five Shielded rows sit in the
        // table unreachable. Refinery, Fuel Scoop and Heat Sink Launcher are in the same state.
        // Until the generator fills that column this says what d47 knows, which is true either
        // way; a fuel tank, which really does take none, reads no worse for it.
        if (Offered(named, module.Item) is not { Count: 0 })
        {
            return [new ChoiceOption(string.Empty, $"Keep {what} — I only want the engineering")];
        }

        // **Three states where there were two** (reported 2026-08-20 against a Guardian Gauss
        // Cannon: *"it does have 1 engineering option — Anti-Guardian Zone Resistance"*, and it
        // does). "No recipes I hold" and "no engineering exists" are different claims, and d47 was
        // making the second while entitled only to the first.
        //
        // The distinction is already shipped and needed no new data: EDSY's offer table says the
        // Gauss Cannon's `hexgg` takes `Weapon_RapidFire` and `GuardianModule_Sturdy`, while
        // `Blueprints.tsv` — built from EDEngineer's recipes — carries a row for neither, because
        // EDEngineer has no Guardian weapon recipes at all. So a non-empty offer with no recipes
        // behind it is exactly "Frontier engineers this and I cannot cost it".
        // **And a fourth, for engineering d47 will not describe rather than cannot price**
        // (<a href="https://github.com/dseelinger/d47/issues/127">#127</a>). Two sources describe
        // Anti-Guardian Zone Resistance and they disagree about what it costs, so it is withheld
        // from the offer table entirely — which left the Guardian FSD Booster, whose only
        // blueprint it is, reading as a module Frontier does not engineer. That is the claim about
        // Elite this whole method exists to stop d47 making, so the withholding says so itself.
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

    /// <summary>
    /// The module fitted in this slot right now, or null where nothing is or nothing can see.
    /// <para>
    /// <b>Null and "empty" are different answers</b> and the callers need both: Elite reports the
    /// loadout of the ship the Commander is sitting in and no other, so a slot on a ship in another
    /// dock is unknown rather than empty.
    /// </para>
    /// </summary>
    private ShipModule? FittedIn(ShipBuild build, ShipSlot slot) =>
        Picture(build)?.Loadout.Modules.FirstOrDefault(candidate =>
            string.Equals(candidate.Slot, slot.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether this slot is known to be empty — as opposed to holding something, or being on a ship
    /// nothing can see into.
    /// </summary>
    private bool KnownEmpty(ShipBuild build, ShipSlot slot) =>
        Picture(build) is not null && FittedIn(build, slot) is null;

    /// <summary>
    /// What a module can be engineered with, or null where d47 does not know its type.
    /// <para>
    /// The exact variant first, because it names one module and answers exactly. A bare name is
    /// enough where the Commander declined the variant — every size of a pulse laser takes the
    /// same rolls — so the first module of that name answers for all of them.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// The chooser's second line: what is being chosen for, and what is in it now.
    /// <para>
    /// <b>The record's own documentation asked for this and the call site delivered neither</b>
    /// (remediation.md 15, item 11). <see cref="ChoiceRequest.Context"/> is described as "the
    /// slot's size, and what is fitted now"; what it received was the ship, the slot and the
    /// promote sentence. The marker reserved for <em>fitted</em> was meanwhile spent on
    /// <em>planned</em>, so the one thing a dropdown cannot do was the one thing missing.
    /// </para>
    /// <para>
    /// Fitted and planned stay two facts and never one merged line, which is Phase 26's rule: this
    /// carries fitted, and the row marker goes on keeping "planned now".
    /// </para>
    /// </summary>
    private string Context(ShipBuild build, ShipSlot slot)
    {
        // A utility mount is size 0, which is not a size a Commander says — and a compartment has
        // already said its own, because `ShipSlot.Describe` reads it out of `Slot04_Size4`. Saying
        // it again produced "Compartment 4 (size 4) (size 4)", which was on screen when the
        // experimental page above was reported.
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

    /// <summary>
    /// The few figures that answer "why is this one better", under a module's name.
    /// <para>
    /// Reported as "I should be able to tell the first two lasers apart by something besides the
    /// price" (remediation.md 15, item 2b). Frontier's own sentence first where they wrote one,
    /// because "enhanced with Guardian technology to speed up capacitor recharge rates, at the cost
    /// of smaller capacitors" answers the question better than any number does (item 9).
    /// </para>
    /// <para>
    /// Capped, because a row is a row: a rail gun carries fourteen figures and the first three are
    /// the ones anybody compares.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// What a blueprint does in general, from its highest grade (remediation.md 15, item 8).
    /// <para>
    /// <b>The top grade rather than the union, and measured rather than assumed.</b> 34 of 160
    /// module-and-blueprint pairs change their attribute set across grades, and in every one of
    /// the 34 the highest grade's set contains all the others — so the top grade says everything
    /// the union would and says it with real numbers rather than with the widest of five.
    /// </para>
    /// </summary>
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
                        : EntryVerdict.No("A grade is 1 to 5.")),
                grade =>
                {
                    ships.Plan(build.Id, new SlotPlan(
                        slot,
                        string.IsNullOrWhiteSpace(blueprint) ? null : blueprint.Trim(),
                        int.TryParse(grade.Trim(), out var level) ? level : 0));

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

        var said = verdict?.Says
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

    /// <summary>
    /// The picture of this ship to read, live where the Commander is in it and remembered where
    /// they are not (asked for 2026-08-20: <i>"Don't get amnesia"</i>).
    /// <para>
    /// <b>Two facts, not one.</b> <see cref="Seen.Loadout"/> is what the ship held; <c>SeenAt</c>
    /// is when — null for the ship being flown, because that answer is current. Every caller that
    /// puts a module in front of the Commander is expected to carry the date where there is one:
    /// a remembered loadout is true about a moment, and the Commander may have re-outfitted at a
    /// station d47 never watched.
    /// </para>
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
    /// What a planned module is called, with its class and rating (reported 2026-08-20: <i>"Size
    /// five is missing the 5D designation"</i>).
    /// <para>
    /// <b>The variant first, because it names one module.</b> A plan stores both — <c>Variant</c>
    /// is the journal's symbol for the exact module and <c>Module</c> is the group it belongs to —
    /// and this drew the group, so a planned slot read "Hull Reinforcement Package" beside a
    /// fitted one reading "4D Module Reinforcement". Same column, same row height, two different
    /// amounts of fact.
    /// </para>
    /// <para>
    /// The group name stands where the Commander declined the variant, which is a real answer:
    /// "a hull reinforcement, I do not mind which".
    /// </para>
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
