using D47.Core.Actions;
using D47.Core.Callouts;
using D47.Core.Hotas;
using D47.Core.Input;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// Carries out the key presses and announcements the tick decided, on the pool, one batch at a time.
/// </summary>
public sealed class GameActions(
    ILogger logger,
    Func<Announcement, Task> announceSwitch,
    Func<Announcement, Task> announceAction)
{
    private readonly SemaphoreSlim _acting = new(1, 1);

    /// <summary>
    /// Runs <paramref name="work"/> on the pool holding the semaphore. A failure is logged as
    /// <paramref name="failure"/>; <paramref name="releaseAll"/>, when given, has every held key released afterwards.
    /// </summary>
    public void Run(Func<Task> work, string failure, IGameInput? releaseAll)
    {
        _ = Task.Run(async () =>
        {
            await _acting.WaitAsync().ConfigureAwait(false);

            try
            {
                await work().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Failure}", failure);
            }
            finally
            {
                releaseAll?.ReleaseAll();
                _acting.Release();
            }
        });
    }

    /// <summary>Carries out whatever the reconciler decided this tick.</summary>
    public void CarryOutReconciles(
        SwitchReconciler reconciler,
        IGameInput input,
        HeadsetViewKey headsetKey,
        ChordSender chords,
        bool offDuty,
        Action<string> showPage)
    {
        var drained = reconciler.Drain();

        if (drained.Count == 0)
        {
            return;
        }

        var route = ReconcileRoute.Of(drained, offDuty);

        // A page move holds no keys, so it does not wait on the semaphore.
        foreach (var page in route.Pages)
        {
            showPage(page.Destination!);
        }

        if (route.Headset.Count > 0)
        {
            CarryOutHeadsetViews(route.Headset, headsetKey, reconciler);
        }

        if (route.Chords.Count > 0)
        {
            CarryOutChords(route.Chords, chords);
        }

        var pending = route.Game;

        if (pending.Count == 0)
        {
            return;
        }

        Run(
            async () =>
            {
                foreach (var reconcile in pending)
                {
                    if (reconcile.Steps.Count > 0)
                    {
                        var result = await input.SendAsync(reconcile.Steps).ConfigureAwait(false);

                        logger.LogInformation(
                            "Switch {Name} reconciled {Label}: {Outcome}",
                            reconcile.Switch,
                            reconcile.Label,
                            result.Outcome);

                        // The Commander is watching for the change, so a refusal left in the log would look
                        // like the feature not working.
                        if (!result.Sent)
                        {
                            await announceSwitch(new Announcement(
                                reconcile.Switch,
                                $"I could not set {reconcile.Label} from {reconcile.Switch}. {result.Reason}"))
                                .ConfigureAwait(false);
                        }
                    }

                    if (reconcile.Say is { } say)
                    {
                        await announceSwitch(new Announcement(reconcile.Switch, say)).ConfigureAwait(false);
                    }
                }
            },
            "A switch could not be reconciled",
            input);
    }

    /// <summary>Presses Shift+Win+D for the headset views decided this tick, off duty as well.</summary>
    public void CarryOutHeadsetViews(
        IReadOnlyList<PendingReconcile> pending, HeadsetViewKey headsetKey, SwitchReconciler reconciler) =>
        Run(
            async () =>
            {
                foreach (var reconcile in pending)
                {
                    if (headsetKey.CarryOut(reconcile, reconciler) is { } say)
                    {
                        await announceSwitch(new Announcement(reconcile.Switch, say)).ConfigureAwait(false);
                    }
                }
            },
            "A headset view could not be set",
            releaseAll: null);

    /// <summary>Presses the key chords decided this tick in whatever window is in front, off duty as well.</summary>
    public void CarryOutChords(IReadOnlyList<PendingReconcile> pending, ChordSender chords) =>
        Run(
            async () =>
            {
                foreach (var reconcile in pending)
                {
                    var refused = chords.Press(reconcile.Chord!);

                    logger.LogInformation(
                        "Switch {Name} pressed {Chord}: {Outcome}",
                        reconcile.Switch,
                        reconcile.Label,
                        refused ?? "done");

                    if (refused is not null)
                    {
                        await announceSwitch(new Announcement(
                            reconcile.Switch,
                            $"I could not press {reconcile.Label} from {reconcile.Switch}. {refused}"))
                            .ConfigureAwait(false);
                    }
                }
            },
            "A key chord could not be pressed",
            releaseAll: null);

    /// <summary>Carries out whatever the autonomous actions decided this tick; nothing is carried out off duty.</summary>
    public void CarryOutPendingActions(AutonomousActionRunner runner, IGameInput input, bool offDuty)
    {
        var pending = runner.Drain();

        // Dropped rather than held, so nothing decided off duty is carried out on return.
        if (pending.Count == 0 || offDuty)
        {
            return;
        }

        Run(
            async () =>
            {
                foreach (var action in pending)
                {
                    if (action.Decision.Acts)
                    {
                        var result = await input.SendAsync(action.Decision.Steps).ConfigureAwait(false);

                        logger.LogInformation(
                            "Autonomous action {Id} finished: {Outcome}", action.Id, result.Outcome);

                        // Nobody is watching for this to fail, and the action says when Elite was not in front.
                        if (!result.Sent && result.Outcome != InjectionOutcome.NotForeground)
                        {
                            await announceAction(new Announcement(
                                action.Id, $"I could not use {action.Label}. {result.Reason}")).ConfigureAwait(false);
                        }
                    }

                    if (action.Decision.Say is { } say)
                    {
                        await announceAction(new Announcement(action.Id, say)).ConfigureAwait(false);
                    }
                }
            },
            "An autonomous action could not be carried out",
            input);
    }
}
