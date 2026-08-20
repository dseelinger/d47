using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>
/// One line of the fleet page: a hull, whatever is planned for it, and where it is (list.md Phase
/// 26, "The fleet, and the fleet you intend").
/// </summary>
/// <param name="Build">The build, or null for a ship the Commander owns and has planned nothing for.</param>
/// <param name="Stored">The journal's record of it, or null for a hull that is only intended.</param>
/// <param name="IsActive">Whether this is the ship the Commander is sitting in.</param>
public sealed record FleetEntry(ShipBuild? Build, StoredShip? Stored, bool IsActive)
{
    /// <summary>
    /// <b>Owned is derived and intended is authored</b>, which is the same rule the checklist
    /// already draws between a line the journal settles and a line a person does — so it should
    /// look like the same rule.
    /// </summary>
    public bool IsOwned => Stored is not null || IsActive;

    public string Hull => Stored?.Type ?? Build?.Hull ?? "unknown";

    public string HullName => EliteSpecifications.Ship(Hull)?.Name ?? Hull;

    public string? Name => Stored?.Name ?? Build?.Name;

    /// <summary>How many slots this build has an opinion about. Zero for an unplanned ship.</summary>
    public int Planned => Build?.Slots.Count ?? 0;

    /// <summary>
    /// Where it is, in one phrase, or what it is waiting on. The fleet page earns being landed on
    /// by answering this before anything is drilled.
    /// </summary>
    public string Where()
    {
        if (IsActive)
        {
            // Named for the ship under the Commander too. It is where they are, so they know it —
            // but the list is read as a list, and one row that declines to say where it is is the
            // row the eye stops on.
            return Stored is { HasSystem: true } flown
                ? $"you are flying it — {flown.StarSystem}"
                : "you are flying it";
        }

        if (Stored is null)
        {
            return "not bought yet";
        }

        if (Stored.InTransit)
        {
            return "in transit";
        }

        // **The system is named either way** (asked for 2026-08-20: "print in the ship list what
        // system the ship is in"). "here" alone answered a different question from the one the
        // list is for — a Commander scanning the fleet is working out where to fly, and a row
        // that says "here" makes them select the ship to find out where "here" is.
        return Stored.Here && Stored.HasSystem
            ? $"here — {Stored.StarSystem}"
            : Stored.StarSystem;
    }

    /// <summary>The line as the index shows it and as d47 says it.</summary>
    public string Describe()
    {
        var said = Name is { Length: > 0 } name ? $"{name} ({HullName})" : HullName;

        return $"{said} — {Where()}";
    }
}

