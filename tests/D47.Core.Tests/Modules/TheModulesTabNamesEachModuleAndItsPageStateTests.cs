using D47.Core.Interface;
using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

public class TheModulesTabNamesEachModuleAndItsPageStateTests
{
    private static ReadyModule Ready(string id, string name, int pages) =>
        new(
            "data\\modules\\" + id,
            new ModuleManifest(
                1, id, name, "CMDR Ana Vey", "1.0.0", id + ".exe", [], 120,
                [.. Enumerable.Range(1, pages).Select(n => new ModulePage("p" + n, "Page " + n))], null),
            id + ".exe");

    [Fact]
    public void TheModulesTabSitsBetweenNavigationAndSettings()
    {
        var tabs = Enum.GetValues<PanelTab>().ToList();

        Assert.Equal(tabs.IndexOf(PanelTab.Astrometrics) + 1, tabs.IndexOf(PanelTab.Modules));
        Assert.Equal(tabs.IndexOf(PanelTab.Modules) + 1, tabs.IndexOf(PanelTab.Settings));
    }

    [Fact]
    public void AStoredTabNameStillMeansTheSameTab()
    {
        Assert.Equal(PanelTab.Astrometrics, PanelTabNames.Parse("Astrometrics"));
        Assert.Equal(PanelTab.Astrometrics, PanelTabNames.Parse("Navigation"));
        Assert.Equal(PanelTab.Astrometrics, PanelTabNames.Parse("Routing"));
        Assert.Equal(PanelTab.Settings, PanelTabNames.Parse("Settings"));
        Assert.Equal(PanelTab.Modules, PanelTabNames.Parse("Modules"));
    }

    [Theory]
    [InlineData(0, "A module by CMDR Ana Vey · tools and lines, no page")]
    [InlineData(1, "A module by CMDR Ana Vey · 1 page")]
    [InlineData(2, "A module by CMDR Ana Vey · 2 pages")]
    public void ARowStatesTheAuthorAndThePageCount(int pages, string expected) =>
        Assert.Equal(expected, ModulesTab.RowSub(Ready("ledger", "Bounty ledger", pages)));

    [Fact]
    public void AModuleWithNoPageAndARefusedOneAreNotOpenable()
    {
        Assert.True(ModulesTab.Openable(Ready("ledger", "Bounty ledger", 1)));
        Assert.False(ModulesTab.Openable(Ready("comms", "Comms filter", 0)));
        Assert.False(ModulesTab.Openable(new RefusedModule("signal", ModuleRefusalReason.UnknownContract, "contract 9")));
    }

    [Fact]
    public void ARefusedModuleIsNamedByItsFolder()
    {
        var refused = new RefusedModule(Path.Combine("data", "modules", "signal") + Path.DirectorySeparatorChar, ModuleRefusalReason.UnknownContract, "contract 9");

        Assert.Equal("signal", ModulesTab.NameOf(refused));
        Assert.Equal("Built for a contract this D47 does not know", ModulesTab.RowSub(refused));
        Assert.Equal(ModuleStatus.Refused, ModulesTab.StatusOf(refused, []));
    }

    [Theory]
    [InlineData(ModuleStatus.Starting, ModulePageState.Starting)]
    [InlineData(ModuleStatus.NotResponding, ModulePageState.NotResponding)]
    [InlineData(ModuleStatus.StoppedAfterRepeatedCrashes, ModulePageState.Stopped)]
    [InlineData(ModuleStatus.Off, ModulePageState.Off)]
    [InlineData(ModuleStatus.Running, ModulePageState.NotArrived)]
    public void EachStatusHasItsPageState(ModuleStatus status, ModulePageState state) =>
        Assert.Equal(state, ModulesTab.StateOf(status));

    [Fact]
    public void TheStateStringsAreTheOnesTheDesignGives()
    {
        Assert.Equal("Exo notes is starting. Its page appears when it answers.", ModulesTab.StartingText("Exo notes"));
        Assert.Equal("Bounty ledger is not responding", ModulesTab.NotRespondingLabel("Bounty ledger"));
        Assert.Equal("Route notes stopped", ModulesTab.StoppedLabel("Route notes"));
        Assert.Equal("Trade whisper is off. Its tools, lines and pages come back when you turn it on.", ModulesTab.OffText("Trade whisper"));
        Assert.Equal("Bounty ledger is running, but it did not send this page. Try again, or restart it.", ModulesTab.NotArrivedText("Bounty ledger"));
        Assert.Equal("A module by CMDR Ana Vey.", ModulesTab.PageSummary("CMDR Ana Vey"));
    }

    [Fact]
    public void AModuleNoStatusHasBeenReportedForIsOff()
    {
        var module = Ready("ledger", "Bounty ledger", 1);

        Assert.Equal(ModuleStatus.Off, ModulesTab.StatusOf(module, []));
        Assert.Equal(
            ModuleStatus.Running,
            ModulesTab.StatusOf(module, [new ModuleStatusReport(module.Folder.ToUpperInvariant(), "ledger", ModuleStatus.Running)]));
    }

    [Fact]
    public void ALevelKeyRoundTripsTheModuleId()
    {
        Assert.Equal("ledger", ModulesTab.IdOfKey(ModulesTab.PageKey("ledger")));
        Assert.Null(ModulesTab.IdOfKey("modules"));
    }
}
