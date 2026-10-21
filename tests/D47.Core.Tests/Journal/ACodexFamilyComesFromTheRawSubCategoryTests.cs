using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ACodexFamilyComesFromTheRawSubCategoryTests
{
    private static CodexLogEntry Logged(string localised, string symbol)
    {
        var line = "{\"timestamp\":\"2026-10-02T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":101,\"Name\":\"$Codex_Ent_101;\""
            + ",\"Category\":\"$Codex_Category_Biology;\",\"SubCategory\":\"" + symbol + "\",\"SubCategory_Localised\":\"" + localised
            + "\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"SystemAddress\":1,\"BodyID\":5}";

        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return Assert.Single(CodexLedger.None.Apply(parsed!).Entries);
    }

    [Theory]
    [InlineData("$Codex_SubCategory_Stars;", "stars.jpg")]
    [InlineData("$Codex_SubCategory_Gas_Giants;", "gas-giant-planets.jpg")]
    [InlineData("$Codex_SubCategory_Terrestrials;", "terrestrial-planets.jpg")]
    [InlineData("$Codex_SubCategory_Geology_and_Anomalies;", "geology-and-anomalies.jpg")]
    [InlineData("$Codex_SubCategory_Organic_Structures;", "organic-structures.jpg")]
    [InlineData("$Codex_SubCategory_Guardian;", "guardian-objects.jpg")]
    public void EachFamilyHasItsPlaceholder(string symbol, string file) =>
        Assert.Equal(file, CodexFamilies.FileOfSymbol(symbol));

    [Theory]
    [InlineData("$Codex_SubCategory_X;")]
    [InlineData("")]
    [InlineData(null)]
    public void ASymbolOutsideTheFamiliesHasNoPlaceholder(string? symbol) =>
        Assert.Null(CodexFamilies.FileOfSymbol(symbol));

    [Theory]
    [InlineData("Organic Structures")]
    [InlineData("Strukturen")]
    [InlineData("Structures organiques")]
    public void TheLedgerKeepsTheSymbolWhateverLanguageTheGameNamesItIn(string localised)
    {
        var entry = Logged(localised, "$Codex_SubCategory_Organic_Structures;");

        Assert.Equal(localised, entry.SubCategory);
        Assert.Equal("$Codex_SubCategory_Organic_Structures;", entry.SubCategorySymbol);
        Assert.Equal("organic-structures.jpg", CodexFamilies.FileOfSymbol(entry.SubCategorySymbol));
    }

    [Theory]
    [InlineData("Gas giant planets", "gas-giant-planets.jpg")]
    [InlineData("Geology and anomalies", "geology-and-anomalies.jpg")]
    [InlineData("Organic structures", "organic-structures.jpg")]
    [InlineData("Guardian objects", "guardian-objects.jpg")]
    public void AnEntryOnlyTheCodexTableKnowsFindsItsFamilyByTheTablesName(string name, string file) =>
        Assert.Equal(file, CodexFamilies.FileOfTableName(name));

    [Fact]
    [Trait("Category", "Gate")]
    public void EveryPlaceholderTheFamiliesNameShipsInTheAssetsFolder()
    {
        var root = AppContext.BaseDirectory;

        while (!File.Exists(Path.Combine(root, "d47.slnx")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("d47.slnx not found");
        }

        var folder = Path.Combine(root, "assets", "codex-placeholders");
        var named = new[]
        {
            "stars.jpg", "gas-giant-planets.jpg", "terrestrial-planets.jpg", "geology-and-anomalies.jpg",
            "organic-structures.jpg", "guardian-objects.jpg", "thargoid-objects.jpg",
        };

        Assert.All(named, file => Assert.True(File.Exists(Path.Combine(folder, file)), file));
    }
}
