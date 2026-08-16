using D47.Core.Checklists;

namespace D47.Core.Callouts;

/// <summary>
/// Speaking up about the checklist (list.md Phase 17).
/// <para>
/// Two things, and both of them are moments rather than questions.
/// </para>
/// <para>
/// <b>A computed tick going backwards is information.</b> Selling the engineered multi-cannon
/// un-completes an item, and d47 says so <em>once</em> — the recomputed verdict is written down as
/// it is announced, so the next tick finds nothing left to say. Hiding it would be worse than
/// saying it: the Commander would find out by reading a list that had quietly changed under them.
/// </para>
/// <para>
/// <b>And picking up the last unit a plan needed is a moment too.</b> That half of Phase 14's
/// "Go and get it" was deferred here because it needs a plan to exist before there is a last unit
/// to pick up. It is netted across <em>every</em> live plan, because caps are shared and a
/// shopping trip is a trip for everything.
/// </para>
/// </summary>
public sealed class ChecklistCallout(ChecklistService checklists) : ICallout
{
    public string Id => "checklist";

    /// <summary>
    /// Materials whose shortfall has already been announced as cleared. Dropped again the moment
    /// the shortfall comes back, so a plan revised upwards can announce the same material twice
    /// for two different reasons rather than falling silent for the session.
    /// </summary>
    private readonly HashSet<string> _filled = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            // Primed silently. The polling itself happens on the tick loop rather than here, so
            // that switching this callout off silences d47 without also freezing the panel.
            Prime(context);
            yield break;
        }

        foreach (var item in checklists.Drain())
        {
            yield return new Announcement(item.Key, item.Text)
            {
                Cooldown = TimeSpan.FromMinutes(1),
            };
        }

        if (!context.Events.Any(journalEvent => journalEvent.Kind is "MaterialCollected" or "MaterialTrade"))
        {
            yield break;
        }

        foreach (var announcement in Gathered(context))
        {
            yield return announcement;
        }
    }

    private IEnumerable<Announcement> Gathered(CalloutContext context)
    {
        var costing = EngineeringPlan.Cost(checklists.Document.Items, context.State);

        foreach (var ingredient in costing.Ingredients)
        {
            if (ingredient.Short > 0)
            {
                _filled.Remove(ingredient.Material.Symbol);
                continue;
            }

            if (!_filled.Add(ingredient.Material.Symbol))
            {
                continue;
            }

            yield return new Announcement(
                "checklist.filled." + ingredient.Material.Symbol,
                $"That is the last {ingredient.Material.Name} your plans needed. "
                + $"{ingredient.Held} of {ingredient.Needed}.");
        }
    }

    private void Prime(CalloutContext context)
    {
        foreach (var ingredient in EngineeringPlan.Cost(checklists.Document.Items, context.State).Ingredients)
        {
            if (ingredient.Short == 0)
            {
                _filled.Add(ingredient.Material.Symbol);
            }
        }
    }
}
