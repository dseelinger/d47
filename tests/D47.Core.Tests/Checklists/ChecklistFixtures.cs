using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Tests.Journal;

namespace D47.Core.Tests.Checklists;

/// <summary>Checklist items and journal state shared by the checklist tests.</summary>
public static class ChecklistFixtures
{
    private const int LeiCheung = 300120;

    /// <summary>A derived engineering-plan item for the intent on a ship.</summary>
    public static ChecklistItem DerivedItem(ChecklistIntent intent, int shipId, string text, string? hull = null) => new()
    {
        Key = ChecklistKeys.For(intent),
        Scope = ChecklistScope.Ship(shipId),
        Kind = ChecklistItemKind.Derived,
        Source = ChecklistSource.EngineeringPlan,
        Text = text,
        Intent = intent,
        Hull = hull,
    };

    /// <summary>Grade 5 Dirty Drive Tuning on MainEngines.</summary>
    public static ChecklistIntent DirtyDrivesIntent() => new(ChecklistIntentKind.Blueprint, "MainEngines")
    {
        Detail = "Engine_Dirty",
        Grade = 5,
    };

    /// <summary>One line of an engineering plan: a Heavy Duty blueprint wanted on a slot, at a grade.</summary>
    public static ChecklistItem Booster(string slot, int grade, int shipId = 51) => new()
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
    public static GameStateStore Flying(string system, int rank, bool secondBooster = false)
    {
        const string First = """{"Slot":"TinyHardpoint5","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}""";
        const string Second = """,{"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0}""";

        return JournalScript.Store(
            """{"timestamp":"2026-08-20T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
            $$"""{"timestamp":"2026-08-20T09:00:01Z","event":"Location","StarSystem":"{{system}}","Docked":true,"StationName":"Trader's Rest"}""",
            $$"""{"timestamp":"2026-08-20T09:00:02Z","event":"EngineerProgress","Engineers":[{"Engineer":"Lei Cheung","EngineerID":{{LeiCheung}},"Progress":"Unlocked","Rank":{{rank}}}]}""",
            $$"""{"timestamp":"2026-08-20T09:00:03Z","event":"Loadout","Ship":"anaconda","ShipID":51,"ShipName":"Flamebrand","ShipIdent":"FB-01","Modules":[{{First}}{{(secondBooster ? Second : string.Empty)}}]}""");
    }
}
