using System.Globalization;

using System.Text;
using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>Something worth saying out loud about an item whose computed verdict moved.</summary>
/// <param name="Key">Stable per item, so the callout engine's cooldown does its usual job.</param>
public sealed record ChecklistNews(string Key, string Text);

/// <summary>
/// The one place the checklist is read from and written to (list.md Phase 17, "TheApp keeps 'The
/// Ultimate' checklist").
/// <para>
/// <b>"What am I working on" has exactly one answer.</b> The derived lists — a ship's build, a
/// system's construction — appear on this one surface rather than each growing a surface of its
/// own, so there is one panel, one set of voice commands, and one thing to fix a bug in.
/// </para>
/// <para>
/// <b>The trust boundary is a property of this class rather than of a file format.</b> Everything
/// named <c>Propose</c> is reachable by the model and writes only
/// <see cref="ChecklistProposalStore"/>; everything that changes the Commander's own list is
/// reachable from the panel and from the model-free keyword router and from nowhere else. That is
/// the same rule <c>architecture.md</c> §7 states for safety-critical settings: protected is a
/// property of the caller, not of the modality.
/// </para>
/// </summary>
public sealed class ChecklistService(
    ChecklistStore list,
    ChecklistProposalStore proposals,
    Func<CommanderGameState?> commander)
{
    public ChecklistStore List => list;

    public ChecklistProposalStore Proposals => proposals;

    private CommanderGameState? State => commander();

    private string Fid => State?.Identity.FrontierId ?? string.Empty;

    private string? Name => State?.Identity.Name;

    public ChecklistDocument Document => list.For(Fid, Name);

    /// <summary>
    /// What the journal says about one item right now, or null when nothing can be said. Exposed
    /// so the panel and the spoken report read the same verdict from the same place rather than
    /// each holding an opinion about live state.
    /// </summary>
    public ChecklistVerdict? Verdict(ChecklistItem item) => ChecklistEvaluator.Evaluate(item, State);

    /// <summary>
    /// The scope a bare phrase means. "This ship" and "this system" are the two a Commander says
    /// without naming, and both are answerable from where they are right now.
    /// </summary>
    public ChecklistScope ScopeFor(string? group, string? key)
    {
        var state = State;

        return group?.Trim().ToLowerInvariant() switch
        {
            "ship" => key is { Length: > 0 }
                ? new ChecklistScope(ChecklistGroup.Ship, key.Trim())
                : state?.Ship.ShipId is { } id ? ChecklistScope.Ship(id) : ChecklistScope.Universal,

            "system" => key is { Length: > 0 }
                ? ChecklistScope.System(key)
                : state?.Location.StarSystem is { } system ? ChecklistScope.System(system) : ChecklistScope.Universal,

            // Keyed on the id rather than on the name, because a Maverick taken from grade 3 to 5
            // changes its symbol and keeps its id — and a list keyed on the symbol would be
            // abandoned by the very upgrade it was written to plan.
            "suit" => key is { Length: > 0 }
                ? new ChecklistScope(ChecklistGroup.Suit, key.Trim())
                : state?.OnFoot.SuitId is { } suit ? ChecklistScope.Suit(suit) : ChecklistScope.Universal,

            // The weapon in the Commander's hands, where exactly one is carried. More than one is
            // an ambiguity the plan has to resolve by name rather than by guessing at a slot.
            "weapon" => key is { Length: > 0 }
                ? new ChecklistScope(ChecklistGroup.Weapon, key.Trim())
                : state?.OnFoot.Weapons is [{ ModuleId: { } only }]
                    ? ChecklistScope.Weapon(only)
                    : ChecklistScope.Universal,

            _ => ChecklistScope.Universal,
        };
    }

    /// <summary>
    /// What the journal calls the slot a Commander just named — "thrusters" becoming
    /// "MainEngines".
    /// <para>
    /// <b>Identity, not presentation.</b> Two conversations about the same slot in different words
    /// have to produce the same item, or a revision reads as the old plan abandoned and an
    /// identical new one opened beside it. Resolved against the ship they are actually in, because
    /// that is the only thing that knows; unmatched words stand as given rather than being mapped
    /// to whichever slot looked closest.
    /// </para>
    /// </summary>
    public string SlotFor(string spoken)
    {
        if (State?.Ship is not { } ship || string.IsNullOrWhiteSpace(spoken))
        {
            return spoken;
        }

        var wanted = ChecklistKeys.Compact(spoken);

        if (ship.Modules.FirstOrDefault(module => ChecklistKeys.Compact(module.Slot) == wanted) is { } exact)
        {
            return exact.Slot;
        }

        var byItem = ship.Modules
            .Where(module => ChecklistKeys.Compact(ModuleNames.Readable(module.Item))
                .Contains(wanted, StringComparison.Ordinal))
            .ToList();

        // One match or none. Mapping "shield" onto whichever of four shield boosters came first
        // would give the Commander an item about a slot they did not mean.
        return byItem.Count == 1 ? byItem[0].Slot : spoken;
    }

    /// <summary>The hull to stamp on a ship-scoped item, so a later swap can be called stale.</summary>
    public string? HullFor(ChecklistScope scope) =>
        scope.Group == ChecklistGroup.Ship
        && State?.Ship is { ShipId: { } id, Type: { } type }
        && scope.Key == id.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ? type
            : null;

    // ------------------------------------------------------------- reading

    /// <summary>News waiting to be spoken. Written by <see cref="Poll"/>, read by the callout.</summary>
    private readonly Queue<ChecklistNews> _news = new();

    /// <summary>
    /// Takes what is waiting to be said. Separate from <see cref="Poll"/> because the two have
    /// different masters: polling keeps the list and its verdicts honest and must happen whatever
    /// the Commander has switched off, while speaking is a callout and is theirs to silence. A
    /// muted callout that also froze the panel would be a setting doing two things, one of them
    /// invisible.
    /// </summary>
    public IReadOnlyList<ChecklistNews> Drain()
    {
        lock (_news)
        {
            if (_news.Count == 0)
            {
                return [];
            }

            var taken = _news.ToArray();
            _news.Clear();
            return taken;
        }
    }

    /// <summary>
    /// Re-reads both files and brings every derived item's verdict up to date.
    /// <para>
    /// <b>A computed tick going backwards is information, not a glitch to hide</b>, so a verdict
    /// that moves is queued as news and said <em>once</em> — once, because the new verdict is
    /// written back, and the next tick therefore finds nothing to say.
    /// </para>
    /// </summary>
    /// <param name="announce">
    /// False on the priming tick, which replays the whole journal backlog. The verdicts are
    /// folded either way; without this, starting d47 after Elite reads out every plan item the
    /// journal has ever satisfied, all at once, as though it had just happened.
    /// </param>
    public IReadOnlyList<ChecklistNews> Poll(bool announce = true)
    {
        list.Poll();
        proposals.Poll();

        var state = State;

        if (state is null)
        {
            return [];
        }

        Adopt(state);

        var document = list.For(state.Identity.FrontierId, state.Identity.Name);
        var news = new List<ChecklistNews>();
        var moved = new List<ChecklistItem>();

        foreach (var item in document.Items)
        {
            if (ChecklistEvaluator.Evaluate(item, state) is not { } verdict)
            {
                continue;
            }

            var noted = item.Noted || verdict.State == ChecklistState.Unverified;

            if (verdict.State == item.State && noted == item.Noted)
            {
                continue;
            }

            moved.Add(item with { State = verdict.State, Noted = noted });

            if (verdict.State == item.State)
            {
                continue;
            }

            // An unknown is said once and a contradiction every time — but a contradiction is
            // only ever *raised* once here, because the state it disagrees with has just been
            // written down. What repeats is the report, which says it whenever it is read.
            if (item.IsComplete)
            {
                news.Add(new ChecklistNews(
                    $"checklist.undone.{item.Id}",
                    $"\"{item.Text}\" is no longer done. {verdict.Reason}"));
            }
            else if (verdict.State == ChecklistState.Done)
            {
                news.Add(new ChecklistNews($"checklist.done.{item.Id}", $"\"{item.Text}\" is done. {verdict.Reason}"));
            }
            else if (verdict.State is ChecklistState.Blocked or ChecklistState.Stale)
            {
                news.Add(new ChecklistNews($"checklist.{verdict.State}.{item.Id}", verdict.Reason));
            }
        }

        if (announce && news.Count > 0)
        {
            lock (_news)
            {
                foreach (var item in news)
                {
                    _news.Enqueue(item);
                }
            }
        }

        if (moved.Count == 0)
        {
            return news;
        }

        list.Apply(
            state.Identity.FrontierId,
            state.Identity.Name,
            current =>
            {
                var updated = current;

                foreach (var item in moved)
                {
                    if (updated.Find(item.Id) is not null)
                    {
                        updated = updated.WithState(item);
                    }
                }

                return new ChecklistChange(updated, Changed: true, "Recomputed.");
            });

        return news;
    }

    /// <summary>
    /// Hands anything written before a Commander was known to the first one who appears.
    /// <para>
    /// d47 can be running before Elite is, and the Frontier id only exists once a journal has been
    /// read — so a line jotted at that point has nobody to belong to. It is kept in an unowned
    /// document and moved here rather than refused on the way in, because a checklist that quietly
    /// loses a line is the one failure this store exists to prevent. Keys are re-minted on the way
    /// across, so nothing collides with what the Commander already had.
    /// </para>
    /// </summary>
    private void Adopt(CommanderGameState state)
    {
        if (state.Identity.FrontierId.Length == 0
            || list.Documents.FirstOrDefault(document => document.CommanderFid.Length == 0)
                is not { Items.Count: > 0 } unowned)
        {
            return;
        }

        var mine = list.For(state.Identity.FrontierId, state.Identity.Name);

        foreach (var item in unowned.Items)
        {
            mine = mine.AddNote(item.Scope, item.Text).Document;
        }

        list.Save(
        [
            .. list.Documents.Where(document =>
                document.CommanderFid.Length > 0
                && !string.Equals(document.CommanderFid, mine.CommanderFid, StringComparison.Ordinal)),
            mine with { CommanderName = state.Identity.Name },
        ]);
    }

    /// <summary>
    /// The checklist as words. <b>Completed items are kept and shown below the open ones with
    /// their count</b>, because on something that runs for weeks seeing how far you have come is
    /// most of the point — and forty finished ones must not bury the six still open.
    /// </summary>
    public string Report(string? group = null, string? key = null, string? state = null, string? kind = null)
    {
        var document = Document;
        var live = document.Items.Where(item => item.IsLive).ToList();

        if (group is { Length: > 0 })
        {
            var scope = ScopeFor(group, key);
            live = [.. live.Where(item => item.Scope.Same(scope))];
        }

        if (kind is { Length: > 0 } wantedKind)
        {
            live = [.. live.Where(item => item.Kind.ToString().Equals(wantedKind, StringComparison.OrdinalIgnoreCase)
                                          || item.Source.ToString().Equals(wantedKind, StringComparison.OrdinalIgnoreCase))];
        }

        var open = live.Where(item => !item.IsComplete).ToList();
        var done = live.Where(item => item.IsComplete).ToList();

        if (state is { Length: > 0 } wantedState)
        {
            var onlyOpen = wantedState.Equals("open", StringComparison.OrdinalIgnoreCase);
            var onlyDone = wantedState.Equals("complete", StringComparison.OrdinalIgnoreCase)
                           || wantedState.Equals("done", StringComparison.OrdinalIgnoreCase);

            if (onlyOpen)
            {
                done = [];
            }
            else if (onlyDone)
            {
                open = [];
            }
        }

        var report = new StringBuilder();

        if (open.Count == 0 && done.Count == 0)
        {
            report.AppendLine(live.Count == 0 && document.Items.Count == 0
                ? "Your checklist is empty."
                : "Nothing on your checklist matches that.");
        }

        foreach (var scope in open.Select(item => item.Scope).Distinct())
        {
            report.AppendLine($"{Heading(scope)}:");

            foreach (var item in open.Where(item => item.Scope.Same(scope)))
            {
                report.AppendLine("  " + Line(item));
            }
        }

        if (done.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"Done ({done.Count}):");

            foreach (var item in done)
            {
                report.AppendLine("  " + Line(item));
            }
        }

        var tombstoned = document.Items.Count(item => !item.IsLive);

        if (tombstoned > 0)
        {
            report.AppendLine();
            report.AppendLine(
                $"{tombstoned} dropped from earlier versions of a plan, kept so you can see what changed.");
        }

        foreach (var problem in list.Problems)
        {
            report.AppendLine($"Refused: {problem.Where} — {problem.Reason}");
        }

        var pending = proposals.PendingFor(Fid);

        if (pending.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"Waiting for you ({pending.Count}):");

            foreach (var proposal in pending)
            {
                report.AppendLine($"  [{proposal.Id}] {proposal.Summary}");
            }
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// What every live plan still costs, netted across all of them at once (list.md Phase 17, "An
    /// engineering plan writes the checklist"; and the batching half of Phase 14's "Go and get it",
    /// which was deferred here because it needs a plan to exist).
    /// <para>
    /// <b>Caps run first, and against the exact total rather than a projection.</b> Needing more
    /// than the cap is a flat certainty — at least two trips, however the rolls go — while
    /// anything below it is a possibility and has to read like one. And caps are shared, so two
    /// plans that each fit can be jointly impossible; that is the arithmetic nobody can do in
    /// their head, and it is the reason this counts every plan rather than one.
    /// </para>
    /// <para>
    /// <b>The three ledgers are totalled apart and never together.</b> Meta-alloys are a material,
    /// Gold ×200 is two hundred tonnes of cargo, and Opinion Polls ×40 are ship locker — summing
    /// them produces a feasibility verdict that is nonsense delivered confidently.
    /// </para>
    /// </summary>
    /// <summary>
    /// Two costings as one. Their ingredient lists are disjoint by intent kind, and each computes
    /// <c>Held</c> from the inventory its own materials actually live in — ship materials from the
    /// <c>Materials</c> event, micro-resources from the backpack and locker — which is why they are
    /// computed apart and joined here rather than being one pass over both.
    /// </summary>
    private static PlanCosting Merge(PlanCosting ship, PlanCosting foot) => new()
    {
        Ingredients = [.. ship.Ingredients.Concat(foot.Ingredients)
            .OrderBy(ingredient => ingredient.Material.Name, StringComparer.Ordinal)],
        Gates = [.. ship.Gates.Concat(foot.Gates)],
        Uncovered = [.. ship.Uncovered.Concat(foot.Uncovered)],
    };

    public string Shortfall()
    {
        var state = State;
        var document = Document;
        // Both plans, netted together. A shopping trip is a trip for everything, and the ship
        // and on-foot halves are disjoint by intent kind but not by destination — a settlement
        // that owes a Commander two micro-resources is one stop whichever plan wanted them.
        var costing = Merge(
            EngineeringPlan.Cost(document.Items, state),
            OnFootPlan.Cost(document.Items, state));

        var report = new StringBuilder();

        // Caps first. A certainty stated after a list of possibilities reads as one more
        // possibility, and this one is the only line here that cannot be gathered away.
        foreach (var over in costing.OverCapacity)
        {
            report.AppendLine(
                $"{over.Material.Name}: your plans need {over.Needed} and you can only hold {over.Capacity}. "
                + "That is at least two trips whatever happens.");
        }

        foreach (var gate in costing.Gates)
        {
            report.AppendLine(gate);
        }

        foreach (var ledger in costing.Shortfall.GroupBy(ingredient => ingredient.Material.Ledger))
        {
            report.AppendLine();
            report.AppendLine($"{Ledger(ledger.Key)}:");

            foreach (var ingredient in ledger)
            {
                report.AppendLine(
                    $"  {ingredient.Material.Name}: {ingredient.Short} short ({ingredient.Held} of {ingredient.Needed}).");
            }
        }

        // Where to go, grouped by the sourcing string the materials table already carries. A
        // greedy grouping and said as one — the best cover, not the optimal one, and claiming
        // otherwise would be claiming an answer nobody computed.
        var origins = costing.Shortfall
            .SelectMany(ingredient => ingredient.Material.Origins.Select(origin => (origin, ingredient)))
            .GroupBy(pair => pair.origin, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderByDescending(group => group.Count())
            .Take(3)
            .ToList();

        if (origins.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("Worth batching — one trip covers several:");

            foreach (var group in origins)
            {
                report.AppendLine(
                    $"  {group.Key}: {string.Join(", ", group.Select(pair => pair.ingredient.Material.Name))}");
            }
        }

        // The on-foot half of batching, which is sharper than anything the ship materials can
        // offer: "Planetary Settlement" is the best origin string an Odyssey ingredient has, and
        // the building code is what turns it into a place to walk to.
        var buildings = costing.Shortfall
            .Where(ingredient => ingredient.Material.Ledger == Knowledge.MaterialLedger.ShipLocker)
            .SelectMany(ingredient => ingredient.Material.Buildings.Select(building => (building, ingredient)))
            .GroupBy(pair => pair.building, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderByDescending(group => group.Count())
            .Take(3)
            .ToList();

        if (buildings.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("On foot, by building:");

            foreach (var group in buildings)
            {
                report.AppendLine(
                    $"  {group.Key}: {string.Join(", ", group.Select(pair => pair.ingredient.Material.Name))}");
            }
        }

        if (state is not null)
        {
            var owed = ColonisationPlan.Outstanding(state.Colonisation, null);

            if (owed.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Still to haul, as of your last visit to each site:");

                foreach (var (site, resource) in owed.Take(12))
                {
                    report.AppendLine(
                        $"  {resource.Name}: {resource.Remaining} of {resource.Required} to {site.Where}.");
                }
            }
        }

        // Kept and marked, never refused. A macro refuses an unknown action because it presses
        // keys; a checklist line presses nothing, so the honest move is to carry it and say what
        // is not known about it.
        foreach (var unknown in costing.Uncovered)
        {
            report.AppendLine();
            report.AppendLine(unknown);
        }

        return report.Length == 0
            ? "Nothing on your plans is outstanding that I can price."
            : report.ToString().TrimEnd();
    }

    private static string Ledger(Knowledge.MaterialLedger ledger) => ledger switch
    {
        Knowledge.MaterialLedger.Material => "Materials",
        Knowledge.MaterialLedger.Cargo => "Cargo, in tonnes",
        Knowledge.MaterialLedger.RareCargo => "Rare cargo, in tonnes",
        Knowledge.MaterialLedger.ShipLocker => "Ship locker",
        _ => "Not in any ledger I recognise",
    };

    /// <summary>
    /// The filter row, <b>generated from the three axes rather than hand-listed</b> — kind, source
    /// and state — so a fourth kind of plan appears without anybody remembering to add it. The
    /// same relationship the settings surface has to the capability registry: a projection, never
    /// a parallel list.
    /// </summary>
    public IReadOnlyList<string> Filters()
    {
        var live = Document.Items.Where(item => item.IsLive).ToList();

        var kinds = live.Select(item => item.Kind.ToString().ToLowerInvariant());
        var sources = live.Where(item => item.Source != ChecklistSource.Commander)
            .Select(item => item.Source.ToString().ToLowerInvariant());
        var scopes = live.Select(item => ChecklistScope.Word(item.Scope.Group));
        var states = live.Select(item => item.IsComplete ? "complete" : "open");

        return [.. kinds.Concat(sources).Concat(scopes).Concat(states).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static string Heading(ChecklistScope scope) => scope.Group switch
    {
        ChecklistGroup.Ship => $"Ship {scope.Key}",
        ChecklistGroup.System => scope.Key ?? "A system",
        _ => "Everything else",
    };

    /// <summary>
    /// One line, carrying its own verdict. A derived item shows what the journal says now rather
    /// than what was stored, so the two can never silently disagree on screen.
    /// </summary>
    private string Line(ChecklistItem item)
    {
        var box = item.IsComplete ? "[x]" : "[ ]";
        var text = $"{box} {item.Text}";

        if (item.Kind == ChecklistItemKind.Authored)
        {
            return text;
        }

        var verdict = ChecklistEvaluator.Evaluate(item, State);

        return verdict is { } known
            ? $"{text} — {known.Reason}"
            : $"{text} — {Stale(item)}";
    }

    private static string Stale(ChecklistItem item) => item.State switch
    {
        ChecklistState.Done => "done, as of the last time I could see it.",
        _ => "I cannot see this right now — you are not in that ship, or have not visited that site.",
    };

    // ---------------------------------------------- the Commander's own hand

    /// <summary>
    /// Adds the Commander's own line. <b>Not reachable from the tool surface</b>: the model
    /// proposes and this commits, which is what keeps a hostile in-game message to a proposal that
    /// gets declined.
    /// </summary>
    public ChecklistChange AddNote(ChecklistScope scope, string text, string? goal = null) =>
        list.Apply(Fid, Name, document => document.AddNote(scope, text, goal));

    public ChecklistChange Complete(ChecklistItemId id) =>
        list.Apply(Fid, Name, document => document.Complete(id));

    public ChecklistChange Uncomplete(ChecklistItemId id) =>
        list.Apply(Fid, Name, document => document.Uncomplete(id));

    public ChecklistChange Delete(ChecklistItemId id) =>
        list.Apply(Fid, Name, document => document.Delete(id));

    /// <summary>
    /// Moves an item in the Commander's own order (list.md Phase 25). Reachable from the panel and
    /// from a phrase, and — like every other write here — <b>not from the tool surface</b>: the
    /// order is the Commander's answer to what they are working on next, which is not a thing an
    /// in-game message gets to rearrange.
    /// </summary>
    public ChecklistChange Move(ChecklistItemId id, int by) =>
        list.Apply(Fid, Name, document => document.Move(id, by));

    /// <summary>
    /// The same, for a phrase naming an item rather than an id — "move buy limpets up".
    /// <para>
    /// Through <see cref="ChecklistDocument.Match"/>, which answers null for nought and for
    /// several rather than guessing: acting on the wrong item of two is worse than asking which.
    /// </para>
    /// </summary>
    public ChecklistChange Move(string phrase, int by)
    {
        var document = Document;

        return document.Match(phrase) is { } item
            ? Move(item.Id, by)
            : ChecklistChange.Refused(
                document,
                $"I could not tell which item \"{phrase}\" means.");
    }

    /// <summary>
    /// Rewords a line the Commander wrote (remediation.md 10, item 13). Not reachable from the
    /// tool surface, like every other write here.
    /// </summary>
    public ChecklistChange Reword(ChecklistItemId id, string text) =>
        list.Apply(Fid, Name, document => document.Reword(id, text));

    /// <summary>
    /// This Commander's whole checklist as JSON — every line, derived ones and tombstones
    /// included, with their provenance (remediation.md 10, item 15).
    /// <para>
    /// <b>Everything, deliberately.</b> Settled with the Commander as a move-machines feature
    /// rather than a share-with-a-friend one: a derived line and the plan it came from are what
    /// make the list mean anything on the other side, and a tombstone is the answer to "why did
    /// you stop tracking that". Dropping them would produce a file that imports into something
    /// subtly different from what was exported, which is the one thing a round trip must not do.
    /// </para>
    /// <para>
    /// One Commander, not the file. Somebody else's list is not this Commander's to carry, and a
    /// whole-file export would put another Frontier id in a document they are about to send
    /// somewhere.
    /// </para>
    /// </summary>
    public string Export() =>
        System.Text.Json.JsonSerializer.Serialize(Document, ChecklistStore.Json);

    /// <summary>
    /// Replaces this Commander's checklist with an exported one (remediation.md 10, item 15).
    /// <para>
    /// <b>Checked before anything is written, and refused as a whole.</b> The store already drops
    /// a bad line on load and reports it, which is right for a file somebody hand-edited and
    /// wrong for an import: half a checklist arriving with a note about the other half is worse
    /// than the import not happening. So every line is validated first and one bad line refuses
    /// the lot.
    /// </para>
    /// <para>
    /// <b>The Commander in the file is ignored.</b> An import is "put this list on my account",
    /// not "become whoever exported it" — the document is re-stamped with this Commander's own
    /// id, which is also what stops an import writing over somebody else's document.
    /// </para>
    /// </summary>
    public ChecklistChange Import(string json)
    {
        ChecklistDocument? incoming;

        try
        {
            incoming = System.Text.Json.JsonSerializer.Deserialize<ChecklistDocument>(json, ChecklistStore.Json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ChecklistChange.Refused(Document, $"That is not a checklist file: {ex.Message}");
        }

        if (incoming is null)
        {
            return ChecklistChange.Refused(Document, "That file has nothing in it.");
        }

        foreach (var item in incoming.Items)
        {
            if (ChecklistValidation.Problem(item) is { } wrong)
            {
                return ChecklistChange.Refused(Document, $"Nothing was imported: {wrong}");
            }
        }

        var items = incoming.Items;

        return list.Apply(
            Fid,
            Name,
            document => new ChecklistChange(
                document with { Items = items },
                Changed: true,
                items.Count == 1
                    ? "Imported 1 line."
                    : $"Imported {items.Count} lines."));
    }

    public ChecklistChange Revise(
        ChecklistScope scope,
        ChecklistSource source,
        IReadOnlyList<ChecklistItem> items) =>
        list.Apply(Fid, Name, document => document.Revise(scope, source, items));

    /// <summary>
    /// Accepts everything waiting. One phrase, because a Commander answering out loud is answering
    /// the question d47 just asked rather than picking an id off a list.
    /// </summary>
    public string Accept(string? id = null)
    {
        var taken = TakeOne(id);

        if (taken.Count == 0)
        {
            return "There is nothing waiting for you to accept.";
        }

        var said = new List<string>();

        foreach (var proposal in taken)
        {
            said.Add(Apply(proposal).Report);
        }

        return Once(said);
    }

    /// <summary>
    /// Several outcomes as one answer, with anything said twice said once
    /// (remediation.md 11, item 1).
    /// <para>
    /// Accepting two proposals whose items are both gone produced "There is no such item on your
    /// checklist. There is no such item on your checklist." — one fact stated twice, which read
    /// out loud is indistinguishable from a stutter.
    /// </para>
    /// <para>
    /// <b>Collapsed rather than counted.</b> "That happened twice" is a fact about how many
    /// proposals were waiting, which is d47's bookkeeping rather than the Commander's business;
    /// what they asked was what happened to their list. Two <em>different</em> outcomes are still
    /// both reported, because a Commander who accepted two things is owed what became of each.
    /// </para>
    /// </summary>
    private static string Once(IEnumerable<string> said) =>
        string.Join(
            " ",
            said.Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal));

    /// <summary>
    /// The one line about a proposal the Commander has not answered, or null when there is none
    /// (remediation.md 10, item 10).
    /// <para>
    /// Written by the thing that knows, and appended by <see cref="Conversation.TurnLoop.Standing"/>
    /// only when it reads the same before and after a turn — so a turn that actually accepted
    /// says nothing extra, and a turn that only claimed to is corrected on the spot.
    /// </para>
    /// <para>
    /// It names the proposal rather than counting one, because the identity is what tells the two
    /// cases apart: accepting one of two leaves a different sentence, not the same one again.
    /// </para>
    /// </summary>
    public string? Standing()
    {
        var waiting = proposals.PendingFor(Fid);

        return waiting.Count switch
        {
            0 => null,
            1 => $"Still waiting on you: {waiting[0].Summary.TrimEnd('.')}. "
                 + "Say \"accept\" or \"decline\".",
            _ => $"Still waiting on you: {waiting.Count} proposals, including "
                 + $"{waiting[0].Summary.TrimEnd('.')}. Say \"accept\" or \"decline\".",
        };
    }

    public string Decline(string? id = null)
    {
        var taken = TakeOne(id);

        return taken.Count == 0
            ? "There is nothing waiting for you to decline."
            : $"Dropped {taken.Count} proposal{(taken.Count == 1 ? string.Empty : "s")}.";
    }

    /// <summary>
    /// The named proposal, or every one waiting. Named is the panel's route and "every one
    /// waiting" is the spoken one, because a Commander answering out loud is answering the
    /// question that was just asked rather than reading an id back.
    /// </summary>
    private IReadOnlyList<ChecklistProposal> TakeOne(string? id)
    {
        if (id is not { Length: > 0 })
        {
            return proposals.Take(Fid);
        }

        return proposals.Take(Fid, id) is { } one ? [one] : [];
    }

    private ChecklistChange Apply(ChecklistProposal proposal)
    {
        var target = new ChecklistItemId(proposal.Scope, proposal.TargetKey ?? string.Empty);

        return proposal.Kind switch
        {
            ProposalKind.Plan => Revise(proposal.Scope, proposal.Source, proposal.Items),
            ProposalKind.Complete => Complete(target),
            ProposalKind.Reopen => Uncomplete(target),
            ProposalKind.Remove => Delete(target),
            _ => AddAll(proposal),
        };
    }

    /// <summary>
    /// Adds every line a proposal carried, one at a time so each gets its own minted key — and
    /// reports what each one did rather than only the last. A proposal of three lines where the
    /// second is refused must not read as though all three landed.
    /// </summary>
    private ChecklistChange AddAll(ChecklistProposal proposal)
    {
        if (proposal.Items.Count == 0)
        {
            return ChecklistChange.Refused(Document, "There was nothing to add.");
        }

        var said = new List<string>();
        var moved = false;

        foreach (var item in proposal.Items)
        {
            var change = AddNote(proposal.Scope, item.Text, item.Goal);
            moved |= change.Changed;
            said.Add(change.Report);
        }

        return new ChecklistChange(Document, moved, Once(said));
    }

    // ------------------------------------------------- what the model may do

    /// <summary>
    /// Records a proposal to add lines. Model-callable, and it writes the proposals file rather
    /// than the Commander's list.
    /// </summary>
    /// <param name="goal">
    /// The arc that asked for these lines, where one did (list.md Phase 34). Carried onto every
    /// item so that finishing one visibly moves something bigger than itself.
    /// </param>
    public string ProposeAdd(ChecklistScope scope, IReadOnlyList<string> lines, string? goal = null)
    {
        var wanted = lines
            .Select(line => line.Trim())
            .Where(line => line.Length is > 0 and <= ChecklistLimits.MaxTextLength)
            .Take(ChecklistLimits.MaxPendingProposals)
            .ToList();

        if (wanted.Count == 0)
        {
            return "There was nothing to propose.";
        }

        var items = wanted.Select((line, index) => new ChecklistItem
        {
            Key = ChecklistKeys.NotePrefix + "proposed-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Scope = scope,
            Kind = ChecklistItemKind.Authored,
            Text = line,
            Provenance = ChecklistProvenance.Quoted,
            Goal = goal,
        }).ToList();

        var summary = wanted.Count == 1
            ? $"Add \"{wanted[0]}\" to the {scope} list"
            : $"Add {wanted.Count} lines to the {scope} list";

        return Record(new ChecklistProposal
        {
            Id = "pending",
            CommanderFid = Fid,
            Kind = ProposalKind.Add,
            Scope = scope,
            Summary = Trim(summary),
            Items = items,
        });
    }

    /// <summary>
    /// Proposes that a line is finished, re-opened or gone. <b>The whole of item five</b>: where
    /// the journal can tell, d47 asks and marks only after the Commander agrees, because a line
    /// they wrote in their own words is something no table can settle.
    /// </summary>
    public string ProposeChange(string phrase, ProposalKind change)
    {
        if (Document.Match(phrase) is not { } item)
        {
            return $"I could not tell which item \"{phrase}\" means.";
        }

        if (!item.TicksByHand)
        {
            // Observing rather than asserting. A derived item's state is read out of the journal
            // and simply stated; there is nothing here for the Commander to agree to.
            var verdict = ChecklistEvaluator.Evaluate(item, State);

            return verdict is { } known
                ? $"\"{item.Text}\" is worked out from your journal rather than agreed: {known.Reason}"
                : $"\"{item.Text}\" is worked out from your journal, and I cannot see it from here.";
        }

        var said = change switch
        {
            ProposalKind.Reopen => $"Re-open \"{item.Text}\"",
            ProposalKind.Remove => $"Remove \"{item.Text}\" from the list",
            _ => $"Mark \"{item.Text}\" done",
        };

        return Record(new ChecklistProposal
        {
            Id = "pending",
            CommanderFid = Fid,
            Kind = change,
            Scope = item.Scope,
            TargetKey = item.Key,
            Summary = Trim(said),
        });
    }

    /// <summary>
    /// Proposes what a plan should say about one <em>subject</em> — one slot, one place — and
    /// leaves everything else the plan says alone.
    /// <para>
    /// <b>That is what makes a revision a diff rather than a rebuild.</b> "Burst lasers instead of
    /// multi-cannons" is a statement about one hardpoint; applying it as a whole-plan replacement
    /// would tombstone the shield boosters nobody mentioned. So the proposal carries the plan's
    /// whole item set with just that subject rewritten, and <see cref="ChecklistDocument.Revise"/>
    /// does the diffing it always did.
    /// </para>
    /// <para>
    /// Pending proposals for the same plan are folded in as well, so two builds proposed in one
    /// breath and accepted together do not have the second silently undo the first.
    /// </para>
    /// </summary>
    /// <param name="replacing">
    /// The subjects this proposal has an opinion about. Anything the plan currently says about one
    /// of these and this proposal does not repeat is being dropped on purpose.
    /// </param>
    /// <summary>
    /// Puts a build on the checklist outright, and says what landed there.
    /// <para>
    /// <b>Because the button says so</b> (remediation.md 15, item 12). "Put this build on my
    /// checklist" made a *proposal* and said nothing about where forty items had gone, so a
    /// Commander who pressed it, went to the Checklist tab and found one custom note reasonably
    /// concluded nothing had saved. Everything had saved; it was waiting behind a button showing a
    /// "1", at the far edge of the bar.
    /// </para>
    /// <para>
    /// <b>Phase 25's rule is not weakened.</b> "Suggestions are a page rather than an interruption,
    /// and accepting stays the Commander's act" governs what d47 raises <em>unbidden</em>. This is
    /// not unbidden: the Commander found the build and pressed a button labelled with exactly this
    /// outcome, and that press <em>is</em> the act of accepting. Routing it through a proposal asks
    /// for a decision already given. <see cref="ProposePlan"/> stays, for the plans d47 offers on
    /// its own — a bought hull adopting a prospective build, and the like.
    /// </para>
    /// </summary>
    public string AdoptPlan(
        ChecklistScope scope,
        ChecklistSource source,
        IReadOnlyList<ChecklistItem> items,
        IReadOnlyCollection<string> replacing)
    {
        var wanted = Wanted(scope, source, items, replacing);

        if (wanted.Count == 0)
        {
            return "That would leave the plan empty, and I would rather you dropped it from the panel.";
        }

        var change = Revise(scope, source, wanted);

        // The count, said out loud. Forty items arriving is an event, and the interface used to
        // report it by changing a hidden button into one showing a 1.
        var landed = items.Count;

        return landed == 0
            ? change.Report
            : $"{landed.ToString(CultureInfo.InvariantCulture)} "
              + $"{(landed == 1 ? "item is" : "items are")} on your checklist now, "
              + "under the Checklist tab.";
    }

    public string ProposePlan(
        ChecklistScope scope,
        ChecklistSource source,
        IReadOnlyList<ChecklistItem> items,
        IReadOnlyCollection<string> replacing,
        string? describing = null)
    {
        var wanted = Wanted(scope, source, items, replacing);

        if (wanted.Count == 0)
        {
            return "That would leave the plan empty, and I would rather you dropped it from the panel.";
        }

        // The Commander's word for the ship where the caller has one, and the scope's own
        // "ship 53" only where it does not (remediation.md 15, item 12, thread B). A caller
        // holding the build knows it is called Oxen; the scope holds an id and nothing else.
        var whose = describing is { Length: > 0 } named ? named : scope.ToString();

        var said = items.Count == 0
            ? $"Drop {string.Join(", ", replacing)} from the {whose} plan"
            : $"Set the {whose} plan's {string.Join(", ", replacing)} to {string.Join("; ", items.Select(item => item.Text))}";

        return Record(new ChecklistProposal
        {
            Id = "pending",
            CommanderFid = Fid,
            Kind = ProposalKind.Plan,
            Scope = scope,
            Source = source,
            Items = wanted,
            Summary = Trim(said),
        });
    }

    /// <summary>
    /// What the checklist should hold for this scope and source once a build is applied: what is
    /// already standing for slots this build has no opinion about, plus what it does want.
    /// </summary>
    private List<ChecklistItem> Wanted(
        ChecklistScope scope,
        ChecklistSource source,
        IReadOnlyList<ChecklistItem> items,
        IReadOnlyCollection<string> replacing)
    {
        var touched = replacing.Select(ChecklistKeys.Compact).ToHashSet(StringComparer.Ordinal);

        var standing = Document.Items
            .Where(item => item.IsLive && item.Scope.Same(scope) && item.Source == source)
            .Concat(proposals.PendingFor(Fid)
                .Where(proposal => proposal.Kind == ProposalKind.Plan
                                   && proposal.Scope.Same(scope)
                                   && proposal.Source == source)
                .SelectMany(proposal => proposal.Items))
            .Where(item => item.Intent is { } intent && !touched.Contains(ChecklistKeys.Compact(intent.Subject)))
            .ToList();

        return
        [
            .. standing
                .Concat(items)
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last()),
        ];
    }

    private string Record(ChecklistProposal proposal)
    {
        if (proposals.Add(proposal) is { } refused)
        {
            return refused;
        }

        return $"{proposal.Summary}? Say \"accept the proposal\" and I will, or \"decline the proposal\" and I will not. "
               + "I cannot make this change myself.";
    }

    private static string Trim(string text) =>
        text.Length <= ChecklistLimits.MaxTextLength ? text : text[..ChecklistLimits.MaxTextLength];
}
