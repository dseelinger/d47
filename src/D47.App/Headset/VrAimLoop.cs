using Microsoft.Extensions.Logging;

namespace D47.App.Headset;

/// <summary>The one thing in the headset that has to keep up with a hand (#19).</summary>
public sealed class VrAimLoop : IDisposable
{
    /// <summary>How often the ray is placed.</summary>
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
        // PeriodicTimer does not accumulate, exactly as the tick loop's does not: a frame that overran costs
        // one skipped placement rather than a burst of stale ones afterwards.
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
                    // The runtime going away underneath is a fact about the machine rather than a defect of
                    // ours, and it must not take this thread down: a dead aim loop is a ray that stops
                    // moving, which is the very report this was built for and would be indistinguishable from
                    // it.
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

        // Joined rather than abandoned.
        if (_thread.IsAlive && !_thread.Join(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("The VR aim loop did not stop within two seconds");
        }

        _stopping.Dispose();
    }
}
