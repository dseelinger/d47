using System.Reflection;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public sealed class AStepNamesAnEventOnlyIfTheTableHoldsItTests
{
    private const string Events =
        "# source\tevent\tdrawable\tneeds\tcareer\tdescription\n" +
        "journal\tDocked\tyes\t\t\tWhen landing at a station\n" +
        "journal\tLaunchSRV\tyes\tBuggyBay\t\tWhen launching the SRV\n" +
        "journal\tScanOrganic\tyes\tOdyssey\tExobiology\tWhen scanning a species\n" +
        "journal\tLoadGame\tno\t\t\tWhen the game loads\n" +
        "journal\tNoSuchAppEvent\tyes\t\t\tAn event d47 does not act on\n" +
        "d47\tToolRun\tyes\t\t\tA tool ran\n";

    private const string Fields =
        "# event\tfield\ttype\tvalue\tdrawable\n" +
        "Docked\tStationType\tstring\t\tyes\n" +
        "Docked\tStationType\tstring\tCoriolis\tyes\n" +
        "Docked\tStationName\tstring\t\tyes\n" +
        "Docked\tTaxi\tboolean\t\tyes\n" +
        "Docked\tTaxi\tboolean\ttrue\tyes\n" +
        "Docked\tTaxi\tboolean\tfalse\tyes\n" +
        "NoSuchAppEvent\tKind\tstring\tx\tyes\n" +
        "ToolRun\tTool\tstring\t\tyes\n" +
        "ToolRun\tTool\tstring\tadd_log_entry\tyes\n" +
        "ToolRun\tTool\tstring\tget_checklist\tno\n";

    private static readonly StepEventCatalogue Catalogue = StepEventCatalogue.Parse(Events, Fields);

    [Fact]
    public void AJournalEventD47ActsOnExists()
    {
        Assert.Contains("Docked", (IEnumerable<string>)HandledEvents.ActedOn);
        Assert.True(Catalogue.Exists("Docked"));
    }

    [Fact]
    public void AJournalEventD47DoesNotActOnDoesNotExist()
    {
        Assert.DoesNotContain("NoSuchAppEvent", (IEnumerable<string>)HandledEvents.ActedOn);
        Assert.False(Catalogue.Exists("NoSuchAppEvent"));
        Assert.Empty(Catalogue.FieldsOf("NoSuchAppEvent"));
    }

    [Fact]
    public void AD47EventExistsWithoutBeingAJournalEvent()
    {
        Assert.True(Catalogue.Exists("ToolRun"));
    }

    [Fact]
    public void AnEventNotInTheTableDoesNotExist()
    {
        Assert.False(Catalogue.Exists("Nothing"));
        Assert.False(Catalogue.Exists(null));
    }

    [Fact]
    public void AnEventListsItsFieldsInNameOrder()
    {
        Assert.Equal(["StationName", "StationType", "Taxi"], Catalogue.FieldsOf("Docked"));
    }

    [Fact]
    public void AValueIsAllowedOnlyWhereTheTableHoldsIt()
    {
        Assert.True(Catalogue.AllowsValue("Docked", "StationType", "Coriolis"));
        Assert.False(Catalogue.AllowsValue("Docked", "StationType", "Orbis"));
        Assert.False(Catalogue.AllowsValue("Docked", "StationType", "coriolis"));
    }

    [Fact]
    public void AFieldWithNoValueRowsAllowsNoValue()
    {
        Assert.Contains("StationName", Catalogue.FieldsOf("Docked"));
        Assert.False(Catalogue.AllowsValue("Docked", "StationName", "Jameson Memorial"));
        Assert.Empty(Catalogue.ValuesOf("Docked", "StationName"));
    }

    [Fact]
    public void ABooleanFieldAllowsTrueAndFalse()
    {
        Assert.Equal(["false", "true"], Catalogue.ValuesOf("Docked", "Taxi"));
    }

    [Fact]
    public void ADrawableRowCarriesItsGatesAndCareer()
    {
        var drawable = Catalogue.Drawable.ToDictionary(row => row.Event);

        Assert.Equal(["BuggyBay"], drawable["LaunchSRV"].Needs);
        Assert.Equal("Exobiology", drawable["ScanOrganic"].Career);
        Assert.Empty(drawable["Docked"].Needs);
        Assert.False(drawable.ContainsKey("LoadGame"));
        Assert.False(drawable.ContainsKey("NoSuchAppEvent"));
    }

    [Fact]
    public void OnlyToolsMarkedDrawableAreDrawableValues()
    {
        Assert.True(Catalogue.IsDrawableValue("ToolRun", "Tool", "add_log_entry"));
        Assert.False(Catalogue.IsDrawableValue("ToolRun", "Tool", "get_checklist"));
    }

    [Fact]
    public void TheShippedTablesHoldAnEventRowForEveryActedOnEventWithASchema()
    {
        var shipped = StepEventCatalogue.Default;

        Assert.True(shipped.Exists("Docked"));
        Assert.True(shipped.Exists("ToolRun"));
        Assert.True(shipped.Exists("ChecklistLineAdded"));
        Assert.False(shipped.Exists("ChoiceMade"));
        Assert.Equal(["custom", "planned ship", "ship", "suit", "system", "weapon"], shipped.ValuesOf("ChecklistLineAdded", "Group"));
    }

    [Fact]
    public void OnlyWhitelistedStringFieldsCarryValueRows()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Docked.StationType", "Scan.PlanetClass", "Scan.StarType", "ScanOrganic.Genus",
            "ScanOrganic.Species", "ScanOrganic.ScanType", "StartJump.StarClass", "StartJump.JumpType",
            "MaterialCollected.Category", "MaterialCollected.Name", "CommitCrime.CrimeType",
            "USSDrop.USSType", "LaunchSRV.SRVType", "EngineerCraft.BlueprintName", "Synthesis.Name",
            "CodexEntry.Category", "CodexEntry.SubCategory", "MarketSell.Type", "MarketBuy.Type",
            "MiningRefined.Type", "CollectItems.Type", "CollectItems.Name",
            "ToolRun.Tool", "ChecklistLineAdded.Group",
        };

        using var stream = typeof(StepEventCatalogue).GetTypeInfo().Assembly.GetManifestResourceStream("D47.Core.StepFields");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);

        var stray = new List<string>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var cells = line.Split('\t');

            if (cells[3].Length > 0 && cells[2] == "string" && !allowed.Contains($"{cells[0]}.{cells[1]}"))
            {
                stray.Add($"{cells[0]}.{cells[1]} = {cells[3]}");
            }
        }

        Assert.Empty(stray);
    }

    [Fact]
    public void MaterialCollectedNamesOnlyEngineeringMaterials()
    {
        var shipped = StepEventCatalogue.Default;
        var materials = MaterialCatalogue.All.Where(entry => entry.Ledger == MaterialLedger.Material).Select(entry => entry.Symbol).ToHashSet(StringComparer.Ordinal);
        var stray = shipped.ValuesOf("MaterialCollected", "Name").Where(value => !materials.Contains(value)).ToList();

        Assert.Empty(stray);
        Assert.True(shipped.AllowsValue("MaterialCollected", "Name", "iron"));
        Assert.True(shipped.IsDrawableValue("MaterialCollected", "Name", "adaptiveencryptors"));

        foreach (var cargoOrItem in new[] { "ancientkey", "gold", "aerogel" })
        {
            Assert.False(shipped.AllowsValue("MaterialCollected", "Name", cargoOrItem));
        }
    }
}
