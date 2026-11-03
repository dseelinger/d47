using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

public class AModulePageIsReadOnlyFromTheComponentSetTests
{
    private static readonly HashSet<string> Tools = ["remind", "clear_session", "say_each", "count", "threshold"];

    private static readonly HashSet<string> Pages = ["ledger", "open"];

    private static ModulePageParse Parse(string parts) => ModulePageParser.Parse($$"""{"parts":[{{parts}}]}""", Tools, Pages);

    private static T Only<T>(string part)
        where T : ModulePart
    {
        var parse = Parse(part);
        Assert.Null(parse.Refusal);
        return Assert.IsType<T>(Assert.Single(parse.Page!.Parts));
    }

    private static string Refusal(string parts)
    {
        var parse = Parse(parts);
        Assert.Null(parse.Page);
        return parse.Refusal!;
    }

    [Fact]
    public void AGroupHeadHasATitleAndADescription()
    {
        var head = Only<ModuleGroupHead>("""{"type":"GroupHead","title":"This session","description":"Bounties since you launched the game."}""");

        Assert.Equal("This session", head.Title);
        Assert.Equal("Bounties since you launched the game.", head.Description);
    }

    [Fact]
    public void ADataBlockIsAGridOfUpToThreeOrALineOfProse()
    {
        var grid = Only<ModuleDataBlock>(
            """{"type":"DataBlock","cells":[{"label":"Claimed","value":"14"},{"label":"Value","value":"4,210,500 CR"},{"label":"Hunting in","value":"GIRYAK","tone":"here"}]}""");

        Assert.Equal(["Claimed", "Value", "Hunting in"], grid.Cells.Select(c => c.Label));
        Assert.Equal(ModuleTone.Here, grid.Cells[2].Tone);
        Assert.Null(grid.Cells[0].Tone);
        Assert.Null(grid.Prose);

        var prose = Only<ModuleDataBlock>("""{"type":"DataBlock","prose":"Fourteen bounties claimed.","tone":"name"}""");

        Assert.Equal("Fourteen bounties claimed.", prose.Prose);
        Assert.Equal(ModuleTone.Name, prose.Tone);
        Assert.Contains("1 to 3 entries", Refusal("""{"type":"DataBlock","cells":[{"label":"a","value":"1"},{"label":"b","value":"2"},{"label":"c","value":"3"},{"label":"d","value":"4"}]}"""));
    }

    [Fact]
    public void ANoticeIsAWarningOrAnError()
    {
        var notice = Only<ModulePageNotice>("""{"type":"Notice","tone":"warning","label":"3 vouchers not handed in","text":"Hand them in."}""");

        Assert.Equal(ModuleTone.Warning, notice.Tone);
        Assert.Equal("3 vouchers not handed in", notice.Label);
        Assert.Contains("warning or an error", Refusal("""{"type":"Notice","tone":"here","label":"a","text":"b"}"""));
    }

    [Fact]
    public void ASettingsRowHoldsAStatusASegmentedOrAStepper()
    {
        var status = Assert.IsType<ModuleStatusBlock>(Only<ModuleSettingsRow>(
            """{"type":"SettingsRow","label":"Open bounties","status":{"text":"5 open","tone":"value","button":{"label":"All open bounties","page":"open"}}}""").Control);

        Assert.Equal("5 open", status.Text);
        Assert.Equal("open", status.Button!.Opens);

        var segmented = Assert.IsType<ModuleChoice>(Only<ModuleSettingsRow>(
            """{"type":"SettingsRow","label":"Count","segmented":{"tool":"count","argument":"period","options":[{"label":"This session","value":"session"},{"label":"Today","value":"today"},{"label":"All time","value":"all"}],"selected":"today"}}""").Control);

        Assert.False(segmented.Stepper);
        Assert.Equal(1, segmented.Selected);
        Assert.Equal("\"all\"", segmented.Options[2].ValueJson);
        Assert.Equal(new ModuleSends("count", "period"), segmented.Sends);

        var stepper = Assert.IsType<ModuleChoice>(Only<ModuleSettingsRow>(
            """{"type":"SettingsRow","label":"Point out a bounty above","stepper":{"tool":"threshold","argument":"credits","options":[{"label":"50,000 CR","value":50000},{"label":"100,000 CR","value":100000}],"selected":100000}}""").Control);

        Assert.True(stepper.Stepper);
        Assert.Equal(1, stepper.Selected);
        Assert.Equal("100000", stepper.Options[1].ValueJson);

        Assert.Contains("one of status, segmented or stepper", Refusal("""{"type":"SettingsRow","label":"Count"}"""));
        Assert.Contains("selects a value none of its options has", Refusal(
            """{"type":"SettingsRow","label":"Count","segmented":{"tool":"count","argument":"period","options":[{"label":"A","value":"a"},{"label":"B","value":"b"}],"selected":"c"}}"""));
    }

