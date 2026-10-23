using System.Diagnostics;
using D47.Core.Hotas;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// Reading the Commander's controllers through Raw Input and hid.dll, each interface under its
/// <see cref="HidIdentity"/>. Each collection is read on a thread of its own; <see cref="IsSettled"/> re-lists
/// the collections, and <see cref="Poll"/> returns the states those threads last read.
/// </summary>
public sealed class HidControllers : IHotasReader, IDisposable
{
    /// <summary>How long the list must stay unchanged before it is read.</summary>
    internal static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1500);

    /// <summary>How long to wait for a first device, or for a collection's first report.</summary>
    internal static readonly TimeSpan GiveUp = TimeSpan.FromSeconds(6);

    private readonly IHidDevices _devices;
    private readonly ILogger<HidControllers> _logger;
    private readonly Func<TimeSpan> _now;
    private readonly Lock _gate = new();

    private readonly Dictionary<string, Collection> _collections = new(StringComparer.OrdinalIgnoreCase);
    private TimeSpan _changed;
    private bool _listed;
    private bool _settled;
    private bool _announced;
    private bool _disposed;

    public HidControllers(ILogger<HidControllers> logger)
        : this(
            logger,
            new HidDevices(why => logger.LogInformation("A game controller interface is left out: {Why}", why)),
            Elapsed())
    {
    }

    internal HidControllers(ILogger<HidControllers> logger, IHidDevices devices, Func<TimeSpan> now)
    {
        _logger = logger;
        _devices = devices;
        _now = now;
    }

    /// <summary>
    /// Set when Raw Input or hid.dll could not be reached, and null otherwise — including on a machine with no
    /// controllers at all, which is not a fault.
    /// </summary>
    public string? Fault { get; private set; }

    /// <summary>How many interfaces Windows listed at the last look, settled or not.</summary>
    public int Interfaces
    {
        get
        {
            lock (_gate)
            {
                return _collections.Count;
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>Re-lists the collections; the sampler thread calls it every sample.</remarks>
    public bool IsSettled
    {
        get
        {
            lock (_gate)
            {
                return _settled = Settled();
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>Answers from the last <see cref="IsSettled"/>, without listing the collections.</remarks>
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

                return _settled && _collections.Count == 0
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

        lock (_gate)
        {
            var readings = new List<HotasReading>(_collections.Count);

            foreach (var collection in _collections.Values)
            {
                if (collection.Reading() is { } reading)
                {
                    readings.Add(reading);
                }
            }

            if (!_announced)
            {
                _announced = true;
                _logger.LogInformation("Reading {Count} game controller interface(s)", readings.Count);

                foreach (var reading in readings)
                {
                    _logger.LogInformation("Game controller interface {Id}: {Description}", reading.Id, reading.Describe());
                }
            }

            return readings;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;

            foreach (var collection in _collections.Values)
            {
                collection.Stop();
            }

            _collections.Clear();
        }
    }

    private bool Settled()
    {
        if (Fault is not null || _disposed)
        {
            return true;
        }

        var now = _now();

        var changed = Relist(now);

        if (Fault is not null)
        {
            return true;
        }

        if (changed)
        {
            _changed = now;
            _announced = false;
            return false;
        }

        if (_collections.Count == 0)
        {
            return now > GiveUp;
        }

        if (now - _changed <= Settle)
        {
            return false;
        }

        var waiting = false;

        foreach (var collection in _collections.Values.Where(collection => collection.Waiting))
        {
            if (now - collection.Started > GiveUp)
            {
                collection.GaveUp = true;
                _logger.LogInformation(
                    "Game controller interface {Path} sent no report in {Seconds} s and is left out",
                    collection.Device.Path,
                    GiveUp.TotalSeconds);
            }
            else
            {
                waiting = true;
            }
        }

        return !waiting;
    }

    /// <summary>Brings the collections in line with what Windows lists; true when the list changed.</summary>
    private bool Relist(TimeSpan now)
    {
        IReadOnlyList<HidDevice> listed;

        try
        {
            listed = _devices.List();
        }
        catch (Exception ex)
        {
            Fault = "D47 could not list the game controllers through Windows Raw Input.";
            _logger.LogWarning(ex, "Raw Input or hid.dll is not available");
            return false;
        }

        var changed = false;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var device in listed)
        {
            if (!paths.Add(device.Path))
            {
                continue;
            }

            if (_collections.TryGetValue(device.Path, out var existing) && existing.Device.Handle == device.Handle)
            {
                continue;
            }

            existing?.Stop();
            _collections[device.Path] = Start(device, now);
            changed = true;

            if (_listed)
            {
                _logger.LogInformation(
                    "Game controller added: VID 0x{Vendor:X4} PID 0x{Product:X4}, now {Count} interface(s)",
                    device.VendorId,
                    device.ProductId,
                    listed.Count);
            }
        }

        foreach (var gone in _collections.Keys.Where(path => !paths.Contains(path)).ToList())
        {
            var collection = _collections[gone];
            collection.Stop();
            _collections.Remove(gone);
            changed = true;

            _logger.LogInformation(
                "Game controller removed: VID 0x{Vendor:X4} PID 0x{Product:X4}, now {Count} interface(s)",
                collection.Device.VendorId,
                collection.Device.ProductId,
                listed.Count);
        }

        _listed = true;

        if (changed)
        {
            var ordered = _collections.Values.ToList();
            var ids = HidIdentity.Build([.. ordered.Select(c => new HidInterface(c.Device.Path, c.Device.Layout.ButtonCount))]);

            for (var index = 0; index < ordered.Count; index++)
            {
                ordered[index].Id = ids[index];
            }
        }

        return changed;
    }

    private Collection Start(HidDevice device, TimeSpan now)
    {
        var collection = new Collection(device, now);

        collection.Reader = _devices.Read(
            device,
            state => collection.State = state,
            why =>
            {
                collection.Ended = true;
                _logger.LogInformation("Game controller interface {Path} {Why} and is left out", device.Path, why);
            });

        return collection;
    }

    private static Func<TimeSpan> Elapsed()
    {
        var clock = Stopwatch.StartNew();
        return () => clock.Elapsed;
    }

    /// <summary>One listed collection; its state and end are written by its reader thread.</summary>
    private sealed class Collection(HidDevice device, TimeSpan started)
    {
        private volatile HidState? _state;
        private volatile bool _ended;

        public HidDevice Device { get; } = device;

        public TimeSpan Started { get; } = started;

        public string Id { get; set; } = "";

        public IDisposable? Reader { get; set; }

        public bool GaveUp { get; set; }

        public HidState? State
        {
            get => _state;
            set => _state = value;
        }

        public bool Ended
        {
            get => _ended;
            set => _ended = value;
        }

        /// <summary>Open, with no report yet, and not given up on.</summary>
        public bool Waiting => State is null && !Ended && !GaveUp;

        public HotasReading? Reading()
        {
            if (Ended || State is not { } state)
            {
                return null;
            }

            return new HotasReading
            {
                Id = Id,
                VendorId = Device.VendorId,
                ProductId = Device.ProductId,
                Buttons = state.Buttons,
                Hats = state.Hats,
                AxisCount = Device.Layout.AxisCount,
            };
        }

        public void Stop() => Reader?.Dispose();
    }
}