/// <summary>
/// The Commander's ship builds, joined to the fleet the journal reports (list.md Phase 26,
/// "Ships").
/// <para>
/// <b>The plan owns what and the checklist owns when.</b> Nothing here writes to the checklist:
/// promotion goes through <see cref="ChecklistService.ProposePlan"/> and accepting stays the
/// Commander's own act, which is the third use of that mechanism and a good sign the trust
/// boundary is in the right place.
/// </para>
/// <para>
/// <b>The link is a reference rather than a snapshot.</b> A promoted checklist line follows the
/// plan it came from while the Commander reorders freely — which is the whole reason the slot key
/// underneath has to be stable, and why <see cref="ShipBuild.Id"/> is independent of the journal's
/// <c>ShipID</c> from the moment a build is created.
/// </para>
/// <para>
/// Clock-free and thread-free like everything else in Core: identities come from the caller, and
/// the journal is read through <see cref="Observe"/> when the tick loop hands it events.
/// </para>
/// </summary>
public sealed class ShipPlanService(
    ShipBuildStore store,
    ChecklistService checklists,
    Func<CommanderGameState?> state)
{
    public ShipBuildStore Store => store;

    /// <summary>
    /// The fleet as the page shows it: every ship the Commander owns, and every hull they intend,
    /// in one list.
    /// <para>
    /// Owned first and the active ship at the top of those, because the commonest reason to open
    /// this page is a question about the ship you are sitting in. Intended hulls last, because
    /// they are the ones that are not anywhere yet.
    /// </para>
    /// </summary>
    public IReadOnlyList<FleetEntry> Fleet()
    {
        var live = state();
        var active = live?.Ship;
        var builds = store.Builds;
        var entries = new List<FleetEntry>();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The ship being flown, which the fleet snapshot does not list: StoredShips is what is in
        // the racks, and the one under the Commander is by definition not.
        if (active is { IsKnown: true, ShipId: { } activeId })
        {
            var build = builds.FirstOrDefault(candidate => candidate.ShipId == activeId);

            entries.Add(new FleetEntry(
                build,
                // "unknown" rather than "here" when the location is not known yet: it is the word
                // every other path writes for a system nothing has named, and StoredShip.HasSystem
                // is what asks. "here" read as a system name to anything that did not know better
                // — including the glyph that copies one onto the clipboard.
                new StoredShip(activeId, active.Type ?? "unknown", active.Name, live?.Location.StarSystem ?? "unknown")
                {
                    Here = true,
                },
                IsActive: true));

            if (build is not null)
            {
                claimed.Add(build.Id);
            }
        }

        foreach (var ship in live?.Fleet.Ships ?? [])
        {
            if (active is { ShipId: { } flying } && ship.ShipId == flying)
            {
                continue;
            }

            var build = builds.FirstOrDefault(candidate => candidate.ShipId == ship.ShipId);

            entries.Add(new FleetEntry(build, ship, IsActive: false));

            if (build is not null)
            {
                claimed.Add(build.Id);
            }
        }

        // Everything else the Commander has planned. A build bound to a ship id the journal has
        // not mentioned lands here too rather than vanishing — a fleet snapshot the Commander has
        // not refreshed is a reason to say less about a ship, never a reason to hide their plan.
        foreach (var build in builds)
        {
            if (!claimed.Contains(build.Id))
            {
                entries.Add(new FleetEntry(build, Stored: null, IsActive: false));
            }
        }

        return entries;
    }

    /// <summary>The entry for one build, or null.</summary>
    public FleetEntry? Entry(string buildId) =>
        Fleet().FirstOrDefault(entry => entry.Build?.Id == buildId);

    /// <summary>
    /// The build a phrase means, made if it does not exist yet: the ship named, the one being
    /// flown when nothing is named, or a fresh intended build when the phrase names a hull the
    /// Commander does not own.
    /// <para>
    /// <b>Null on nought and on several rather than the first match</b>, which is the rule every
    /// other matcher in this repository follows — planning the wrong Python of two is worse than
    /// asking which.
    /// </para>
    /// <para>
    /// <b>Making one is the point.</b> "Plan grade 5 dirty drives on a Corsair" from somebody who
    /// has never owned a Corsair should start the plan whose first step is buying one, rather
    /// than refusing until they have. That is the phase's own framing: acquiring the hull is the
    /// plan's first step and not a precondition sitting outside it.
    /// </para>
    /// </summary>
    public static ShipBuild? Which(ShipPlanService ships, string? named)
    {
        var fleet = ships.Fleet();

        if (string.IsNullOrWhiteSpace(named))
        {
            return ships.Existing(fleet.FirstOrDefault(entry => entry.IsActive));
        }

        var asked = ChecklistKeys.Compact(named);

        var byName = fleet.Where(entry => ChecklistKeys.Compact(entry.Name) == asked).ToList();

        if (byName.Count == 1)
        {
            return ships.Existing(byName[0]);
        }

        var byHull = fleet
            .Where(entry => ChecklistKeys.Compact(entry.HullName) == asked
                            || ChecklistKeys.Compact(entry.Hull) == asked)
            .ToList();

        if (byHull.Count == 1)
        {
            return ships.Existing(byHull[0]);
        }

        // Nothing owned answers to that name. A hull the shipped table knows is an intent; a word
        // it does not know is a question, and answering it with a new plan for something that is
        // not a ship would be inventing a hull.
        return byHull.Count == 0 && byName.Count == 0 ? ships.Intend(named) : null;
    }

    /// <summary>The build behind an entry, started if the ship has none yet.</summary>
    private ShipBuild? Existing(FleetEntry? entry) => entry switch
    {
        null => null,
        { Build: { } build } => build,
        { Stored: { } stored } => BuildFor(stored.ShipId, stored.Type, stored.Name),
        _ => null,
    };

    /// <summary>
    /// Starts a build for a ship the Commander owns, or hands back the one that is already there.
    /// <b>One build per ship</b>, so this never makes a second.
    /// </summary>
    public ShipBuild BuildFor(int shipId, string hull, string? name = null)
    {
        if (store.ForShip(shipId) is { } existing)
        {
            return existing;
        }

        var build = new ShipBuild(NextId(), hull, shipId, name);

        store.Save([.. store.Builds, build]);
        return build;
    }

    /// <summary>
    /// Starts a build for a hull the Commander does not own (list.md Phase 26).
    /// <para>
    /// <b>A hull you do not own is not in the fleet.</b> It is its own class with no <c>ShipID</c>,
    /// because <see cref="ChecklistScope.Ship(int)"/> takes the journal's id and a Corsair nobody
    /// has bought has none — so <em>acquiring the hull is the plan's first step</em> rather than a
    /// precondition sitting outside it.
    /// </para>
    /// <para>
    /// Null when the hull is not a hull. The shipped table is what answers that, so "a Corsair"
    /// and "corsair" both land and "a Corsaire" does not — the same resolver every other hull
    /// name in this repository goes through.
    /// </para>
    /// </summary>
    public ShipBuild? Intend(string hull, string? name = null)
    {
        if (EliteSpecifications.Ship(hull) is not { } specification)
        {
            return null;
        }

        var build = new ShipBuild(NextId(), specification.Symbol, ShipId: null, name);

        store.Save([.. store.Builds, build]);
        return build;
    }

    /// <summary>
    /// Plans one slot, replacing whatever was planned there. Says nothing to the checklist: the
    /// plan owns what, and reaching the list is a separate act.
    /// </summary>
    public bool Plan(string buildId, SlotPlan slot)
    {
        if (store.Find(buildId) is not { } build || string.IsNullOrWhiteSpace(slot.Slot))
        {
            return false;
        }

        Replace(build.With(slot));
        return true;
    }

    /// <summary>Takes a slot's plan out, leaving the rest of the build alone.</summary>
    public bool Clear(string buildId, string slot)
    {
        if (store.Find(buildId) is not { } build || build.For(slot) is null)
        {
            return false;
        }

        Replace(build.Without(slot));
        return true;
    }

    /// <summary>
    /// Drops a build, and cleans up whatever it put on the checklist.
    /// <para>
    /// <b>Deleting a plan cleans its whole subtree rather than one line</b>, because promotion is
    /// one-to-many: a planned modification emits the modification <em>and</em> whatever unlocking
    /// and ranking it needs. Done through a proposal like every other write to the list, so the
    /// cleanup is the Commander's act too.
    /// </para>
    /// <para>
    /// <b>What was already promoted is kept rather than vanished</b> — see
    /// <see cref="Promote"/>. A Commander ordered their list around those lines, and silently
    /// removing them is a history that lies.
    /// </para>
    /// </summary>
    public string Delete(string buildId)
    {
        if (store.Find(buildId) is not { } build)
        {
            return "There is no such build.";
        }

        store.Save([.. store.Builds.Where(other => other.Id != buildId)]);

        if (build.Scope is not { } scope)
        {
            return $"Dropped the plan for a {build.HullName}.";
        }

        var promoted = checklists.Document.Items
            .Any(item => item.IsLive
                         && item.Scope.Same(scope)
                         && item.Source == ChecklistSource.EngineeringPlan);

        if (!promoted)
        {
            return $"Dropped the plan for {build.Describe()}.";
        }

        // Kept, and said. The alternative is a Commander finding six lines gone from a list they
        // spent an evening ordering, with nothing on screen explaining it — so the lines stay and
        // the sentence says they have, which is a fact they can act on rather than a silence.
        return $"Dropped the plan for {build.Describe()}. What it already put on your checklist is "
               + "still there, on the Checklist tab, because you ordered your list around it.";
    }

    /// <summary>
    /// Offers a build to the checklist (list.md Phase 26, "A plan reaches the checklist when you
    /// say so").
    /// <para>
    /// <b>Promotion is one-to-many.</b> <see cref="EngineeringPlan.Items"/> already emits an
    /// <c>EngineerAccess</c> step beside a modification, so one planned change produces the
    /// modification plus whatever unlocking and ranking it needs — and each of those lines carries
    /// the slot that caused it in its intent, which is what lets a later revision find them again.
    /// </para>
    /// <para>
    /// <b>Straight onto the list, because the button says so</b> (remediation.md 15, item 12). This
    /// used to make a proposal and say nothing about where the items had gone, so a Commander who
    /// pressed "Put this build on my checklist" and then looked at their checklist found it
    /// unchanged. Phase 25's rule that accepting stays the Commander's act is not weakened: it
    /// governs what d47 raises unbidden, and pressing a button labelled with the outcome <em>is</em>
    /// the act of accepting. A revision is still a diff rather than a rebuild, so the ordering they
    /// spent time on survives.
    /// </para>
    /// </summary>
    public string Promote(string buildId)
    {
        if (store.Find(buildId) is not { } build)
        {
            return "There is no such build.";
        }

        if (build.Scope is not { } scope)
        {
            // A prospective hull has no ShipID, so there is no list for its items to be in. Said
            // rather than invented: scoping them to the Commander's universal list would put a
            // Corsair's hardpoints somewhere they would outlive the decision to buy one.
            return $"You do not own that {build.HullName} yet, so there is no ship list to put it "
                   + "on. Buy the ship and I will offer to adopt this plan onto it.";
        }

        var planned = build.Slots.Where(slot => !slot.IsEmpty).ToList();

        if (planned.Count == 0)
        {
            return $"Nothing is planned for {build.Describe()} yet.";
        }

        var items = EngineeringPlan.Items(
            scope,
            build.Hull,
            [.. planned.Select(slot => slot.ToRequest())],
            checklists.SlotFor);

        return checklists.AdoptPlan(
            scope,
            ChecklistSource.EngineeringPlan,
            items,

            // Every slot this build has an opinion about, so a slot the Commander cleared is
            // dropped from the list rather than left standing because nothing mentioned it.
            [.. build.Slots.Select(slot => slot.Slot)]);
    }

    /// <summary>
    /// Watches for the Commander buying — or boarding — a hull they had planned for, and adopts the
    /// plan onto it (list.md Phase 26; the boarding half is remediation.md 17, item 7).
    /// <para>
    /// <b>Buying one adopts the plan rather than making the Commander re-point it.</b> Doug's
    /// words: "don't make me re-point it".
    /// </para>
    /// <para>
    /// <b>On <c>ShipyardNew</c> rather than <c>ShipyardBuy</c>, which is a correction to what the
    /// phase says.</b> list.md has "ShipyardBuy names the hull and the new id"; measured against
    /// the Commander's own journals, <c>ShipyardBuy</c> carries <c>ShipType</c>, <c>ShipPrice</c>
    /// and the old ship being stored, and <b>no id for the new one at all</b> — the id arrives in
    /// the <c>ShipyardNew</c> written immediately after, as <c>NewShipID</c>. Matching on the
    /// event that actually carries the id is the difference between adopting a plan and offering
    /// to adopt it onto nothing.
    /// </para>
    /// <para>
    /// Only ever the <em>first</em> matching prospective build, and only when the hull matches
    /// exactly. Two Corsairs planned and one bought is a question rather than a guess, and
    /// adopting the wrong one silently is worse than adopting neither.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Observe(IEnumerable<JournalEvent> events)
    {
        var said = new List<string>();

        foreach (var journalEvent in events)
        {
            // Two events, and the second is remediation.md 17 item 7. `ShipyardNew` is the moment
            // of purchase and was the only one — which binds nothing for a Commander who bought
            // the hull before planning it, or who bought it while d47 was not running. `Loadout`
            // is the stronger signal anyway: it says the Commander is sitting in the thing.
            //
            // Without this a plan keeps `ShipId` null for ever, and since every question a ship
            // page asks about what is fitted is keyed on that id, the page answers *not seen* while
            // the Commander is flying the very ship — which is exactly what was reported.
            var (hull, shipId) = journalEvent.Kind switch
            {
                "ShipyardNew" => (journalEvent.Named("ShipType"), journalEvent.Int("NewShipID")),
                "Loadout" => (journalEvent.Named("Ship"), journalEvent.Int("ShipID")),
                _ => (null, null),
            };

            if (hull is null || shipId is not { } id)
            {
                continue;
            }

            // **Nothing is adopted onto a ship another build already holds.** One build per ship
            // is the rule Phase 26 is built on, and a second Python planned to buy would otherwise
            // bind itself to the Python already being flown the moment the Commander boarded it.
            // `ShipyardNew` cannot collide — the id is brand new — so this guards the path added
            // here, which can be raised for a ship that has been owned for a year.
            if (store.Builds.Any(build => build.ShipId == id))
            {
                continue;
            }

            var candidates = store.Builds
                .Where(build => !build.IsOwned
                                && string.Equals(build.Hull, hull, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count != 1)
            {
                continue;
            }

            said.Add(Adopt(candidates[0].Id, id));
        }

        return said;
    }

    /// <summary>
    /// Binds a prospective build to a real ship id. The plan keeps its own identity throughout,
    /// which is what there was to rebind.
    /// </summary>
    public string Adopt(string buildId, int shipId)
    {
        if (store.Find(buildId) is not { } build)
        {
            return "There is no such build.";
        }

        if (store.ForShip(shipId) is { } already && already.Id != buildId)
        {
            return $"Ship {shipId.ToString(CultureInfo.InvariantCulture)} already has a build, and a ship has one.";
        }

        Replace(build with { ShipId = shipId });

        return $"That {build.HullName} is yours now, and the plan you had for one is pointed at it.";
    }

    private void Replace(ShipBuild build) =>
        store.Save([.. store.Builds.Select(other => other.Id == build.Id ? build : other)]);

    /// <summary>
    /// The next build identity: the lowest number nothing is using.
    /// <para>
    /// Deterministic, exactly as an authored checklist key is and for the same two reasons — Core
    /// reads no clock and owns no randomness, so a replay produces the same file every run, and a
    /// person with the file open in an editor can read it.
    /// </para>
    /// </summary>
    private string NextId()
    {
        var taken = new HashSet<int>();

        foreach (var build in store.Builds)
        {
            if (build.Id.StartsWith("ship-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(build.Id[5..], CultureInfo.InvariantCulture, out var number))
            {
                taken.Add(number);
            }
        }

        var next = 1;

        while (taken.Contains(next))
        {
            next++;
        }

        return "ship-" + next.ToString(CultureInfo.InvariantCulture);
    }
}
