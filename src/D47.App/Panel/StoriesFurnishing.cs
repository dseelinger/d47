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
        }

        host.Furnish(
            PanelTab.Stories,
            crumb => crumb.Key == AdventuresPage.RootKey
                ? Page()
                : page?.Build(crumb) ?? new TextBlock { Text = "Nothing here." },
            new NavCrumb(AdventuresPage.RootKey, "Stories")
            {
                Help = D47.Core.Capabilities.Builtin.AdventureCapability.Id,
            });

        Mini = new AdventureMini(surface);
    }
}
