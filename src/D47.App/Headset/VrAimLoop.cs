using Microsoft.Extensions.Logging;

namespace D47.App.Headset;

/// <summary>
/// The one thing in the headset that has to keep up with a hand
/// (<a href="https://github.com/dseelinger/d47/issues/19">#19</a>).
/// <para>
/// <b>A pointer is not content.</b> The panel's text, its clocks and the compose animation are all
/// correctly served at the tick's 10 Hz — a transcript nobody scrolled does not want redrawing
/// faster. An aim ray does: hands move at hand speed, and ten updates a second is about where
/// motion stops reading as motion and starts reading as a fault. It was reported as one.
/// </para>
/// <para>
/// <b>So the rate is split rather than raised.</b> Putting the whole tick up would drag the journal
/// poll, the key sampling and everything else that reads the world along with it, and none of those
/// want to run faster. This loop carries the pose read, the ray arithmetic and the placement of the
/// beam and cursor; every <em>decision</em> — trigger, grip, back, carry — stays on the tick with
/// its state, which is worth more than the latency it costs.
/// </para>
/// <para>
/// <b>It never touches the drawing thread.</b> The beam and the cursor are SteamVR overlay quads
/// rather than parts of the widget tree, so nothing here goes near Avalonia or its dispatcher —
/// which is what makes a second thread affordable at all in this part of the code.
/// </para>
/// <para>
/// <b>Its own thread, not the pool.</b> The same argument <see cref="Ticking.TickDriver"/> already
/// makes: this runs at a fixed cadence doing a small amount of work, and handing it to the pool
/// means competing with turn execution and speech synthesis for a worker. A starved pool would
/// present as a stuttering ray — the exact symptom this exists to remove, with nothing pointing at
/// its cause.
/// </para>
/// </summary>
public sealed class VrAimLoop : IDisposable
{
    /// <summary>
    /// How often the ray is placed. Not the headset's refresh rate and deliberately not called
    /// that: what matters is that it is far enough above 10 Hz to read as continuous, and low
    /// enough to be unnoticeable beside a running Elite. Ninety hertz is the starting number and
    /// the plan says to settle it by measuring against the game rather than by argument.
    /// </summary>
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromMilliseconds(11);

    private readonly Action _aim;
    private readonly TimeSpan _period;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _thread;

    public VrAimLoop(Action aim, ILogger logger, TimeSpan? period = null)
    {
        _aim = aim;
        _logger = logger;
        _period = period ?? DefaultPeriod;

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "d47-vr-aim",
        };
    }

    public VrAimLoop Start()
    {
        _logger.LogInformation(
            "VR aim loop starting at {Hz:0.#} Hz", 1000.0 / _period.TotalMilliseconds);

        _thread.Start();

        return this;
    }

    private void Run()
    {
        // PeriodicTimer does not accumulate, exactly as the tick loop's does not: a frame that
        // overran costs one skipped placement rather than a burst of stale ones afterwards. For a
        // loop whose whole job is "where is the hand now", late once is right and catching up is
        // not.
        using var timer = new PeriodicTimer(_period);

        try
        {
            while (timer.WaitForNextTickAsync(_stopping.Token).AsTask().GetAwaiter().GetResult())
            {
                try
                {
                    _aim();
                }
                catch (Exception ex)
                {
                    // The runtime going away underneath is a fact about the machine rather than a
                    // defect of ours, and it must not take this thread down: a dead aim loop is a
                    // ray that stops moving, which is the very report this was built for and would
                    // be indistinguishable from it.
                    _logger.LogError(ex, "The VR aim loop threw; the ray will be placed next frame");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();

        // Joined rather than abandoned. This thread makes OpenVR calls, and a session torn down
        // underneath one of them is the shape of fault that takes the runtime out from inside
        // vrclient rather than throwing something catchable.
        if (_thread.IsAlive && !_thread.Join(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("The VR aim loop did not stop within two seconds");
        }

        _stopping.Dispose();
    }
}
