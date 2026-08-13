using Microsoft.Extensions.Logging;

namespace D47.Core.Vr;

/// <summary>
/// Where the headset link is (architecture.md D2). Three states rather than a bool, because
/// the middle one is a real condition the spike hit: <c>VR_Init</c> returned
/// <c>Init_HmdNotFound</c> while <c>vrserver.exe</c> was already running. "SteamVR is
/// installed", "SteamVR is running" and "a headset is present and usable" are three different
/// facts, and collapsing them is what makes a machine that will never produce a headset sit in
/// a connecting state forever.
/// </summary>
public enum VrState
{
    /// <summary>No OpenVR runtime on this machine. Nothing to wait for.</summary>
    Unavailable,

    /// <summary>A runtime is installed but not yet usable. Keep asking.</summary>
    Connecting,

    /// <summary>The session is up and the overlays exist.</summary>
    Active,
}

/// <summary>Why a start attempt did not produce a session.</summary>
public enum VrStartOutcome
{
    Started,

    /// <summary>No runtime is installed. There is nothing here to come back to.</summary>
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

/// <summary>
/// The headset runtime, as everything above it needs to see it. Core owns the policy —
/// when to try, when to give up, what a failure means — and this is the one thing it cannot
/// do itself, which is exactly what makes <em>order agnostic overlay</em> testable with no
/// hardware in the room (architecture.md §8).
/// </summary>
public interface IVrRuntime
{
    /// <summary>
    /// Brings up the session and every overlay handle. Called only from
    /// <see cref="VrLifecycle"/>, and expected to be safe to call again after
    /// <see cref="Stop"/>.
    /// </summary>
    VrStart Start();

    /// <summary>
    /// One serving of the live session: pump events, submit anything that changed. Returns
    /// false when the session has gone away underneath and has to be rebuilt.
    /// </summary>
    bool Serve(DateTimeOffset now);

    /// <summary>Gives the handles back. Safe to call when nothing is running.</summary>
    void Stop();
}

/// <summary>
/// <em>Order agnostic Overlay</em>, and there is nothing else to it: this is the whole
/// feature (list.md Phase 9). SteamVR may start after d47, exit under it, or restart
/// mid-session, and none of those is an ordering the Commander should have to know about.
/// <para>
/// It owns no thread and reads no clock — the tick loop calls <see cref="Tick"/> and supplies
/// the time, like everything else in Core. That is what lets a test drive a whole session's
/// worth of SteamVR coming and going in a few microseconds.
/// </para>
/// <para>
/// Recovery rebuilds rather than repairs. A session that has gone away leaves every handle
/// stale, and a handle that is stale in a way we did not anticipate is a call that fails
/// forever with a plausible error — so the next attempt starts from nothing.
/// </para>
/// </summary>
public sealed class VrLifecycle(IVrRuntime runtime, ILogger<VrLifecycle> logger)
{
    /// <summary>
    /// How long between attempts while there is no session. Long enough that a Commander who
    /// never puts a headset on is not paying for a failing call every tick; short enough that
    /// starting SteamVR after d47 feels like it worked rather than like it eventually worked.
    /// </summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private DateTimeOffset? _lastAttempt;
    private string? _saidOnce;

    public VrState State { get; private set; } = VrState.Connecting;

    /// <summary>Why, in a sentence, when the state is not <see cref="VrState.Active"/>.</summary>
    public string? Reason { get; private set; } = "Looking for a headset.";

    /// <summary>How many times a session has come up. A restart increments it.</summary>
    public int Sessions { get; private set; }

    /// <summary>Raised on a real transition, never on a repeat.</summary>
    public event Action<VrState>? Changed;

    /// <summary>
    /// One tick. Cheap and non-blocking in the common cases — serving a live session, or
    /// waiting out the retry interval with nothing to do.
    /// </summary>
    public void Tick(DateTimeOffset now)
    {
        if (State == VrState.Active)
        {
            if (!runtime.Serve(now))
            {
                // Logged at warning rather than error: SteamVR being closed under us is a
                // thing the Commander did, not a thing that went wrong.
                logger.LogWarning("The headset session ended; D47 will look for it again");
                runtime.Stop();
                Enter(VrState.Connecting, "The headset session ended.");
                _lastAttempt = now;
            }

            return;
        }

        // Unavailable keeps asking on the same interval as Connecting. A runtime can be
        // installed while d47 is running, the cost of asking is a file check, and a state that
        // stopped asking would need something to tell it to start again.
        if (!DueFor(now))
        {
            return;
        }

        _lastAttempt = now;
        Attempt();
    }

    /// <summary>Gives the session back. Called at shutdown.</summary>
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

        // Said once per distinct reason, not once per attempt. At one attempt every five
        // seconds an unthrottled line is 720 an hour saying the same thing.
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
