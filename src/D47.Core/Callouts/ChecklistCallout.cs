using D47.Core.Checklists;

namespace D47.Core.Callouts;

/// <summary>Speaking up about the checklist (Phase 17).</summary>
public sealed class ChecklistCallout(ChecklistService checklists) : ICallout
{
    public string Id => "checklist";

    /// <summary>Materials whose shortfall has already been announced as cleared.</summary>
    private readonly HashSet<string> _filled = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            // Primed silently.
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
