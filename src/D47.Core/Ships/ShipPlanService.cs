using System.Globalization;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Ships;

/// <summary>
/// One line of the fleet page: a hull, whatever is planned for it, and where it is (Phase 26, "The
/// fleet, and the fleet you intend").
/// </summary>
/// <param name="Build">
/// The build, or null for a ship the Commander owns and has planned nothing for.
/// </param>
/// <param name="Stored">
/// The journal's record of it, or null for a hull that is only intended.
/// </param>
/// <param name="IsActive">Whether this is the ship the Commander is sitting in.</param>
public sealed record FleetEntry(ShipBuild? Build, StoredShip? Stored, bool IsActive)
{
    /// <summary>
    /// Owned is derived and intended is authored, which is the same rule the checklist already draws
    /// between a line the journal settles and a line a person does — so it should look like the same
    /// rule.
    /// </summary>
    public bool IsOwned => Stored is not null || IsActive;

    public string Hull => Stored?.Type ?? Build?.Hull ?? "unknown";

    public string HullName => EliteSpecifications.HullSaid(Hull);

    public string? Name => Stored?.Name ?? Build?.Name;

    /// <summary>How many slots this build has an opinion about.</summary>
    public int Planned => Build?.Slots.Count ?? 0;

    /// <summary>Where it is, in one phrase, or what it is waiting on.</summary>
    public string Where()
    {
        if (IsActive)
        {
            // Named for the ship under the Commander too.
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

        // **The system is named either way** (asked for 2026-08-20: "print in the ship list what system the
        // ship is in"). "here" alone answered a different question from the one the list is for — a Commander
        // scanning the fleet is working out where to fly, and a row that says "here" makes them select the
        // ship to find out where "here" is.
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

/// <summary>The Commander's ship builds, joined to the fleet the journal reports (Phase 26, "Ships").</summary>
public sealed class ShipPlanService(
    ShipBuildStore store,
    ChecklistService checklists,
    Func<CommanderGameState?> state)
{
    public ShipBuildStore Store => store;

    /// <summary>Whose plans are being read and written.</summary>
    private string Fid => state()?.Identity.FrontierId ?? string.Empty;

    private string? CommanderName => state()?.Identity.Name;

    /// <summary>This Commander's builds — every read here goes through this, never the whole file.</summary>
    private IReadOnlyList<ShipBuild> Mine => store.BuildsFor(Fid);

    /// <summary>This Commander's build for a journal ship id, or null when nothing is planned.</summary>
    public ShipBuild? ForShip(int shipId) => store.ForShip(Fid, shipId);

    /// <summary>
    /// The fleet as the page shows it: every ship the Commander owns, and every hull they intend, in
    /// one list.
    /// </summary>
    public IReadOnlyList<FleetEntry> Fleet()
    {
        var live = state();
        var active = live?.Ship;
        var builds = Mine;
        var entries = new List<FleetEntry>();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The ship being flown, which the fleet snapshot does not list: StoredShips is what is in the racks,
        // and the one under the Commander is by definition not.
        if (active is { IsKnown: true, ShipId: { } activeId })
        {
            var build = builds.FirstOrDefault(candidate => candidate.ShipId == activeId);

            entries.Add(new FleetEntry(
                build,
                // "unknown" rather than "here" when the location is not known yet: it is the word every other
                // path writes for a system nothing has named, and StoredShip.HasSystem is what asks. "here"
                // read as a system name to anything that did not know better — including the glyph that
                // copies one onto the clipboard.
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

        // Everything else the Commander has planned.
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
    /// The build a phrase means, made if it does not exist yet: the ship named, the one being flown
    /// when nothing is named, or a fresh intended build when the phrase names a hull the Commander does
    /// not own.
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

        // Nothing owned answers to that name.
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
    /// </summary>
    public ShipBuild BuildFor(int shipId, string hull, string? name = null)
    {
        if (ForShip(shipId) is { } existing)
        {
            return existing;
        }

        // The symbol, whatever the caller had.
        var build = new ShipBuild(Fid, NextId(), Knowledge.EliteSpecifications.Ship(hull)?.Symbol ?? hull, shipId, name)
        {
            CommanderName = CommanderName,
        };

        store.Save([.. store.Builds, build]);
        return build;
    }

    /// <summary>Starts a build for a hull the Commander does not own (Phase 26).</summary>
    public ShipBuild? Intend(string hull, string? name = null)
    {
        if (EliteSpecifications.Ship(hull) is not { } specification)
        {
            return null;
        }

        var build = new ShipBuild(Fid, NextId(), specification.Symbol, ShipId: null, name)
        {
            CommanderName = CommanderName,
        };

        store.Save([.. store.Builds, build]);
        return build;
    }

    /// <summary>Plans one slot, replacing whatever was planned there.</summary>
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

    /// <summary>Drops a build, and cleans up whatever it put on the checklist.</summary>
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

        // Kept, and said.
        return $"Dropped the plan for {build.Describe()}. What it already put on your checklist is "
               + "still there, on the Checklist tab, because you ordered your list around it.";
    }

    /// <summary>
    /// Deletes each build whose ship is gone — sold in <paramref name="events"/>, or its id now reporting
    /// a different hull — with the derived checklist lines about that ship, and says nothing. Pass no
    /// events for a replayed backlog, where a sold id may already belong to a later ship.
    /// </summary>
    public void DropGone(IEnumerable<JournalEvent> events)
    {
        foreach (var journalEvent in events)
        {
            // A part exchange sells the current ship too.
            if (journalEvent.Kind is "ShipyardSell" or "ShipyardBuy"
                && journalEvent.Int("SellShipID") is { } sold)
            {
                Drop(ForShip(sold));
                checklists.ForgetShip(sold, _ => true);
            }
        }

        if (state()?.Loadouts is not { IsKnown: true } loadouts)
        {
            return;
        }

        // Only a loadout actually reported for the id counts: a sale removes the id's loadout, so a sold id is
        // not compared.
        foreach (var build in Mine)
        {
            if (build.ShipId is { } id
                && loadouts.For(id)?.Loadout.Type is { } type
                && !ChecklistEvaluator.SameHull(build.Hull, type))
            {
                Drop(build);
                checklists.ForgetShip(id, item => item.Hull is not { } hull || !ChecklistEvaluator.SameHull(hull, type));
            }
        }

        // Lines with no build behind them are judged by the hull written on each.
        var ships = checklists.Document.Items
            .Where(item => item is { Kind: ChecklistItemKind.Derived, Hull: not null }
                           && item.Scope.Group == ChecklistGroup.Ship)
            .Select(item => int.TryParse(item.Scope.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                ? id
                : (int?)null)
            .OfType<int>()
            .Distinct()
            .ToList();

        foreach (var id in ships)
        {
            if (loadouts.For(id)?.Loadout.Type is { } type)
            {
                checklists.ForgetShip(id, item => item.Hull is { } hull && !ChecklistEvaluator.SameHull(hull, type));
            }
        }
    }

    private void Drop(ShipBuild? build)
    {
        if (build is not null)
        {
            store.Save([.. store.Builds.Where(other => other.Id != build.Id)]);
        }
    }

    /// <summary>
    /// Offers a build to the checklist (Phase 26, "A plan reaches the checklist when you say so").
    /// </summary>
    public string Promote(string buildId)
    {
        if (store.Find(buildId) is not { } build)
        {
            return "There is no such build.";
        }

        if (build.Scope is not { } scope)
        {
            // A prospective hull has no ShipID, so there is no list for its items to be in.
            return $"You do not own that {build.HullName} yet, so there is no ship list to put it "
                   + "on. Buy the ship and I will offer to adopt this plan onto it.";
        }

        // **A plan that names a module and no engineering has nothing to promote** (reported 2026-08-20).
        // `SlotPlan.Grade` is an int where 0 means "no engineering stated" and `BuildRequest.Grade` is an
        // int? where a number means a real grade, so a module-only plan arrived at the checklist as *"Grade 0
        // engineering on LargeHardpoint1"* — which checklist validation then refused, four times over, in red
        // at the top of the tab.
        var planned = build.Slots
            .Where(slot => slot.Blueprint is not null || slot.Grade > 0 || slot.Experimental is not null)
            .ToList();

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

            // Every slot this build has an opinion about, so a slot the Commander cleared is dropped from the
            // list rather than left standing because nothing mentioned it.
            [.. build.Slots.Select(slot => slot.Slot)]);
    }

    /// <summary>
    /// Watches for the Commander buying — or boarding — a hull they had planned for, and adopts the
    /// plan onto it (Phase 26; the boarding half is remediation.md 17, item 7).
    /// </summary>
    public IReadOnlyList<string> Observe(IEnumerable<JournalEvent> events)
    {
        // Builds from before the file carried a Commander are claimed by the first one seen — a pre-existing
        // file was written by the installation's one Commander, and leaving them unowned would empty every
        // fleet page on the release that added the key.
        if (state()?.Identity is { FrontierId.Length: > 0 } identity)
        {
            store.Adopt(identity.FrontierId, identity.Name);
        }

        var said = new List<string>();

        foreach (var journalEvent in events)
        {
            // Two events, and the second is remediation.md 17 item 7. `ShipyardNew` is the moment of purchase
            // and was the only one — which binds nothing for a Commander who bought the hull before planning
            // it, or who bought it while d47 was not running. `Loadout` is the stronger signal anyway: it
            // says the Commander is sitting in the thing.
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

            // **Nothing is adopted onto a ship another build already holds.** One build per ship is the rule
            // Phase 26 is built on, and a second Python planned to buy would otherwise bind itself to the
            // Python already being flown the moment the Commander boarded it. `ShipyardNew` cannot collide —
            // the id is brand new — so this guards the path added here, which can be raised for a ship that
            // has been owned for a year.
            if (Mine.Any(build => build.ShipId == id))
            {
                continue;
            }

            var candidates = Mine
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

    /// <summary>Binds a prospective build to a real ship id.</summary>
    public string Adopt(string buildId, int shipId)
    {
        if (store.Find(buildId) is not { } build)
        {
            return "There is no such build.";
        }

        if (ForShip(shipId) is { } already && already.Id != buildId)
        {
            return $"Ship {shipId.ToString(CultureInfo.InvariantCulture)} already has a build, and a ship has one.";
        }

        Replace(build with { ShipId = shipId });

        return $"That {build.HullName} is yours now, and the plan you had for one is pointed at it.";
    }

    /// <summary>
    /// Records that the Commander has said no to one particular disagreement between a build and the
    /// checklist (Phase 38).
    /// </summary>
    public void Settle(string buildId, string fingerprint)
    {
        if (store.Find(buildId) is { } build)
        {
            Replace(build with { Settled = fingerprint });
        }
    }

    private void Replace(ShipBuild build) =>
        store.Save([.. store.Builds.Select(other => other.Id == build.Id ? build : other)]);

    /// <summary>The next build identity: the lowest number nothing is using.</summary>
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
