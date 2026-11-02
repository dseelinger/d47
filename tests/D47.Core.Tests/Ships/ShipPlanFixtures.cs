using System.Globalization;
using D47.Core.Journal;
using D47.Core.Tests.Journal;

namespace D47.Core.Tests.Ships;

/// <summary>Python loadouts shared by the ship-plan tests.</summary>
public static class ShipPlanFixtures
{
    /// <summary>A Python with a plain Class 5 engine in MainEngines.</summary>
    public static JournalEvent Loadout(int shipId) => JournalScript.Parse(
        $$"""
        {"timestamp":"2026-09-10T09:05:00Z","event":"Loadout","Ship":"python","ShipID":{{shipId}},
         "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true}]}
        """);

    /// <summary>The same Python with Dirty Drive Tuning at grade 5 rolled on MainEngines.</summary>
    public static JournalEvent Engineered(int shipId) => JournalScript.Parse(
        """
        {"timestamp":"2026-09-10T09:10:00Z","event":"Loadout","Ship":"python","ShipID":SHIPID,
         "Modules":[{"Slot":"MainEngines","Item":"int_engine_size5_class5","On":true,
           "Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0,
                          "Engineer":"Felicity Farseer","EngineerID":300100}}]}
        """.Replace("SHIPID", shipId.ToString(CultureInfo.InvariantCulture)));
}
