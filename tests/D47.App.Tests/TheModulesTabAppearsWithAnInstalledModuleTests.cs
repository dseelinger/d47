using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.Core.Interface;
using D47.Core.Modules;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Modules tab: absent with no module installed, and a page per state when one is (#2083).</summary>
[Trait("Category", "Integration")]
public sealed class TheModulesTabAppearsWithAnInstalledModuleTests
{
    private sealed class Modules
    {
        public List<InstalledModule> Installed { get; } = [];

        public List<ModuleStatusReport> Statuses { get; } = [];

        public List<string> Restarted { get; } = [];

        public ModulesSource Source() => new(() => Installed, () => Statuses, Restarted.Add, () => [], () => [], () => []);

        public ReadyModule Add(string id, string name, int pages, ModuleStatus? status = null)
        {
            var module = new ReadyModule(
                "modules\\" + id,
                new ModuleManifest(
                    1, id, name, "CMDR Ana Vey", "1.0.0", id + ".exe", [], 120,
                    [.. Enumerable.Range(1, pages).Select(n => new ModulePage("p" + n, "Page " + n))], null),
                id + ".exe");

            Installed.Add(module);

            if (status is { } s)
            {
                Statuses.Add(new ModuleStatusReport(module.Folder, id, s, new DateTimeOffset(2026, 11, 2, 19, 42, 0, TimeSpan.Zero)));
            }

            return module;
        }
    }

    private static SettingsHost Open(Modules modules, bool headset = false)
    {
        new D47.App.Theming.ThemeManager(Avalonia.Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths, headset: headset, width: 1280, height: 800);
        host.Panel.EnableModules(modules.Source());
        Dispatcher.UIThread.RunJobs();
        return host;
    }

    private static T Named<T>(SettingsHost host, string name)
        where T : Control =>
        host.Panel.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static void OpenModule(SettingsHost host, string name)
    {
        host.Panel.Tab = PanelTab.Modules;
        Dispatcher.UIThread.RunJobs();
        Named<Button>(host, ModulesInstalledPage.RowName + "." + name).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void WithNoModuleInstalledThereIsNoModulesTab()
    {
        var host = Open(new Modules());

        Assert.False(host.Panel.Nav.Has(PanelTab.Modules));
        Assert.False(host.Panel.GetControl<RadioButton>("ModulesTab").IsVisible);

        host.Close();
    }

    [AvaloniaFact]
    public void WithAModuleInstalledTheTabIsThereOnTheWindowAndTheBigPanel()
    {
        var modules = new Modules();
        modules.Add("ledger", "Bounty ledger", 2, ModuleStatus.Running);

        foreach (var headset in new[] { false, true })
        {
            var host = Open(modules, headset);

            Assert.True(host.Panel.Nav.Has(PanelTab.Modules));
            Assert.True(host.Panel.GetControl<RadioButton>("ModulesTab").IsVisible);

            host.Close();
        }
    }

    [AvaloniaFact]
    public void TheTabAppearsWhenTheCatalogFindsAModuleAfterStartup()
    {
        var modules = new Modules();
        var source = modules.Source();
        var (settings, viewState, paths) = TestSurface.Create();
        var host = SettingsHost.Open(settings, viewState, paths);
        host.Panel.EnableModules(source);

        Assert.False(host.Panel.Nav.Has(PanelTab.Modules));

        modules.Add("ledger", "Bounty ledger", 1, ModuleStatus.Running);
        source.Raise();
        Dispatcher.UIThread.RunJobs();

        Assert.True(host.Panel.Nav.Has(PanelTab.Modules));

        host.Close();
    }

    [AvaloniaFact]
    public void TheInstalledListGivesEveryModuleAndDisablesTheOnesWithNoPage()
    {
        var modules = new Modules();
        modules.Add("ledger", "Bounty ledger", 2, ModuleStatus.Running);
        modules.Add("comms", "Comms filter", 0, ModuleStatus.Running);
        modules.Installed.Add(new RefusedModule("modules\\signal", ModuleRefusalReason.UnknownContract, "contract 9"));
        var host = Open(modules);

        host.Panel.Tab = PanelTab.Modules;
        Dispatcher.UIThread.RunJobs();

        host.Window.CaptureRenderedFrame()!.SaveCapture("modules-installed.png");

        Assert.True(Named<Button>(host, ModulesInstalledPage.RowName + ".Bounty ledger").IsEnabled);
        Assert.False(Named<Button>(host, ModulesInstalledPage.RowName + ".Comms filter").IsEnabled);
        Assert.False(Named<Button>(host, ModulesInstalledPage.RowName + ".signal").IsEnabled);

        host.Close();
    }

    [AvaloniaFact]
    public void PressingAModuleDrillsToItsPageUnderTheModulesCrumb()
    {
        var modules = new Modules();
        modules.Add("ledger", "Bounty ledger", 1, ModuleStatus.Running);
        var host = Open(modules);

        OpenModule(host, "Bounty ledger");

        var trail = host.Panel.Nav.TrailOf(PanelTab.Modules);
        Assert.Equal("Bounty ledger", trail[^1].Word);
        Assert.DoesNotContain(host.Panel.GetVisualDescendants().OfType<ModulesInstalledPage>(), p => p.IsEffectivelyVisible);
        Assert.Equal("A module by CMDR Ana Vey.", ((IPageSummary)host.Panel.GetVisualDescendants().OfType<ModulePageView>().Single()).Summary);

        host.Close();
    }

    [AvaloniaFact]
    public void ARunningModuleWhosePageHasNotArrivedOffersTryAgainAndRestartAtFortyFourPixels()
    {
        var modules = new Modules();
        modules.Add("ledger", "Bounty ledger", 1, ModuleStatus.Running);
        var host = Open(modules);

        OpenModule(host, "Bounty ledger");

        host.Window.CaptureRenderedFrame()!.SaveCapture("modules-page-not-arrived.png");

        Assert.NotNull(Named<Notice>(host, ModulePageView.NoticeName));
        var again = Named<Button>(host, ModulePageView.TryAgainName);
        var restart = Named<Button>(host, ModulePageView.RestartName);

        Assert.True(again.Bounds.Height >= 44);
        Assert.True(restart.Bounds.Height >= 44);

        restart.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(["ledger"], modules.Restarted);

        host.Close();
    }

    [AvaloniaFact]
    public void EachOtherStateDrawsItsOwnNoticeAndControls()
    {
        var modules = new Modules();
        modules.Add("exo", "Exo notes", 1, ModuleStatus.Starting);
        modules.Add("ledger", "Bounty ledger", 1, ModuleStatus.NotResponding);
        modules.Add("route", "Route notes", 1, ModuleStatus.StoppedAfterRepeatedCrashes);
        modules.Add("trade", "Trade whisper", 1);
        var host = Open(modules);

        OpenModule(host, "Exo notes");
        Assert.NotNull(Named<Border>(host, ModulePageView.StartingName));
        host.Panel.GoBack();

        OpenModule(host, "Bounty ledger");
        Assert.NotNull(Named<Notice>(host, ModulePageView.NoticeName));
        Assert.True(Named<Button>(host, ModulePageView.RestartName).Bounds.Height >= 44);
        host.Panel.GoBack();

        OpenModule(host, "Route notes");
        Assert.NotNull(Named<Notice>(host, ModulePageView.NoticeName));
        Assert.True(Named<Button>(host, ModulePageView.RestartName).Bounds.Height >= 44);
        host.Panel.GoBack();

        OpenModule(host, "Trade whisper");
        host.Window.CaptureRenderedFrame()!.SaveCapture("modules-page-off.png");
        Assert.NotNull(Named<Border>(host, ModulePageView.OffName));
        Assert.True(Named<Button>(host, ModulePageView.SettingsName).Bounds.Height >= 44);

        host.Close();
    }
}
