using D47.Core.Checklists;
using D47.Core.Journal;

namespace D47.Core.Ships;

/// <summary>
/// Asks, once, when the ship the Commander has just boarded carries a build their checklist does
/// not (Phase 38, "Ask before the plan and the checklist drift apart").
/// <para>
/// <b>The moment of boarding is the moment to say so.</b> A plan and a checklist fall out of step
/// in both directions — a roll gets done in game and the line is still sitting there, or a plan is
/// changed afterwards and the line still asks for the old grade — and the Commander finds out
/// weeks later, at an engineer, with the wrong materials.
/// </para>
/// <para>
/// <b>It asks and never acts.</b> The question goes through <see cref="ChecklistService.ProposePlan"/>,
/// which is the accept-or-decline boundary the model writes across — so this can no more rewrite a
/// list than a hostile in-game message can. The Commander's yes is what applies it, and applying it
/// is a <see cref="ChecklistDocument.Revise"/>: the ordering they spent an evening on survives,
/// which is the whole reason it is a revision rather than a rebuild.
/// </para>
/// <para>
/// <b>Asking is the difference from the promote button.</b> Pressing <i>Put this build on my
/// checklist</i> is the Commander's own act, so it revises the list there and then; this is d47
/// noticing something unbidden, and Phase 25's rule is that what d47 raises unbidden is a
/// question.
/// </para>
/// <para>
/// <b>No means no, and it is remembered on the build rather than in this object.</b> A session's
/// memory would ask again tomorrow. <see cref="ShipBuild.Settled"/> holds the fingerprint of the
/// difference that was declined, so d47 asks again only when the difference itself changes — which
/// is a new question rather than the same one repeated.
/// </para>
/// </summary>
public sealed class ShipDriftWatch(ShipPlanService ships, ChecklistService checklists)
{
    /// <summary>The last ship seen, so a <c>Loadout</c> for the same one is not a swap.</summary>
    private int? _aboard;

    /// <summary>What was asked about, and for which ship, while the answer is outstanding.</summary>
    private (int Ship, string Fingerprint)? _asked;

    /// <summary>
    /// Forgets the last ship seen, so the new Commander's first <c>Loadout</c> is a swap and is
    /// compared (Phase 44). Ship ids are per Commander, so without this two Commanders
    /// both in ship 7 read as the same ship and the new one's build is never checked. A question
    /// outstanding for the old Commander is dropped with it: the answer would be theirs, not the
    /// new one's.
    /// </summary>
    public void Reset()
    {
        _aboard = null;
        _asked = null;
    }

    /// <summary>
    /// Folds this tick's events and answers with the question to ask out loud, or null.
    /// <para>
    /// <b>On a ship swap only.</b> A <c>Loadout</c> arrives on every re-outfit and every dock, and
    /// asking on each of those would be a question every few minutes. Boarding a <em>different</em>
    /// ship is the event this is about.
    /// </para>
    /// </summary>
    public string? Observe(IEnumerable<JournalEvent> events)
    {
        Settle();

        int? boarded = null;

        foreach (var journalEvent in events)
        {
            if (journalEvent.Kind == "Loadout" && journalEvent.Int("ShipID") is { } id)
            {
                boarded = id;
            }
        }

        if (boarded is not { } ship)
        {
            return null;
        }

        var swapped = _aboard != ship;

        _aboard = ship;

        // The first Loadout of a session is not a swap the Commander made — d47 has simply started
        // up beside a ship they were already in — but it is still the first time the two lists can
        // be compared, and the difference is as real then as later. So it asks, once.
        return swapped ? Ask(ship) : null;
    }

    /// <summary>
    /// Raises the question for one ship, or answers null where there is nothing to ask about.
    /// </summary>
    private string? Ask(int ship)
    {
        if (ships.ForShip(ship) is not { } build
            || build.Scope is not { } scope
            || Drift(build) is not { } drift)
        {
            return null;
        }

        // Already declined, and the difference has not moved since. The Commander's no stands.
        if (string.Equals(build.Settled, drift.Fingerprint, StringComparison.Ordinal))
        {
            return null;
        }

        // Something is already waiting on them for this ship — a question from a previous
        // boarding, or a plan proposed some other way. One question at a time.
        if (checklists.Proposals.PendingFor(checklists.Document.CommanderFid)
            .Any(proposal => proposal.Scope.Same(scope)))
        {
            return null;
        }

        var said = checklists.ProposePlan(
            scope,
            ChecklistSource.EngineeringPlan,
            drift.Items,
            drift.Slots,
            build.Describe());

        _asked = (ship, drift.Fingerprint);

        return $"You are in {build.Describe()}, and its build carries engineering your checklist "
               + $"has not got. {said}";
    }

    /// <summary>
    /// Records a no.
    /// <para>
    /// <b>Declining is a disappearance, not an event.</b> A proposal that is no longer waiting was
    /// either accepted — in which case the difference is gone and the fingerprint would not match
    /// anything anyway — or declined. So the test is exactly that: the question is gone and the
    /// difference is still there, which is a Commander who said no.
    /// </para>
    /// </summary>
    private void Settle()
    {
        if (_asked is not { } asked
            || ships.ForShip(asked.Ship) is not { } build
            || build.Scope is not { } scope)
        {
            _asked = null;
            return;
        }

        if (checklists.Proposals.PendingFor(checklists.Document.CommanderFid)
            .Any(proposal => proposal.Scope.Same(scope)))
        {
            return;
        }

        _asked = null;

        if (Drift(build) is { } drift && string.Equals(drift.Fingerprint, asked.Fingerprint, StringComparison.Ordinal))
        {
            ships.Settle(build.Id, drift.Fingerprint);
        }
    }

    /// <summary>
    /// What the checklist would hold if this build were promoted, and how that differs from what it
    /// holds now — or null where the two agree.
    /// <para>
    /// <b>Compared by key rather than by wording.</b> A key is the slot and the roll; the text is a
    /// sentence about them, and a sentence rewritten in a later release is not a difference the
    /// Commander needs waking up for.
    /// </para>
    /// </summary>
    private Difference? Drift(ShipBuild build)
    {
        if (build.Scope is not { } scope)
        {
            return null;
        }

        var planned = build.Slots
            .Where(slot => slot.Blueprint is not null || slot.Grade > 0 || slot.Experimental is not null)
            .ToList();

        if (planned.Count == 0)
        {
            return null;
        }

        var items = EngineeringPlan.Items(
            scope,
            build.Hull,
            [.. planned.Select(slot => slot.ToRequest())],
            checklists.SlotFor);

        var wanted = items
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var standing = checklists.Document.Items
            .Where(item => item.IsLive
                           && item.Scope.Same(scope)
                           && item.Source == ChecklistSource.EngineeringPlan)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (wanted.SetEquals(standing))
        {
            return null;
        }

        // The fingerprint is both sides of the difference, ordered, so "the plan grew an item" and
        // "the checklist lost one" are different questions even where the wanted set is identical.
        var fingerprint = string.Join(
            "|",
            wanted.Order(StringComparer.Ordinal).Concat(["→"]).Concat(standing.Order(StringComparer.Ordinal)));

        return new Difference(items, [.. build.Slots.Select(slot => slot.Slot)], fingerprint);
    }

    /// <summary>What promotion would put on the list, which slots it speaks for, and the identity of
    /// this particular disagreement.</summary>
    private readonly record struct Difference(
        IReadOnlyList<ChecklistItem> Items,
        IReadOnlyCollection<string> Slots,
        string Fingerprint);
}
