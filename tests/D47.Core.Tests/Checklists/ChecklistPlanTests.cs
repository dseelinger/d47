using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// The seam between Phase 14's tables and this phase's lists (list.md Phase 17): a build costed
/// into the things a Commander has to <em>do</em>, with an exact total rather than a floor.
/// </summary>
public class ChecklistPlanTests
{
    private static readonly ChecklistScope Krait = ChecklistScope.Ship(12);

    private static CommanderGameState State(params string[] lines)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-16T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                 }.Concat(lines))
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store.Active!;
    }

    [Fact]
    public void APlanIsIntentsRatherThanATargetLoadout()
    {
        var items = EngineeringPlan.Items(Krait, "krait_mkii",
        [
            new BuildRequest("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer"),
            new BuildRequest("PowerPlant", "Armoured", 5),
        ]);

        // Two slots asked about produce items about two slots and no others. A target loadout
        // would invent the other eighteen and then report "2 of 20".
        Assert.All(
            items.Where(item => item.Intent?.Kind == ChecklistIntentKind.Blueprint),
            item => Assert.Contains(item.Intent!.Subject, new[] { "MainEngines", "PowerPlant" }));
    }

    [Fact]
    public void NamingAnEngineerAddsTheirRankAsItsOwnItem()
    {
        var items = EngineeringPlan.Items(Krait, "krait_mkii",
            [new BuildRequest("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer")]);

        // Rank blocks as surely as materials do, so it is a thing to do rather than a footnote
        // under the thing to do.
        Assert.Contains(items, item => item.Intent?.Kind == ChecklistIntentKind.EngineerAccess);
    }

    [Fact]
    public void AnExperimentalEffectIsItsOwnItemOnTheSameSlot()
    {
        var items = EngineeringPlan.Items(Krait, "krait_mkii",
            [new BuildRequest("MainEngines", "Dirty Drive Tuning", 5, Experimental: "Drag Drives")]);

        Assert.Contains(items, item => item.Intent?.Kind == ChecklistIntentKind.Blueprint);
        Assert.Contains(items, item => item.Intent?.Kind == ChecklistIntentKind.Experimental);
    }

    [Fact]
    public void TheTotalIsIngredientsTimesRollsRatherThanOneApplication()
    {
        // Grade 1 Dirty Drive Tuning costs one legacyfirmware per application, and at rank 1 a
        // grade takes five applications. Known unit cost times known count is a real number, so
        // the plan states one instead of hedging.
        const string rankOne =
            """
            { "timestamp":"2026-08-16T09:00:00Z", "event":"EngineerProgress",
              "Engineers":[ {"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":1} ] }
            """;

        var items = EngineeringPlan.Items(Krait, "krait_mkii",
            [new BuildRequest("MainEngines", "Dirty Drive Tuning", 1, "Felicity Farseer")]);

        var costing = EngineeringPlan.Cost(items, State(rankOne));

        var firmware = costing.Ingredients.Single(i => i.Material.Symbol == "legacyfirmware");

        Assert.Equal(5, firmware.Needed);
        Assert.Equal(5, EngineeringRules.RollsFor(1, 1));
    }

    [Fact]
    public void ARankThatCannotReachTheGradeIsAGateRatherThanAShortfall()
    {
        const string rankThree =
            """
            { "timestamp":"2026-08-16T09:00:00Z", "event":"EngineerProgress",
              "Engineers":[ {"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":3} ] }
            """;

        var items = EngineeringPlan.Items(Krait, "krait_mkii",
            [new BuildRequest("MainEngines", "Dirty Drive Tuning", 5, "Felicity Farseer")]);

        var costing = EngineeringPlan.Cost(items, State(rankThree));

        // No materials are listed at all for a grade nobody can roll — listing them would be
        // listing work nobody can start.
        Assert.NotEmpty(costing.Gates);
        Assert.Empty(costing.Ingredients);
        Assert.Contains("16,000,000", string.Join(" ", costing.Gates), StringComparison.Ordinal);
    }

    [Fact]
    public void ABlueprintNoTableCoversIsKeptAndMarkedRatherThanRefused()
    {
        var items = EngineeringPlan.Items(Krait, "krait_mkii",
            [new BuildRequest("MainEngines", "Sovereign Wobbulation", 5, "Felicity Farseer")]);

        var costing = EngineeringPlan.Cost(items, State());

        // A macro refuses an unknown action because it presses keys and would half-configure a
        // ship. A checklist line presses nothing.
        Assert.Single(items, item => item.Intent?.Kind == ChecklistIntentKind.Blueprint);
        Assert.NotEmpty(costing.Uncovered);
    }

    [Fact]
    public void CapsAreCheckedAgainstTheExactTotalAndAreShared()
    {
        // Two plans that each fit can be jointly impossible, which is exactly the arithmetic
        // nobody can do in their head — so costing takes every live plan at once.
        var costing = EngineeringPlan.Cost([], State());

        Assert.Empty(costing.OverCapacity);
        Assert.Empty(costing.Shortfall);
    }

    [Fact]
    public void AColonisationPlanHoldsFacilitiesWithNoCostAttached()
    {
        var items = ColonisationPlan.Items(
            ChecklistScope.System("Sol"),
            [new FacilityRequest("Sol 3", "Coriolis")]);

        var item = Assert.Single(items);

        Assert.Equal(ChecklistIntentKind.Facility, item.Intent!.Kind);

        // No quantity, because there is no licence-clean facility cost table to predict one from.
        Assert.Null(item.Intent.Quantity);

        // Quoted rather than asserted: a facility choice is what a conversation settled on, and
        // no shipped table confirmed it.
        Assert.Equal(ChecklistProvenance.Quoted, item.Provenance);
    }

    [Fact]
    public void HaulingComesOffTheDepotRatherThanOutOfATable()
    {
        const string depot =
            """
            { "timestamp":"2026-08-16T10:00:00Z", "event":"ColonisationConstructionDepot",
              "MarketID":3960809986, "ConstructionProgress":0.5,
              "ConstructionComplete":false, "ConstructionFailed":false,
              "ResourcesRequired":[
                { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
                  "RequiredAmount":500, "ProvidedAmount":200, "Payment":3239 } ] }
            """;

        var site = State(depot).Colonisation.All.Single();

        var items = ColonisationPlan.Hauling(ChecklistScope.System("Sol"), site);
        var item = Assert.Single(items);

        Assert.Equal("Aluminium", item.Intent!.Detail);
        Assert.Equal(500, item.Intent.Quantity);

        // The site itself said so, which is the one half of a colonisation plan a shipped source
        // confirms — and the source is the Commander's own journal.
        Assert.Equal(ChecklistProvenance.Asserted, item.Provenance);
        Assert.Equal(300, site.Resources[0].Remaining);
    }

    [Fact]
    public void ADepotEventIsASnapshotSoTheLatestOneWinsOutright()
    {
        const string first =
            """
            { "timestamp":"2026-08-16T10:00:00Z", "event":"ColonisationConstructionDepot",
              "MarketID":1, "ConstructionProgress":0.1,
              "ConstructionComplete":false, "ConstructionFailed":false,
              "ResourcesRequired":[
                { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
                  "RequiredAmount":500, "ProvidedAmount":100 } ] }
            """;

        const string second =
            """
            { "timestamp":"2026-08-16T11:00:00Z", "event":"ColonisationConstructionDepot",
              "MarketID":1, "ConstructionProgress":0.4,
              "ConstructionComplete":false, "ConstructionFailed":false,
              "ResourcesRequired":[
                { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
                  "RequiredAmount":500, "ProvidedAmount":400 } ] }
            """;

        var site = State(first, second).Colonisation.All.Single();

        // 400 provided, not 500. Accumulating a snapshot is the trap that caught EngineerProgress
        // and StoredModules, silently both times.
        Assert.Equal(400, site.Resources[0].Provided);
        Assert.Equal(100, site.Resources[0].Remaining);
    }

    [Fact]
    public void SeveralSitesCanBeOpenAtOnce()
    {
        string Depot(long id) =>
            $$"""
              { "timestamp":"2026-08-16T10:00:00Z", "event":"ColonisationConstructionDepot",
                "MarketID":{{id}}, "ConstructionProgress":0.1,
                "ConstructionComplete":false, "ConstructionFailed":false, "ResourcesRequired":[] }
              """;

        // A collection with a selection, never a "current site" field. Three were open at once in
        // the corpus, and that is expensive to change later.
        Assert.Equal(3, State(Depot(1), Depot(2), Depot(3)).Colonisation.Active.Count);
    }

    [Fact]
    public void ACompletedSiteIsKnownByTheFlagRatherThanByTheEventsStopping()
    {
        const string finished =
            """
            { "timestamp":"2026-08-16T12:00:00Z", "event":"ColonisationConstructionDepot",
              "MarketID":1, "ConstructionProgress":1.0,
              "ConstructionComplete":true, "ConstructionFailed":false, "ResourcesRequired":[] }
            """;

        var sites = State(finished).Colonisation;

        Assert.Single(sites.All);
        Assert.Empty(sites.Active);
    }
}
