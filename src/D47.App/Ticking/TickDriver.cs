using D47.Core.Ticking;
using Microsoft.Extensions.Logging;

namespace D47.App.Ticking;

/// <summary>The thing that makes the tick loop tick.</summary>
public sealed class TickDriver : IDisposable
{
    private readonly TickLoop _loop;
    private readonly TimeSpan _period;
    private readonly ILogger<TickDriver> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _thread;

    public TickDriver(TickLoop loop, ILogger<TickDriver> logger, TimeSpan? period = null)
    {
        _loop = loop;
        _logger = logger;
        _period = period ?? TickLoop.DefaultPeriod;

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "d47-tick",
        };
    }

    public TickDriver Start()
    {
        _logger.LogInformation("Tick loop starting at {Hz:0.#} Hz", 1000.0 / _period.TotalMilliseconds);
        _thread.Start();
        return this;
    }

    private void Run()
    {
        // PeriodicTimer does not accumulate: a tick that overruns its period costs one skipped tick rather
        // than a backlog that then fires back-to-back.
        using var timer = new PeriodicTimer(_period);

        try
        {
            while (timer.WaitForNextTickAsync(_stopping.Token).AsTask().GetAwaiter().GetResult())
            {
                _loop.Tick(DateTimeOffset.Now);
            }
        }
        catch (OperationCanceledException)
        {
        // Shutdown.
        }
        catch (Exception ex)
        {
            // TickLoop already isolates a throwing subscriber, so reaching here means the loop itself failed.
            _logger.LogCritical(ex, "The tick loop stopped");
        }

        _logger.LogInformation("Tick loop stopped after {Count} ticks", _loop.Count);

        // A subscriber still paused here ran for part of the session and not the rest of it, and nothing
        // else at shutdown reports that (#58).
        if (_loop.Paused is { Count: > 0 } paused)
        {
            _logger.LogWarning(
                "Tick subscribers still paused after repeated failures: {Names}",
                string.Join(", ", paused));
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _thread.Join(TimeSpan.FromSeconds(2));
        _stopping.Dispose();
    }
}
