namespace D47.Vr.Tests;

/// <summary>Stands in for the device a picture of the eye is read back on.</summary>
public sealed class FakeEyeReader : IVrEyeReader
{
    private IntPtr _next = 0x9000;

    public List<(uint Low, int High)> Luids { get; } = [];

    public List<(IntPtr Texture, int Width, int Height, uint Format, string Name)> Created { get; } = [];

    public List<IntPtr> Released { get; } = [];

    /// <summary>The byte every channel of the read-back eye holds.</summary>
    public byte Shade { get; set; }

    public string? ReadRefused { get; set; }

    public string? OpenOnLuid(uint luidLow, int luidHigh)
    {
        Luids.Add((luidLow, luidHigh));
        return null;
    }

    public IntPtr CreateShared(int width, int height, uint format, string name, out string? refused)
    {
        refused = null;
        var texture = _next++;
        Created.Add((texture, width, height, format, name));
        return texture;
    }

    public byte[]? Read(IntPtr texture, out string? refused)
    {
        refused = ReadRefused;
        if (refused is not null)
        {
            return null;
        }

        var made = Created.Single(c => c.Texture == texture);
        var pixels = new byte[made.Width * made.Height * 4];
        Array.Fill(pixels, Shade);
        for (var at = 3; at < pixels.Length; at += 4)
        {
            pixels[at] = 255;
        }

        return pixels;
    }

    public void Release(IntPtr texture) => Released.Add(texture);

    public void Close()
    {
    }
}
