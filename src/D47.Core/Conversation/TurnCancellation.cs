using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>
/// The handle on the turn that is running, so it can be abandoned rather than merely muffled.
/// <para>
/// Silencing and cancelling are different things and the difference costs money. Silencing
/// stops the mouth: the queue is flushed, the sentence is cut off, and the model carries on
/// generating into a void that is still billed for. Cancelling stops the work — the provider
/// stream is torn down, tool calls stop, and generation ends.
/// </para>
/// <para>
/// One turn at a time by construction. A second <see cref="Begin"/> while one is in flight
/// cancels the first, because two turns talking over each other is not a state worth
/// supporting and letting the older one continue is how a "cancelled" turn keeps spending.
/// </para>
/// </summary>
public sealed class TurnCancellation(ILogger<TurnCancellation> logger)
{
    private readonly Lock _gate = new();

    private CancellationTokenSource? _current;

    /// <summary>Whether there is a turn that <see cref="Cancel"/> would abandon.</summary>
    public bool InFlight
    {
        get
        {
            lock (_gate)
            {
                return _current is not null;
            }
        }
    }

    /// <summary>
    /// Claims the turn slot and returns the token the turn must run under. The caller owns the
    /// returned source and must pass it back to <see cref="End"/>.
    /// </summary>
    public CancellationTokenSource Begin(CancellationToken linkedTo = default)
    {
        lock (_gate)
        {
            if (_current is { } previous)
            {
                logger.LogInformation("A new turn started while one was in flight; cancelling the old one");
                previous.Cancel();
            }

            _current = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
            return _current;
        }
    }

    /// <summary>
    /// Releases the slot, if this source is still the one holding it. The identity check
    /// matters: a turn that was cancelled by a newer one must not release the newer one's slot
    /// on its way out.
    /// </summary>
    public void End(CancellationTokenSource source)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_current, source))
            {
                _current = null;
            }
        }

        source.Dispose();
    }

    /// <summary>
    /// Abandons the turn in flight. Returns false when there was nothing to abandon, which the
    /// caller reports rather than swallows — "cancel" answered by silence is indistinguishable
    /// from "cancel" not working.
    /// </summary>
    public bool Cancel()
    {
        lock (_gate)
        {
            if (_current is not { } current)
            {
                return false;
            }

            logger.LogInformation("Turn cancelled by the Commander");
            current.Cancel();
            return true;
        }
    }
}
