using D47.Core.Help;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The Journal events page, and the paragraph read from it (#814).</summary>
public class EveryCommonJournalEventHasAParagraphTests
{
    private static readonly string[] CommonKinds =
    [
        "ReceiveText", "Scan", "Cargo", "CommunityGoal", "MaterialCollected", "StartJump", "ShipTargeted",
        "FSDTarget", "FSDJump", "EngineerCraft", "MiningRefined", "UnderAttack", "FuelScoop",
        "ColonisationConstructionDepot", "FSSDiscoveryScan", "SupercruiseExit", "LaunchDrone",
        "DockingRequested", "DockingGranted", "SupercruiseDestinationDrop", "Undocked", "Docked", "RefuelAll",
        "NavRoute", "SupercruiseEntry", "NavRouteClear", "Loadout", "BackpackChange", "PowerplayMerits",
        "CollectItems", "StoredModules", "ScanBaryCentre", "ProspectedAsteroid", "EngineerProgress", "Market",
        "FSSAllBodiesFound", "ShieldState", "CarrierLocation", "Location", "Missions", "Rank", "Progress",
        "Reputation", "Commander", "DatalinkScan",
    ];

    private static string[] PageKinds =>
    [
        .. (HelpLibrary.PageFor("general-journal-events") ?? string.Empty)
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(line => line[3..].Trim())
            .Where(kind => JournalExplainers.For(kind) is not null),
    ];

    [Fact]
    public void ADockedEventExplainsItselfAndAnUnlistedOneDoesNot()
    {
        Assert.StartsWith("Elite writes Docked when your ship finishes landing", JournalExplainers.For("Docked"));
        Assert.Null(JournalExplainers.For("Music"));
    }

    [Fact]
    public void EachOfTheFortyFiveKindsHasAParagraph()
    {
        Assert.Equal(45, CommonKinds.Length);
        Assert.All(CommonKinds, kind => Assert.NotNull(JournalExplainers.For(kind)));
    }

    [Fact]
    public void EveryHeadingOnThePageIsAKindTheJournalKnows()
    {
        Assert.All(PageKinds, kind => Assert.True(HandledEvents.All.Contains(kind), kind));
        Assert.Equal(CommonKinds.Order(StringComparer.Ordinal), PageKinds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NoParagraphCarriesMarkup()
    {
        foreach (var kind in PageKinds)
        {
            Assert.DoesNotMatch(@"[\[*`<]", JournalExplainers.For(kind)!);
        }
    }

    [Fact]
    public void AKindOutsideActedOnSaysItDoesNothingElse()
    {
        foreach (var kind in PageKinds.Where(kind => !HandledEvents.ActedOn.Contains(kind)))
        {
            Assert.EndsWith("d47 shows it on the Journal page and does nothing else with it.", JournalExplainers.For(kind));
        }
    }
}
