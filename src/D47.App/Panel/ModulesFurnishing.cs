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
    /// <summary>The page views built; going back shows a view without building it again.</summary>
    private readonly List<(string Key, ModulePageView View)> _pages = [];

    public void Enable(ModulesSource source)
    {
        if (_source is not null)
        {
            return;
        }

        _source = source;
        Furnish();
        source.Changed += () => Dispatcher.UIThread.Post(Refresh);
        source.Pages?.Watch?.Invoke((id, uri) => Dispatcher.UIThread.Post(() =>
        {
            foreach (var page in Live())
            {
                page.Updated(id, uri);
            }
        }));
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

        foreach (var page in Live())
        {
            page.Draw();
        }
    }

    /// <summary>Drills to another of the module's pages, its crumb the title its manifest gives.</summary>
    private void OpenPage(string id, string pageId)
    {
        var title = _source?.Installed.OfType<ReadyModule>().FirstOrDefault(m => m.Manifest.Id == id)?.Manifest.Pages
            .FirstOrDefault(p => p.Id == pageId)?.Title ?? pageId;

        host.Nav.Drill(PanelTab.Modules, new NavCrumb(ModulesTab.PageKey(id, pageId), title) { Whole = true });
    }

    /// <summary>The views on the Modules breadcrumb now; the rest are forgotten, so a page left behind reads nothing more.</summary>
    private List<ModulePageView> Live()
    {
        var trail = host.Nav.TrailOf(PanelTab.Modules).Select(crumb => crumb.Key).ToHashSet(StringComparer.Ordinal);
        _pages.RemoveAll(page => !trail.Contains(page.Key));

        return [.. _pages.Select(page => page.View)];
    }

    private Control Build(NavCrumb crumb)
    {
        var source = _source!;

        if (ModulesTab.IdOfKey(crumb.Key) is { } id)
        {
            var view = new ModulePageView(source, id, ModulesTab.PageIdOfKey(crumb.Key), host.OpenSettingsPlace, page => OpenPage(id, page));
            _pages.Add((crumb.Key, view));
            return view;
        }

        return _installed = new ModulesInstalledPage(
            source,
            module => host.Nav.Drill(PanelTab.Modules, new NavCrumb(ModulesTab.PageKey(((ReadyModule)module).Manifest.Id), ModulesTab.NameOf(module)) { Whole = true }),
            () => host.OpenSettingsPlace(D47.Core.Configuration.SettingsLayout.ModulesPlace));
    }
}
