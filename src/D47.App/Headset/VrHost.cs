using Avalonia.Threading;
using D47.App.Panel;
using D47.Core.Configuration;
using D47.Core.Ticking;
using D47.Core.Vr;
using D47.Vr;
using Microsoft.Extensions.Logging;

namespace D47.App.Headset;

/// <summary>
/// Wires the headset into the running app: the surfaces, the runtime, the state machine, and
/// the one tick that drives all three.
/// <para>
/// <b>Everything here happens on the UI thread</b>, and the tick loop only posts to it. A
/// <c>Visual</c> is thread-affine, so rasterising the panel is the dispatcher's work whatever
/// else happens; the alternative is the tick thread blocking on a dispatcher hop every frame,
/// which is a deadlock waiting for the day the UI thread waits on something the tick loop
/// owns. Posting also keeps the tick loop's contract — a subscriber must not block, and
/// <see cref="Dispatcher.Post"/> does not (architecture.md §4).
/// </para>
/// </summary>
public sealed class VrHost : IDisposable
{
    private readonly SettingsService _settings;
    private readonly VrLifecycle _lifecycle;
    private readonly SteamVrRuntime _runtime;
    private readonly VrPanelSurface _panel;
    private readonly ILogger<VrHost> _logger;

    private int _pending;
    private bool _disposed;

    private VrHost(
        SettingsService settings,
        VrPanelSurface panel,
        SteamVrRuntime runtime,
        VrLifecycle lifecycle,
        ILogger<VrHost> logger)
    {
        _settings = settings;
        _panel = panel;
        _runtime = runtime;
        _lifecycle = lifecycle;
        _logger = logger;
    }

    public VrState State => _lifecycle.State;

    public string? Reason => _lifecycle.Reason;

    /// <summary>Which adapter the graphics device landed on. Null until a session is up.</summary>
    public string? Adapter => _runtime.Adapter;

    /// <summary>
    /// Builds the headset path and subscribes it to the tick loop.
    /// <para>
    /// Constructed unconditionally, on every machine, whether or not there is a headset. An
    /// absent headset is a first-class state and not a branch: the alternative is a code path
    /// that only runs for people who have one, which is the code path that breaks.
    /// </para>
    /// </summary>
    public static VrHost Start(
        PanelViewModel model,
        SettingsService settings,
        TickLoop tick,
        ILoggerFactory loggers)
    {
        var panel = new VrPanelSurface(model, PlacementFor);
        var runtime = new SteamVrRuntime([panel], loggers.CreateLogger<SteamVrRuntime>());
        var lifecycle = new VrLifecycle(runtime, loggers.CreateLogger<VrLifecycle>());

        var host = new VrHost(settings, panel, runtime, lifecycle, loggers.CreateLogger<VrHost>());

        tick.Add("vr", host.OnTick);
        return host;
    }

    public void Dispose()
    {
        _disposed = true;
        _lifecycle.Stop();
        _runtime.Stop();
        _panel.Dispose();
    }

    /// <summary>
    /// The placement each panel mode opens at. Settings take this over in the phase's last
    /// merge; these are the numbers a previous implementation arrived at by looking, which is
    /// a better starting point than round ones — 1.4 m across read as enormous, close to fifty
    /// degrees of view, with the cockpit behind the panel rather than around it.
    /// </summary>
    private static SurfacePlacement PlacementFor(PanelMode mode) => mode == PanelMode.Mini
        ? new SurfacePlacement { WidthMetres = 0.34f, DistanceMetres = 0.9f, DropMetres = -0.30f }
        : new SurfacePlacement { WidthMetres = 1.1f, DistanceMetres = 1.1f, DropMetres = -0.25f };

    private void OnTick(TickContext context)
    {
        // Coalesced. At 10 Hz a dispatcher that fell behind would otherwise accumulate a queue
        // of frames it will draw late and then draw all at once.
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _pending, 0);
            Serve(context.Now);
        });
    }

    private void Serve(DateTimeOffset now)
    {
        if (_disposed)
        {
            return;
        }

        var wanted = _settings.Current.Vr.Enabled;

        if (!wanted)
        {
            if (_lifecycle.State == VrState.Active)
            {
                _logger.LogInformation("The headset overlays are switched off; giving the session back");
                _lifecycle.Stop();
            }

            return;
        }

        _panel.Enabled = true;

        try
        {
            _lifecycle.Tick(now);
        }
        catch (Exception ex)
        {
            // The runtime going away underneath is a fact about the machine rather than a
            // defect of ours, and it must not take the desktop panel down with it. The state
            // machine's own recovery is a rebuild, so the next tick starts clean either way.
            _logger.LogError(ex, "The headset path threw; the session will be rebuilt");
            _lifecycle.Stop();
        }
    }
}
