using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary> What the engineer whose system the Commander is standing in could actually roll today. </summary>
public class WhatTheEngineerHereCanDoTests
{
    private const int LeiCheung = 300120;

    /// <summary>Lei Cheung grades Shield Boosters to 3, and is in Laksak.</summary>
    [Fact]
    public void TheEngineerInThisSystemIsFoundWithTheWorkTheyCanDo()
    {
        var state = Flying("Laksak", rank: 5).Active!;
        var items = new[] { Booster("TinyHardpoint5", grade: 3) };

        var here = Assert.Single(EngineersHere.For(items, state));

        Assert.Equal("Lei Cheung", here.Engineer.Name);
        Assert.True(here.Unlocked);
        Assert.Single(here.Ready);
        Assert.Empty(here.OutOfRank);

        // The sentence the callout says.
        Assert.Contains("Lei Cheung is here", here.Describe(), StringComparison.Ordinal);
        Assert.Contains("one item", here.Describe(), StringComparison.Ordinal);
    }

    /// <summary>Rank gates, it does not filter.</summary>
    [Fact]
    public void WorkBeyondTheCommandersGradeIsKeptApartRatherThanDropped()
    {
        // Grade 3, which is the top Lei Cheung offers for Heavy Duty on a Shield Booster.
        var state = Flying("Laksak", rank: 1).Active!;
        var items = new[] { Booster("TinyHardpoint5", grade: 3) };

        var here = Assert.Single(EngineersHere.For(items, state));

        Assert.Empty(here.Ready);
        Assert.Single(here.OutOfRank);
        Assert.Contains("waiting on your grade", here.Describe(), StringComparison.Ordinal);
    }

    /// <summary>An engineer's grades are not the blueprint's.</summary>
    [Fact]
    public void AGradeThisEngineerDoesNotOfferIsNotTheirWork()
    {
        var state = Flying("Laksak", rank: 5).Active!;

        var here = Assert.Single(EngineersHere.For([Booster("TinyHardpoint5", grade: 5)], state));

        // Not his work, and no longer nothing at all.
        Assert.Empty(here.Ready);
        Assert.Empty(here.OutOfRank);

        var partial = Assert.Single(here.Partial);

        Assert.Equal(3, partial.Reaches);
        Assert.Equal(5, partial.Wanted);
    }

    /// <summary>Somewhere no engineer is based answers with nothing, rather than with the list.</summary>
    [Fact]
    public void AwayFromAnyEngineerThereIsNothingToSay()
    {
        var state = Flying("Shinrarta Dezhra", rank: 5).Active!;

        Assert.Empty(EngineersHere.For([Booster("TinyHardpoint5", grade: 3)], state));
    }

