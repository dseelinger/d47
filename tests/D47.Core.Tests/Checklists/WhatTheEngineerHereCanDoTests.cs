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

    private static ChecklistItem Booster(string slot, int grade) => new()
    {
        Key = $"blueprint:{slot}",
        Scope = ChecklistScope.Ship(51),
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
}
