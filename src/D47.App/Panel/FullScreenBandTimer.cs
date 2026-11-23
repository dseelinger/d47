namespace D47.App.Panel;

/// <summary>
/// When the window's full-screen band shows: from entering full screen or a pointer move until the pointer has been
/// still for <see cref="HideAfter"/>, and for as long as the pointer is over the band.
/// </summary>
public sealed class FullScreenBandTimer(TimeProvider time)
{
    public static readonly TimeSpan HideAfter = TimeSpan.FromSeconds(5);

    private DateTimeOffset _moved = time.GetUtcNow();

    /// <summary>The pointer moved, or full screen was entered.</summary>
    public void Moved() => _moved = time.GetUtcNow();

    public bool Shows(bool overBand) => overBand || time.GetUtcNow() - _moved < HideAfter;
}
