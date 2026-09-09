using Microsoft.Extensions.Logging;

namespace D47.Core.Vr;

/// <summary>Where the headset link is.</summary>
public enum VrState
{
    /// <summary>No OpenVR runtime on this machine.</summary>
    Unavailable,

    /// <summary>A runtime is installed but not yet usable.</summary>
    Connecting,

    /// <summary>The session is up and the overlays exist.</summary>
    Active,
}

/// <summary>Why a start attempt did not produce a session.</summary>
public enum VrStartOutcome
{
    Started,

    /// <summary>No runtime is installed.</summary>
    NoRuntime,

    /// <summary>The runtime is there and not ready — not started, or no headset yet.</summary>
    NotReady,

    /// <summary>Another copy of d47 owns the overlay keys.</summary>
    AlreadyOwned,

    Failed,
}

public readonly record struct VrStart(VrStartOutcome Outcome, string? Detail = null)
{
    public static readonly VrStart Started = new(VrStartOutcome.Started);
}

/// <summary>The headset runtime, as everything above it needs to see it.</summary>
public interface IVrRuntime
{
    /// <summary>Brings up the session and every overlay handle.</summary>
    VrStart Start();

    /// <summary>One serving of the live session: pump events, submit anything that changed.</summary>
    bool Serve(DateTimeOffset now);

    /// <summary>Gives the handles back.</summary>
    void Stop();
}

/// <summary>
/// Order agnostic Overlay, and there is nothing else to it: this is the whole feature (Phase 9).
/// </summary>
public sealed class VrLifecycle(IVrRuntime runtime, ILogger<VrLifecycle> logger)
{
    /// <summary>How long between attempts while there is no session.</summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private DateTimeOffset? _lastAttempt;
    private string? _saidOnce;

    public VrState State { get; private set; } = VrState.Connecting;

    /// <summary>Why, in a sentence, when the state is not <see cref="VrState.Active"/>.</summary>
    public string? Reason { get; private set; } = "Looking for a headset.";

    /// <summary>How many times a session has come up.</summary>
    public int Sessions { get; private set; }

    /// <summary>Raised on a real transition, never on a repeat.</summary>
    public event Action<VrState>? Changed;

    /// <summary>One tick.</summary>
    public void Tick(DateTimeOffset now)
    {
        if (State == VrState.Active)
        {
            if (!runtime.Serve(now))
            {
                // Logged at warning rather than error: SteamVR being closed under us is a thing the Commander
                // did, not a thing that went wrong.
                logger.LogWarning("The headset session ended; D47 will look for it again");
                runtime.Stop();
                Enter(VrState.Connecting, "The headset session ended.");
                _lastAttempt = now;
            }

            return;
        }

        // Unavailable keeps asking on the same interval as Connecting.
        if (!DueFor(now))
        {
            return;
        }

        _lastAttempt = now;
        Attempt();
    }

    /// <summary>Gives the session back.</summary>
    public void Stop()
    {
        if (State == VrState.Active)
        {
            runtime.Stop();
        }

        Enter(VrState.Connecting, "Stopped.");
    }

    private bool DueFor(DateTimeOffset now) =>
        _lastAttempt is not { } last || now - last >= RetryInterval;

    private void Attempt()
    {
        var start = runtime.Start();

        if (start.Outcome == VrStartOutcome.Started)
        {
            Sessions++;
            _saidOnce = null;
            logger.LogInformation("Headset session {Number} is up", Sessions);
            Enter(VrState.Active, null);
            return;
        }

        var reason = start.Detail ?? Describe(start.Outcome);

        // Said once per distinct reason, not once per attempt.
        if (_saidOnce != reason)
        {
            _saidOnce = reason;
            logger.LogInformation("No headset yet, and D47 will keep looking: {Reason}", reason);
        }

        Enter(
            start.Outcome == VrStartOutcome.NoRuntime ? VrState.Unavailable : VrState.Connecting,
            reason);
    }

    private static string Describe(VrStartOutcome outcome) => outcome switch
    {
        VrStartOutcome.NoRuntime => "No SteamVR runtime is installed on this machine.",
        VrStartOutcome.NotReady => "SteamVR is not running, or no headset is present yet.",
        VrStartOutcome.AlreadyOwned =>
            "Another copy of D47 already owns the headset overlays. Close it.",
        _ => "The headset session could not be started.",
    };

    private void Enter(VrState state, string? reason)
    {
        Reason = reason;

        if (State == state)
        {
            return;
        }

        State = state;
        Changed?.Invoke(state);
    }
}
