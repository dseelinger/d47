namespace D47.Core.Interface;

public sealed record ScreenPicture(byte[] Jpeg, int Width, int Height, string Source);

public sealed record ScreenCaptureResult(ScreenPicture? Picture, string? Refusal);

/// <summary>What the Commander is looking at. Blocks for up to about half a second; never call it from a tick.</summary>
public interface IScreenCapture
{
    ScreenCaptureResult Take();
}

/// <summary>Where a picture of the screen comes from.</summary>
public enum ScreenSource
{
    EliteNotRunning,
    EliteWindow,
    HeadsetEye,
}

/// <summary>The source and the shape of a picture of the screen.</summary>
public static class ScreenPictures
{
    public const string FromHeadset = "the headset's left eye";

    public const string FromWindow = "Elite's window";

    /// <summary>The longest edge a picture is sent at.</summary>
    public const int MaxLongEdge = 1568;

    /// <summary>The most pixels a picture is sent with.</summary>
    public const int MaxPixels = 1_150_000;

    /// <summary>
    /// The headset's eye while SteamVR is showing Elite's process, otherwise Elite's window. Either id is 0
    /// when there is none.
    /// </summary>
    public static ScreenSource Choose(uint sceneProcess, uint eliteProcess)
    {
        if (eliteProcess == 0)
        {
            return ScreenSource.EliteNotRunning;
        }

        return sceneProcess == eliteProcess ? ScreenSource.HeadsetEye : ScreenSource.EliteWindow;
    }

    /// <summary>The centred 80% of an eye image in each direction, which leaves out the lens mask.</summary>
    public static (int X, int Y, int Width, int Height) LensCrop(int width, int height)
    {
        var croppedWidth = Math.Max(1, width * 4 / 5);
        var croppedHeight = Math.Max(1, height * 4 / 5);

        return ((width - croppedWidth) / 2, (height - croppedHeight) / 2, croppedWidth, croppedHeight);
    }

    /// <summary>The size a picture is scaled down to so it fits both caps. Never scales up.</summary>
    public static (int Width, int Height) Fit(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var scale = Math.Min(
            1.0,
            Math.Min(
                (double)MaxLongEdge / Math.Max(width, height),
                Math.Sqrt((double)MaxPixels / ((long)width * height))));

        return (Math.Max(1, (int)Math.Floor(width * scale)), Math.Max(1, (int)Math.Floor(height * scale)));
    }
}
