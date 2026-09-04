using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace D47.App.Media;

/// <summary>
/// Reads an MP4 one frame at a time, through Media Foundation
/// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>).
/// <para>
/// <b>Windows' own decoder rather than a package, and that was the open question the issue
/// filed.</b> A NuGet decoder would have to pass <c>PackageLicenceGateTests</c> and would add a
/// native payload to a self-contained build that already carries Whisper's and OpenVR's. Media
/// Foundation is in Windows, reachable through the COM interop this project already uses for the
/// Start Menu shortcut, and costs the build nothing.
/// </para>
/// <para>
/// <b>Sequential, one frame held.</b> A 180-frame turntable is 663 MB decoded, so nothing here
/// decodes ahead: the reader is asked for the next frame when the next frame is due, which is what
/// a source reader is good at. What is held is one <see cref="WriteableBitmap"/>, written in place,
/// so a hull that plays through costs 3.7 MB rather than the video.
/// </para>
/// <para>
/// <b>Every failure is null.</b> A file half-fetched, a codec missing from an N edition of
/// Windows, a video track that is not there — all of them mean the card keeps the still it was
/// already drawing. Nothing here is worth an error to a Commander looking at a picture.
/// </para>
/// </summary>
internal sealed class VideoFrames : IDisposable
{
    /// <summary>MF_SDK_VERSION 2, MF_API_VERSION 0x70. What <c>MFStartup</c> is told.</summary>
    private const int Version = 0x00020070;

    /// <summary>MFSTARTUP_LITE: no sockets, which nothing here reads.</summary>
    private const int Lite = 1;

    private const uint FirstVideoStream = 0xFFFFFFFC;
    private const uint AllStreams = 0xFFFFFFFE;
    private const uint EndOfStream = 0x00000002;

    private static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
    private static readonly Guid FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    private static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    private static readonly Guid VideoMajorType = new("73646976-0000-0010-8000-00AA00389B71");

    /// <summary>MFVideoFormat_RGB32, which is D3DFMT_X8R8G8B8 — B, G, R, unused, in that order.</summary>
    private static readonly Guid Rgb32 = new("00000016-0000-0010-8000-00AA00389B71");

    /// <summary>
    /// MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING. Without it the reader hands back whatever the
    /// codec produces — NV12 for H.264 — and converting that here would be a colour-space
    /// conversion written by hand. With it, Windows' own video processor is put in the chain and
    /// the reader can be asked for RGB32 directly.
    /// </summary>
    private static readonly Guid EnableVideoProcessing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");

    private static readonly object Gate = new();
    private static bool _started;

    private readonly IMFSourceReader _reader;
    private readonly int _width;
    private readonly int _height;

    private bool _ended;
    private byte[]? _scratch;

    private VideoFrames(IMFSourceReader reader, int width, int height, double framesPerSecond)
    {
        _reader = reader;
        _width = width;
        _height = height;
        FramesPerSecond = framesPerSecond;
    }

    /// <summary>What the file says it was encoded at, or 12 where it does not say.</summary>
    internal double FramesPerSecond { get; }

    internal PixelSize Size => new(_width, _height);

    /// <summary>Whether the last <see cref="Next"/> ran off the end of the video.</summary>
    internal bool Ended => _ended;

