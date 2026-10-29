using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The Settings tab: the settings page and Settings › Phrases.</summary>
internal sealed class SettingsFurnishing(IPanelTabHost host)
{
    public const string Root = "settings";

    private PhrasesPage? _phrases;

    /// <summary>The settings page this tab built, which a link naming a setting's row opens.</summary>
    public Control? Page { get; private set; }

    /// <summary>Jumps the settings page to one capability's card, or null where the surface has none.</summary>
    public Action<string>? Reveal { get; private set; }

    public void EnableSettings(Func<Control> build, Action<string>? reveal = null, Func<PhrasesPage>? phrases = null)
    {
        Reveal = reveal;

        var roots = new List<NavCrumb>
        {
            new(Root, "Settings")
            {
                Help = D47.Core.Capabilities.Builtin.SettingsCapability.Id,
            },
        };

        if (phrases is not null)
        {
            roots.Add(new NavCrumb(PhrasesPage.RootKey, PhrasesPage.Word)
            {
                Help = D47.Core.Capabilities.Builtin.LearnedPhrasesCapability.Id,
                Spoken = ["commands", "voice commands"],
            });
        }

        host.Furnish(
            PanelTab.Settings,
            crumb => crumb.Key switch
            {
                PhrasesPage.RootKey when phrases is not null => _phrases ??= phrases(),
                _ => Page = build(),
            },
            [.. roots]);
    }

    /// <summary>Moves the Phrases footer on when the journal has.</summary>
    public bool Tick() =>
        host.Tab == PanelTab.Settings
        && _phrases is { } phrases
        && host.Nav.RootKeyOf(PanelTab.Settings) == PhrasesPage.RootKey
        && phrases.Tick();
}
