using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// What the engineer whose system the Commander is standing in could actually roll today
/// (reported 2026-08-20).
/// <para>
/// Two complaints, one gap. Asked <i>"I'm in Laksak. What can I retire from my checklist here?"</i>
/// d47 answered with all thirty-five items, because no filter knew where the Commander was. And
/// the opening callout said <i>"Selene Jean is one stop away"</i> — an unlock hint about somebody
/// else — while they stood at Lei Cheung's base with a list he could work through.
/// </para>
/// <para>
/// <b>Every input had been on disk for phases.</b> The recipe rows name their engineers, the
/// directory knows where each is based, <c>EngineerProgress</c> knows who is unlocked and at what
/// grade, and the journal knows the system. Nothing joined them.
/// </para>
/// </summary>
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

        // The sentence the callout says. It leads with the work, because the work is the errand.
        Assert.Contains("Lei Cheung is here", here.Describe(), StringComparison.Ordinal);
        Assert.Contains("one item", here.Describe(), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Rank gates, it does not filter.</b> An item this engineer offers but cannot yet roll to
    /// the grade wanted is the reason to do some of their other work first, so it is reported
    /// beside the ready ones rather than dropped.
    /// </summary>
    [Fact]
    public void WorkBeyondTheCommandersGradeIsKeptApartRatherThanDropped()
    {
        var state = Flying("Laksak", rank: 1).Active!;
        var items = new[] { Booster("TinyHardpoint5", grade: 5) };

        var here = Assert.Single(EngineersHere.For(items, state));

        Assert.Empty(here.Ready);
        Assert.Single(here.OutOfRank);
        Assert.Contains("waiting on your grade", here.Describe(), StringComparison.Ordinal);
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

    /// <summary>
    /// <b>Work on a ship in another dock is still work</b> (change-requests.md 33).
    /// <para>
    /// Slot names are shared across hulls — every ship has a <c>TinyHardpoint5</c> — so resolving
    /// the item's slot against the ship being <i>flown</i> answered with that ship's module and
    /// then narrowed the blueprint match to it. A Heavy Duty roll wanted on the Krait's shield
    /// booster was measured against the Anaconda's chaff launcher, which has no Heavy Duty at all,
    /// and the item left the answer with nothing said. The remembered loadout is the fix and the
    /// same one <c>ChecklistWording.InSlot</c> already makes.
    /// </para>
    /// </summary>
    [Fact]
    public void WorkOnAShipInAnotherDockIsStillOffered()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        var here = Assert.Single(
            EngineersHere.For([Booster("TinyHardpoint5", grade: 3, shipId: 52)], state));

        Assert.Single(here.Ready);
    }

    /// <summary>
    /// And the ship being flown is still matched on its own modules, which is what stops the fix
    /// above from being "assume nothing and match on the name alone". Heavy Duty belongs to a
    /// shield booster and to armour; the Anaconda's <c>TinyHardpoint5</c> holds a chaff launcher,
    /// which has neither, so there is nothing here for Lei Cheung to roll.
    /// </summary>
    [Fact]
    public void TheShipBeingFlownIsStillMatchedOnItsOwnModules()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        Assert.Empty(EngineersHere.For([Booster("TinyHardpoint5", grade: 3, shipId: 51)], state));
    }

    /// <summary>
    /// <b>And the other ship is matched on <i>its</i> modules, not merely left unmatched.</b> This
    /// is the test that separates the fix from the cheap version of it — giving up and matching on
    /// the blueprint name alone would pass both tests above and be wrong here, because Heavy Duty
    /// belongs to a shield booster and to armour and the Krait's <c>TinyHardpoint1</c> holds
    /// neither. Lei Cheung cannot roll Heavy Duty on a chaff launcher on any ship.
    /// </summary>
    [Fact]
    public void AnotherShipsWorkIsMatchedOnThatShipsOwnModules()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        Assert.Empty(EngineersHere.For([Booster("TinyHardpoint1", grade: 3, shipId: 52)], state));
    }

    /// <summary>
    /// <b>A bi-weave shield generator is still a shield generator</b> (reported 2026-08-23).
    /// <para>
    /// The recipe table's module column is a category vocabulary — <c>Shield Generator</c> — and a
    /// specification's name is Frontier's product name — <c>Bi-Weave Shield Generator</c>. Narrowing
    /// on the readable name matched no row at all, and the lookup gave up before the blueprint name
    /// was ever read, so the line left every engineer's answer with nothing said. Thirty of the
    /// Commander's lines were invisible this way, on four hulls.
    /// </para>
    /// </summary>
    [Fact]
    public void ABiWeaveShieldGeneratorIsStillAShieldGenerator()
    {
        var state = FlyingWithAShipLeftBehind().Active!;

        var here = Assert.Single(
            EngineersHere.For([Roll("Slot01_Size3", "Reinforced Shields", grade: 3, shipId: 52)], state));

        Assert.Single(here.Ready);
    }

    /// <summary>
    /// And the narrowing still narrows, which is the half that stops the fix being "give up and
    /// match on the name alone". A hull's own armour — <c>Krait MkII Lightweight Alloy</c> —
    /// carries Heavy Duty, and Lei Cheung does not do armour. Matching by name alone would draw the
    /// Armour and Shield Booster rows together and credit him with work he cannot take.
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

    /// <summary>
    /// In a system, in an Anaconda with a shield booster fitted, and known to Lei Cheung. The
    /// module matters: a blueprint name belongs to several module kinds and they do not share an
    /// engineer list.
    /// </summary>
    private static GameStateStore Flying(string system, int rank)
    {
        var store = new GameStateStore();

        foreach (var line in new[]
                 {
                     """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     $$"""{"timestamp":"2026-08-20T09:00:01Z","event":"Location","StarSystem":"{{system}}","Docked":true,"StationName":"Trader's Rest"}""",
                     $$"""{"timestamp":"2026-08-20T09:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":{{LeiCheung}},"Progress":"Unlocked","Rank":{{rank}}}]}""",
                     """{"timestamp":"2026-08-20T09:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{"Slot":"TinyHardpoint5","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}]}""",
                 })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            store.Apply(parsed!);
        }

        return store;
    }

    /// <summary>
    /// In Laksak in an Anaconda whose utility slot holds a chaff launcher, having previously been
    /// aboard a Krait with a shield booster in the slot of the same name. <b>The collision is the
    /// point</b>: two hulls, one slot name, two different modules, and only one of them has a
    /// Heavy Duty blueprint Lei Cheung offers.
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
}
