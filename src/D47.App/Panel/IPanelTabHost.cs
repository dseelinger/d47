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

    /// <summary>Opens the settings section of a capability, or null where this surface cannot.</summary>
    Action<string>? SettingsJump { get; }

    Func<string, Task<bool>>? Copy { get; }

    /// <summary>Opens an address in the Commander's browser, or null where this surface has none.</summary>
    Action<string>? Browse { get; }

    SpeakerPortraits? Portraits { get; }

    Func<string?>? CommanderName { get; }

    /// <summary>Where dragged pane widths are remembered, or null where the surface has no mouse.</summary>
    PaneWidthMemory? PaneWidths { get; }

    /// <summary>Where dragged column widths are remembered, or null where the surface has no mouse.</summary>
    ColumnWidthMemory? ColumnWidths { get; }

    /// <summary>Which fleet carrier model each carrier is drawn as, or null where the surface keeps none.</summary>
    CarrierHullMemory? CarrierHulls { get; }

    void Furnish(PanelTab tab, Func<NavCrumb, Control> build, params NavCrumb[] roots);

    void Forget(PanelTab tab, Func<string, bool> match);

    bool ClearSearch();

    void ShowSearch();
}
