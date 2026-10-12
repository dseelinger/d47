using System.Runtime.InteropServices;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// The Direct3D 11 device behind the texture route, on this machine's first adapter: two textures made,
/// one written, both released. Skipped where no device can be made.
/// </summary>
[Trait("Category", "Integration")]
public class TheRealDeviceMakesAndWritesTexturesTests
{
    [Fact]
    public void TwoTexturesAreMadeWrittenAndReleased()
    {
        var device = new D3D11TextureDevice();
        var refused = device.Open(0);

        Assert.SkipWhen(refused is not null, refused ?? string.Empty);

        try
        {
            var first = device.Create(128, 128, out var firstRefused);
            var second = device.Create(128, 128, out var secondRefused);

            Assert.Null(firstRefused);
            Assert.Null(secondRefused);
            Assert.NotEqual(IntPtr.Zero, first);
            Assert.NotEqual(first, second);

            var pixels = new byte[128 * 128 * 4];
            Array.Fill(pixels, (byte)0x80);

            var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);

            try
            {
                device.Write(first, pinned.AddrOfPinnedObject(), 128 * 4);
            }
            finally
            {
                pinned.Free();
            }

            device.Release(first);
            device.Release(second);
        }
        finally
        {
            device.Close();
        }
    }
}
