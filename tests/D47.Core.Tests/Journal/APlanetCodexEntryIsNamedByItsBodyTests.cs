using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class APlanetCodexEntryIsNamedByItsBodyTests
{
    private static string Line(string symbol, string? localised = "Non Terraformable") =>
        "{\"timestamp\":\"2026-10-10T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":1,\"Name\":\"" + symbol + "\""
        + (localised is null ? string.Empty : ",\"Name_Localised\":\"" + localised + "\"")
        + ",\"Category\":\"$Codex_Category_StellarBodies;\",\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\"}";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Theory]
    [InlineData("Standard_Ter_High_Metal_Content", "High metal content world with an atmosphere")]
    [InlineData("Standard_High_Metal_Content_No_Atmos", "High metal content world, no atmosphere")]
    [InlineData("TRF_Ter_High_Metal_Content", "Terraformable high metal content world with an atmosphere")]
    [InlineData("Standard_Ter_Metal_Rich", "Metal-rich world with an atmosphere")]
    [InlineData("Standard_Rocky_No_Atmos", "Rocky world, no atmosphere")]
    [InlineData("TRF_Ter_Rocky", "Terraformable rocky world with an atmosphere")]
    [InlineData("Standard_Rocky_Ice_No_Atmos", "Rocky ice world, no atmosphere")]
    [InlineData("Standard_Ter_Ice", "Icy world with an atmosphere")]
    [InlineData("Standard_Water_Worlds", "Water world")]
    [InlineData("TRF_Water_Worlds", "Terraformable water world")]
    [InlineData("Standard_Ammonia_Worlds", "Ammonia world")]
    [InlineData("Standard_Sudarsky_Class_I", "Class I gas giant")]
    [InlineData("Standard_Sudarsky_Class_V", "Class V gas giant")]
    [InlineData("Standard_Giant_With_Water_Life", "Gas giant with water-based life")]
    [InlineData("Standard_Giant_With_Ammonia_Life", "Gas giant with ammonia-based life")]
    [InlineData("Standard_Water_Giant", "Water giant")]
    [InlineData("TRF_Metal_Rich_No_Atmos", "Terraformable metal-rich world, no atmosphere")]
    public void TheSymbolGivesTheName(string body, string expected)
    {
        var entry = Assert.Single(CodexLedger.None.Apply(Event(Line("$Codex_Ent_" + body + "_Name;"))).Entries);

        Assert.Equal(expected, entry.Name);
    }

    [Theory]
    [InlineData("$Codex_Ent_Earth_Likes_Name;", "Earth-Like Planet")]
    [InlineData("$Codex_Ent_K_Name;", "K Type Star")]
    [InlineData("$Codex_Ent_Standard_Something_New_Name;", "Something New")]
    public void ASymbolThatIsNotParsedKeepsElitesName(string symbol, string localised)
    {
        var entry = Assert.Single(CodexLedger.None.Apply(Event(Line(symbol, localised))).Entries);

        Assert.Equal(localised, entry.Name);
    }

    [Fact]
    public void ASymbolThatIsNotParsedAndHasNoLocalisedNameKeepsTheSymbol()
    {
        var entry = Assert.Single(CodexLedger.None.Apply(Event(Line("$Codex_Ent_Standard_Something_New_Name;", null))).Entries);

        Assert.Equal("$Codex_Ent_Standard_Something_New_Name;", entry.Name);
    }

    [Fact]
    public void TheTranscriptLineUsesTheBodyName()
    {
        var line = JournalSentence.For(Event(Line("$Codex_Ent_Standard_Ter_High_Metal_Content_Name;")));

        Assert.Equal("Codex entry: High metal content world with an atmosphere", line);
    }
}
