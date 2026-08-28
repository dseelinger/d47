using System.Diagnostics;
using D47.Core.Hotas;
using Microsoft.Extensions.Logging;
using Windows.Gaming.Input;

namespace D47.App.Input;

/// <summary>
/// Reading the Commander's controllers through <c>Windows.Gaming.Input.RawGameController</c>
/// (Phase 21, "Read the Commander's controllers").
/// <para>
/// <b>No driver, no window, no elevation.</b> That is what keeps this inside the per-user
/// no-elevation install and off the vJoy/ViGEmBus row in architecture.md §10 — verified from a
/// plain console process in docs/spikes/hotas-switch-read.md.
/// </para>
/// <para>
/// <b>The device list is not a startup query.</b> Across three spike runs a single enumeration
/// returned 3, then 5, then 6 devices, and the probe's first version waited for the list to
/// become <em>non-empty</em> and confidently reported three of six. So this waits for the count
/// to stop changing and stays subscribed afterwards — hot-plug and slow enumeration are the same
/// code path, so getting it right costs nothing extra. It is the finding most likely to become a
/// shipped bug, because the symptom ("d47 can't see my throttle") is intermittent and does not
/// reproduce on the developer's machine.
/// </para>
/// <para>
/// It owns a subscription but no thread and no cadence: <see cref="Poll"/> is what takes a
/// reading, and the tick loop is what calls it (architecture.md §4).
/// </para>
/// </summary>
public sealed class HotasControllers : IHotasReader, IDisposable
{
    /// <summary>
    /// How long the count has to hold still before the list is a list. The spike's figure, and it
    /// is a floor rather than a guess — the sixth device arrived seconds after the first.
    /// </summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1500);

    /// <summary>
    /// How long to wait for a first device before concluding there are none. A machine with no
    /// controllers is the normal state for most Commanders and must not leave the surface saying
    /// "still looking" forever.
    /// </summary>
    private static readonly TimeSpan GiveUp = TimeSpan.FromSeconds(6);

    private readonly ILogger<HotasControllers> _logger;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _gate = new();

    private readonly EventHandler<RawGameController> _added;
    private readonly EventHandler<RawGameController> _removed;

    private TimeSpan _changed;
    private int _count = -1;
    private bool _announced;

    public HotasControllers(ILogger<HotasControllers> logger)
    {
        _logger = logger;

        // Subscribed before the first read, so a device that arrives between the subscription and
        // the read is counted rather than missed.
        _added = (_, controller) => Changed("added", controller);
        _removed = (_, controller) => Changed("removed", controller);

        try
        {
            RawGameController.RawGameControllerAdded += _added;
            RawGameController.RawGameControllerRemoved += _removed;
        }
        catch (Exception ex)
        {
            // A machine where the projection cannot be reached at all is a machine with no
            // switches, not a machine that fails to start.
            Fault = "D47 could not reach the Windows game-controller service.";
            _logger.LogWarning(ex, "Windows.Gaming.Input is not available");
        }
    }

    /// <summary>
    /// Set when the Windows SDK projection itself could not be reached, and null otherwise —
    /// including on a machine with no controllers at all, which is not a fault.
    /// <para>
    /// Separate from <see cref="Unavailable"/> because <c>--selftest</c> needs the difference.
    /// The projection is a managed assembly and a COM activation that reach a published build
    /// through the single-file bundle rather than through <c>bin\</c>, which is the same layout
    /// difference that once shipped a d47 whose transcriber had never worked. "No controllers
    /// plugged in" must pass that gate; "the projection did not load" must fail it, and one
    /// string for both cannot say which happened.
    /// </para>
    /// </summary>
    public string? Fault { get; private set; }

    /// <summary>How many interfaces Windows is reporting right now, settled or not.</summary>
    public int Interfaces => Count();

    /// <inheritdoc />
    public bool IsSettled
    {
        get
        {
            lock (_gate)
            {
                if (Fault is not null)
                {
                    return true;
                }

                var count = Count();

                if (count != _count)
                {
                    _count = count;
                    _changed = _clock.Elapsed;
                    return false;
                }

                // Nothing at all, and long enough to say so. Two separate windows because they
                // answer different questions: "has it stopped growing" and "did it ever start".
                if (count == 0)
                {
                    return _clock.Elapsed > GiveUp;
                }

                return _clock.Elapsed - _changed > Settle;
            }
        }
    }

    /// <inheritdoc />
    public string? Unavailable
    {
        get
        {
            lock (_gate)
            {
                if (Fault is not null)
                {
                    return Fault;
                }

                return IsSettled && Count() == 0
                    ? "D47 cannot see any game controllers. Nothing is plugged in, or Windows is not reporting it."
                    : null;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HotasReading> Poll()
    {
        if (!IsSettled)
        {
            // Deliberately nothing rather than a partial list. A caller that acted on half a
            // stick would be acting on the bug this class exists to prevent.
            return [];
        }

        IReadOnlyList<RawGameController> controllers;

        try
        {
            controllers = RawGameController.RawGameControllers;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The controller list could not be read");
            return [];
        }

        if (!_announced)
        {
            _announced = true;
            _logger.LogInformation("Reading {Count} game controller interface(s)", controllers.Count);
        }

        var readings = new List<HotasReading>(controllers.Count);

        foreach (var controller in controllers)
        {
            if (Read(controller) is { } reading)
            {
                readings.Add(reading);
            }
        }

        return readings;
    }

    public void Dispose()
    {
        try
        {
            RawGameController.RawGameControllerAdded -= _added;
            RawGameController.RawGameControllerRemoved -= _removed;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The controller subscriptions could not be released");
        }
    }

    private HotasReading? Read(RawGameController controller)
    {
        try
        {
            var buttons = new bool[controller.ButtonCount];
            var hats = new GameControllerSwitchPosition[controller.SwitchCount];
            var axes = new double[controller.AxisCount];

            controller.GetCurrentReading(buttons, hats, axes);

            return new HotasReading
            {
                // The only field that separates the throttle's four otherwise-identical blocks,
                // and the only thing a mapping is ever keyed on (spike findings 2 and 7).
                Id = controller.NonRoamableId,
                VendorId = controller.HardwareVendorId,
                ProductId = controller.HardwareProductId,
                Buttons = buttons,
                Hats = [.. hats.Select(hat => (int)hat)],
                AxisCount = axes.Length,
            };
        }
        catch (Exception ex)
        {
            // A device unplugged between the list read and this one. Skipped rather than thrown:
            // the mapping that named it will fail closed on the next poll and say so, which is
            // the honest answer and is already built.
            _logger.LogDebug(ex, "A controller could not be read");
            return null;
        }
    }

    private static int Count()
    {
        try
        {
            return RawGameController.RawGameControllers.Count;
        }
        catch
        {
            return 0;
        }
    }

    private void Changed(string what, RawGameController controller)
    {
        lock (_gate)
        {
            _changed = _clock.Elapsed;
            _count = Count();
        }

        try
        {
            _logger.LogInformation(
                "Game controller {What}: VID 0x{Vendor:X4} PID 0x{Product:X4}, now {Count} interface(s)",
                what,
                controller.HardwareVendorId,
                controller.HardwareProductId,
                _count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "A controller was {What} and could not be described", what);
        }
    }
}