    /// <summary>Opens a file for reading, or null if it cannot be read as video.</summary>
    internal static VideoFrames? Open(string path)
    {
        try
        {
            lock (Gate)
            {
                if (!_started)
                {
                    // Once per process, and never shut down: MFShutdown while another reader is
                    // open takes that reader with it, and the process is about to exit anyway.
                    if (MFStartup(Version, Lite) != 0)
                    {
                        return null;
                    }

                    _started = true;
                }
            }

            if (MFCreateAttributes(out var attributes, 1) != 0)
            {
                return null;
            }

            var processing = EnableVideoProcessing;
            attributes.SetUINT32(ref processing, 1);

            if (MFCreateSourceReaderFromURL(path, attributes, out var reader) != 0)
            {
                return null;
            }

            // Everything off, then the video back on. A turntable has no audio track, but a reader
            // left with every stream selected would decode one if a file ever grew it.
            reader.SetStreamSelection(AllStreams, false);
            reader.SetStreamSelection(FirstVideoStream, true);

            if (MFCreateMediaType(out var wanted) != 0)
            {
                return null;
            }

            var major = MajorType;
            var video = VideoMajorType;
            var sub = Subtype;
            var rgb = Rgb32;

            wanted.SetGUID(ref major, ref video);
            wanted.SetGUID(ref sub, ref rgb);

            if (reader.SetCurrentMediaType(FirstVideoStream, IntPtr.Zero, wanted) != 0
                || reader.GetCurrentMediaType(FirstVideoStream, out var actual) != 0)
            {
                return null;
            }

            // A positive stride asks the processor for top-down rows. Uncompressed RGB in Media
            // Foundation is bottom-up by convention, and a turntable played upside down is the
            // kind of defect that survives a green suite.
            var stride = DefaultStride;
            var size = FrameSize;

            if (actual.GetUINT64(ref size, out var packed) != 0)
            {
                return null;
            }

            var width = (int)(packed >> 32);
            var height = (int)(packed & 0xFFFFFFFF);

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            actual.SetUINT32(ref stride, (uint)(width * 4));
            reader.SetCurrentMediaType(FirstVideoStream, IntPtr.Zero, actual);

            var rate = FrameRate;
            var fps = 12.0;

            if (actual.GetUINT64(ref rate, out var timing) == 0)
            {
                var numerator = (double)(timing >> 32);
                var denominator = (double)(timing & 0xFFFFFFFF);

                if (numerator > 0 && denominator > 0)
                {
                    fps = numerator / denominator;
                }
            }

            return new VideoFrames(reader, width, height, fps);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// A bitmap of the right shape to be written into, opaque because RGB32 has no alpha to
    /// honour — its fourth byte is padding, and read as transparency it would draw nothing.
    /// </summary>
    internal WriteableBitmap Frame() =>
        new(Size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    /// <summary>
    /// Writes the next frame into <paramref name="target"/>, or returns false at the end of the
    /// video or on any failure.
    /// </summary>
    internal bool Next(WriteableBitmap target)
    {
        if (_ended || target.PixelSize != Size)
        {
            return false;
        }

        try
        {
            // A reader can answer with no sample and no end of stream - a gap, a format change -
            // so the ask is a short loop rather than one call. Bounded, because "keep asking until
            // it answers" is how a decoder hangs a UI thread.
            for (var attempt = 0; attempt < 8; attempt++)
            {
                if (_reader.ReadSample(FirstVideoStream, 0, out _, out var flags, out _, out var sample) != 0)
                {
                    _ended = true;

                    return false;
                }

                if ((flags & EndOfStream) != 0)
                {
                    _ended = true;

                    return false;
                }

                if (sample is null)
                {
                    continue;
                }

                try
                {
                    return Copy(sample, target);
                }
                finally
                {
                    Marshal.ReleaseComObject(sample);
                }
            }

            return false;
        }
        catch (Exception)
        {
            _ended = true;

            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            Marshal.ReleaseComObject(_reader);
        }
        catch (Exception)
        {
            // Releasing twice, or after the runtime has already torn the RCW down, is not worth
            // taking a page down for.
        }
    }

    private bool Copy(IMFSample sample, WriteableBitmap target)
    {
        if (sample.ConvertToContiguousBuffer(out var buffer) != 0)
        {
            return false;
        }

        try
        {
            if (buffer.Lock(out var source, out _, out var length) != 0)
            {
                return false;
            }

            try
            {
                var stride = _width * 4;

                if (length < stride * _height)
                {
                    return false;
                }

                using var frame = target.Lock();

                // Through a managed array rather than pointer to pointer, so this project stays
                // free of `unsafe`. It is two memcpys of 3.7 MB where one would do — 90 MB a
                // second at twelve frames, against a memory bus that does thousands — and the
                // scratch is kept rather than allocated per frame, which is the part that would
                // actually have cost anything.
                _scratch ??= new byte[stride * _height];

                Marshal.Copy(source, _scratch, 0, stride * _height);

                for (var row = 0; row < _height; row++)
                {
                    Marshal.Copy(
                        _scratch, row * stride, frame.Address + (row * frame.RowBytes), stride);
                }

                return true;
            }
            finally
            {
                buffer.Unlock();
            }
        }
        finally
        {
            Marshal.ReleaseComObject(buffer);
        }
    }

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(int version, int flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateMediaType(out IMFMediaType type);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int MFCreateSourceReaderFromURL(
        string url, IMFAttributes? attributes, out IMFSourceReader reader);

    /// <summary>
    /// <b>The unused slots are load-bearing.</b> A COM vtable is ordered, so every method before
    /// the one being called has to occupy its place whether or not this code has any use for it.
    /// They are named for what they actually are, so that adding a call later means changing a
    /// signature rather than counting.
    /// </summary>
    [ComImport]
    [Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFAttributes
    {
        void GetItem();

        void GetItemType();

        void CompareItem();

        void Compare();

        void GetUINT32();

        void GetUINT64();

        void GetDouble();

        void GetGUID();

        void GetStringLength();

        void GetString();

        void GetAllocatedString();

        void GetBlobSize();

        void GetBlob();

        void GetAllocatedBlob();

        void GetUnknown();

        void SetItem();

        void DeleteItem();

        void DeleteAllItems();

        [PreserveSig]
        int SetUINT32(ref Guid key, uint value);
    }

    /// <inheritdoc cref="IMFAttributes"/>
    [ComImport]
    [Guid("44AE0FA8-EA31-4109-8D2E-4CAE4997C555")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaType
    {
        void GetItem();

        void GetItemType();

        void CompareItem();

        void Compare();

        void GetUINT32();

        [PreserveSig]
        int GetUINT64(ref Guid key, out ulong value);

        void GetDouble();

        void GetGUID();

        void GetStringLength();

        void GetString();

        void GetAllocatedString();

        void GetBlobSize();

        void GetBlob();

        void GetAllocatedBlob();

        void GetUnknown();

        void SetItem();

        void DeleteItem();

        void DeleteAllItems();

        [PreserveSig]
        int SetUINT32(ref Guid key, uint value);

        void SetUINT64();

        void SetDouble();

        [PreserveSig]
        int SetGUID(ref Guid key, ref Guid value);
    }

    /// <inheritdoc cref="IMFAttributes"/>
    [ComImport]
    [Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSourceReader
    {
        void GetStreamSelection();

        [PreserveSig]
        int SetStreamSelection(uint index, [MarshalAs(UnmanagedType.Bool)] bool selected);

        void GetNativeMediaType();

        [PreserveSig]
        int GetCurrentMediaType(uint index, out IMFMediaType type);

        [PreserveSig]
        int SetCurrentMediaType(uint index, IntPtr reserved, IMFMediaType type);

        void SetCurrentPosition();

        [PreserveSig]
        int ReadSample(
            uint index,
            uint controlFlags,
            out uint actualIndex,
            out uint streamFlags,
            out long timestamp,
            out IMFSample? sample);
    }

    /// <inheritdoc cref="IMFAttributes"/>
    [ComImport]
    [Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSample
    {
        void GetItem();

        void GetItemType();

        void CompareItem();

        void Compare();

        void GetUINT32();

        void GetUINT64();

        void GetDouble();

        void GetGUID();

        void GetStringLength();

        void GetString();

        void GetAllocatedString();

        void GetBlobSize();

        void GetBlob();

        void GetAllocatedBlob();

        void GetUnknown();

        void SetItem();

        void DeleteItem();

        void DeleteAllItems();

        void SetUINT32();

        void SetUINT64();

        void SetDouble();

        void SetGUID();

        void SetString();

        void SetBlob();

        void SetUnknown();

        void LockStore();

        void UnlockStore();

        void GetCount();

        void GetItemByIndex();

        void CopyAllItems();

        void GetSampleFlags();

        void SetSampleFlags();

        void GetSampleTime();

        void SetSampleTime();

        void GetSampleDuration();

        void SetSampleDuration();

        void GetBufferCount();

        void GetBufferByIndex();

        [PreserveSig]
        int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
    }

    /// <inheritdoc cref="IMFAttributes"/>
    [ComImport]
    [Guid("045FA593-8799-42b8-BC8D-8968C6453507")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaBuffer
    {
        [PreserveSig]
        int Lock(out IntPtr buffer, out int maxLength, out int currentLength);

        [PreserveSig]
        int Unlock();
    }
}
