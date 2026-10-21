using D47.Core.Hotas;

namespace D47.App.Input;

/// <summary>One drain of the switch reconciler, divided between the panel, the headset, the game and key chords.</summary>
internal readonly record struct ReconcileRoute(
    IReadOnlyList<PendingReconcile> Pages,
    IReadOnlyList<PendingReconcile> Headset,
    IReadOnlyList<PendingReconcile> Game,
    IReadOnlyList<PendingReconcile> Chords)
{
    /// <summary>
    /// Off duty the game's share is dropped rather than held, so nothing decided then is carried out on return.
    /// The panel's, the headset's and the chords' are kept, since none of them reaches Elite.
    /// </summary>
    public static ReconcileRoute Of(IReadOnlyList<PendingReconcile> pending, bool offDuty) => new(
        [.. pending.Where(reconcile => reconcile.Destination is not null)],
        [.. pending.Where(reconcile => reconcile.Destination is null && reconcile.View is not null)],
        offDuty ? [] : [.. pending.Where(reconcile => reconcile.Destination is null && reconcile.View is null && reconcile.Chord is null)],
        [.. pending.Where(reconcile => reconcile.Destination is null && reconcile.View is null && reconcile.Chord is not null)]);
}
