namespace D47.Core.Checklists;

/// <summary>
/// The result of asking a document to change: the document afterwards, whether anything actually
/// moved, and the sentence explaining it.
/// <para>
/// The sentence is here rather than at each call site because the panel and the voice path have
/// to give the same answer. A refusal a Commander hears one way from the panel and another way
/// out loud is two behaviours to keep in step, and one of them will drift.
/// </para>
/// </summary>
public sealed record ChecklistChange(ChecklistDocument Document, bool Changed, string Report)
{
    public static ChecklistChange Refused(ChecklistDocument document, string why) =>
        new(document, Changed: false, why);
}

/// <summary>
/// One Commander's whole checklist — universal, per ship and per system, authored and derived,
/// live and tombstoned (list.md Phase 17).
/// <para>
/// <b>The Commander's key lives inside the document, never in a path.</b> The Frontier id comes
/// out of the journal, and journal content is untrusted input; turning it into a filename would
/// buy a path-traversal surface for an organisational convenience.
/// </para>
/// <para>
/// Immutable, and every operation returns a new one. That is what lets the store validate what it
/// is about to write rather than what somebody hoped they had written, and it is the same shape
/// every folded journal state in this repo already has.
/// </para>
/// </summary>
public sealed record ChecklistDocument
{
    public required string CommanderFid { get; init; }

    /// <summary>The name at the time of writing, for a person reading the file. Never the key.</summary>
    public string? CommanderName { get; init; }

    public IReadOnlyList<ChecklistItem> Items { get; init; } = [];

    public static ChecklistDocument For(string fid, string? name = null) =>
        new() { CommanderFid = fid, CommanderName = name };

    /// <summary>Everything still live in a scope, tombstones excluded, in the order it was added.</summary>
    public IReadOnlyList<ChecklistItem> In(ChecklistScope scope) =>
        [.. Items.Where(item => item.IsLive && item.Scope.Same(scope))];

    /// <summary>Every scope that has anything in it, live or not. What the filter row is built from.</summary>
    public IReadOnlyList<ChecklistScope> Scopes =>
        [.. Items.Select(item => item.Scope).Distinct()];

    public ChecklistItem? Find(ChecklistItemId id) =>
        Items.FirstOrDefault(item => item.Id.Same(id));

    /// <summary>
    /// The one live item whose wording or key a Commander's phrase names, or null when that is
    /// nought or several. Null on several rather than the first match, because acting on the
    /// wrong item of two is worse than asking which.
    /// </summary>
    public ChecklistItem? Match(string phrase, ChecklistScope? within = null)
    {
        var wanted = ChecklistKeys.Compact(phrase);

        if (wanted.Length == 0)
        {
            return null;
        }

        var candidates = Items
            .Where(item => item.IsLive && (within is null || item.Scope.Same(within)))
            .ToList();

        var exact = candidates
            .Where(item => string.Equals(item.Key, phrase.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count == 1)
        {
            return exact[0];
        }

        var byText = candidates
            .Where(item => ChecklistKeys.Compact(item.Text) == wanted)
            .ToList();

        if (byText.Count == 1)
        {
            return byText[0];
        }

        var containing = candidates
            .Where(item => ChecklistKeys.Compact(item.Text).Contains(wanted, StringComparison.Ordinal))
            .ToList();

        return containing.Count == 1 ? containing[0] : null;
    }

    /// <summary>Adds an authored line. The Commander's own words, attributed to them.</summary>
    /// <param name="goal">
    /// The arc that asked for it, where one did (list.md Phase 34). Carried here rather than only
    /// on the proposal, because a proposal is accepted into a <em>new</em> item and provenance a
    /// Commander loses on the way onto their own list is provenance they never had.
    /// </param>
    public ChecklistChange AddNote(ChecklistScope scope, string text, string? goal = null)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return ChecklistChange.Refused(this, "There was nothing to add.");
        }

