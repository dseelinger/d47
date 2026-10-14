using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using D47.Core.Vr;

namespace D47.Vr;

/// <summary>
/// Reads Virtual Desktop's view from its driver's shared memory block. The layout is undocumented
/// and a Virtual Desktop update can move it without an error anywhere.
/// </summary>
public sealed class VirtualDesktopView(string blockName) : IHeadsetViewSource
{
    public const string BlockName = "VirtualDesktop.Oculus.DriverData";

    /// <summary>A <see cref="Stopwatch"/> timestamp the driver refreshes while the headset is connected.</summary>
    public const int StampOffset = 0xF8;

    /// <summary>A uint32: 0 in Virtual Desktop's desktop view, 1 in SteamVR.</summary>
    public const int ViewOffset = 0x204;

    /// <summary>The bytes a reading needs.</summary>
    public const int Length = ViewOffset + sizeof(uint);

    /// <summary>The oldest stamp trusted; the block keeps stale contents after the headset disconnects.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromSeconds(1);

    public VirtualDesktopView()
        : this(BlockName)
    {
    }

    /// <summary>Opens the block for this read only, since it can disappear while d47 runs.</summary>
    public HeadsetView Read()
    {
        var bytes = new byte[Length];
        int read;

        try
        {
            using var map = MemoryMappedFile.OpenExisting(blockName, MemoryMappedFileRights.Read);
            using var view = map.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            read = view.ReadAtLeast(bytes, Length, throwOnEndOfStream: false);
        }
        catch (IOException)
        {
            return HeadsetView.Unknown;
        }
        catch (UnauthorizedAccessException)
        {
            return HeadsetView.Unknown;
        }

        return Decode(bytes.AsSpan(0, read), Stopwatch.GetTimestamp());
    }

    /// <summary>The view a block holds, given the <see cref="Stopwatch"/> timestamp taken after it was read.</summary>
    public static HeadsetView Decode(ReadOnlySpan<byte> block, long now)
    {
        if (block.Length < Length)
        {
            return HeadsetView.Unknown;
        }

        var age = now - BinaryPrimitives.ReadInt64LittleEndian(block[StampOffset..]);
        if (age < 0 || age > Fresh.TotalSeconds * Stopwatch.Frequency)
        {
            return HeadsetView.Unknown;
        }

        return BinaryPrimitives.ReadUInt32LittleEndian(block[ViewOffset..]) switch
        {
            0 => HeadsetView.Desktop,
            1 => HeadsetView.Vr,
            _ => HeadsetView.Unknown,
        };
    }
}
