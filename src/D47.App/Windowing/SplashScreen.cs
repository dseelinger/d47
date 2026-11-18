namespace D47.App.Windowing;

/// <summary>A splash window the host can show and close from any thread.</summary>
internal interface ISplashSurface : IDisposable
{
    void Show();

    void Close();
}

/// <summary>The startup splash's lifecycle: whether a launch shows one, and closing it exactly once.</summary>
internal sealed class SplashScreen : IDisposable
{
    private ISplashSurface? _surface;

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

    /// <summary>Closes the window; later calls do nothing.</summary>
    public void Close()
    {
        var surface = Interlocked.Exchange(ref _surface, null);

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
