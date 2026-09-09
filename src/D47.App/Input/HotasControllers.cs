using System.Diagnostics;
using D47.Core.Hotas;
using Microsoft.Extensions.Logging;
using Windows.Gaming.Input;

namespace D47.App.Input;

/// <summary>
/// Reading the Commander's controllers through <c>Windows.Gaming.Input.RawGameController</c> (Phase 21,
/// "Read the Commander's controllers").
/// </summary>
public sealed class HotasControllers : IHotasReader, IDisposable
{
    /// <summary>How long the count has to hold still before the list is a list.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1500);

    /// <summary>How long to wait for a first device before concluding there are none.</summary>
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

        // Subscribed before the first read, so a device that arrives between the subscription and the read is
        // counted rather than missed.
        _added = (_, controller) => Changed("added", controller);
        _removed = (_, controller) => Changed("removed", controller);

        try
        {
            RawGameController.RawGameControllerAdded += _added;
            RawGameController.RawGameControllerRemoved += _removed;
        }
        catch (Exception ex)
        {
            // A machine where the projection cannot be reached at all is a machine with no switches, not a
            // machine that fails to start.
            Fault = "D47 could not reach the Windows game-controller service.";
            _logger.LogWarning(ex, "Windows.Gaming.Input is not available");
        }
    }

    /// <summary>
    /// Set when the Windows SDK projection itself could not be reached, and null otherwise — including
    /// on a machine with no controllers at all, which is not a fault.
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

                // Nothing at all, and long enough to say so.
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
            // Deliberately nothing rather than a partial list.
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
                // The only field that separates the throttle's four otherwise-identical blocks, and the only
                // thing a mapping is ever keyed on (spike findings 2 and 7).
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
            // A device unplugged between the list read and this one.
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
