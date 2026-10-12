namespace D47.Vr.Tests;

/// <summary>A Direct3D 11 device that hands out numbered textures and remembers what it was asked.</summary>
public sealed class FakeTextureDevice(FakeOpenVr? openVr = null) : IVrTextureDevice
{
    private IntPtr _nextTexture = 0x1000;

    /// <summary>What <c>Open</c> answers with. Null opens the device.</summary>
    public string? OpenRefused { get; set; }

    /// <summary>What <c>Create</c> answers with. Null makes a texture.</summary>
    public string? CreateRefused { get; set; }

    /// <summary>The adapter each <c>Open</c> named, in order.</summary>
    public List<int> Opens { get; } = [];

    public int Closes { get; private set; }

    /// <summary>Whether the device is open right now.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Every texture ever made, in order.</summary>
    public List<IntPtr> Created { get; } = [];

    /// <summary>The textures that exist right now.</summary>
    public HashSet<IntPtr> Live { get; } = [];

    /// <summary>The texture each <c>Write</c> went into, in order.</summary>
    public List<IntPtr> Writes { get; } = [];

    public string? Open(int adapter)
    {
        Opens.Add(adapter);
        IsOpen = OpenRefused is null;
        return OpenRefused;
    }

    public IntPtr Create(int width, int height, out string? refused)
    {
        refused = CreateRefused;

        if (refused is not null)
        {
            return 0;
        }

        var texture = _nextTexture++;
        Created.Add(texture);
        Live.Add(texture);
        return texture;
    }

    /// <summary>How many textures <see cref="FakeOpenVr"/> had been handed at each <c>Flush</c>, in order.</summary>
    public List<int> FlushedAfterSubmits { get; } = [];

    public void Write(IntPtr texture, IntPtr pixels, int rowBytes) => Writes.Add(texture);

    public void Flush() => FlushedAfterSubmits.Add(openVr?.TexturesSubmitted.Count ?? -1);

    public void Release(IntPtr texture) => Live.Remove(texture);

    public void Close()
    {
        Closes++;
        IsOpen = false;
    }
}
