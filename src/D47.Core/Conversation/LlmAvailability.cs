namespace D47.Core.Conversation;

public enum LlmAvailability
{
    /// <summary>A provider is configured and nothing has recently failed.</summary>
    Available,

    /// <summary>
    /// No provider, or one that cannot work until settings change — no key, a rejected key, an
    /// unknown model. Retrying without a change would just fail again.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// Rate limited, overloaded, or a network failure. Clears itself after a few turns.
    /// </summary>
    TemporarilyUnavailable,
}

/// <summary>
/// "Capabilities as state, not guard" (list.md Phase 3). The turn loop reads this at the start
/// of each turn and routes accordingly; failures write to it. There is no failure handler to
/// author because a failure is just a state the next turn reads.
/// <para>
/// Recovery is counted in turns, not seconds — no Core component reads the clock, so a
/// transient outage clears after <see cref="ProbeAfterTurns"/> further turns rather than after
/// an interval. That also means it can never latch off permanently.
/// </para>
/// </summary>
public sealed class LlmAvailabilityState
{
    public const int ProbeAfterTurns = 3;

    private int _turnsUntilProbe;

    public LlmAvailabilityState(bool providerConfigured)
    {
        Current = providerConfigured ? LlmAvailability.Available : LlmAvailability.NotConfigured;
        Reason = providerConfigured ? null : "No language model provider is configured.";
    }

    public LlmAvailability Current { get; private set; }

    public string? Reason { get; private set; }

    public bool CanAttemptModelTurn => Current == LlmAvailability.Available;

    /// <summary>Called once per turn, before routing. Lets a transient outage lapse.</summary>
    public void BeginTurn()
    {
        if (Current != LlmAvailability.TemporarilyUnavailable)
        {
            return;
        }

        if (--_turnsUntilProbe <= 0)
        {
            Current = LlmAvailability.Available;
            Reason = null;
        }
    }

    public void MarkAvailable()
    {
        Current = LlmAvailability.Available;
        Reason = null;
        _turnsUntilProbe = 0;
    }

    public void MarkFailed(string reason, bool transient)
    {
        Reason = reason;

        if (transient)
        {
            Current = LlmAvailability.TemporarilyUnavailable;
            _turnsUntilProbe = ProbeAfterTurns;
        }
        else
        {
            Current = LlmAvailability.NotConfigured;
            _turnsUntilProbe = 0;
        }
    }
}
