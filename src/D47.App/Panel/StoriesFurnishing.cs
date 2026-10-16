using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Stories tab and its mini-size story.</summary>
internal sealed class StoriesFurnishing(IPanelTabHost host)
{
    private AdventureSurface? _surface;
    private Action? _refresh;
    private bool _askShown;

    public AdventureMini? Mini { get; private set; }

    /// <summary>Shows or hides Short Story and Story log as the settings and the book change.</summary>
    public bool Tick() => ShowConditionalRoots();

    private bool ShowConditionalRoots()
    {
        if (_surface is not { } surface)
        {
            return false;
        }

        var ask = AdventuresPage.CanAsk(surface);
        var log = surface.Messages is { } store && AdventuresPage.InAStory(surface, store);
        var changed = host.Nav.SetVisible(PanelTab.Stories, AdventuresPage.AskKey, ask);
        changed |= host.Nav.SetVisible(PanelTab.Stories, MessagesView.RootKey, log);

        if (_askShown != ask)
        {
            _askShown = ask;
            _refresh?.Invoke();
        }

        return changed;
    }

    public void EnableAdventures(
        AdventureSurface surface, Func<Control?>? settingsStrip = null, D47.Core.Capabilities.Builtin.AdventureCapability.AdventureDesk? desk = null)
    {
        AdventuresPage? page = null;

        AdventuresPage Page() => page ??= new AdventuresPage(surface, host.Nav, host.Prompts, settingsStrip?.Invoke(), () => host.Copy);

        if (desk is not null)
        {
            static string? OnUi(Func<string?> act) =>
                Avalonia.Threading.Dispatcher.UIThread.CheckAccess() ? act() : Avalonia.Threading.Dispatcher.UIThread.Invoke(act);

            desk.Change = () => OnUi(() => Page().ChangeDraft());
            desk.Accept = () => OnUi(() => Page().AcceptDraft());
            desk.Reject = () => OnUi(() => Page().RejectDraft());
            desk.Ask = () => OnUi(() => Page().AskByVoice());
            desk.ChangeObjective = () => OnUi(() => Page().ChangeObjectiveByVoice());
        }

        var help = D47.Core.Capabilities.Builtin.AdventureCapability.Id;
        List<NavCrumb> roots = [new NavCrumb(AdventuresPage.RootKey, "Your stories") { Help = help }];

        if (surface.Stories is not null)
        {
            roots.Add(StoriesView.Crumb with { Help = help });
        }

        roots.Add(AdventuresPage.AskCrumb with { Help = help });
        roots.Add(MessagesView.Crumb with { Help = help });

        if (surface.Stories is not null && surface.Bench is not null)
        {
            roots.Add(StoryBenchPage.Crumb with { Help = help });
        }

        host.Furnish(
            PanelTab.Stories,
            crumb => crumb.Key == AdventuresPage.RootKey
                ? Page()
                : Page().Build(crumb) ?? new TextBlock { Text = "Nothing here." },
            [.. roots]);

        _surface = surface;
        _refresh = () => page?.Refresh();
        ShowConditionalRoots();

        if (surface.Switched is { } switched)
        {
            switched.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                host.Nav.ToRootIfAny(PanelTab.Stories, crumb => AdventuresPage.BelongsToACommander(crumb.Key));
                host.Forget(PanelTab.Stories, AdventuresPage.BelongsToACommander);
            });
        }

        Mini = new AdventureMini(surface);
    }
}
