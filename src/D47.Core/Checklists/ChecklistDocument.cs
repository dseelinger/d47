namespace D47.Core.Checklists;

/// <summary>
/// The result of asking a document to change: the document afterwards, whether anything actually moved,
/// and the sentence explaining it.
/// </summary>
public sealed record ChecklistChange(ChecklistDocument Document, bool Changed, string Report)
{
    /// <summary>Which item the change was about, where it was about one.</summary>
    public ChecklistItemId? Subject { get; init; }

    public static ChecklistChange Refused(ChecklistDocument document, string why) =>
        new(document, Changed: false, why);
}

/// <summary>Where a reorder puts an item.</summary>
public enum ChecklistMove
{
    Up,
    Down,
    Top,
    Bottom,
}

/// <summary>
/// One Commander's whole checklist — universal, per ship and per system, authored and derived, live and
/// tombstoned (Phase 17).
/// </summary>
public sealed record ChecklistDocument
{
    public required string CommanderFid { get; init; }

    /// <summary>The name at the time of writing, for a person reading the file.</summary>
    public string? CommanderName { get; init; }

    public IReadOnlyList<ChecklistItem> Items { get; init; } = [];

    /// <summary>
    /// The Commander's order between projects (Phase 42), as project keys — see <see
    /// cref="ChecklistOrdering.Key"/>.
    /// </summary>
    public IReadOnlyList<string> ProjectOrder { get; init; } = [];

    public static ChecklistDocument For(string fid, string? name = null) =>
        new() { CommanderFid = fid, CommanderName = name };

    /// <summary>Everything still live in a scope, tombstones excluded, in the order it was added.</summary>
    public IReadOnlyList<ChecklistItem> In(ChecklistScope scope) =>
        [.. Items.Where(item => item.IsLive && item.Scope.Same(scope))];

    /// <summary>Every scope that has anything in it, live or not.</summary>
    public IReadOnlyList<ChecklistScope> Scopes =>
        [.. Items.Select(item => item.Scope).Distinct()];

    public ChecklistItem? Find(ChecklistItemId id) =>
        Items.FirstOrDefault(item => item.Id.Same(id));

    /// <summary>
    /// The one live item whose wording or key a Commander's phrase names, or null when that is nought
    /// or several.
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

    /// <summary>Adds an authored line.</summary>
    /// <param name="goal">The arc that asked for it, where one did (Phase 34).</param>
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
            $"Added \"{trimmed}\" to the {scope} list.")
        {
            Subject = item.Id,
        };
    }

    /// <summary>Ticks an item.</summary>
    public ChecklistChange Complete(ChecklistItemId id) => SetTicked(id, ticked: true);

    /// <summary>Un-ticks an item.</summary>
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

    /// <summary>Rewords a line the Commander wrote (remediation.md 10, item 13).</summary>
    public ChecklistChange Reword(ChecklistItemId id, string text)
    {
        if (Find(id) is not { } item)
        {
            return ChecklistChange.Refused(this, "There is no such item on your checklist.");
        }

        if (item.Kind != ChecklistItemKind.Authored)
        {
            return ChecklistChange.Refused(
                this,
                $"\"{item.Text}\" came from a plan, so its wording is the plan's. "
                + "Revising the plan is what changes it.");
        }

        var wanted = text?.Trim() ?? string.Empty;

        if (wanted.Length == 0)
        {
            return ChecklistChange.Refused(this, "A line with nothing written on it is not a line.");
        }

        if (wanted.Length > ChecklistLimits.MaxTextLength)
        {
            return ChecklistChange.Refused(
                this,
                $"That is {wanted.Length} characters; the most is {ChecklistLimits.MaxTextLength}.");
        }

        if (string.Equals(wanted, item.Text, StringComparison.Ordinal))
        {
            return ChecklistChange.Refused(this, "That is what it already says.");
        }

        return new ChecklistChange(
            Replace(item with { Text = wanted }),
            Changed: true,
            $"Now reads \"{wanted}\".");
    }

    /// <summary>Removes an item outright.</summary>
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
    /// Moves an item up or down the Commander's own order (Phase 25, "The checklist leaves its
    /// window").
    /// </summary>
    /// <param name="by">How far, and which way.</param>
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

        // Rebuilt by walking the original list and drawing a live item from the reordered sequence each time
        // one is reached.
        var reordered = new List<ChecklistItem>(Items.Count);
        var next = 0;

        foreach (var existing in Items)
        {
            reordered.Add(existing.IsLive ? live[next++] : existing);
        }

        return new ChecklistChange(
            this with { Items = reordered },
            Changed: true,
            by < 0 ? $"Moved \"{item.Text}\" up." : $"Moved \"{item.Text}\" down.")
        {
            Subject = item.Id,
        };
    }

    /// <summary>
    /// The same reorder said the way a Commander says it, one end included (reported 2026-08-21: "There
    /// should be a 'Move to Top', 'Move to Bottom' voice and UI controls, as well as 'Move Up', and
    /// 'Move down'").
    /// </summary>
    public ChecklistChange Move(ChecklistItemId id, ChecklistMove move)
    {
        if (move is ChecklistMove.Up or ChecklistMove.Down)
        {
            return Move(id, move is ChecklistMove.Up ? -1 : 1);
        }

        var live = Items.Where(other => other.IsLive).ToList();
        var from = live.FindIndex(other => other.Id.Same(id));
        var toTop = move is ChecklistMove.Top;

        // A step that would be nought — including the item not being here at all, where `from` is -1 and
        // there is nothing to compute — falls back to one, so the refusals below are the ones Move already
        // writes rather than a second set saying the same things differently.
        var by = from < 0 ? 0 : toTop ? -from : live.Count - 1 - from;
        var change = Move(id, by == 0 ? (toTop ? -1 : 1) : by);

        return change.Changed && Find(id) is { } item
            ? change with { Report = $"Moved \"{item.Text}\" to the {(toTop ? "top" : "bottom")}." }
            : change;
    }

    /// <summary>
    /// Replaces one plan's items in one scope with a new set — as a diff, never as a rebuild (Phase
    /// 17).
    /// </summary>
    /// <param name="scope">The list being revised.</param>
    /// <param name="source">Which plan is revising.</param>
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

            // The wording is refreshed and nothing else is.
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

        // Named rather than counted, and the two reasons kept apart. "Done, then superseded" is the case that
        // decides whether the history tells the truth.
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

/// <summary>The bounds a checklist has, and why it has any.</summary>
public static class ChecklistLimits
{
    public const int MaxTextLength = 200;

    public const int MaxLiveItemsPerScope = 200;

    /// <summary>How many proposals may be outstanding before new ones are refused.</summary>
    public const int MaxPendingProposals = 20;
}
