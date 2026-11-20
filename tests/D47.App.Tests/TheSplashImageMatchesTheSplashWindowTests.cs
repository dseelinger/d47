using System.Buffers.Binary;
using D47.App.Windowing;
using Xunit;

namespace D47.App.Tests;

/// <summary>The embedded 100% splash image is the size the splash window is built at.</summary>
public class TheSplashImageMatchesTheSplashWindowTests
{
    [Fact]
    public void TheEmbeddedImageIsTheDesignSize()
    {
        using var stream = typeof(NativeSplash).Assembly.GetManifestResourceStream("D47.Splash.splash-100.png");

        Assert.NotNull(stream);

        var header = new byte[24];
        stream.ReadExactly(header);

        Assert.Equal(NativeSplash.DesignWidth, BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)));
        Assert.Equal(NativeSplash.DesignHeight, BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)));
    }
}
