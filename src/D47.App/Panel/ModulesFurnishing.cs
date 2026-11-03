using Avalonia.Controls;
using Avalonia.Threading;
using D47.App.Settings;
using D47.Core.Interface;
using D47.Core.Modules;

namespace D47.App.Panel;

/// <summary>The Modules tab: the installed list, and a page for each module that has one. Furnished only when a module is installed.</summary>
internal sealed class ModulesFurnishing(IPanelTabHost host)
{
    public const string Root = "modules";

    private ModulesSource? _source;
    private bool _furnished;
    private ModulesInstalledPage? _installed;
    private ModulePageView? _page;

    public void Enable(ModulesSource source)
    {
        if (_source is not null)
        {
            return;
        }

        _source = source;
        Furnish();
        source.Changed += () => Dispatcher.UIThread.Post(Refresh);
    }

    private void Furnish()
    {
        if (_furnished || _source is not { Installed.Count: > 0 })
        {
            return;
        }

        _furnished = true;
        host.Furnish(PanelTab.Modules, Build, new NavCrumb(Root, "Installed") { Help = D47.Core.Capabilities.Builtin.InterfaceCapability.Id });
    }

    private void Refresh()
    {
        Furnish();
        _installed?.Draw();
        _page?.Draw();
    }

    private Control Build(NavCrumb crumb)
    {
        var source = _source!;

        if (ModulesTab.IdOfKey(crumb.Key) is { } id)
        {
            return _page = new ModulePageView(source, id, host.OpenSettingsPlace);
        }

        return _installed = new ModulesInstalledPage(
            source,
            module => host.Nav.Drill(PanelTab.Modules, new NavCrumb(ModulesTab.PageKey(((ReadyModule)module).Manifest.Id), ModulesTab.NameOf(module)) { Whole = true }),
            () => host.OpenSettingsPlace(D47.Core.Configuration.SettingsLayout.ModulesPlace));
    }
}