    [Fact]
    public void CheckboxTilesAreAGridOfTwoOrFour()
    {
        var tiles = Only<ModuleCheckboxTiles>(
            """{"type":"CheckboxTiles","columns":4,"tiles":[{"label":"Say each bounty as it lands","checked":true,"tool":"say_each","argument":"on"}]}""");

        Assert.Equal(4, tiles.Columns);
        Assert.True(tiles.Tiles[0].Checked);
        Assert.Equal(2, Only<ModuleCheckboxTiles>("""{"type":"CheckboxTiles","tiles":[{"label":"a","tool":"say_each","argument":"on"}]}""").Columns);
        Assert.Contains("2 or 4", Refusal("""{"type":"CheckboxTiles","columns":3,"tiles":[{"label":"a","tool":"say_each","argument":"on"}]}"""));
    }

    [Fact]
    public void ListRowsSitUnderAListHeadAndMayOpenAnotherPage()
    {
        var list = Only<ModuleList>(
            """{"type":"List","head":"Open bounties","rows":[{"name":"Kel Ardon","sub":"Python · Giryak","aside":"212,000 CR","page":"open"},{"name":"Sabre Lune"}]}""");

        Assert.Equal("Open bounties", list.Head);
        Assert.Equal(new ModuleListRow("Kel Ardon", "Python · Giryak", "212,000 CR", "open"), list.Rows[0]);
        Assert.Null(list.Rows[1].Opens);
    }

    [Fact]
    public void TileButtonsCallAToolOrOpenAPageAndMayBeDestructive()
    {
        var buttons = Only<ModuleTileButtons>(
            """{"type":"TileButtons","buttons":[{"label":"Remind me to hand them in","tool":"remind","arguments":{"minutes":30}},{"label":"Clear the session","tool":"clear_session","destructive":true}]}""");

        Assert.Equal(new ModuleToolCall("remind", """{"minutes":30}"""), buttons.Buttons[0].Calls);
        Assert.Equal("{}", buttons.Buttons[1].Calls!.ArgumentsJson);
        Assert.True(buttons.Buttons[1].Destructive);
        Assert.Contains("either a tool or a page", Refusal("""{"type":"TileButtons","buttons":[{"label":"a","tool":"remind","page":"open"}]}"""));
    }

    [Fact]
    public void ATableHasColumnsAndRowsOfTheSameWidth()
    {
        var table = Only<ModuleTable>(
            """{"type":"Table","columns":[{"label":"Target"},{"label":"System"},{"label":"Bounty","numeric":true}],"rows":[["Kel Ardon",{"text":"Giryak","tone":"here"},212000]]}""");

        Assert.Equal([false, false, true], table.Columns.Select(c => c.Numeric));
        Assert.Equal(new ModuleCell("Giryak", ModuleTone.Here), table.Rows[0][1]);
        Assert.Equal("212000", table.Rows[0][2].Text);
        Assert.Contains("3 cells", Refusal("""{"type":"Table","columns":[{"label":"a"},{"label":"b"},{"label":"c"}],"rows":[["x","y"]]}"""));
    }

