using System.Globalization;

using System.Text;
using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>Something worth saying out loud about an item whose computed verdict moved.</summary>
/// <param name="Key">Stable per item, so the callout engine's cooldown does its usual job.</param>
public sealed record ChecklistNews(string Key, string Text);

/// <summary>
/// What the list is being looked at through — remembered between sessions, and the same on every
/// surface. Not what is <em>on</em> the list, which is the document's business.
/// </summary>
/// <param name="Filter">The chooser's key, or <see cref="ChecklistService.Everything"/>.</param>
/// <param name="IncludePartialGrades">
/// Whether the engineer filter also shows work an engineer here can only take part of the way
/// (change-requests.md 35). Off is the default and is exactly what shipped before it existed.
/// </param>
public sealed record ChecklistView(string Filter, bool IncludePartialGrades);

/// <summary>
/// The one place the checklist is read from and written to (Phase 17, "TheApp keeps 'The
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
/// <param name="remember">
/// How the host writes the view down, or null where nothing should be remembered — a
/// test, or a surface with no view state behind it. Core never opens a file.
/// </param>
public sealed class ChecklistService(
    ChecklistStore list,
    ChecklistProposalStore proposals,
    Func<CommanderGameState?> commander,
    Action<ChecklistView>? remember = null)
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
    /// The line as a Commander reads it, with the slot resolved to the module in it. Exposed on
    /// the same terms as <see cref="Verdict"/>: the panel and the spoken report say the same
    /// sentence because they ask the same place for it.
    /// </summary>
    public string Said(ChecklistItem item) => ChecklistWording.Said(item, State);

    /// <summary>The list an item is in, named — "Flamebrand (Anaconda)" rather than "ship 51".</summary>
    public string Where(ChecklistItem item) => ChecklistWording.Where(item, State);

    /// <summary>
    /// The line the Commander is working on, which is what a phrase saying "it" means
    /// (reported 2026-08-21).
    /// <para>
    /// <b>Here rather than on the page, because two surfaces have to agree what "it" is.</b> The
    /// selection used to be a string inside <c>ChecklistPage</c>, so a spoken "move it up" had
    /// nothing to refer to and said so — while the panel, a foot away, was drawing a highlight
    /// round the very line that was meant.
    /// </para>
    /// <para>
    /// <b>Not persisted, and that is the distinction.</b> A selection is where a Commander is
    /// looking this minute, not a preference — reloading d47 with a line still highlighted from
    /// last week would be asserting a train of thought that ended.
    /// </para>
    /// </summary>
    public ChecklistItemId? Selected { get; private set; }

    /// <summary>Raised when the selection moves and the list did not, so a page can redraw.</summary>
    public event Action? SelectionChanged;

    /// <summary>
    /// Which filter the list is under — the chooser's key, or <see cref="Everything"/>.
    /// <para>
    /// <b>Here for the reason <see cref="Selected"/> is here, and it is the same report.</b> This
    /// was a string on <c>ChecklistPage</c>, and there is one page per surface — so a filter
    /// applied at the desk left the headset drawing the unfiltered list a foot away, with neither
    /// surface saying they disagreed (reported 2026-08-23). What you are <em>reading</em> is
    /// shared; only how a surface draws it — mini or full, zoom — stays with the surface.
    /// </para>
    /// <para>
    /// <b>And unlike the selection, this one is remembered.</b> A selection is where a Commander is
    /// looking this minute; a filter is closer to a preference, and was asked for as one on the
    /// same day. It is kept in <c>view-state.json</c> rather than in settings — it has no default
    /// worth documenting and nothing should fail loudly because it could not be read — which is
    /// what <paramref name="remember"/> is for. Core stays clock-free and file-free; the host hands
    /// in how to read and write it.
    /// </para>
    /// </summary>
    public string Filter { get; private set; } = Everything;

    /// <summary>
    /// What a surface's search box has narrowed the list to. <b>Shared like the filter and
    /// <em>not</em> remembered</b>, which is the line between the two: a typed query is where a
    /// Commander is this minute, and one restored from last week would be a list that looks broken
    /// until they find the box.
    /// </summary>
    public string Query { get; private set; } = string.Empty;

    /// <summary>The key that means no filter at all. One spelling, shared by both surfaces.</summary>
    public const string Everything = "everything";

    /// <summary>Raised when the filter or the search text moves, so every surface can redraw.</summary>
    public event Action? FilterChanged;

    /// <summary>
    /// Puts the list under a filter, or back under <see cref="Everything"/>. Remembered as it is
    /// applied rather than on shutdown, because d47 is closed by closing its window.
    /// </summary>
    public void Choose(string? key)
    {
        var wanted = string.IsNullOrWhiteSpace(key) ? Everything : key.Trim();

        if (string.Equals(wanted, Filter, StringComparison.Ordinal))
        {
            return;
        }

        Filter = wanted;
        Remember();
        FilterChanged?.Invoke();
    }

    /// <summary>
    /// Whether the engineer filter also shows work an engineer here can only take part of the way
    /// — <i>"Include Partial Grades"</i>, asked for 2026-08-23.
    /// <para>
    /// <b>Shared and remembered like the filter</b>, and for the same reasons: it changes what is on
    /// the list rather than how a surface draws it, and its own change request said outright that
    /// it should travel the same road as the filter rather than growing a second one.
    /// </para>
    /// </summary>
    public bool IncludePartialGrades { get; private set; }

    /// <summary>Switches the partial band on or off.</summary>
    public void IncludePartial(bool on)
    {
        if (on == IncludePartialGrades)
        {
            return;
        }

        IncludePartialGrades = on;
        Remember();
        FilterChanged?.Invoke();
    }

    private void Remember() => remember?.Invoke(new ChecklistView(Filter, IncludePartialGrades));

    /// <summary>Narrows the list to what a Commander typed, or clears it.</summary>
    public void Search(string? query)
    {
        var wanted = (query ?? string.Empty).Trim();

        if (string.Equals(wanted, Query, StringComparison.Ordinal))
        {
            return;
        }

        Query = wanted;
        FilterChanged?.Invoke();
    }

    /// <summary>
    /// Takes up the filter a previous run was left under. Separate from <see cref="Choose"/> so
    /// restoring does not write back what was just read, and silent — the Commander did not do
    /// anything, so there is nothing to announce and no surface to redraw yet.
    /// </summary>
    public void Restore(ChecklistView? view)
    {
        if (view is null)
        {
            return;
        }

        Filter = string.IsNullOrWhiteSpace(view.Filter) ? Everything : view.Filter.Trim();
        IncludePartialGrades = view.IncludePartialGrades;
    }

    /// <summary>
    /// Points the selection at a line, or clears it. Silently forgets an id nothing answers to,
    /// which is what keeps a stale selection from outliving the line it named.
    /// </summary>
    public void Select(ChecklistItemId? id)
    {
        var wanted = id is { } named && Document.Find(named) is { IsLive: true } ? id : null;

        if (Nullable.Equals(wanted, Selected))
        {
            return;
        }

        Selected = wanted;
        SelectionChanged?.Invoke();
    }

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
        // <b>A document that arrived from outside is silent too</b>, reported 2026-08-23 as a
        // stream of "X is done" for work finished while d47 was not running. The priming rule
        // above was attached to the <em>tick</em> and not to the <em>document</em>, so it covered
        // only the copy that was on disk at startup. A file rewritten under a running d47 — the
        // hand edit <see cref="ChecklistStore.Poll"/> exists to support, a restored backup, a data
        // folder refreshed from another install — was re-read mid-session and every disagreement
        // between what it stored and what the game says was announced as though it had just
        // happened. `Loaded 1 checklists … (279 items)` and the callout that followed it in the
        // same second are in `d47-20260823.log` at 21:13:34.
        //
        // d47's own writes are not this: <see cref="ChecklistStore.Save"/> re-stamps and re-reads
        // inside the write, so a tick that finds the file changed found somebody else's change.
        // The Commander's remedy is the one they asked for — mark them done, say nothing.
        var arrived = list.Poll();

        proposals.Poll();

        announce &= !arrived;

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
            //
            // Said with nothing around it — no heading, no caption, no page — so it carries its
            // own subject. The ship rides it only when the ship is not the one being flown
            // (<see cref="ChecklistWording.Aloud"/>): "I know what ship I'm in", 2026-08-23.
            var said = ChecklistWording.Aloud(item, state);

            if (item.IsComplete)
            {
                news.Add(new ChecklistNews(
                    $"checklist.undone.{item.Id}",
                    $"\"{said}\" is no longer done. {verdict.Says}"));
            }
            else if (verdict.State == ChecklistState.Done)
            {
                // <b>One way of saying it, and the shorter one</b> — asked for 2026-08-23 against
                // "Grade 5 Reinforced Shields on 5C Bi-Weave Shield Generator on Tulimiekka
                // (smallcombat01_nx)" is done. 5C Bi-Weave Shield Generator is at grade 5 and
                // finished. The verdict's reason already names the module and says what happened
                // to it, so quoting the line in front of it says the same thing twice.
                //
                // The framed form survives for the case the reason cannot cover on its own: a
                // reason names no ship, so an item about a ship the Commander is not sitting in
                // keeps the line that does. Today the evaluator returns nothing for those at all
                // (bugs.md, "a parked ship's lines carry no verdict"), which is exactly why this
                // must not assume it stays that way.
                var aboard = ChecklistEvaluator.IsActive(item.Scope, state.Ship);

                news.Add(new ChecklistNews(
                    $"checklist.done.{item.Id}",
                    aboard && verdict.Reason is { Length: > 0 }
                        ? verdict.Says
                        : $"\"{said}\" is done. {verdict.Says}"));
            }
            else if (verdict.State is ChecklistState.Blocked or ChecklistState.Stale)
            {
                news.Add(new ChecklistNews($"checklist.{verdict.State}.{item.Id}", verdict.Says));
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
    public string Report(
        string? group = null,
        string? key = null,
        string? state = null,
        string? kind = null,
        bool hereOnly = false)
    {
        var document = Document;

        // In the Commander's order rather than the file's (Phase 42): the scope headings
        // below follow first appearance, so the projects arrive ranked and the lines within one
        // arrive actionable-first without this method holding an opinion of its own.
        var live = ChecklistOrdering.Arrange(document, State).ToList();

        // **Only what the engineer in this system could roll** (asked for 2026-08-20). "I am in
        // Laksak, what can I retire here?" used to answer with the whole list, because no filter
        // knew where the Commander was — see EngineersHere for the join that was never made.
        if (hereOnly)
        {
            var reachable = EngineersHere.For(live, State)
                .SelectMany(engineer => engineer.Ready)
                .Select(item => item.Id)
                .ToHashSet();

            live = [.. live.Where(item => reachable.Contains(item.Id))];
        }

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

        // One set for the whole answer, not one per scope: the explanation is about an engineer
        // and does not become new again because the next heading is a different ship (#33).
        var explained = new HashSet<string>(StringComparer.Ordinal);

        foreach (var scope in open.Select(item => item.Scope).Distinct())
        {
            report.AppendLine($"{Heading(scope)}:");

            foreach (var item in open.Where(item => item.Scope.Same(scope)))
            {
                report.AppendLine("  " + Line(item, explained: explained));
            }
        }

        if (done.Count > 0)
        {
            report.AppendLine();
            report.AppendLine($"Done ({done.Count}):");

            foreach (var item in done)
            {
                report.AppendLine("  " + Line(item, naming: true, explained: explained));
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
    /// What every live plan still costs, netted across all of them at once (Phase 17, "An
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
    public IReadOnlyList<string> Filters() => [.. FilterAxes().Select(filter => filter.Key)];

    /// <summary>
    /// The same filters, <b>each under the question it answers</b> (asked for 2026-08-20).
    /// <para>
    /// They were one flat list of lower-cased enum names — <c>derived</c>, <c>engineeringplan</c>,
    /// <c>open</c>, <c>ship</c> — which reads as four alternatives and is nothing of the kind:
    /// those are answers to four different questions, and an item is all four at once. Every item
    /// promoted from a ship's build is a derived, engineering-plan, ship-scoped, open one, so all
    /// four rows selected the identical set and the filter looked broken.
    /// </para>
    /// <para>
    /// <b>Still a projection of the axes rather than a hand-written list</b>, which is the property
    /// the flat version had and the one worth keeping: a fourth kind of plan appears here without
    /// anybody remembering to add it. What is new is that each row carries the axis it came from
    /// and a word a person would use, and <see cref="Key"/> is untouched — the matching in the
    /// panel compares against enum names and must go on doing so.
    /// </para>
    /// </summary>
    /// <summary>
    /// The filter key for "what can be done in this system". A constant because the panel matches
    /// filter keys against enum names, and this one is not an enum.
    /// </summary>
    public const string HereKey = "here";

    /// <summary>
    /// Whether this item is one an engineer in this system could roll now.
    /// <para>
    /// <c>Ready</c> only, matching what the spoken <c>here</c> parameter has meant since
    /// 2026-08-20. The out-of-rank band answers a different question — <i>why can I not do this
    /// here</i> — and folding it in would quietly change an answer that has already shipped.
    /// </para>
    /// </summary>
    public bool CanBeDoneHere(ChecklistItem item) =>
        Here().SelectMany(engineer => engineer.Ready).Any(ready => ready.Id.Same(item.Id))
        || (IncludePartialGrades && PartlyHere(item) is not null);

    /// <summary>
    /// How far an engineer here can take this line, where one can take it part of the way — <i>"Lei
    /// Cheung takes this to 3 of 5"</i>. Null for anything else.
    /// <para>
    /// Said on the line itself as well as in the help, because the two readings of a filtered page
    /// are a sentence apart and a Commander cannot be expected to infer which one is showing.
    /// </para>
    /// </summary>
    public string? PartlyHere(ChecklistItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        foreach (var engineer in Here())
        {
            if (engineer.Partial.FirstOrDefault(partial => partial.Item.Id.Same(item.Id)) is { } found)
            {
                return found.Describe(engineer.Engineer.Name);
            }
        }

        return null;
    }

    /// <summary>Whether any engineer here has work of this kind, so a control can be offered.</summary>
    public bool HasPartialWorkHere() => Here().Any(engineer => engineer.Partial.Count > 0);

    private IReadOnlyList<EngineerAtHand> Here() =>
        EngineersHere.For(Document.Items.Where(live => live.IsLive).ToList(), State);

    public IReadOnlyList<ChecklistFilter> FilterAxes()
    {
        var live = Document.Items.Where(item => item.IsLive).ToList();

        IEnumerable<ChecklistFilter> Axis<T>(
            string heading, IEnumerable<T> values, Func<T, string> key, Func<T, string> word) =>
            values.Select(value => new ChecklistFilter(key(value), word(value), heading))
                .DistinctBy(filter => filter.Key, StringComparer.OrdinalIgnoreCase)
                .OrderBy(filter => filter.Word, StringComparer.OrdinalIgnoreCase);

        return
        [
            .. Axis(
                "Can d47 tell when it is done?",
                live.Select(item => item.Kind),
                kind => kind.ToString().ToLowerInvariant(),
                kind => kind == ChecklistItemKind.Derived
                    ? "Derived — d47 watches for it"
                    : "Written down — you say when"),

            .. Axis(
                "What wrote it",
                live.Where(item => item.Source != ChecklistSource.Commander).Select(item => item.Source),
                source => source.ToString().ToLowerInvariant(),
                Word),

            // **No Ship row** (the Commander's ruling, 2026-08-20). Nearly everything derived is
            // ship-scoped — every promoted build is — so it narrows almost nothing while reading
            // like a real choice. The other scopes still earn their place: a construction site's
            // deliveries are the System ones and an on-foot roll is Suit or Weapon, and each of
            // those genuinely picks out a slice.
            //
            // The *scope* is untouched and still keyed on the journal's ShipID — it is what makes
            // a ship's plan follow that ship through a swap. Only the filter row is gone.
            .. Axis(
                "What it belongs to",
                live.Select(item => item.Scope.Group).Where(group => group != ChecklistGroup.Ship),
                ChecklistScope.Word,
                Capitalised),

            .. Axis(
                "Where it stands",
                live.Select(item => item.IsComplete),
                done => done ? "complete" : "open",
                done => done ? "Finished" : "Still open"),

            // **What the engineer in this system can do** (change-requests.md 32), and offered
            // only where there is one. The spoken half of this shipped on 2026-08-20 as the
            // `here` parameter on `get_checklist`; this is the row that puts it on the page, which
            // is the half the request was actually missing.
            //
            // Absent rather than empty when there is no engineer here — which is the
            // overwhelmingly common case. A filter that can show nothing is alarming in a way a
            // re-ordered list is not, so the answer to "no engineer in this system" is that the
            // choice is not offered, rather than a blank page after taking it.
            .. EngineersHere.For(live, State) is { Count: > 0 } workshops
                ? [new ChecklistFilter(
                    HereKey,
                    workshops.Count == 1
                        ? $"What {workshops[0].Engineer.Name} can do here"
                        : "What the engineers here can do",
                    "Where you are")]
                : Array.Empty<ChecklistFilter>(),
        ];
    }

    /// <summary>A scope's own word, with its first letter up. "ship" becomes "Ship".</summary>
    private static string Capitalised(ChecklistGroup group)
    {
        var said = ChecklistScope.Word(group);

        return said is { Length: > 0 } ? char.ToUpperInvariant(said[0]) + said[1..] : said;
    }

    /// <summary>What wrote an item, in words rather than in the enum's spelling.</summary>
    private static string Word(ChecklistSource source) => Named(source);

    private static string Named(ChecklistSource source) => source switch
    {
        ChecklistSource.EngineeringPlan => "A ship's build",
        ChecklistSource.ColonisationPlan => "A construction site",
        ChecklistSource.OnFootPlan => "A suit or weapon build",
        _ => "You",
    };

    /// <summary>
    /// The heading over one scope's open items. A ship is named rather than numbered — the hull
    /// the plan was written for is not to hand here, and the loadout the ship is remembered by
    /// carries a better one anyway.
    /// </summary>
    private string Heading(ChecklistScope scope) => scope.Group switch
    {
        ChecklistGroup.Ship => ChecklistWording.Where(scope, null, State),
        ChecklistGroup.System => scope.Key ?? "A system",
        _ => "Everything else",
    };

    /// <summary>
    /// One line, carrying its own verdict. A derived item shows what the journal says now rather
    /// than what was stored, so the two can never silently disagree on screen.
    /// </summary>
    /// <param name="naming">
    /// Whether the line has to name its own ship. True under the flat "Done" list, where there is
    /// no heading over it saying which ship it was on — which is how three finished rolls on two
    /// ships came to read as three lines about nothing in particular.
    /// </param>
    private string Line(ChecklistItem item, bool naming = false, HashSet<string>? explained = null)
    {
        var box = item.IsComplete ? "[x]" : "[ ]";
        var text = $"{box} {(naming ? ChecklistWording.Line(item, State) : Said(item))}";

        if (item.Kind == ChecklistItemKind.Authored)
        {
            return text;
        }

        var verdict = ChecklistEvaluator.Evaluate(item, State);

        return verdict is { } known
            ? $"{text} — {Explaining(known, explained)}"
            : $"{text} — {Stale(item)}";
    }

    /// <summary>
    /// A verdict's sentence, with the explanation dropped where this answer has already given it
    /// (<a href="https://github.com/dseelinger/d47/issues/33">#33</a>).
    /// <para>
    /// <b>The first line that needs it keeps it and the rest do not.</b> "Rank rises by working
    /// with them, and it compounds" is one fact about an engineer, so six modules waiting on the
    /// same rank said it six times and it read as canned — which is what it was.
    /// </para>
    /// <para>
    /// <b>Once per answer rather than once per engineer</b>, and that is the Commander's rule read
    /// rather than bent: the sentence names no engineer, so keying it per engineer would print the
    /// identical words twice on a page blocked at two of them, which is the repetition being
    /// removed. Keyed on the sentence itself for the same reason — two explanations that differ get
    /// said, two that do not get said once.
    /// </para>
    /// <para>
    /// <b>A line that arrives alone always keeps it</b>: with no set to consult there is nothing to
    /// have said already, which is what a single-line answer and a one-line spoken reply both are.
    /// The explanation is never lost, only never repeated.
    /// </para>
    /// </summary>
    private static string Explaining(ChecklistVerdict verdict, HashSet<string>? explained) =>
        verdict.Advice is { Length: > 0 } advice && explained is not null && !explained.Add(advice)
            ? verdict.Reason
            : verdict.Says;

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
    /// <param name="goal">The arc that asked for the line, where one did (Phase 34).</param>
    public ChecklistChange AddNote(ChecklistScope scope, string text, string? goal = null) =>
        // The new line becomes the selected one (reported 2026-08-21). A Commander who has just
        // said a line is thinking about that line, and "put it at the top" said in the next breath
        // has to mean the one they were talking about rather than nothing.
        Selecting(list.Apply(Fid, Name, document => document.AddNote(scope, text, goal)));

    public ChecklistChange Complete(ChecklistItemId id) =>
        list.Apply(Fid, Name, document => document.Complete(id));

    public ChecklistChange Uncomplete(ChecklistItemId id) =>
        list.Apply(Fid, Name, document => document.Uncomplete(id));

    /// <summary>
    /// Clears the list a ship left behind when the Commander sold it
    /// (docs/plans/change-requests.md item 27). Says what it cleared, or null when there was
    /// nothing on that ship.
    /// <para>
    /// <b>Deleted rather than reset, and that is settled by the journal rather than by taste.</b>
    /// The request offered a second option — put the items back to Open and add "Purchase X" — and
    /// it is not buildable on this scope. Frontier reissues <c>ShipID</c>: measured across the
    /// 925-journal corpus on 2026-08-23, 17 of 55 sold ships had their id come back alive
    /// afterwards, one of them as a <c>ShipyardNew</c> three days later. A list left keyed to that
    /// id would silently attach itself to a different ship.
    /// </para>
    /// <para>
    /// <b>Driven by the sale rather than by the fleet.</b> Asking "is this ship still in the
    /// fleet" would also catch a sale d47 was not running for, and it would empty a Commander's
    /// checklist on any tick where the fleet had not finished loading. The event says exactly one
    /// thing and says it once; the inference is right until the moment it is catastrophically
    /// wrong.
    /// </para>
    /// </summary>
    public ChecklistNews? ShipSold(int shipId)
    {
        var scope = ChecklistScope.Ship(shipId);

        var doomed = list.For(Fid, Name).Items
            .Where(item => item.Scope == scope)
            .ToArray();

        if (doomed.Length == 0)
        {
            return null;
        }

        // The hull, from the items themselves. The fleet has already forgotten this ship by the
        // time the sale is read, so the only place left that knows what it was is the lines that
        // were about it (remediation.md 17, item 15 put it there).
        var hull = doomed.Select(item => item.Hull).FirstOrDefault(name => name is { Length: > 0 });

        foreach (var item in doomed)
        {
            Delete(item.Id);
        }

        var what = doomed.Length == 1 ? "one item" : $"{doomed.Length} items";

        var news = new ChecklistNews(
            $"checklist.sold.{shipId}",
            hull is { Length: > 0 }
                ? $"You sold the {hull}. I cleared {what} from your list that were about it."
                : $"That ship is sold. I cleared {what} from your list that were about it.");

        // Onto the same queue everything else the list says goes through, so it is the callout
        // that decides whether it is spoken and the Commander who can switch that off. Returned
        // as well, for a caller that wants to know without draining the queue.
        _news.Enqueue(news);

        return news;
    }

    public ChecklistChange Delete(ChecklistItemId id)
    {
        var change = list.Apply(Fid, Name, document => document.Delete(id));

        // The selected line has gone, so the selection goes with it rather than pointing at an id
        // nothing answers to. Here rather than in the panel, so a line dropped by a phrase leaves
        // the same nothing behind that a line dropped by a button does.
        if (change.Changed && Selected is { } held && held.Same(id))
        {
            Select(null);
        }

        return change;
    }

    /// <summary>Points the selection at whatever a change was about, where it did anything.</summary>
    private ChecklistChange Selecting(ChecklistChange change)
    {
        if (change is { Changed: true, Subject: { } subject })
        {
            Select(subject);
        }

        return change;
    }

    /// <summary>
    /// Moves an item in the Commander's own order (Phase 25). Reachable from the panel and
    /// from a phrase, and — like every other write here — <b>not from the tool surface</b>: the
    /// order is the Commander's answer to what they are working on next, which is not a thing an
    /// in-game message gets to rearrange.
    /// </summary>
    public ChecklistChange Move(ChecklistItemId id, int by) =>
        list.Apply(Fid, Name, document => document.Move(id, by));

    /// <summary>
    /// The same, said the way a Commander says it — up, down, to the top, to the bottom
    /// (reported 2026-08-21). The moved line stays selected, so a second "and again" means the
    /// line that just moved.
    /// </summary>
    public ChecklistChange Move(ChecklistItemId id, ChecklistMove move) =>
        Selecting(list.Apply(Fid, Name, document => document.Move(id, move)));

    /// <summary>
    /// The spoken form: a phrase naming an item — "move buy limpets to the top" — or nothing at
    /// all, which means the selected line.
    /// <para>
    /// A named phrase goes through <see cref="ChecklistDocument.Match"/>, which answers null for
    /// nought and for several rather than guessing: acting on the wrong item of two is worse than
    /// asking which.
    /// </para>
    /// <para>
    /// <b>Nothing selected is refused in words that say what to do about it.</b> "It" with no
    /// antecedent is the one failure this path has, and a Commander who hears
    /// <i>"nothing is selected"</i> and nothing else has been told they were wrong rather than
    /// told what to say.
    /// </para>
    /// </summary>
    public ChecklistChange Move(string? phrase, ChecklistMove move)
    {
        var document = Document;

        if (phrase is { Length: > 0 } named)
        {
            return document.Match(named) is { } item
                ? Move(item.Id, move)
                : ChecklistChange.Refused(document, $"I could not tell which item \"{named}\" means.");
        }

        return Selected is { } selected
            ? Move(selected, move)
            : ChecklistChange.Refused(
                document,
                "No line is selected, so I do not know which one you mean. Name it — "
                + "\"move buy limpets to the top\" — or pick it on the Checklist tab first.");
    }

    /// <summary>
    /// The list in the order the Commander cares about (Phase 42): their project order,
    /// then what can be done now, where they are standing. The one reading the panel, the report
    /// and the opening line all take, so the drawn top of the list and the spoken one cannot
    /// disagree.
    /// </summary>
    public IReadOnlyList<ChecklistItem> Arranged() => ChecklistOrdering.Arrange(Document, State);

    /// <summary>The projects in that same order, for the panel's chooser.</summary>
    public IReadOnlyList<ChecklistProject> Projects() => ChecklistOrdering.Projects(Document, State);

    /// <summary>
    /// Moves a whole project in the Commander's order (Phase 42). Reachable from the
    /// panel and from a phrase, and — like every other write here — <b>not from the tool
    /// surface</b>: the order is the Commander's answer to what they are working on next, which
    /// is not a thing an in-game message gets to rearrange.
    /// </summary>
    public ChecklistChange Rank(ChecklistScope scope, ChecklistMove move) =>
        list.Apply(Fid, Name, document => ChecklistOrdering.Rank(document, State, scope, move));

    /// <summary>
    /// The spoken form: a phrase naming a project — "move the Sol project up" — or nothing at
    /// all, which means the project of the selected line.
    /// <para>
    /// A named phrase is matched against each project's own word, unique-or-refused like
    /// <see cref="ChecklistDocument.Match"/> and for the same reason: acting on the wrong list of
    /// two is worse than asking which.
    /// </para>
    /// </summary>
    public ChecklistChange Rank(string? phrase, ChecklistMove move)
    {
        var document = Document;

        if (phrase is { Length: > 0 } named)
        {
            var wanted = ChecklistKeys.Compact(named);

            var matches = Projects()
                .Where(project => wanted.Length > 0
                    && ChecklistKeys.Compact(project.Word).Contains(wanted, StringComparison.Ordinal))
                .ToList();

            return matches is [{ } only]
                ? Rank(only.Scope, move)
                : ChecklistChange.Refused(document, $"I could not tell which project \"{named}\" means.");
        }

        return Selected is { } selected && document.Find(selected) is { } item
            ? Rank(item.Scope, move)
            : ChecklistChange.Refused(
                document,
                "No line is selected, so I do not know which project you mean. Name it — "
                + "\"move the Sol project up\" — or pick a line on the Checklist tab first.");
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

        // The project order travels with the list (Phase 42): it is part of what the
        // export means, and an import that kept the items and dropped the ranking would arrive
        // subtly different from what left — the one thing a round trip must not do.
        var order = incoming.ProjectOrder;

        return list.Apply(
            Fid,
            Name,
            document => new ChecklistChange(
                document with { Items = items, ProjectOrder = order },
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
    /// The arc that asked for these lines, where one did (Phase 34). Carried onto every
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
                ? $"\"{item.Text}\" is worked out from your journal rather than agreed: {known.Says}"
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

/// <summary>
/// One way of narrowing the checklist, and the question it answers.
/// </summary>
/// <param name="Key">
/// What the panel matches against — an enum's own spelling, a scope word, or a state. Unchanged
/// from the flat list this replaced, because the matching compares against these and must go on
/// doing so.
/// </param>
/// <param name="Word">How it reads to a Commander.</param>
/// <param name="Heading">The question it is an answer to.</param>
public sealed record ChecklistFilter(string Key, string Word, string Heading);
