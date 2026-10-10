using Avalonia.Media.Imaging;

namespace D47.App.Headset;

/// <summary>Writes a rendered headset frame to disk as a PNG, for a capture.</summary>
internal static class VrFrameDump
{
    public static void Save(Bitmap rendered, string path) => rendered.Save(path, new PngBitmapEncoderOptions());
}
