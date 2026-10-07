using System.Diagnostics;
using D47.Core.Hotas;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// Reads the controllers on a thread of its own and answers the tick from the latest sample, so a tick never
/// calls into Windows.Gaming.Input.
/// </summary>
public sealed class ControllerSampler : IHotasReader, IDisposable
{
    /// <summary>Shorter than a tick, so the sample a tick reads was taken during the tick before it.</summary>
    public static readonly TimeSpan Period = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// How old a sample can be before its readings are dropped, so a hung read releases a held push-to-talk
    /// rather than holding it.
    /// </summary>
    public static readonly TimeSpan Stale = TimeSpan.FromSeconds(1);

    private readonly IHotasReader _reader;
    private readonly ILogger<ControllerSampler> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _thread;
    private readonly Func<TimeSpan> _now;

    private Sampled _latest = new(false, [], TimeSpan.Zero);
    private bool _faulted;

    public ControllerSampler(IHotasReader reader, ILogger<ControllerSampler> logger)
        : this(reader, logger, Elapsed())
    {
    }

    internal ControllerSampler(IHotasReader reader, ILogger<ControllerSampler> logger, Func<TimeSpan> now)
    {
        _reader = reader;
        _logger = logger;
        _now = now;

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "d47-controllers",
        };
    }

    /// <inheritdoc />
    public bool IsSettled => Volatile.Read(ref _latest).Settled;

    /// <summary>Asked of the reader directly; not for the tick.</summary>
    public string? Unavailable => _reader.Unavailable;

    /// <summary>
    /// The latest sample's readings: empty until the device list has settled, and empty once the sample is
    /// older than <see cref="Stale"/>.
    /// </summary>
    public IReadOnlyList<HotasReading> Poll()
    {
        var latest = Volatile.Read(ref _latest);

        return _now() - latest.At > Stale ? [] : latest.Readings;
    }

    public ControllerSampler Start()
    {
        _thread.Start();
        return this;
    }

    /// <summary>Reads the controllers once and publishes what it read.</summary>
    internal void Sample()
    {
        var settled = _reader.IsSettled;
        var readings = settled ? _reader.Poll() : [];

        Volatile.Write(ref _latest, new Sampled(settled, readings, _now()));
    }

    private void Run()
    {
        var stopping = _stopping.Token;

        using var timer = new PeriodicTimer(Period);

        try
        {
            while (timer.WaitForNextTickAsync(stopping).AsTask().GetAwaiter().GetResult())
            {
                try
                {
                    Sample();
                }
                catch (Exception ex)
                {
                    if (!_faulted)
                    {
                        _faulted = true;
                        _logger.LogWarning(ex, "The controllers could not be read");
                    }
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

        // Left undisposed while a hung read still holds the thread, which reads the token when it returns.
        if (!_thread.IsAlive || _thread.Join(TimeSpan.FromSeconds(2)))
        {
            _stopping.Dispose();
        }
    }

    private static Func<TimeSpan> Elapsed()
    {
        var clock = Stopwatch.StartNew();
        return () => clock.Elapsed;
    }

    private sealed record Sampled(bool Settled, IReadOnlyList<HotasReading> Readings, TimeSpan At);
}
