namespace D47.Core.Audio;

/// <summary>
/// The loudest sample of a clip's first 300 ms, or of the whole clip when it is shorter, raised by any louder
/// sample after that. An effect that measures against it waits for <see cref="Primed"/> before it reads a value.
/// </summary>
internal sealed class ReferencePeak(int window)
{
    private const int WindowMs = 300;

    private readonly List<double> _running = [];

    /// <summary>The samples of the first 300 ms at <paramref name="rate"/>.</summary>
    public static int WindowAt(int rate) => rate * WindowMs / 1000;

    public int Window { get; } = Math.Max(1, window);

    public int Count => _running.Count;

    public bool Primed => Count >= Window;

    public void Add(double sample)
    {
        var magnitude = Math.Abs(sample);

        _running.Add(Count == 0 ? magnitude : Math.Max(_running[^1], magnitude));
    }

    /// <summary>
    /// The peak for the sample ending at <paramref name="through"/>: the loudest of the window and every sample
    /// up to there. Zero before any sample has been added.
    /// </summary>
    public double At(int through) =>
        Count == 0 ? 0 : _running[Math.Min(Count, Math.Max(Window, through)) - 1];
}
