using D47.Core.Vr;

namespace D47.Vr.Tests;

/// <summary>A video quad whose picture a test sets.</summary>
public sealed class FakeVideo : IVrVideoSource
{
    public bool Visible { get; set; } = true;

    public SurfacePlacement Placement { get; set; } = new() { Opacity = 1f };

    public VrPicture? Picture { get; set; }

    /// <summary>A new picture of <paramref name="width"/> by <paramref name="height"/>, every byte <paramref name="paint"/>.</summary>
    public VrPicture Show(int width = 4, int height = 2, byte paint = 0x40)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, paint);

        var picture = new VrPicture(new object(), pixels, width, height);
        Picture = picture;
        return picture;
    }
}

/// <summary>A presenter clock the test steps by hand, on its own thread.</summary>
public sealed class ManualVideoClock : IVrVideoClock
{
    private Action? _step;

    /// <summary>How many runs have been started.</summary>
    public int Runs { get; private set; }

    /// <summary>How many runs have been stopped, each after its finish ran.</summary>
    public int Finishes { get; private set; }

    public bool Running => _step is not null;

    public IDisposable Run(Action step, Action finish)
    {
        Runs++;
        _step = step;
        return new Stopping(this, finish);
    }

    /// <summary>One step of the presenter.</summary>
    public void Step() => (_step ?? throw new InvalidOperationException("The presenter is not running."))();

    private sealed class Stopping(ManualVideoClock clock, Action finish) : IDisposable
    {
        public void Dispose()
        {
            clock._step = null;
            finish();
            clock.Finishes++;
        }
    }
}