        if (trimmed.Length > ChecklistLimits.MaxTextLength)
        {
            return ChecklistChange.Refused(
                this,
                $"That is {trimmed.Length} characters; a checklist line is at most {ChecklistLimits.MaxTextLength}.");
        }

        if (In(scope).Count >= ChecklistLimits.MaxLiveItemsPerScope)
        {
            return ChecklistChange.Refused(
                this,
                $"That list already holds {ChecklistLimits.MaxLiveItemsPerScope} open items, which is the most.");
        }

        var item = new ChecklistItem
        {
            Key = ChecklistKeys.Note(Items.Where(existing => existing.Scope.Same(scope))),
            Scope = scope,
            Kind = ChecklistItemKind.Authored,
            Text = trimmed,
            Provenance = ChecklistProvenance.Attributed,
            Goal = goal,
        };

        return new ChecklistChange(
            this with { Items = [.. Items, item] },
            Changed: true,
            $"Added \"{trimmed}\" to the {scope} list.");
    }

    /// <summary>
    /// Ticks an item. <b>Refuses a derived one, and says why</b> — the next journal read would
    /// either undo it or, worse, leave it standing and lying.
    /// </summary>
    public ChecklistChange Complete(ChecklistItemId id) => SetTicked(id, ticked: true);

    /// <summary>Un-ticks an item. Same refusal, same reason.</summary>
    public ChecklistChange Uncomplete(ChecklistItemId id) => SetTicked(id, ticked: false);

    private ChecklistChange SetTicked(ChecklistItemId id, bool ticked)
    {
        if (Find(id) is not { } item)
        {
            return ChecklistChange.Refused(this, "There is no such item on your checklist.");
        }

        if (!item.TicksByHand)
        {
            return ChecklistChange.Refused(
                this,
                $"\"{item.Text}\" is worked out from your journal rather than ticked. "
                + "If I marked it by hand the next read would either undo it or leave it standing and wrong.");
        }

        if (item.IsComplete == ticked)
        {
            return ChecklistChange.Refused(
                this, $"\"{item.Text}\" is already {(ticked ? "done" : "open")}.");
        }

        var updated = item with { State = ticked ? ChecklistState.Done : ChecklistState.Open };

        return new ChecklistChange(
            Replace(updated),
            Changed: true,
            ticked ? $"Marked \"{item.Text}\" done." : $"Re-opened \"{item.Text}\".");
    }

    /// <summary>
    /// Removes an item outright. <b>A different act from completing</b>, and it can happen to an
    /// item whether or not it was ever finished — deleting is changing your mind.
    /// <para>
    /// A derived item is not deleted here: it belongs to a plan, and the way to be rid of it is
    /// to revise the plan, which leaves the tombstone that answers "why did you stop tracking the
    /// shield boosters" three weeks later.
    /// </para>
    /// </summary>
    public ChecklistChange Delete(ChecklistItemId id)
    {
        if (Find(id) is not { } item)
        {
            return ChecklistChange.Refused(this, "There is no such item on your checklist.");
        }

        if (item.Kind == ChecklistItemKind.Derived)
        {
            return ChecklistChange.Refused(
                this,
                $"\"{item.Text}\" came from a plan, so revising the plan is what drops it. "
                + "That leaves a note saying it was dropped, which deleting would not.");
        }

        return new ChecklistChange(
            this with { Items = [.. Items.Where(other => !other.Id.Same(id))] },
            Changed: true,
            $"Removed \"{item.Text}\".");
    }

    /// <summary>
    /// Moves an item up or down the Commander's own order (list.md Phase 25, "The checklist
    /// leaves its window").
    /// <para>
    /// <b>The order is one order, across every scope.</b> What gets reordered on a whim is
    /// everything being worked on, not one ship's share of it — so scope is a label and a filter
    /// and never a partition, and moving up means past the item above it whatever list that item
    /// would have been in.
    /// </para>
    /// <para>
    /// <b>Over the live items only.</b> Tombstones sit in <see cref="Items"/> so a history
    /// survives, and stepping over them would make one press move an item by nothing visible —
    /// which reads as the control being broken. Completed items count as live and are stepped
    /// over normally: they are still on the list, below the line.
    /// </para>
    /// <para>
    /// A step rather than a drag. <b>A drag is the worst gesture available to a ray at a metre</b>
    /// — it needs a press, an aim held through motion, and a release, any one of which a hand at
    /// arm's length gets wrong — and the spoken form of a drag does not exist at all, where the
    /// spoken form of "move that up" is the phrase itself.
    /// </para>
    /// </summary>
    /// <param name="by">How far, and which way. Negative is towards the top.</param>
    public ChecklistChange Move(ChecklistItemId id, int by)
    {
        if (Find(id) is not { } item)
        {
            return ChecklistChange.Refused(this, "There is no such item on your checklist.");
        }

        if (!item.IsLive)
        {
            return ChecklistChange.Refused(
                this,
                $"\"{item.Text}\" was dropped by a later version of a plan, so it is a record rather "
                + "than something to work on.");
        }

        if (by == 0)
        {
            return ChecklistChange.Refused(this, $"\"{item.Text}\" is already where it is.");
        }

        var live = Items.Where(other => other.IsLive).ToList();
        var from = live.FindIndex(other => other.Id.Same(id));
        var to = Math.Clamp(from + by, 0, live.Count - 1);

        if (to == from)
        {
            return ChecklistChange.Refused(
                this,
                by < 0
                    ? $"\"{item.Text}\" is already at the top."
                    : $"\"{item.Text}\" is already at the bottom.");
        }

        live.RemoveAt(from);
        live.Insert(to, item);

        // Rebuilt by walking the original list and drawing a live item from the reordered
        // sequence each time one is reached. That keeps every tombstone exactly where it was,
        // which is what makes this a reordering of the Commander's work rather than of their
        // history.
        var reordered = new List<ChecklistItem>(Items.Count);
        var next = 0;

        foreach (var existing in Items)
        {
            reordered.Add(existing.IsLive ? live[next++] : existing);
        }

        return new ChecklistChange(
            this with { Items = reordered },
            Changed: true,
            by < 0 ? $"Moved \"{item.Text}\" up." : $"Moved \"{item.Text}\" down.");
    }

    /// <summary>
    /// Replaces one plan's items in one scope with a new set — <b>as a diff, never as a rebuild</b>
    /// (list.md Phase 17).
    /// <list type="bullet">
    /// <item>Present in both: kept exactly as it is, history and all.</item>
    /// <item>Dropped: tombstoned — <see cref="ChecklistTombstone.Superseded"/> when it was done,
    /// <see cref="ChecklistTombstone.Abandoned"/> when it was not.</item>
    /// <item>Added: opened — unless a tombstone with that key is already there, in which case it
    /// comes back carrying what it had.</item>
    /// </list>
    /// <para>
    /// Rebuilding instead would wipe a fortnight of progress the first time somebody changed one
    /// weapon, which quietly kills the whole reason completed items are kept.
    /// </para>
    /// </summary>
    /// <param name="scope">The list being revised.</param>
    /// <param name="source">
    /// Which plan is revising. Scoped, because a system can hold a colonisation plan and a
    /// Commander's own notes at once and revising one must not touch the other.
    /// </param>
    /// <param name="wanted">The new plan's items, already keyed.</param>
    public ChecklistChange Revise(
        ChecklistScope scope,
        ChecklistSource source,
        IReadOnlyList<ChecklistItem> wanted)
    {
        if (source == ChecklistSource.Commander)
        {
            return ChecklistChange.Refused(this, "A Commander's own notes are not a plan and are not revised as one.");
        }

        if (wanted.Count > ChecklistLimits.MaxLiveItemsPerScope)
        {
            return ChecklistChange.Refused(
                this,
                $"That plan has {wanted.Count} items; the most one list holds is "
                + $"{ChecklistLimits.MaxLiveItemsPerScope}.");
        }

        var mine = Items
            .Where(item => item.Scope.Same(scope) && item.Source == source)
            .ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

        var kept = new List<ChecklistItem>();
        var opened = new List<ChecklistItem>();
        var revived = new List<ChecklistItem>();

        foreach (var item in wanted)
        {
            if (!mine.TryGetValue(item.Key, out var existing))
            {
                opened.Add(item);
                continue;
            }

            // The wording is refreshed and nothing else is. A plan restated in better words is
            // still the same plan, and the state is the part that took a fortnight to earn.
            var carried = existing with { Text = item.Text, Intent = item.Intent, Hull = item.Hull };

            if (existing.IsLive)
            {
                kept.Add(carried);
            }
            else
            {
                revived.Add(carried with { Tombstone = ChecklistTombstone.None });
            }
        }

        var wantedKeys = wanted.Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dropped = mine.Values
            .Where(item => item.IsLive && !wantedKeys.Contains(item.Key))
            .Select(item => item with
            {
                Tombstone = item.IsComplete ? ChecklistTombstone.Superseded : ChecklistTombstone.Abandoned,
            })
            .ToList();

        var untouched = Items
            .Where(item => !(item.Scope.Same(scope) && item.Source == source))
            .ToList();

        var alreadyGone = mine.Values
            .Where(item => !item.IsLive && !wantedKeys.Contains(item.Key))
            .ToList();

        var document = this with
        {
            Items = [.. untouched, .. kept, .. revived, .. opened, .. dropped, .. alreadyGone],
        };

        var moved = opened.Count + revived.Count + dropped.Count;

        return new ChecklistChange(document, moved > 0, Report(kept.Count, opened, revived, dropped));
    }

    private static string Report(
        int kept,
        IReadOnlyList<ChecklistItem> opened,
        IReadOnlyList<ChecklistItem> revived,
        IReadOnlyList<ChecklistItem> dropped)
    {
        if (opened.Count == 0 && revived.Count == 0 && dropped.Count == 0)
        {
            return kept == 0
                ? "That plan is empty, so nothing changed."
                : $"Nothing changed — all {kept} items were already on the list.";
        }

        var parts = new List<string>();

        if (opened.Count > 0)
        {
            parts.Add($"{opened.Count} new");
        }

        if (revived.Count > 0)
        {
            parts.Add($"{revived.Count} brought back");
        }

        if (kept > 0)
        {
            parts.Add($"{kept} kept with what they had done");
        }

        // Named rather than counted, and the two reasons kept apart. "Done, then superseded" is
        // the case that decides whether the history tells the truth.
        var superseded = dropped.Where(item => item.Tombstone == ChecklistTombstone.Superseded).ToList();
        var abandoned = dropped.Where(item => item.Tombstone == ChecklistTombstone.Abandoned).ToList();

        if (abandoned.Count > 0)
        {
            parts.Add($"{abandoned.Count} dropped");
        }

        if (superseded.Count > 0)
        {
            parts.Add(
                $"{superseded.Count} done and then designed out ({string.Join("; ", superseded.Select(item => item.Text))})");
        }

        return "Plan revised: " + string.Join(", ", parts) + ".";
    }

    private ChecklistDocument Replace(ChecklistItem item) =>
        this with { Items = [.. Items.Select(other => other.Id.Same(item.Id) ? item : other)] };

    /// <summary>Puts a recomputed state back, which only the evaluator does.</summary>
    internal ChecklistDocument WithState(ChecklistItem item) => Replace(item);
}

/// <summary>
/// The bounds a checklist has, and why it has any.
/// <para>
/// Not technical limits. A checklist is the one store in this repo an untrusted path can grow —
/// a hostile in-game message reaching the model can attempt a proposal, and a proposal the
/// Commander accepts by mistake should be a line, not a novel. These bound the blast radius of
/// that mistake to something a person can read and undo.
/// </para>
/// </summary>
public static class ChecklistLimits
{
    public const int MaxTextLength = 200;

    public const int MaxLiveItemsPerScope = 200;

    /// <summary>How many proposals may be outstanding before new ones are refused.</summary>
    public const int MaxPendingProposals = 20;
}
