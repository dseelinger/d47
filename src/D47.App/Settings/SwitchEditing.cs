using D47.Core.Hotas;
using D47.Core.Interface;

namespace D47.App.Settings;

/// <summary>Everything the switch row needs to open the walk (Phase 21).</summary>
/// <param name="Store">The Commander's mappings, and the file behind them.</param>
/// <param name="Reader">The controllers, for the walk.</param>
/// <param name="Reconciler">
/// Where each mapped switch stands, and the only thing that can resume a paused one.
/// </param>
/// <param name="Now">The clock.</param>
/// <param name="ExportPath">Where a declined capture's report is written.</param>
/// <param name="Destinations">
/// Every page a surface has registered, for the position editor's list (Phase 46).
/// </param>
public sealed record SwitchEditing(
    SwitchStore Store,
    IHotasReader Reader,
    SwitchReconciler Reconciler,
    Func<DateTimeOffset> Now,
    string ExportPath,
    Func<IReadOnlyList<PanelDestination>> Destinations);
