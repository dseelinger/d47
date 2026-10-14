using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Stories tab and its mini-size story.</summary>
internal sealed class StoriesFurnishing(IPanelTabHost host)
{
    public AdventureMini? Mini { get; private set; }

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

        host.Furnish(
            PanelTab.Stories,
            crumb => crumb.Key == AdventuresPage.RootKey
                ? Page()
                : Page().Build(crumb) ?? new TextBlock { Text = "Nothing here." },
            new NavCrumb(AdventuresPage.RootKey, "Stories")
            {
                Help = D47.Core.Capabilities.Builtin.AdventureCapability.Id,
            });

        // Read from the surface rather than the page's buttons: the page is not built until the tab is first shown.
        host.Nav.Declare(
            AdventuresPage.RootKey,
            MessagesView.Crumb,
            () => surface.Messages is { } store && AdventuresPage.InAStory(surface, store));
        host.Nav.Declare(AdventuresPage.RootKey, AdventuresPage.AskCrumb, () => AdventuresPage.CanAsk(surface));

        if (surface.Stories is not null)
        {
            host.Nav.Declare(AdventuresPage.RootKey, StoriesView.Crumb);

            if (surface.Bench is not null)
            {
                host.Nav.Declare(AdventuresPage.RootKey, StoryBenchPage.Crumb);
            }
        }

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