    /// <summary>
    /// The reported question, through the tool the model actually calls: the same list, narrowed to
    /// what can be retired here.
    /// </summary>
    [Fact]
    public void TheReportNarrowsToWhatCanBeRetiredHere()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths, Flying("Laksak", rank: 5));

        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        var everything = checklists.Report();
        var narrowed = checklists.Report(hereOnly: true);

        // The authored line is nobody's to roll, so it is in the full answer and not in this one.
        Assert.Contains("buy limpets", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("buy limpets", narrowed, StringComparison.Ordinal);
    }

    /// <summary>Work on a ship in another dock is still work.</summary>
    [Fact]
    public void WorkOnAShipInAnotherDockIsStillOffered()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        var here = Assert.Single(
            EngineersHere.For([Booster("TinyHardpoint5", grade: 3, shipId: 52)], state));

        Assert.Single(here.Ready);
    }

    /// <summary>
    /// And the ship being flown is still matched on its own modules, which is what stops the fix above
    /// from being "assume nothing and match on the name alone".
    /// </summary>
    [Fact]
    public void TheShipBeingFlownIsStillMatchedOnItsOwnModules()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        Assert.Empty(EngineersHere.For([Booster("TinyHardpoint5", grade: 3, shipId: 51)], state));
    }

    /// <summary>And the other ship is matched on its modules, not merely left unmatched.</summary>
    [Fact]
    public void AnotherShipsWorkIsMatchedOnThatShipsOwnModules()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        Assert.Empty(EngineersHere.For([Booster("TinyHardpoint1", grade: 3, shipId: 52)], state));
    }

    /// <summary>A bi-weave shield generator is still a shield generator.</summary>
    [Fact]
    public void ABiWeaveShieldGeneratorIsStillAShieldGenerator()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        var here = Assert.Single(
            EngineersHere.For([Roll("Slot01_Size3", "Reinforced Shields", grade: 3, shipId: 52)], state));

        Assert.Single(here.Ready);
    }

    /// <summary>
    /// And the narrowing still narrows, which is the half that stops the fix being "give up and match
    /// on the name alone".
    /// </summary>
    [Fact]
    public void AHullsOwnArmourDoesNotBecomeAShieldEngineersWork()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        Assert.Empty(EngineersHere.For([Roll("Armour", "Heavy Duty", grade: 3, shipId: 52)], state));
    }

    /// <summary>One line of an engineering plan: a blueprint wanted on a slot, at a grade.</summary>
    private static ChecklistItem Roll(string slot, string blueprint, int grade, int shipId) => new()
    {
        Key = $"blueprint:{slot}",
        Scope = ChecklistScope.Ship(shipId),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = $"Grade {grade} {blueprint} on {slot}",
        Intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, slot)
        {
            Detail = blueprint,
            Grade = grade,
        },
    };

    private static ChecklistItem Booster(string slot, int grade, int shipId = 51) => new()
    {
        Key = $"blueprint:{slot}",
        Scope = ChecklistScope.Ship(shipId),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = $"Grade {grade} Heavy Duty on {slot}",
        Intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, slot)
        {
            Detail = "Heavy Duty",
            Grade = grade,
        },
    };

    /// <summary>In a system, in an Anaconda with a shield booster fitted, and known to Lei Cheung.</summary>
    private static GameStateStore Flying(string system, int rank)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     $$"""{"timestamp":"2026-08-20T09:00:01Z","event":"Location","StarSystem":"{{system}}","Docked":true,"StationName":"Trader's Rest"}""",
                     $$"""{"timestamp":"2026-08-20T09:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":{{LeiCheung}},"Progress":"Unlocked","Rank":{{rank}}}]}""",
                     """{"timestamp":"2026-08-20T09:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint5","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    /// <summary>
    /// In Laksak in an Anaconda whose utility slot holds a chaff launcher, having previously been
    /// aboard a Krait with a shield booster in the slot of the same name.
    /// </summary>
    private static GameStateStore FlyingWithAShipLeftBehind()
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-23T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     """{"timestamp":"2026-08-23T09:00:01Z","event":"Location","StarSystem":"Laksak","Docked":true,"StationName":"Trader's Rest"}""",
                     $$"""{"timestamp":"2026-08-23T09:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":{{LeiCheung}},"Progress":"Unlocked","Rank":5}]}""",
                     """{"timestamp":"2026-08-23T09:00:03Z","event":"Loadout","Ship":"krait_mkii","ShipID":52,"ShipName":"Second Thoughts","ShipIdent":"KR-01","Modules":[{"Slot":"TinyHardpoint5","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0},{"Slot":"TinyHardpoint1","Item":"hpt_chafflauncher_tiny","On":true,"Priority":0,"Health":1.0},{"Slot":"Slot01_Size3","Item":"int_shieldgenerator_size3_class3_fast","On":true,"Priority":0,"Health":1.0},{"Slot":"Armour","Item":"krait_mkii_armour_grade1","On":true,"Priority":0,"Health":1.0}]}""",
                     """{"timestamp":"2026-08-23T09:00:04Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint5","Item":"hpt_chafflauncher_tiny","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    /// <summary>An effect goes where its module's blueprint went: "if I'm not showing partial grades, then
    /// don't show that module's corresponding experimental effect".</summary>
    [Fact]
    public void AnEffectFollowsItsModulesBlueprintIntoThePartialBand()
    {
        var state = Flying("Laksak", rank: 5).Active!;

        var here = Assert.Single(EngineersHere.For(
            [Booster("TinyHardpoint5", grade: 5), Effect("TinyHardpoint5", "Force Block")],
            state));

        // Grade 5 Heavy Duty is Lei Cheung's to 3, so both lines are work he cannot finish.
        Assert.Empty(here.Ready);
        Assert.Equal(2, here.Partial.Count);

        var effect = Assert.Single(
            here.Partial,
            part => part.Item.Intent?.Kind == ChecklistIntentKind.Experimental);

        // And it says why it is here in its own terms rather than borrowing the module's sentence: an effect
        // is applied outright at any grade, so "takes this to 3 of 5" would be false.
        Assert.True(effect.RidesAlong);
        Assert.Equal(
            "Lei Cheung can apply this, but only takes that module to 3 of 5",
            effect.Describe(here.Engineer.Name));
    }

    /// <summary>
    /// An effect on a module whose blueprint this engineer can finish is ordinary work and stays where
    /// it was — the rule above follows a sibling rather than demoting every effect.
    /// </summary>
    [Fact]
    public void AnEffectBesideWorkTheEngineerCanFinishIsStillReady()
    {
        var state = Flying("Laksak", rank: 5).Active!;

        var here = Assert.Single(EngineersHere.For(
            [Booster("TinyHardpoint5", grade: 3), Effect("TinyHardpoint5", "Force Block")],
            state));

        Assert.Equal(2, here.Ready.Count);
        Assert.Empty(here.Partial);
    }

    /// <summary>
 /// The cell the matrix was missing: a module carrying both a blueprint and an effect, at a
    /// rank below the grade.
    /// </summary>
    [Fact]
    public void AnEffectDoesNotFollowItsBlueprintOutOfRank()
    {
        var state = Flying("Laksak", rank: 1).Active!;

        var here = Assert.Single(EngineersHere.For(
            [Booster("TinyHardpoint5", grade: 3), Effect("TinyHardpoint5", "Force Block")],
            state));

        // Grade 3 Heavy Duty is Lei Cheung's work; rank 1 is what stops it today.
        Assert.Single(here.OutOfRank);
        Assert.Single(here.Ready);
        Assert.Empty(here.Partial);

        Assert.Equal(ChecklistIntentKind.Blueprint, here.OutOfRank[0].Intent?.Kind);
        Assert.Equal(ChecklistIntentKind.Experimental, here.Ready[0].Intent?.Kind);
    }

    /// <summary>
    /// And through the filter the Commander actually reads (#205, ruled 2026-09-01): the engineer row
    /// shows what this engineer does, rank or no rank.
    /// </summary>
    [Fact]
    public void TheHereFilterShowsWorkThisEngineerDoesEvenBelowTheRankForIt()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths, Flying("Laksak", rank: 1));

        checklists.AdoptPlan(
            ChecklistScope.Ship(51),
            ChecklistSource.EngineeringPlan,
            [Booster("TinyHardpoint5", grade: 3), Effect("TinyHardpoint5", "Force Block")],
            ["TinyHardpoint5"]);

        var items = checklists.Document.Items.Where(item => item.IsLive).ToList();

        var blueprint = Assert.Single(items, item => item.Intent?.Kind == ChecklistIntentKind.Blueprint);
        var effect = Assert.Single(items, item => item.Intent?.Kind == ChecklistIntentKind.Experimental);

        // Both, and the blueprint is the half that used to vanish.
        Assert.True(checklists.OfferedHere(blueprint));
        Assert.True(checklists.OfferedHere(effect));

        // Visible and explained rather than visible and misleading.
        Assert.Equal(
            "Lei Cheung rolls this at grade 3, and you are grade 1 with them",
            checklists.RankHere(blueprint));

        // An effect is bought outright, so nothing about it is waiting on a grade.
        Assert.Null(checklists.RankHere(effect));
    }

    /// <summary>And an effect on a slot with no blueprint line at all has no sibling to follow.</summary>
    [Fact]
    public void AnEffectOnItsOwnIsUnaffected()
    {
        var state = Flying("Laksak", rank: 5).Active!;

        var here = Assert.Single(EngineersHere.For(
            [Booster("TinyHardpoint5", grade: 5), Effect("TinyHardpoint1", "Force Block")],
            state));

        Assert.Single(here.Ready);
        Assert.Single(here.Partial);
    }

    private static ChecklistItem Effect(string slot, string effect, int shipId = 51) => new()
    {
        Key = $"experimental:{slot}",
        Scope = ChecklistScope.Ship(shipId),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = $"{effect} on {slot}",
        Intent = new ChecklistIntent(ChecklistIntentKind.Experimental, slot) { Detail = effect },
    };
}
