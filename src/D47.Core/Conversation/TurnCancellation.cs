using Microsoft.Extensions.Logging;

namespace D47.Core.Conversation;

/// <summary>The handle on the turn that is running, so it can be abandoned rather than merely muffled.</summary>
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

    /// <summary>Claims the turn slot and returns the token the turn must run under.</summary>
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

    /// <summary>Releases the slot, if this source is still the one holding it.</summary>
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

    /// <summary>Abandons the turn in flight.</summary>
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
