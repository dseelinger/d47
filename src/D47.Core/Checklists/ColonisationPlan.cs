using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Checklists;

/// <summary>One thing the Commander means to build, and where in the system it goes.</summary>
/// <param name="Place">
/// The body or orbital slot. Half of the item's identity — <b>body or orbital slot plus facility
/// type</b> is to a system what slot plus intent is to a ship.
/// </param>
/// <param name="Facility">What goes there, in the Commander's words.</param>
public sealed record FacilityRequest(string Place, string Facility);

/// <summary>
/// A system objective turned into the things a Commander has to do (Phase 17, "A
/// colonisation plan writes the checklist").
/// <para>
/// <b>It rides the same substrate as the ship builds and that is the whole point.</b> Intents
/// rather than a target state, null meaning wildcard, tombstones on abandonment, no number ever
/// stored, revision as a diff. A second checklist engine for colonisation would double every hard
/// part in this phase and then let the halves drift, so that "what am I working on" has two
/// answers depending on which is asked. What is genuinely different is only the key and the table
/// behind it.
/// </para>
/// <para>
/// <b>And there is no table behind it.</b> The 2026-08-16 spike settled that: every
/// machine-readable rendering of Frontier's facility figures sits inside GPL-3.0 source, EDSC
/// publishes nothing, EDCD has no colonisation repository, and Frontier's own guide states every
/// mechanic and publishes not one number. So this plan is <b>not costed from a table</b>. It
/// carries an objective, an ordered set of intents, and progress diffed against
/// <c>ColonisationConstructionDepot</c> — with quantities read off the site once it exists, or
/// entered by the Commander, and never predicted. See
/// <c>docs/spikes/colonisation-sources.md</c>.
/// </para>
/// </summary>
public static class ColonisationPlan
{
    /// <summary>
    /// What this item is not, said in the plan rather than left for a Commander to discover.
    /// <para>
    /// Two claims d47 must never make. It cannot say a system is free — a claim lasts 24 hours and
    /// lives on Frontier's servers, so no crowd-fed index holds it. And it cannot say what a
    /// facility costs or what it will do to the system, because no licence-clean source publishes
    /// either, which also means it cannot work out the order to build them in.
    /// </para>
    /// </summary>
    public const string WhatThisCannotBe =
        "I cannot tell you what a facility costs, what it will do to the system, or what order to "
        + "build in — nobody publishes those figures in a form I am allowed to ship, so I would be "
        + "inventing them. What I can do is hold the plan and count what the depot says you still owe.";

    /// <summary>
    /// The facilities themselves, one item each. Whether one is done is the depot's
    /// <c>ConstructionComplete</c> flag and never a tick, so these are derived like everything a
    /// plan writes.
    /// </summary>
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

                // Quoted rather than asserted: a facility choice is what a conversation settled
                // on, and no shipped table confirmed it. The provenance survives into the wording.
                Provenance = ChecklistProvenance.Quoted,
            });
        }

        return [.. items.GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase).Select(group => group.First())];
    }

    /// <summary>
    /// The hauling, taken off a site the Commander has actually visited.
    /// <para>
    /// <b>Every quantity here is the depot's own.</b> <c>RequiredAmount</c> never moves mid-build —
    /// 0 of 17 sites across every commodity — and <c>Name_Localised</c> is on every row, so this
    /// needs no commodity table at all. It is also why these items only exist for a site that has
    /// been seen: predicting them would mean predicting a facility's cost, which is exactly what
    /// there is no source for.
    /// </para>
    /// </summary>
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

                    // Carried so a person reading the file can see what the site asked for. The
                    // figure the plan quotes is always recomputed from the latest event — a
                    // stored number gets recited in October with nothing left to say where it
                    // came from.
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

                    // Asserted: the site itself said so. This is the one half of a colonisation
                    // plan a shipped source confirms, and the source is the Commander's journal.
                    Provenance = ChecklistProvenance.Asserted,
                };
            }),
        ];
    }

    /// <summary>
    /// What a Commander still owes across every site in a system, as of their last visit to each.
    /// <para>
    /// The freshness caveat is not decoration: the depot event arrives only while docked at that
    /// very site, so this is a record of where they have been rather than a live feed.
    /// </para>
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
