namespace D47.Core.Vr;

/// <summary>What the ray's cursor looks like: a ring, or a double-headed arrow over a resize handle.</summary>
public enum VrCursor
{
    Ring,
    Horizontal,
    Vertical,
    DiagonalDown,
    DiagonalUp,
}

/// <summary>Maps a resize handle to the cursor that shows its drag direction.</summary>
public static class VrCursors
{
    /// <summary>
    /// Left or Right alone give <see cref="VrCursor.Horizontal"/>, Top or Bottom alone give
    /// <see cref="VrCursor.Vertical"/>. A corner gives a diagonal arrow running the way the corner drags:
    /// top-left/bottom-right is <see cref="VrCursor.DiagonalDown"/>, top-right/bottom-left is
    /// <see cref="VrCursor.DiagonalUp"/>. <see cref="VrHandle.None"/> gives <see cref="VrCursor.Ring"/>.
    /// </summary>
    public static VrCursor For(VrHandle handle) => handle switch
    {
        VrHandle.Left or VrHandle.Right => VrCursor.Horizontal,
        VrHandle.Top or VrHandle.Bottom => VrCursor.Vertical,
        VrHandle.Top | VrHandle.Left or VrHandle.Bottom | VrHandle.Right => VrCursor.DiagonalDown,
        VrHandle.Top | VrHandle.Right or VrHandle.Bottom | VrHandle.Left => VrCursor.DiagonalUp,
        _ => VrCursor.Ring,
    };
}
