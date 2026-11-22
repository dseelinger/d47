using System.Runtime.InteropServices;

namespace D47.EliteVad;

/// <summary>
/// Captures one process's audio, with its child processes, through the process-loopback virtual device.
/// Delivers 48 kHz stereo 16-bit frames. Call from an MTA thread.
/// </summary>
internal sealed class ProcessLoopback : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 2;
    public const int BytesPerFrame = Channels * 2;

    private const string ProcessLoopbackDevice = @"VAD\Process_Loopback";
    private const uint StreamFlagsLoopback = 0x00020000;
    private const uint StreamFlagsEventCallback = 0x00040000;
    private const uint StreamFlagsAutoConvertPcm = 0x80000000;
    private const uint BufferFlagsSilent = 0x2;
    private const long BufferDuration100ns = 2_000_000;

    private readonly IAudioClient _client;
    private readonly IAudioCaptureClient _capture;
    private readonly AutoResetEvent _ready = new(false);
    private byte[] _scratch = new byte[BytesPerFrame * SampleRate];

    private ProcessLoopback(IAudioClient client, IAudioCaptureClient capture)
    {
        _client = client;
        _capture = capture;
    }

    /// <summary>One packet: interleaved 16-bit frames, and the capture time of its first frame in 100 ns QPC units, or 0 when the engine gave none.</summary>
    public delegate void PacketHandler(ReadOnlySpan<byte> frames, ulong qpcPosition100ns);

    public static ProcessLoopback Start(int processId)
    {
        var activation = new ActivationParams { ActivationType = 1, TargetProcessId = (uint)processId, ProcessLoopbackMode = 0 };
        var activationSize = Marshal.SizeOf<ActivationParams>();
        var activationPtr = Marshal.AllocHGlobal(activationSize);
        var variantPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PropVariantBlob>());
        var formatPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WaveFormatEx>());
        try
        {
            Marshal.StructureToPtr(activation, activationPtr, false);
            Marshal.StructureToPtr(new PropVariantBlob { Vt = 65, CbSize = (uint)activationSize, PBlobData = activationPtr }, variantPtr, false);

            var handler = new CompletionHandler();
            var iid = typeof(IAudioClient).GUID;
            Marshal.ThrowExceptionForHR(ActivateAudioInterfaceAsync(ProcessLoopbackDevice, ref iid, variantPtr, handler, out var operation));
            if (!handler.Done.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Process-loopback activation did not complete.");
            }

            Marshal.ThrowExceptionForHR(operation.GetActivateResult(out var activateResult, out var activated));
            Marshal.ThrowExceptionForHR(activateResult);
            var client = (IAudioClient)activated;

            Marshal.StructureToPtr(new WaveFormatEx
            {
                FormatTag = 1,
                Channels = Channels,
                SamplesPerSec = SampleRate,
                AvgBytesPerSec = SampleRate * BytesPerFrame,
                BlockAlign = BytesPerFrame,
                BitsPerSample = 16,
                CbSize = 0,
            }, formatPtr, false);

            Marshal.ThrowExceptionForHR(client.Initialize(0, StreamFlagsLoopback | StreamFlagsEventCallback | StreamFlagsAutoConvertPcm,
                BufferDuration100ns, 0, formatPtr, IntPtr.Zero));

            var captureIid = typeof(IAudioCaptureClient).GUID;
            Marshal.ThrowExceptionForHR(client.GetService(ref captureIid, out var service));
            var loopback = new ProcessLoopback(client, (IAudioCaptureClient)service);
            Marshal.ThrowExceptionForHR(client.SetEventHandle(loopback._ready.SafeWaitHandle.DangerousGetHandle()));
            Marshal.ThrowExceptionForHR(client.Start());
            return loopback;
        }
        finally
        {
            Marshal.FreeHGlobal(formatPtr);
            Marshal.FreeHGlobal(variantPtr);
            Marshal.FreeHGlobal(activationPtr);
        }
    }

    /// <summary>Waits up to <paramref name="timeout"/> for data, then hands every waiting packet to <paramref name="onPacket"/>.</summary>
    public void Drain(TimeSpan timeout, PacketHandler onPacket)
    {
        _ready.WaitOne(timeout);
        while (true)
        {
            Marshal.ThrowExceptionForHR(_capture.GetNextPacketSize(out var next));
            if (next == 0)
            {
                return;
            }

            Marshal.ThrowExceptionForHR(_capture.GetBuffer(out var data, out var frames, out var flags, out _, out var qpc));
            var bytes = (int)frames * BytesPerFrame;
            if (_scratch.Length < bytes)
            {
                _scratch = new byte[bytes];
            }

            var span = _scratch.AsSpan(0, bytes);
            if ((flags & BufferFlagsSilent) != 0)
            {
                span.Clear();
            }
            else
            {
                Marshal.Copy(data, _scratch, 0, bytes);
            }

            Marshal.ThrowExceptionForHR(_capture.ReleaseBuffer(frames));
            onPacket(span, qpc);
        }
    }

    public void Dispose()
    {
        _client.Stop();
        Marshal.ReleaseComObject(_capture);
        Marshal.ReleaseComObject(_client);
        _ready.Dispose();
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true)]
    private static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        ref Guid riid,
        IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);

    [StructLayout(LayoutKind.Sequential)]
    private struct ActivationParams
    {
        public int ActivationType;
        public uint TargetProcessId;
        public int ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariantBlob
    {
        [FieldOffset(0)] public ushort Vt;
        [FieldOffset(8)] public uint CbSize;
        [FieldOffset(16)] public IntPtr PBlobData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormatEx
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort CbSize;
    }

    [ComVisible(true)]
    private sealed class CompletionHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        public ManualResetEventSlim Done { get; } = new(false);

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation) => Done.Set();
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IActivateAudioInterfaceAsyncOperation
    {
        [PreserveSig] int GetActivateResult(out int activateResult, [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
    }

    [ComImport, Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
    }

    [ComImport, Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAgileObject;
}
