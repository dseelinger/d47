namespace D47.Core.Conversation;

public enum LlmAvailability
{
    /// <summary>A provider is configured and nothing has recently failed.</summary>
    Available,

    /// <summary>
    /// No provider, or one that cannot work until settings change — no key, a rejected key, an unknown
    /// model.
    /// </summary>
    NotConfigured,

    /// <summary>Rate limited, overloaded, or a network failure.</summary>
    TemporarilyUnavailable,
}

/// <summary>"Capabilities as state, not guard" (Phase 3).</summary>
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

    /// <summary>Called once per turn, before routing.</summary>
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

    /// <summary>
    /// Called when the provider itself is rebuilt — a key stored, an endpoint changed, the provider set
    /// to none.
    /// </summary>
    public void SetProviderConfigured(bool configured, string? reason = null)
    {
        Current = configured ? LlmAvailability.Available : LlmAvailability.NotConfigured;
        Reason = configured ? null : reason ?? "No language model provider is configured.";
        _turnsUntilProbe = 0;
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
