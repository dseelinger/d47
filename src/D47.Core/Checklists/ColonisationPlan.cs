using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>One thing the Commander means to build, and where in the system it goes.</summary>
/// <param name="Place">The body or orbital slot.</param>
/// <param name="Facility">What goes there, in the Commander's words.</param>
public sealed record FacilityRequest(string Place, string Facility);

/// <summary>
/// A system objective turned into the things a Commander has to do (Phase 17, "A colonisation plan
/// writes the checklist").
/// </summary>
public static class ColonisationPlan
{
    /// <summary>What this item is not, said in the plan rather than left for a Commander to discover.</summary>
    public const string WhatThisCannotBe =
        "I cannot tell you what a facility costs, what it will do to the system, or what order to "
        + "build in — nobody publishes those figures in a form I am allowed to ship, so I would be "
        + "inventing them. What I can do is hold the plan and count what the depot says you still owe.";

    /// <summary>The facilities themselves, one item each.</summary>
    public static IReadOnlyList<ChecklistItem> Items(
        ChecklistScope scope,
        IReadOnlyList<FacilityRequest> facilities)
    {
        var items = new List<ChecklistItem>();

        foreach (var facility in facilities)
        {
            if (string.IsNullOrWhiteSpace(facility.Place) || string.IsNullOrWhiteSpace(facility.Facility))
            {
                continue;
            }

            var intent = new ChecklistIntent(ChecklistIntentKind.Facility, facility.Place.Trim())
            {
                Detail = facility.Facility.Trim(),
            };

            items.Add(new ChecklistItem
            {
                Key = ChecklistKeys.For(intent),
                Scope = scope,
                Kind = ChecklistItemKind.Derived,
                Source = ChecklistSource.ColonisationPlan,
                Text = $"{facility.Facility.Trim()} at {facility.Place.Trim()}",
                Intent = intent,

                // Quoted rather than asserted: a facility choice is what a conversation settled on, and no
                // shipped table confirmed it.
                Provenance = ChecklistProvenance.Quoted,
            });
        }

        return [.. items.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(group => group.First())];
    }

    /// <summary>The hauling, taken off a site the Commander has actually visited.</summary>
    public static IReadOnlyList<ChecklistItem> Hauling(ChecklistScope scope, ConstructionSite site)
    {
        var where = site.StationName ?? site.StarSystem ?? site.MarketId.ToString(CultureInfo.InvariantCulture);

        return
        [
            .. site.Resources.Select(resource =>
            {
                var intent = new ChecklistIntent(ChecklistIntentKind.Commodity, where)
                {
                    Detail = resource.Name,

                    // Carried so a person reading the file can see what the site asked for.
                    Quantity = resource.Required,
                };

                return new ChecklistItem
                {
                    Key = ChecklistKeys.For(intent),
                    Scope = scope,
                    Kind = ChecklistItemKind.Derived,
                    Source = ChecklistSource.ColonisationPlan,
                    Text = $"{resource.Name} to {where}",
                    Intent = intent,

                    // Asserted: the site itself said so.
                    Provenance = ChecklistProvenance.Asserted,
                };
            }),
        ];
    }

    /// <summary>
    /// What a Commander still owes across every site in a system, as of their last visit to each.
    /// </summary>
    public static IReadOnlyList<(ConstructionSite Site, ConstructionResource Resource)> Outstanding(
        ColonisationSites sites,
        string? system)
    {
        var chosen = string.IsNullOrWhiteSpace(system) ? sites.Active : sites.InSystem(system);

        return
        [
            .. chosen
                .Where(site => !site.Complete && !site.Failed)
                .SelectMany(site => site.Outstanding.Select(resource => (Site: site, Resource: resource)))
                .OrderByDescending(pair => pair.Resource.Remaining),
        ];
    }
}
