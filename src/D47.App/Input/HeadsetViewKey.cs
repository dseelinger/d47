using D47.Core.Hotas;
using D47.Core.Input;
using D47.Core.Vr;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// Sends Shift+Win+D, Virtual Desktop's shortcut between its desktop view and SteamVR, and no other key.
/// Virtual Desktop registers it with <c>RegisterHotKey</c>, so it is heard whatever window is in front; this
/// checks nothing about Elite.
/// </summary>
/// <param name="sender">The sender the shortcut goes through; one is made when none is given.</param>
public sealed class HeadsetViewKey(IHeadsetViewSource view, ILogger<HeadsetViewKey> logger, ChordSender? sender = null)
{
    /// <summary>Virtual Desktop's shortcut.</summary>
    public static readonly KeyChord Shortcut = new("Key_D", ChordModifiers.Shift | ChordModifiers.Win);

    private ChordSender? _sender = sender;

    /// <summary>Counts presses but never sends; applies only when no sender was given.</summary>
    public bool DryRun { get; init; }

    /// <summary>How many times the shortcut was sent.</summary>
    public int Presses { get; private set; }

    private ChordSender Sender => _sender ??= new ChordSender(logger) { DryRun = DryRun };

    /// <summary>Presses the shortcut unless the view is unreadable or already <paramref name="wanted"/>.</summary>
    /// <returns>Why nothing was pressed when that is a refusal, or null.</returns>
    private string? Press(HeadsetView wanted, out bool sent)
    {
        sent = false;

        var showing = view.Read();

        if (showing == HeadsetView.Unknown)
        {
            return "Virtual Desktop stopped streaming, so I left the headset view alone.";
        }

        if (showing == wanted)
        {
            logger.LogInformation("The headset is already showing {View}, so Shift+Win+D was not pressed", wanted);
            return null;
        }

        Presses++;

        var refused = Sender.Press(Shortcut);
        sent = refused is null;

        return refused;
    }

    /// <summary>
    /// Carries out one reconcile for a headset view, the press or what it has to say, and tells the reconciler
    /// whether a press was sent.
    /// </summary>
    /// <returns>A sentence to say, or null.</returns>
    public string? CarryOut(PendingReconcile reconcile, SwitchReconciler reconciler)
    {
        if (reconcile.Say is { } say)
        {
            return say;
        }

        if (reconcile.View is not { } wanted)
        {
            return null;
        }

        var refused = Press(wanted, out var sent);

        reconciler.HeadsetPressed(reconcile.Switch, wanted, sent);

        logger.LogInformation(
            "Switch {Name} set the headset view to {View}: {Outcome}",
            reconcile.Switch,
            wanted,
            refused ?? "done");

        return refused is null ? null : $"I could not change the headset to {reconcile.Label} from {reconcile.Switch}. {refused}";
    }
}
