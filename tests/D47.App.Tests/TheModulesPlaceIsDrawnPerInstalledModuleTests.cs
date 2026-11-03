using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using D47.Core.Configuration;
using D47.Core.Modules;
using Xunit;
using static D47.App.Tests.SettingsPageReading;

namespace D47.App.Tests;

/// <summary>The Modules settings place: absent with no module, one group per installed module otherwise.</summary>
[Trait("Category", "Integration")]
public sealed class TheModulesPlaceIsDrawnPerInstalledModuleTests
{
    private static ModuleManifest Manifest(string id, string name, int interval = 120) =>
        new(1, id, name, "Ana Vey", "1.4.0", id + ".exe", [], interval, [], null);

    private static ModulesSource Source(
        IReadOnlyList<InstalledModule> installed, IReadOnlyList<ModuleStatusReport>? statuses = null) =>
        new(
            () => installed,
            () => statuses ?? [],
            _ => { },
            () => [("warden", "Warden")],
            () => [],
            () => []);

    private static SettingsHost Draw(ModulesSource? source)
    {
        var (settings, viewState, paths) = TestSurface.Create();
        return SettingsHost.Open(settings, viewState, paths, modules: source);
    }

    private static T Named<T>(SettingsHost host, string name)
        where T : Control =>
        host.View.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    [AvaloniaFact]
    public void ThePlaceIsAbsentWithNoModuleInstalled()
    {
        var host = Draw(Source([]));

        Assert.DoesNotContain(PlaceItems(host.View), item => item.IsVisible && Name(item).Text == "Modules");

        host.Close();
    }

    [AvaloniaFact]
    public void EachInstalledModuleGetsAGroupWithItsRows()
    {
        var ledger = new ReadyModule("ledger", Manifest("ledger", "Bounty ledger"), "ledger.exe");
        var comms = new ReadyModule("comms", Manifest("comms", "Comms filter"), "comms.exe");
        var host = Draw(Source([ledger, comms]));

        Assert.Contains(PlaceItems(host.View), item => item.IsVisible && Name(item).Text == "Modules");

        Open(host.View, "modules");

        Assert.Equal(2, host.View.GetVisualDescendants().OfType<StackPanel>().Count(p => p.Name == ModulesPlaceView.GroupName));

        var box = Named<CheckBox>(host, ModulesPlaceView.TurnedOnName + ".ledger");
        Assert.Equal("Run Bounty ledger with D47", ((TextBlock)box.Content!).Text);

        Assert.NotNull(Named<Button>(host, ModulesPlaceView.SpeaksAsName + ".comms"));
        Assert.NotNull(Named<Control>(host, ModulesPlaceView.LeavesName + ".comms"));

        host.Close();
    }

    [AvaloniaFact]
    public void TickingTurnedOnTurnsTheModuleOnAndClearingItTurnsItOff()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var ledger = new ReadyModule("ledger", Manifest("ledger", "Bounty ledger"), "ledger.exe");
        var host = SettingsHost.Open(settings, viewState, paths, modules: Source([ledger]));

        Open(host.View, "modules");

        Named<CheckBox>(host, ModulesPlaceView.TurnedOnName + ".ledger").IsChecked = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(settings.Current.ModuleFor("ledger").Enabled);

        Named<CheckBox>(host, ModulesPlaceView.TurnedOnName + ".ledger").IsChecked = false;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(settings.Current.ModuleFor("ledger").Enabled);

        host.Close();
    }

    [AvaloniaFact]
    public void AnIntervalShorterThanTheModulesMinimumIsDisabled()
    {
        var (settings, viewState, paths) = TestSurface.Create();
        var slow = new ReadyModule("slow", Manifest("slow", "Exo notes", interval: 900), "slow.exe");
        var host = SettingsHost.Open(settings, viewState, paths, modules: Source([slow]));

        settings.Replace("on", s => s with { Modules = new Dictionary<string, ModuleSettings> { ["slow"] = new() { Enabled = true } } });
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Open(host.View, "modules");

        var segment = Named<Segment>(host, ModulesPlaceView.IntervalName + ".slow");
        var buttons = segment.GetVisualDescendants().OfType<Button>().ToList();

        Assert.False(buttons[1].IsEnabled);
        Assert.True(buttons[2].IsEnabled);

        host.Close();
    }

    [AvaloniaFact]
    public void ARefusedModuleShowsItsNoticeAndACheckboxThatCannotBeTicked()
    {
        var refused = new RefusedModule("signal", ModuleRefusalReason.UnknownContract, "contract 9");
        var host = Draw(Source([refused]));

        Open(host.View, "modules");

        var box = Named<CheckBox>(host, ModulesPlaceView.TurnedOnName + ".signal");
        Assert.False(box.IsEnabled);
        Assert.NotNull(Named<Notice>(host, ModulesPlaceView.NoticeName));

        host.Close();
    }
}
