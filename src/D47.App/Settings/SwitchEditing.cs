using D47.Core.Hotas;
using D47.Core.Interface;

namespace D47.App.Settings;

/// <summary>
/// Everything the switch row needs to open the walk (list.md Phase 21).
/// <para>
/// One record rather than four parameters on <c>SettingsView.Attach</c>, because the four are
/// meaningless apart: a store with no reader cannot capture, and a reader with no reconciler
/// cannot say whether what was captured is currently doing anything. Null means the app was
/// composed without hardware — the designer, a test — and the button is then absent rather than
/// present and dead.
/// </para>
/// </summary>
/// <param name="Store">The Commander's mappings, and the file behind them.</param>
/// <param name="Reader">The controllers, for the walk. The same instance the tick loop polls.</param>
/// <param name="Reconciler">Where each mapped switch stands, and the only thing that can resume a paused one.</param>
/// <param name="Now">The clock. Injected here because the capture never reads one itself.</param>
/// <param name="ExportPath">Where a declined capture's report is written.</param>
/// <param name="Destinations">
/// Every page a surface has registered, for the position editor's list (list.md Phase 46). Asked
/// for when the window opens rather than captured, and on the thread that owns the navigators.
/// </param>
public sealed record SwitchEditing(
    SwitchStore Store,
    IHotasReader Reader,
    SwitchReconciler Reconciler,
    Func<DateTimeOffset> Now,
    string ExportPath,
    Func<IReadOnlyList<PanelDestination>> Destinations);
