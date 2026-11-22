using System;
using System.Runtime.InteropServices;

namespace D47.App.Media;

/// <summary>The Media Foundation source reader declarations a video playback reads a film through.</summary>
internal static class SourceReader
{
    internal const uint FirstVideoStream = 0xFFFFFFFC;
    internal const uint FirstAudioStream = 0xFFFFFFFD;
    internal const uint AnyStream = 0xFFFFFFFE;
    internal const uint MediaSource = 0xFFFFFFFF;

    internal const uint ErrorFlag = 0x00000001;
    internal const uint EndOfStream = 0x00000002;
    internal const uint CurrentTypeChanged = 0x00000020;
    internal const uint StreamTick = 0x00000100;

    internal const int InvalidStreamNumber = unchecked((int)0xC00D36B3);

    /// <summary>VT_I8, the type <see cref="IMFSourceReader.SetCurrentPosition"/> takes.</summary>
    internal const ushort LongVariant = 20;

    /// <summary>MF_SDK_VERSION 2, MF_API_VERSION 0x70.</summary>
    private const int Version = 0x00020070;

    internal static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    internal static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    internal static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
    internal static readonly Guid FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    internal static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    internal static readonly Guid VideoMajorType = new("73646976-0000-0010-8000-00AA00389B71");
    internal static readonly Guid AudioMajorType = new("73647561-0000-0010-8000-00AA00389B71");

    /// <summary>MFVideoFormat_RGB32: B, G, R and an unused byte, in that order.</summary>
    internal static readonly Guid Rgb32 = new("00000016-0000-0010-8000-00AA00389B71");

    internal static readonly Guid Pcm = new("00000001-0000-0010-8000-00AA00389B71");
    internal static readonly Guid Channels = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    internal static readonly Guid SampleRate = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    internal static readonly Guid BitsPerSample = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    internal static readonly Guid BlockAlignment = new("322de230-9eeb-43bd-ab7a-ff412251541d");
    internal static readonly Guid BytesPerSecond = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");

    /// <summary>MF_PD_DURATION, in 100 ns units.</summary>
    internal static readonly Guid Duration = new("6c990d33-bb8e-477a-8598-0d5d96fcd88a");

    /// <summary>MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING: colour conversion and scaling.</summary>
    internal static readonly Guid AdvancedVideoProcessing = new("0f81da2c-b537-4672-a8b2-a681b17307a3");

    private static readonly object Gate = new();
    private static bool _started;

    /// <summary>Starts Media Foundation once per process with sockets, which a URL needs; false where it cannot start.</summary>
    internal static bool Start()
    {
        lock (Gate)
        {
            if (!_started)
            {
                // Never shut down: MFShutdown while a reader is open takes the reader with it.
                _started = MFStartup(Version, 0) == 0;
            }

            return _started;
        }
    }

    /// <summary>A PROPVARIANT holding a 64-bit integer.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Variant
    {
        public ushort Type;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public long Value;
        public long Padding;
    }

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(int version, int flags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateAttributes(out IMFAttributes attributes, int initialSize);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    internal static extern int MFCreateMediaType(out IMFMediaType type);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern int MFCreateSourceReaderFromURL(string url, IMFAttributes? attributes, out IMFSourceReader reader);

    /// <summary>Releases a COM object, ignoring a second release.</summary>
    internal static void Release(object? com)
    {
        if (com is null)
        {
            return;
        }

        try
        {
            Marshal.ReleaseComObject(com);
        }
        catch (ArgumentException)
        {
        }
    }

    /// <summary>The unused slots are declared to keep the vtable offsets right.</summary>
    [ComImport]
    [Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFAttributes
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
    internal interface IMFMediaType
    {
        void GetItem();

        void GetItemType();

        void CompareItem();

        void Compare();

        [PreserveSig]
        int GetUINT32(ref Guid key, out uint value);

        [PreserveSig]
        int GetUINT64(ref Guid key, out ulong value);

        void GetDouble();

        [PreserveSig]
        int GetGUID(ref Guid key, out Guid value);

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

        [PreserveSig]
        int SetUINT64(ref Guid key, ulong value);

        void SetDouble();

        [PreserveSig]
        int SetGUID(ref Guid key, ref Guid value);
    }

    /// <inheritdoc cref="IMFAttributes"/>
    [ComImport]
    [Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSourceReader
    {
        [PreserveSig]
        int GetStreamSelection(uint index, [MarshalAs(UnmanagedType.Bool)] out bool selected);

        [PreserveSig]
        int SetStreamSelection(uint index, [MarshalAs(UnmanagedType.Bool)] bool selected);

        [PreserveSig]
        int GetNativeMediaType(uint index, uint typeIndex, out IMFMediaType type);

        [PreserveSig]
        int GetCurrentMediaType(uint index, out IMFMediaType type);

        [PreserveSig]
        int SetCurrentMediaType(uint index, IntPtr reserved, IMFMediaType type);

        [PreserveSig]
        int SetCurrentPosition(ref Guid timeFormat, ref Variant position);

        [PreserveSig]
        int ReadSample(
            uint index,
            uint controlFlags,
            out uint actualIndex,
            out uint streamFlags,
            out long timestamp,
            out IMFSample? sample);

        [PreserveSig]
        int Flush(uint index);

        void GetServiceForStream();

        [PreserveSig]
        int GetPresentationAttribute(uint index, ref Guid key, out Variant value);
    }

    /// <inheritdoc cref="IMFAttributes"/>
    [ComImport]
    [Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMFSample
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

        [PreserveSig]
        int GetSampleTime(out long time);

        void SetSampleTime();

        [PreserveSig]
        int GetSampleDuration(out long duration);

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
    internal interface IMFMediaBuffer
    {
        [PreserveSig]
        int Lock(out IntPtr buffer, out int maxLength, out int currentLength);

        [PreserveSig]
        int Unlock();
    }
}
