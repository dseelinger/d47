using Avalonia.Controls;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>What a tab's furnishing reads from the panel that draws it.</summary>
internal interface IPanelTabHost
{
    PanelNavigator Nav { get; }

    PanelPrompts Prompts { get; }

    PanelTab Tab { get; }

    PanelViewModel? Model { get; }

    bool Headset { get; }

    Func<string, Task<bool>>? Copy { get; }

    SpeakerPortraits? Portraits { get; }

    Func<string?>? CommanderName { get; }

    void Furnish(PanelTab tab, Func<NavCrumb, Control> build, params NavCrumb[] roots);

    void Forget(PanelTab tab, Func<string, bool> match);

    bool ClearSearch();

    void ShowSearch();
}
