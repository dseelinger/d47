namespace D47.App.Windowing;

/// <summary>A splash window the host can show and close from any thread.</summary>
internal interface ISplashSurface : IDisposable
{
    void Show();

    void Close();

    /// <summary>Sets the boot bar's fill to this fraction of the track; must not wait on the splash thread.</summary>
    void Progress(double fraction);
}

/// <summary>The startup checkpoints, in the order startup reaches them.</summary>
internal enum BootStage
{
    Foundations,
    Stores,
    GameLoop,
    Services,
    Capabilities,
    Crew,
    Host,
    Running,
    Window,
}

/// <summary>The startup splash's lifecycle: whether a launch shows one, and closing it exactly once.</summary>
internal sealed class SplashScreen : IDisposable
{
    private readonly object _gate = new();
    private ISplashSurface? _surface;
    private int _reached = -1;

    private SplashScreen(ISplashSurface surface) => _surface = surface;

    /// <summary>The splash of this launch, or null when none was opened.</summary>
    public static SplashScreen? Current { get; private set; }

    /// <summary>Whether a launch with these arguments shows a splash.</summary>
    public static bool Wanted(IReadOnlyCollection<string> args, bool ownsInstance)
    {
        if (!ownsInstance || args.Contains(SelfTest.Flag, StringComparer.Ordinal))
        {
            return false;
        }

#if DEBUG
        if (args.Contains(Panel.HullGpuCapture.Flag, StringComparer.Ordinal))
        {
            return false;
        }
#endif

        return true;
    }

    /// <summary>Shows the splash when the launch wants one; returns null otherwise.</summary>
    public static SplashScreen? Open(IReadOnlyCollection<string> args, bool ownsInstance, Func<ISplashSurface> create)
    {
        if (!Wanted(args, ownsInstance))
        {
            return null;
        }

        var surface = create();
        var splash = new SplashScreen(surface);
        Current = splash;
        surface.Show();

        return splash;
    }

    /// <summary>Moves the boot bar to this stage; a stage at or before the last one reported, or one after Close, does nothing.</summary>
    public void Reached(BootStage stage)
    {
        var index = (int)stage;

        lock (_gate)
        {
            if (_surface is null || index <= _reached)
            {
                return;
            }

            _reached = index;
            _surface.Progress((index + 1) / (double)Enum.GetValues<BootStage>().Length);
        }
    }

    /// <summary>Closes the window; later calls do nothing.</summary>
    public void Close()
    {
        ISplashSurface? surface;

        lock (_gate)
        {
            surface = _surface;
            _surface = null;
        }

        if (surface is null)
        {
            return;
        }

        surface.Close();
        surface.Dispose();
    }

    public void Dispose()
    {
        Close();

        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }
}
