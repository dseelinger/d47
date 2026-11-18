using D47.Core.Hotas;
using D47.Core.Journal;
using D47.Core.Presence;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// Whether paid calls stop because the Commander is away: Windows keyboard and mouse input other than d47's own,
/// controller presses and hats, presses on the headset panel and the journal events <see cref="PresenceWatch"/>
/// counts. Worked out on the tick; <see cref="PaidCallsStopped"/> is read from any thread.
/// </summary>
/// <param name="lastInput">The tick count of the last keyboard or mouse input, or null when unreadable.</param>
/// <param name="lastSent">The tick count when d47 last sent input, or null before it has.</param>
public sealed class CommanderPresence(Func<uint?> lastInput, Func<uint?> lastSent, ILogger<CommanderPresence> logger)
{
    /// <summary>How close to a send of d47's own a last-input time is taken as that send.</summary>
    public static readonly TimeSpan OwnInputWindow = TimeSpan.FromMilliseconds(500);

    private readonly PresenceWatch _watch = new();
    private readonly ControllerActivity _controllers = new();
    private uint? _lastInput;
    private int _panelPressed;
    private volatile bool _paidCallsStopped;

    public bool PaidCallsStopped => _paidCallsStopped;

    /// <summary>A press on the headset panel; any thread.</summary>
    public void PanelPressed() => Interlocked.Exchange(ref _panelPressed, 1);

    /// <summary>Takes one tick. An <paramref name="awayMinutes"/> of zero never stops paid calls.</summary>
    public void Tick(DateTimeOffset now, IEnumerable<JournalEvent> events, IReadOnlyList<HotasReading> readings, int awayMinutes)
    {
        _watch.Observe(events, now);

        // Every source is read each tick, so each keeps its own baseline current.
        var keyboardOrMouse = KeyboardOrMouseMoved();
        var controllers = _controllers.Moved(readings);
        var panel = Interlocked.Exchange(ref _panelPressed, 0) == 1;

        if (keyboardOrMouse || controllers || panel)
        {
            _watch.Stir(now);
        }

        var stopped = awayMinutes > 0 && _watch.IsAway(now, TimeSpan.FromMinutes(awayMinutes));

        if (stopped != _paidCallsStopped)
        {
            _paidCallsStopped = stopped;

            if (stopped)
            {
                logger.LogInformation("No activity from the Commander for {Minutes} minutes: paid calls stopped", awayMinutes);
            }
            else
            {
                logger.LogInformation("Paid calls allowed again");
            }
        }
    }

    /// <summary>Whether a last-input time is d47's own send at <paramref name="sent"/>, allowing for the tick count wrapping.</summary>
    public static bool IsOwn(uint input, uint? sent) =>
        sent is { } at && Math.Abs((long)unchecked((int)(input - at))) <= (long)OwnInputWindow.TotalMilliseconds;

    private bool KeyboardOrMouseMoved()
    {
        if (lastInput() is not { } input)
        {
            return false;
        }

        var moved = _lastInput is { } before && before != input && !IsOwn(input, lastSent());
        _lastInput = input;

        return moved;
    }
}