    [Fact]
    public void AMessageGivesItsTextAndWhenButNotWhoSpeaks()
    {
        var message = Only<ModuleMessage>("""{"type":"Message","text":"Fourteen claimed.","at":"2026-11-02T19:42:00Z"}""");

        Assert.Equal("Fourteen claimed.", message.Text);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 19, 42, 0, TimeSpan.Zero), message.At);
        Assert.Contains("unknown property \"speaker\"", Refusal("""{"type":"Message","text":"a","speaker":"Warden"}"""));
    }

    [Fact]
    public void AnUnknownComponentRefusesThePage() =>
        Assert.Contains("unknown component \"Html\"", Refusal("""{"type":"GroupHead","title":"a"},{"type":"Html","text":"<b>a</b>"}"""));

    [Fact]
    public void AnUnknownToneRefusesThePage() =>
        Assert.Contains("unknown tone \"#ff0000\"", Refusal("""{"type":"DataBlock","cells":[{"label":"a","value":"1","tone":"#ff0000"}]}"""));

    [Theory]
    [InlineData("color")]
    [InlineData("colour")]
    [InlineData("font")]
    [InlineData("fontSize")]
    [InlineData("image")]
    [InlineData("size")]
    [InlineData("width")]
    public void AColourFontImageOrSizeRefusesThePage(string property) =>
        Assert.Contains(
            $"gives a colour, font, image or size (\"{property}\")",
            Refusal($$"""{"type":"GroupHead","title":"a","{{property}}":"x"}"""));

    [Fact]
    public void AColourDeepInsideAPartRefusesThePage() =>
        Assert.Contains(
            "gives a colour, font, image or size (\"background\")",
            Refusal("""{"type":"TileButtons","buttons":[{"label":"a","tool":"remind","background":"red"}]}"""));

    [Fact]
    public void AThirdNoticeRefusesThePage()
    {
        const string Notice = """{"type":"Notice","tone":"error","label":"a","text":"b"}""";

        Assert.Null(Parse(Notice + "," + Notice).Refusal);
        Assert.Contains("third notice", Refusal(Notice + "," + Notice + "," + Notice));
    }

    [Theory]
    [InlineData("""{"type":"TileButtons","buttons":[{"label":"Wipe","tool":"mod_other_wipe"}]}""")]
    [InlineData("""{"type":"TileButtons","buttons":[{"label":"Wipe","tool":"wipe"}]}""")]
    [InlineData("""{"type":"CheckboxTiles","tiles":[{"label":"Wipe","tool":"wipe","argument":"on"}]}""")]
    [InlineData("""{"type":"SettingsRow","label":"a","status":{"text":"b","button":{"label":"Wipe","tool":"set_volume"}}}""")]
    public void AButtonNamingAnotherModulesToolRefusesThePage(string part) =>
        Assert.Contains("which is not one of this module's tools", Refusal(part));

    [Fact]
    public void ALinkToAPageTheManifestDoesNotGiveRefusesThePage() =>
        Assert.Contains("which is not one of this module's pages", Refusal("""{"type":"List","rows":[{"name":"a","page":"elsewhere"}]}"""));

    [Fact]
    public void APageThatIsNotJsonOrHasNoPartsIsRefused()
    {
        Assert.StartsWith("the page is not JSON", ModulePageParser.Parse("<html></html>", Tools, Pages).Refusal);
        Assert.Contains("unknown property \"title\"", ModulePageParser.Parse("""{"title":"a","parts":[]}""", Tools, Pages).Refusal);
        Assert.Contains("1 to 200", ModulePageParser.Parse("""{"parts":[]}""", Tools, Pages).Refusal);
    }

    [Fact]
    public void APageIsReadAsItsResource() =>
        Assert.Equal("d47://pages/ledger", ModulePageParser.Uri("ledger"));

    [Fact]
    public void ASubPageHasALevelKeyOfItsOwn()
    {
        var key = ModulesTab.PageKey("ledger", "open");

        Assert.Equal("ledger", ModulesTab.IdOfKey(key));
        Assert.Equal("open", ModulesTab.PageIdOfKey(key));
        Assert.Null(ModulesTab.PageIdOfKey(ModulesTab.PageKey("ledger")));
        Assert.Null(ModulesTab.PageIdOfKey("modules"));
    }
}
