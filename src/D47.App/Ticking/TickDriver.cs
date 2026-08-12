using D47.Core.Ticking;
using Microsoft.Extensions.Logging;

namespace D47.App.Ticking;

/// <summary>
/// The thing that makes the tick loop tick. Owns the timer and the clock, which is precisely
/// what <see cref="TickLoop"/> must not (architecture.md §4) — the split is the whole point:
/// everything about <em>what happens</em> on a tick is testable without this class existing.
/// <para>
/// One dedicated thread rather than a thread-pool timer. The loop runs at a fixed cadence and
/// does a small amount of work each time; handing that to the pool means competing with turn
/// execution and TTS synthesis for a worker, and a starved pool would present as push-to-talk
/// becoming unresponsive under load — a symptom with nothing pointing at its cause.
/// </para>
/// </summary>
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
        // PeriodicTimer does not accumulate: a tick that overruns its period costs one skipped
        // tick rather than a backlog that then fires back-to-back. For a loop whose job is
        // sampling — key state, journal position, game state — sampling late once is correct
        // and catching up in a burst is not.
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
            // TickLoop already isolates a throwing subscriber, so reaching here means the loop
            // itself failed. Logged rather than lost: the visible symptom would be d47 quietly
            // going deaf and blind while the window carries on looking fine.
            _logger.LogCritical(ex, "The tick loop stopped");
        }

        _logger.LogInformation("Tick loop stopped after {Count} ticks", _loop.Count);
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _thread.Join(TimeSpan.FromSeconds(2));
        _stopping.Dispose();
    }
}
