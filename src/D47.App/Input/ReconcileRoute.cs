using D47.Core.Hotas;

namespace D47.App.Input;

/// <summary>One drain of the switch reconciler, divided between the panel, the headset and the game.</summary>
internal readonly record struct ReconcileRoute(
    IReadOnlyList<PendingReconcile> Pages,
    IReadOnlyList<PendingReconcile> Headset,
    IReadOnlyList<PendingReconcile> Game)
{
    /// <summary>
    /// Off duty the game's share is dropped rather than held, so nothing decided then is carried out on return.
    /// The panel's and the headset's are kept, since neither reaches Elite.
    /// </summary>
    public static ReconcileRoute Of(IReadOnlyList<PendingReconcile> pending, bool offDuty) => new(
        [.. pending.Where(reconcile => reconcile.Destination is not null)],
        [.. pending.Where(reconcile => reconcile.Destination is null && reconcile.View is not null)],
        offDuty ? [] : [.. pending.Where(reconcile => reconcile.Destination is null && reconcile.View is null)]);
}
