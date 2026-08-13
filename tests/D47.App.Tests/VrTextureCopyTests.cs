using System.Runtime.InteropServices;
using D47.Vr;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The pixels have to survive the trip from the rasteriser to the texture the compositor reads.
/// <para>
/// This is the one seam where a failure is completely silent in both directions. D3D11 reports
/// an invalid call only through the debug layer, which a release build does not create, and
/// SteamVR accepts any valid texture handle without caring what is in it — so a copy that never
/// happened leaves a zeroed, fully transparent quad that <c>SetOverlayTexture</c>,
/// <c>ShowOverlay</c> and every other call return <c>None</c> for. From inside a headset that is
/// indistinguishable from an overlay that was never shown.
/// </para>
/// <para>
/// Needs a real D3D11 device but no headset and no SteamVR, so it runs anywhere the build does.
/// </para>
/// </summary>
[CollectionDefinition("gpu", DisableParallelization = true)]
public class GpuCollection;

/// <summary>
/// Run on its own. These create a real D3D11 device on the same adapter the headless Avalonia
/// tests are rendering on, and under the full suite — never alone — the read-back came back
/// entirely transparent about one run in eight. Alone it passed twelve times out of twelve, so
/// what is being measured there is the harness rather than the copy, and a gate that fails one
/// run in eight for a reason unrelated to the thing it guards is worse than no gate.
/// </summary>
[Collection("gpu")]
public class VrTextureCopyTests
{
    private const int Width = 64;
    private const int Height = 32;

    [Fact]
    public void WhatIsWrittenIntoTheTextureIsWhatTheCompositorWouldRead()
    {
        using var device = VrDevice.Create(0);
        using var texture = new VrTexture(device, Width, Height);

        // A pattern rather than a flat fill: a copy that moved the wrong number of rows, or
        // ignored the row pitch, still passes "is not all zero".
        var written = Fill(texture);

        var read = ReadBack(device, texture);

        Assert.Equal(written, read);
    }

    /// <summary>
    /// The failure this exists for, stated as its own assertion so the reason is legible when
    /// it goes red: an untouched shared texture is transparent, and transparent is invisible.
    /// </summary>
    [Fact]
    public void TheTextureTheCompositorReadsIsNotLeftTransparent()
    {
        using var device = VrDevice.Create(0);
        using var texture = new VrTexture(device, Width, Height);

        Fill(texture);

        var read = ReadBack(device, texture);

        Assert.Contains(read, pixel => (pixel >> 24) != 0);
    }

    /// <summary>Writes a known pattern through the same Map/Commit the surface uses.</summary>
    private static uint[] Fill(VrTexture texture)
    {
        var (address, rowBytes) = texture.Map();
        var expected = new uint[Width * Height];

        unsafe
        {
            for (var y = 0; y < Height; y++)
            {
                var row = (uint*)((byte*)address + (y * rowBytes));

                for (var x = 0; x < Width; x++)
                {
                    // Opaque, and different in every pixel.
                    var pixel = 0xFF000000u | (uint)((x << 8) | y);
                    row[x] = pixel;
                    expected[(y * Width) + x] = pixel;
                }
            }
        }

        texture.Commit();

        return expected;
    }

    /// <summary>
    /// Copies the shared texture back to the CPU, which is the only way to see what the
    /// compositor would have been handed.
    /// </summary>
    private static uint[] ReadBack(VrDevice device, VrTexture texture)
    {
        using var readback = device.Device.CreateTexture2D(new Texture2DDescription
        {
            Width = Width,
            Height = Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        });

        using var shared = new ID3D11Texture2D(texture.NativePointer);

        device.Context.CopyResource(readback, shared);
        device.Context.Flush();

        var mapped = device.Context.Map(readback, 0, MapMode.Read);
        var pixels = new uint[Width * Height];

        try
        {
            unsafe
            {
                for (var y = 0; y < Height; y++)
                {
                    var row = (uint*)((byte*)mapped.DataPointer + (y * (int)mapped.RowPitch));

                    for (var x = 0; x < Width; x++)
                    {
                        pixels[(y * Width) + x] = row[x];
                    }
                }
            }
        }
        finally
        {
            device.Context.Unmap(readback, 0);
        }

        return pixels;
    }
}
