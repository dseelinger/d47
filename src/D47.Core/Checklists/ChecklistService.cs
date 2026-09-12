using System.Globalization;

using System.Text;
using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>Something worth saying out loud about an item whose computed verdict moved.</summary>
/// <param name="Key">Stable per item, so the callout engine's cooldown does its usual job.</param>
public sealed record ChecklistNews(string Key, string Text);

/// <summary>
/// What the list is being looked at through — remembered between sessions, and the same on every
/// surface.
/// </summary>
/// <param name="Filter">The chooser's key, or <see cref="ChecklistService.Everything"/>.</param>
/// <param name="IncludePartialGrades">
/// Whether the engineer filter also shows work an engineer here can only take part of the way
/// (change-requests.md 35).
/// </param>
public sealed record ChecklistView(string Filter, bool IncludePartialGrades);

/// <summary>
/// The one place the checklist is read from and written to (Phase 17, "TheApp keeps 'The Ultimate'
/// checklist").
/// </summary>
/// <param name="remember">
/// How the host writes the view down, or null where nothing should be remembered — a test, or a surface
/// with no view state behind it.
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

    /// <summary>What the journal says about one item right now, or null when nothing can be said.</summary>
    public ChecklistVerdict? Verdict(ChecklistItem item) => ChecklistEvaluator.Evaluate(item, State);

    /// <summary>The line as a Commander reads it, with the slot resolved to the module in it.</summary>
    public string Said(ChecklistItem item) => ChecklistWording.Said(item, State);

    /// <summary>The list an item is in, named — "Flamebrand (Anaconda)" rather than "ship 51".</summary>
    public string Where(ChecklistItem item) => ChecklistWording.Where(item, State);

    /// <summary>
    /// The line the Commander is working on, which is what a phrase saying "it" means (reported
    /// 2026-08-21).
    /// </summary>
    public ChecklistItemId? Selected { get; private set; }

    /// <summary>Raised when the selection moves and the list did not, so a page can redraw.</summary>
    public event Action? SelectionChanged;

    /// <summary>Which filter the list is under — the chooser's key, or <see cref="Everything"/>.</summary>
    public string Filter { get; private set; } = Everything;

    /// <summary>What a surface's search box has narrowed the list to.</summary>
    public string Query { get; private set; } = string.Empty;

    /// <summary>The key that means no filter at all.</summary>
    public const string Everything = "everything";

    /// <summary>Raised when the filter or the search text moves, so every surface can redraw.</summary>
    public event Action? FilterChanged;

    /// <summary>
    /// Raised when the system the Commander is in changes, so a surface under the engineer filter reads
    /// <see cref="HereKey"/> again instead of showing the system they left (#93).
    /// </summary>
    public event Action? HereChanged;

    /// <summary>
    /// Ties <see cref="HereChanged"/> to the journal. Without this the engineer filter still answers
    /// from live state and nothing tells a surface to ask again.
    /// </summary>
    public void Follow(GameStateStore states)
    {
        ArgumentNullException.ThrowIfNull(states);

        states.SystemChanged += () => HereChanged?.Invoke();
    }

    /// <summary>Puts the list under a filter, or back under <see cref="Everything"/>.</summary>
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
    /// Whether the engineer filter also shows work an engineer here can only take part of the way —
    /// "Include Partial Grades", asked for 2026-08-23.
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

    /// <summary>Takes up the filter a previous run was left under.</summary>
    public void Restore(ChecklistView? view)
    {
        if (view is null)
        {
            return;
        }

        Filter = string.IsNullOrWhiteSpace(view.Filter) ? Everything : view.Filter.Trim();
        IncludePartialGrades = view.IncludePartialGrades;
    }

    /// <summary>Points the selection at a line, or clears it.</summary>
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

            // Keyed on the id rather than on the name, because a Maverick taken from grade 3 to 5 changes its
            // symbol and keeps its id — and a list keyed on the symbol would be abandoned by the very upgrade
            // it was written to plan.
            "suit" => key is { Length: > 0 }
                ? new ChecklistScope(ChecklistGroup.Suit, key.Trim())
                : state?.OnFoot.SuitId is { } suit ? ChecklistScope.Suit(suit) : ChecklistScope.Universal,

            // The weapon in the Commander's hands, where exactly one is carried.
            "weapon" => key is { Length: > 0 }
                ? new ChecklistScope(ChecklistGroup.Weapon, key.Trim())
                : state?.OnFoot.Weapons is [{ ModuleId: { } only }]
                    ? ChecklistScope.Weapon(only)
                    : ChecklistScope.Universal,

            _ => ChecklistScope.Universal,
        };
    }

    /// <summary>
    /// What the journal calls the slot a Commander just named — "thrusters" becoming "MainEngines".
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

        // One match or none.
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

    /// <summary>News waiting to be spoken.</summary>
    private readonly Queue<ChecklistNews> _news = new();

    /// <summary>Takes what is waiting to be said.</summary>
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

    /// <summary>Re-reads both files and brings every derived item's verdict up to date.</summary>
    /// <param name="announce">
    /// False on the priming tick, which replays the whole journal backlog.
    /// </param>
    public IReadOnlyList<ChecklistNews> Poll(bool announce = true)
    {
        // A document that arrived from outside is silent too, reported 2026-08-23 as a stream of "X is
        // done" for work finished while d47 was not running.
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

            // An unknown is said once and a contradiction every time — but a contradiction is only ever
            // *raised* once here, because the state it disagrees with has just been written down.
            var said = ChecklistWording.Aloud(item, state);

            if (item.IsComplete)
            {
                news.Add(new ChecklistNews(
                    $"checklist.undone.{item.Id}",
                    $"\"{said}\" is no longer done. {verdict.Says}"));
            }
            else if (verdict.State == ChecklistState.Done)
            {
                // One way of saying it, and the shorter one — asked for 2026-08-23 against "Grade 5
                // Reinforced Shields on 5C Bi-Weave Shield Generator on Tulimiekka (smallcombat01_nx)" is
                // done. 5C Bi-Weave Shield Generator is at grade 5 and finished.
                var aboard = ChecklistEvaluator.IsActive(item.Scope, state.Ship);

                news.Add(new ChecklistNews(
                    $"checklist.done.{item.Id}",
                    aboard && verdict.Reason is { Length: > 0 }
                        ? verdict.Says
                        : $"\"{said}\" is done. {verdict.Says}"));
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

    /// <summary>Hands anything written before a Commander was known to the first one who appears.</summary>
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

    /// <summary>The checklist as words.</summary>
    public string Report(
        string? group = null,
        string? key = null,
        string? state = null,
        string? kind = null,
        bool hereOnly = false)
    {
        var document = Document;

        // In the Commander's order rather than the file's (Phase 42): the scope headings below follow first
        // appearance, so the projects arrive ranked and the lines within one arrive actionable-first without
        // this method holding an opinion of its own.
        var live = ChecklistOrdering.Arrange(document, State).ToList();

        // **Only what the engineer in this system could roll** (asked for 2026-08-20). "I am in Laksak, what
        // can I retire here?" used to answer with the whole list, because no filter knew where the Commander
        // was — see EngineersHere for the join that was never made.
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
                report.AppendLine("  " + Line(item, naming: true));
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
    /// What every live plan still costs, netted across all of them at once (Phase 17, "An engineering
    /// plan writes the checklist"; and the batching half of Phase 14's "Go and get it", which was
    /// deferred here because it needs a plan to exist).
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
        // Both plans, netted together.
        var costing = Merge(
            EngineeringPlan.Cost(document.Items, state),
            OnFootPlan.Cost(document.Items, state));

        var report = new StringBuilder();

        // Caps first.
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

        // Where to go, grouped by the sourcing string the materials table already carries.
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

        // The on-foot half of batching, which is sharper than anything the ship materials can offer:
        // "Planetary Settlement" is the best origin string an Odyssey ingredient has, and the building code
        // is what turns it into a place to walk to.
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

        // Kept and marked, never refused.
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
    /// The filter row, generated from the three axes rather than hand-listed — kind, source and state —
    /// so a fourth kind of plan appears without anybody remembering to add it.
    /// </summary>
    public IReadOnlyList<string> Filters() => [.. FilterAxes().Select(filter => filter.Key)];

    /// <summary>The same filters, each under the question it answers (asked for 2026-08-20).</summary>
    public const string HereKey = "here";

    /// <summary>
    /// Whether an engineer in this system does this work at all — which is not the same question as
    /// whether the Commander could have it rolled today, and is the one the row is for (#205, ruled
    /// 2026-09-01).
    /// </summary>
    public bool OfferedHere(ChecklistItem item) =>
        Here().Any(engineer =>
            engineer.Ready.Any(ready => ready.Id.Same(item.Id))
            || engineer.OutOfRank.Any(waiting => waiting.Id.Same(item.Id)))
        || (IncludePartialGrades && PartlyHere(item) is not null);

    /// <summary>
    /// How far an engineer here can take this line, where one can take it part of the way — "Lei Cheung
    /// takes this to 3 of 5".
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

    /// <summary>
    /// Why a line this engineer does cannot be rolled today — the grade it asks for against the
    /// Commander's standing with them (#205).
    /// </summary>
    public string? RankHere(ChecklistItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Intent?.Grade is not { } wanted)
        {
            return null;
        }

        foreach (var engineer in Here())
        {
            if (!engineer.OutOfRank.Any(waiting => waiting.Id.Same(item.Id)))
            {
                continue;
            }

            var grade = wanted.ToString(CultureInfo.InvariantCulture);

            return engineer.Rank is { } rank
                ? $"{engineer.Engineer.Name} rolls this at grade {grade}, and you are "
                  + $"grade {rank.ToString(CultureInfo.InvariantCulture)} with them"
                : $"{engineer.Engineer.Name} rolls this at grade {grade}, and your grade with "
                  + "them is not in your journal yet";
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

            // **No Ship row** (the Commander's ruling, 2026-08-20).
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

            // **What the engineer in this system can do** (change-requests.md 32), and offered only where
            // there is one.
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

    /// <summary>The heading over one scope's open items.</summary>
    private string Heading(ChecklistScope scope) => scope.Group switch
    {
        ChecklistGroup.Ship => ChecklistWording.Where(scope, null, State),
        ChecklistGroup.System => scope.Key ?? "A system",
        _ => "Everything else",
    };

    /// <summary>One line, carrying its own verdict.</summary>
    /// <param name="naming">Whether the line has to name its own ship.</param>
    private string Line(ChecklistItem item, bool naming = false)
    {
        var box = item.IsComplete ? "[x]" : "[ ]";
        var text = $"{box} {(naming ? ChecklistWording.Line(item, State) : Said(item))}";

        if (item.Kind == ChecklistItemKind.Authored)
        {
            return text;
        }

        var verdict = ChecklistEvaluator.Evaluate(item, State);

        return verdict is { } known
            ? $"{text} — {known.Says}"
            : $"{text} — {Stale(item)}";
    }

    private static string Stale(ChecklistItem item) => item.State switch
    {
        ChecklistState.Done => "done, as of the last time I could see it.",
        _ => "I cannot see this right now — you are not in that ship, or have not visited that site.",
    };

    // ---------------------------------------------- the Commander's own hand

    /// <summary>Adds the Commander's own line.</summary>
    /// <param name="goal">The arc that asked for the line, where one did (Phase 34).</param>
    public ChecklistChange AddNote(ChecklistScope scope, string text, string? goal = null) =>
        // The new line becomes the selected one (reported 2026-08-21).
        Selecting(list.Apply(Fid, Name, document => document.AddNote(scope, text, goal)));

    public ChecklistChange Complete(ChecklistItemId id) =>
        list.Apply(Fid, Name, document => document.Complete(id));

    public ChecklistChange Uncomplete(ChecklistItemId id) =>
        list.Apply(Fid, Name, document => document.Uncomplete(id));

    /// <summary>
    /// Removes the derived lines about one ship that <paramref name="which"/> selects, and any waiting plan
    /// that would put one back, without saying anything.
    /// </summary>
    public void ForgetShip(int shipId, Func<ChecklistItem, bool> which)
    {
        var scope = ChecklistScope.Ship(shipId);

        bool Doomed(ChecklistItem item) =>
            item.Kind == ChecklistItemKind.Derived && item.Scope.Same(scope) && which(item);

        var waiting = proposals.PendingFor(Fid)
            .Where(proposal => proposal.Scope.Same(scope) && proposal.Items.Any(Doomed))
            .ToList();

        if (waiting.Count > 0)
        {
            proposals.Write([.. proposals.Pending.Except(waiting)]);
        }

        // Checked before writing, because this is asked on every tick.
        if (!Document.Items.Any(Doomed))
        {
            return;
        }

        var change = list.Apply(Fid, Name, document => document.Forget(scope, which));

        if (change.Changed && Selected is { } held && change.Document.Find(held) is null)
        {
            Select(null);
        }
    }

    public ChecklistChange Delete(ChecklistItemId id)
    {
        var change = list.Apply(Fid, Name, document => document.Delete(id));

        // The selected line has gone, so the selection goes with it rather than pointing at an id nothing
        // answers to.
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

    /// <summary>Moves an item in the Commander's own order (Phase 25).</summary>
    public ChecklistChange Move(ChecklistItemId id, int by) =>
        list.Apply(Fid, Name, document => document.Move(id, by));

    /// <summary>
    /// The same, said the way a Commander says it — up, down, to the top, to the bottom (reported
    /// 2026-08-21).
    /// </summary>
    public ChecklistChange Move(ChecklistItemId id, ChecklistMove move) =>
        Selecting(list.Apply(Fid, Name, document => document.Move(id, move)));

    /// <summary>
    /// The spoken form: a phrase naming an item — "move buy limpets to the top" — or nothing at all,
    /// which means the selected line.
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
    /// The list in the order the Commander cares about (Phase 42): their project order, then what can
    /// be done now, where they are standing.
    /// </summary>
    public IReadOnlyList<ChecklistItem> Arranged() => ChecklistOrdering.Arrange(Document, State);

    /// <summary>The projects in that same order, for the panel's chooser.</summary>
    public IReadOnlyList<ChecklistProject> Projects() => ChecklistOrdering.Projects(Document, State);

    /// <summary>Moves a whole project in the Commander's order (Phase 42).</summary>
    public ChecklistChange Rank(ChecklistScope scope, ChecklistMove move) =>
        list.Apply(Fid, Name, document => ChecklistOrdering.Rank(document, State, scope, move));

    /// <summary>
    /// The spoken form: a phrase naming a project — "move the Sol project up" — or nothing at all,
    /// which means the project of the selected line.
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

    /// <summary>Rewords a line the Commander wrote (remediation.md 10, item 13).</summary>
    public ChecklistChange Reword(ChecklistItemId id, string text) =>
        list.Apply(Fid, Name, document => document.Reword(id, text));

    /// <summary>
    /// This Commander's whole checklist as JSON — every line, derived ones and tombstones included,
    /// with their provenance (remediation.md 10, item 15).
    /// </summary>
    public string Export() =>
        System.Text.Json.JsonSerializer.Serialize(Document, ChecklistStore.Json);

    /// <summary>Replaces this Commander's checklist with an exported one (remediation.md 10, item 15).</summary>
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

        // The project order travels with the list (Phase 42): it is part of what the export means, and an
        // import that kept the items and dropped the ranking would arrive subtly different from what left —
        // the one thing a round trip must not do.
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

    /// <summary>Accepts everything waiting.</summary>
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
    /// Several outcomes as one answer, with anything said twice said once (remediation.md 11, item 1).
    /// </summary>
    private static string Once(IEnumerable<string> said) =>
        string.Join(
            " ",
            said.Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal));

    /// <summary>
    /// The one line about a proposal the Commander has not answered, or null when there is none
    /// (remediation.md 10, item 10).
    /// </summary>
    public string? Standing()
    {
        var waiting = proposals.PendingFor(Fid);

        if (waiting.Count == 0)
        {
            return null;
        }

        var told = string.Equals(Queue(waiting), _standingSaidFor, StringComparison.Ordinal)
            ? _standingSaid
            : 0;

        if (told >= Quiet)
        {
            return null;
        }

        if (told > 0)
        {
            // The clause.
            return waiting.Count == 1
                ? "One proposal is still waiting on you. Say \"accept\" or \"decline\"."
                : $"{waiting.Count} proposals are still waiting on you. Say \"accept\" or \"decline\".";
        }

        return waiting.Count == 1
            ? $"Still waiting on you: {waiting[0].Summary.TrimEnd('.')}. "
              + "Say \"accept\" or \"decline\"."
            : $"Still waiting on you: {waiting.Count} proposals, including "
              + $"{waiting[0].Summary.TrimEnd('.')}. Say \"accept\" or \"decline\".";
    }

    /// <summary>
    /// How many turns in a row the same standing business is mentioned before it goes quiet, and which
    /// of those said it in full.
    /// </summary>
    private const int Quiet = 3;

    /// <summary>Which proposals were waiting, so a different set is a different question.</summary>
    private static string Queue(IEnumerable<ChecklistProposal> waiting) =>
        string.Join("\n", waiting.Select(proposal => proposal.Id));

    private string _standingSaidFor = string.Empty;

    private int _standingSaid;

    /// <summary>
    /// Told that <see cref="Standing"/>'s line was actually spoken, which is what advances the decay
    /// (#154).
    /// </summary>
    public void SaidStanding()
    {
        var queue = Queue(proposals.PendingFor(Fid));

        if (!string.Equals(queue, _standingSaidFor, StringComparison.Ordinal))
        {
            _standingSaidFor = queue;
            _standingSaid = 0;
        }

        _standingSaid++;
    }

    public string Decline(string? id = null)
    {
        var taken = TakeOne(id);

        return taken.Count == 0
            ? "There is nothing waiting for you to decline."
            : $"Dropped {taken.Count} proposal{(taken.Count == 1 ? string.Empty : "s")}.";
    }

    /// <summary>The named proposal, or every one waiting.</summary>
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
    /// Adds every line a proposal carried, one at a time so each gets its own minted key — and reports
    /// what each one did rather than only the last.
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

    /// <summary>Records a proposal to add lines.</summary>
    /// <param name="goal">The arc that asked for these lines, where one did (Phase 34).</param>
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

    /// <summary>Proposes that a line is finished, re-opened or gone.</summary>
    public string ProposeChange(string phrase, ProposalKind change)
    {
        if (Document.Match(phrase) is not { } item)
        {
            return $"I could not tell which item \"{phrase}\" means.";
        }

        if (!item.TicksByHand)
        {
            // Observing rather than asserting.
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
    /// Proposes what a plan should say about one subject — one slot, one place — and leaves everything
    /// else the plan says alone.
    /// </summary>
    /// <param name="replacing">The subjects this proposal has an opinion about.</param>
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

        // The count, said out loud.
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

        // The Commander's word for the ship where the caller has one, and the scope's own "ship 53" only
        // where it does not (remediation.md 15, item 12, thread B).
        var whose = describing is { Length: > 0 } named ? named : scope.ToString();

        return Record(new ChecklistProposal
        {
            Id = "pending",
            CommanderFid = Fid,
            Kind = ProposalKind.Plan,
            Scope = scope,
            Source = source,
            Items = wanted,
            Summary = Summarising(whose, items, replacing),
        });
    }

    /// <summary>What a plan proposal says, as against what it contains (#154).</summary>
    private string Summarising(
        string whose,
        IReadOnlyList<ChecklistItem> items,
        IReadOnlyCollection<string> replacing)
    {
        // Through the wording layer, which is the thing that knows Slot05_Size5 is a Class 5 Compartment on
        // this hull and what is fitted in it.
        var subjects = replacing.Select(subject => Readable(subject, items)).ToList();

        var slots = subjects.Count == 1 ? "one slot" : $"{Count(subjects.Count)} slots";

        // Named while naming them is both possible and useful.
        var named = subjects.Count <= 3 && subjects.All(subject => subject is not null)
            ? string.Join(", ", subjects)
            : null;

        if (items.Count == 0)
        {
            return named is null
                ? Fitting($"Drop {slots} from the {whose} plan")
                : Fitting(
                    $"Drop {named} from the {whose} plan",
                    $"Drop {slots} from the {whose} plan");
        }

        var wanted = items.Select(item => item.Text).ToList();
        var gist = wanted.Count switch
        {
            1 => wanted[0],
            2 => $"{wanted[0]} and {wanted[1]}",
            _ => $"{wanted[0]}, {wanted[1]} and {Count(wanted.Count - 2)} more",
        };

        return Fitting(
            named is null ? $"Set {slots} on the {whose} plan: {string.Join("; ", wanted)}"
                          : $"Set the {whose} plan's {named} to {string.Join("; ", wanted)}",
            $"Set {slots} on the {whose} plan: {string.Join("; ", wanted)}",
            $"Set {slots} on the {whose} plan: {gist}",
            $"Set {slots} on the {whose} plan");
    }

    /// <summary>
    /// One subject as it should be said: through <see cref="ChecklistWording"/> where an item in this
    /// proposal is about it, and as written where none is — an engineer's name, most often, which is
    /// already the way a Commander says it.
    /// </summary>
    private string? Readable(string subject, IReadOnlyList<ChecklistItem> items)
    {
        var about = items.FirstOrDefault(item =>
            item.Intent is { } intent
            && string.Equals(intent.Subject, subject, StringComparison.OrdinalIgnoreCase));

        return about is not null
            ? ChecklistWording.Subject(about, State)
            : ChecklistWording.Subject(subject, items.Select(item => item.Hull).FirstOrDefault());
    }

    /// <summary>
    /// A small number in words, because these are read aloud and "6 slots" is not how anybody says it.
    /// </summary>
    private static string Count(int many) => many switch
    {
        1 => "one",
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        7 => "seven",
        8 => "eight",
        9 => "nine",
        10 => "ten",
        11 => "eleven",
        12 => "twelve",
        _ => many.ToString(CultureInfo.InvariantCulture),
    };

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

    /// <summary>
    /// The longest wording in the ladder that fits, which is what "composed to fit rather than
    /// truncated to fit" means in code (#154).
    /// </summary>
    private static string Fitting(params string[] ladder) =>
        Array.Find(ladder, said => said.Length <= ChecklistLimits.MaxTextLength) ?? Trim(ladder[^1]);

    /// <summary>The backstop, and it stops at a word (#154).</summary>
    private static string Trim(string text)
    {
        if (text.Length <= ChecklistLimits.MaxTextLength)
        {
            return text;
        }

        const string Ellipsis = "…";

        var cut = text[..(ChecklistLimits.MaxTextLength - Ellipsis.Length)];
        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > 0 ? cut[..lastSpace] : cut).TrimEnd(' ', ',', ';', ':') + Ellipsis;
    }
}

/// <summary>One way of narrowing the checklist, and the question it answers.</summary>
/// <param name="Key">
/// What the panel matches against — an enum's own spelling, a scope word, or a state.
/// </param>
/// <param name="Word">How it reads to a Commander.</param>
/// <param name="Heading">The question it is an answer to.</param>
public sealed record ChecklistFilter(string Key, string Word, string Heading);
