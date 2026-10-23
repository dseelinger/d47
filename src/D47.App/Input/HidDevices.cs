using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace D47.App.Input;

/// <summary>One HID game-controller collection as Raw Input lists it.</summary>
internal sealed record HidDevice(nint Handle, string Path, ushort VendorId, ushort ProductId, HidLayout Layout)
{
    /// <summary>The collection's preparsed data, which the hid.dll parsing calls read.</summary>
    public byte[] Preparsed { get; init; } = [];
}

/// <summary>One parsed input state; the arrays are never written after it is handed out.</summary>
internal sealed record HidState(bool[] Buttons, int[] Hats);

/// <summary>The HID collections Windows lists, and reading one.</summary>
internal interface IHidDevices
{
    /// <summary>Every game-controller collection listed now; throws when Raw Input or hid.dll cannot be reached.</summary>
    IReadOnlyList<HidDevice> List();

    /// <summary>
    /// Reads a collection on a thread of its own, handing each complete state to <paramref name="state"/> and
    /// the reason to <paramref name="ended"/> when reading stops other than by disposal. Disposing does not wait.
    /// </summary>
    IDisposable Read(HidDevice device, Action<HidState> state, Action<string> ended);
}

/// <summary><see cref="IHidDevices"/> through Raw Input and hid.dll.</summary>
internal sealed class HidDevices : IHidDevices
{
    private const uint RimTypeHid = 2;
    private const uint RidiPreparsedData = 0x20000005;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidiDeviceInfo = 0x2000000b;
    private const int ErrorInsufficientBuffer = 122;

    private static readonly HashSet<ushort> ControllerUsages = [0x04, 0x05, 0x08];

    private readonly Dictionary<nint, HidDevice?> _known = new();
    private readonly Action<string> _refused;
    private bool _probed;
    private int _threads;

    /// <param name="refused">Told once for each listed collection whose name or layout cannot be read.</param>
    public HidDevices(Action<string> refused)
    {
        _refused = refused;
    }

    public IReadOnlyList<HidDevice> List()
    {
        if (!_probed)
        {
            // Loads hid.dll, so a machine without it is a fault here rather than at the first device.
            HidD_GetHidGuid(out _);
            _probed = true;
        }

        var entries = Entries();
        var listed = new List<HidDevice>();
        var seen = new HashSet<nint>();

        foreach (var entry in entries.Where(entry => entry.Type == RimTypeHid))
        {
            seen.Add(entry.Device);

            if (!_known.TryGetValue(entry.Device, out var device))
            {
                _known[entry.Device] = device = Describe(entry.Device);
            }

            if (device is not null)
            {
                listed.Add(device);
            }
        }

        foreach (var gone in _known.Keys.Where(handle => !seen.Contains(handle)).ToList())
        {
            _known.Remove(gone);
        }

        return listed;
    }

    public IDisposable Read(HidDevice device, Action<HidState> state, Action<string> ended)
    {
        var reader = new CollectionReader(device, state, ended);

        new Thread(reader.Run)
        {
            IsBackground = true,
            Name = $"d47-hid-{Interlocked.Increment(ref _threads)}",
        }.Start();

        return reader;
    }

    private static RawInputDeviceList[] Entries()
    {
        var size = (uint)Marshal.SizeOf<RawInputDeviceList>();

        while (true)
        {
            uint count = 0;

            if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var entries = new RawInputDeviceList[count];
            var got = GetRawInputDeviceList(entries, ref count, size);

            if (got != uint.MaxValue)
            {
                return entries[..(int)got];
            }

            // A device arrived between the two calls.
            if (Marshal.GetLastWin32Error() != ErrorInsufficientBuffer)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }

    private HidDevice? Describe(nint handle)
    {
        var info = new RidDeviceInfo { Size = (uint)Marshal.SizeOf<RidDeviceInfo>() };
        var infoSize = info.Size;

        if (GetRawInputDeviceInfoW(handle, RidiDeviceInfo, ref info, ref infoSize) == uint.MaxValue
            || info.Type != RimTypeHid
            || info.UsagePage != HidLayout.GenericDesktop
            || !ControllerUsages.Contains(info.Usage))
        {
            return null;
        }

        var path = Name(handle);

        if (path is null)
        {
            _refused($"VID 0x{info.VendorId:X4} PID 0x{info.ProductId:X4} has no device name");
            return null;
        }

        var layout = Layout(handle);

        if (layout is null)
        {
            _refused($"{path} has no readable report layout");
            return null;
        }

        return new HidDevice(handle, path, (ushort)info.VendorId, (ushort)info.ProductId, layout.Value.Layout)
        {
            Preparsed = layout.Value.Preparsed,
        };
    }

    private static string? Name(nint handle)
    {
        uint length = 0;
        _ = GetRawInputDeviceInfoW(handle, RidiDeviceName, (char[]?)null, ref length);

        if (length == 0)
        {
            return null;
        }

        var name = new char[length];

        if (GetRawInputDeviceInfoW(handle, RidiDeviceName, name, ref length) == uint.MaxValue)
        {
            return null;
        }

        var path = new string(name).TrimEnd('\0');

        // CreateFile does not take the NT form some versions of Windows report.
        return path.StartsWith(@"\??\", StringComparison.Ordinal) ? @"\\?\" + path[4..] : path;
    }

    private static (HidLayout Layout, byte[] Preparsed)? Layout(nint handle)
    {
        uint size = 0;
        _ = GetRawInputDeviceInfoW(handle, RidiPreparsedData, (byte[]?)null, ref size);

        if (size == 0)
        {
            return null;
        }

        var preparsed = new byte[size];

        if (GetRawInputDeviceInfoW(handle, RidiPreparsedData, preparsed, ref size) == uint.MaxValue
            || HidP_GetCaps(preparsed, out var caps) != HidpStatusSuccess
            || caps.InputReportByteLength == 0)
        {
            return null;
        }

        var buttons = new ButtonCaps[caps.NumberInputButtonCaps];
        var buttonCount = caps.NumberInputButtonCaps;

        if (buttonCount > 0 && HidP_GetButtonCaps(HidpInput, buttons, ref buttonCount, preparsed) != HidpStatusSuccess)
        {
            return null;
        }

        var values = new ValueCaps[caps.NumberInputValueCaps];
        var valueCount = caps.NumberInputValueCaps;

        if (valueCount > 0 && HidP_GetValueCaps(HidpInput, values, ref valueCount, preparsed) != HidpStatusSuccess)
        {
            return null;
        }

        var layout = HidLayout.From(
            buttons[..buttonCount].Select(cap => new HidButtonCaps(
                cap.UsagePage,
                cap.ReportId,
                cap.IsAlias != 0,
                cap.UsageMin,
                cap.IsRange != 0 ? cap.UsageMax : cap.UsageMin)),
            values[..valueCount].Select(cap => new HidValueCaps(
                cap.UsagePage,
                cap.ReportId,
                cap.IsAlias != 0,
                cap.LinkCollection,
                cap.UsageMin,
                cap.IsRange != 0 ? cap.UsageMax : cap.UsageMin,
                cap.LogicalMin,
                cap.LogicalMax)),
            caps.InputReportByteLength);

        return (layout, preparsed);
    }

    /// <summary>One collection's handle, read loop and parsing, all on its own thread.</summary>
    private sealed class CollectionReader(HidDevice device, Action<HidState> state, Action<string> ended)
        : IDisposable
    {
        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 1;
        private const uint FileShareWrite = 2;
        private const uint OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000;
        private const int ErrorIoPending = 997;

        // Never disposed: Dispose can come after the thread has ended.
        private readonly CancellationTokenSource _stopping = new();

        public void Dispose() => _stopping.Cancel();

        public void Run()
        {
            var stopping = _stopping.Token;

            try
            {
                using var handle = CreateFileW(
                    device.Path,
                    GenericRead,
                    FileShareRead | FileShareWrite,
                    0,
                    OpenExisting,
                    FileFlagOverlapped,
                    0);

                if (handle.IsInvalid)
                {
                    ended($"could not be opened (error {Marshal.GetLastWin32Error()})");
                    return;
                }

                Read(handle, stopping);
            }
            catch (Exception ex)
            {
                if (!stopping.IsCancellationRequested)
                {
                    ended($"stopped reading ({ex.Message})");
                }
            }
        }

        private void Read(SafeFileHandle handle, CancellationToken stopping)
        {
            var layout = device.Layout;
            var length = layout.ReportLength;
            var reports = new HidReports(layout.Needed);

            foreach (var id in layout.Needed.DefaultIfEmpty((byte)0))
            {
                var report = new byte[length];
                report[0] = id;

                if (HidD_GetInputReport(handle, report, (uint)length) && reports.Store(report))
                {
                    state(Parse(reports));
                }
            }

            var buffer = Marshal.AllocHGlobal(length);
            var overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
            using var done = new ManualResetEvent(false);

            try
            {
                while (!stopping.IsCancellationRequested)
                {
                    Marshal.StructureToPtr(
                        new NativeOverlapped { EventHandle = done.SafeWaitHandle.DangerousGetHandle() },
                        overlapped,
                        false);

                    if (!ReadFile(handle, buffer, (uint)length, 0, overlapped))
                    {
                        var error = Marshal.GetLastWin32Error();

                        if (error != ErrorIoPending)
                        {
                            Ended(error, stopping);
                            return;
                        }

                        if (WaitHandle.WaitAny([done, stopping.WaitHandle]) == 1)
                        {
                            _ = CancelIoEx(handle, overlapped);
                            _ = GetOverlappedResult(handle, overlapped, out _, true);
                            return;
                        }
                    }

                    if (!GetOverlappedResult(handle, overlapped, out var read, false))
                    {
                        Ended(Marshal.GetLastWin32Error(), stopping);
                        return;
                    }

                    var report = new byte[length];
                    Marshal.Copy(buffer, report, 0, (int)Math.Min(read, (uint)length));

                    if (reports.Store(report))
                    {
                        state(Parse(reports));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(overlapped);
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void Ended(int error, CancellationToken stopping)
        {
            if (!stopping.IsCancellationRequested)
            {
                ended($"stopped reading (error {error})");
            }
        }

        private HidState Parse(HidReports reports)
        {
            var layout = device.Layout;
            var preparsed = device.Preparsed;
            var buttons = new bool[layout.ButtonCount];

            if (layout.ButtonCount > 0)
            {
                var usages = new ushort[Math.Max(1, HidP_MaxUsageListLength(HidpInput, HidLayout.ButtonPage, preparsed))];

                foreach (var id in layout.ButtonReports)
                {
                    if (reports.For(id) is not { } report)
                    {
                        continue;
                    }

                    var count = (uint)usages.Length;

                    if (HidP_GetUsages(
                            HidpInput, HidLayout.ButtonPage, 0, usages, ref count, preparsed, report, (uint)report.Length)
                        == HidpStatusSuccess)
                    {
                        HidLayout.Hold(buttons, usages.AsSpan(0, (int)count));
                    }
                }
            }

            var hats = new int[layout.Hats.Count];

            for (var index = 0; index < hats.Length; index++)
            {
                var hat = layout.Hats[index];

                if (reports.For(hat.ReportId) is { } report
                    && HidP_GetUsageValue(
                        HidpInput,
                        HidLayout.GenericDesktop,
                        hat.LinkCollection,
                        HidLayout.HatSwitch,
                        out var value,
                        preparsed,
                        report,
                        (uint)report.Length)
                    == HidpStatusSuccess)
                {
                    hats[index] = HidLayout.Position(value, hat);
                }
            }

            return new HidState(buttons, hats);
        }
    }

    private const int HidpInput = 0;
    private const int HidpStatusSuccess = 0x00110000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceList
    {
        public nint Device;
        public uint Type;
    }

    /// <summary>RID_DEVICE_INFO with its HID member; the keyboard member sets the union's size.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct RidDeviceInfo
    {
        [FieldOffset(0)]
        public uint Size;

        [FieldOffset(4)]
        public uint Type;

        [FieldOffset(8)]
        public uint VendorId;

        [FieldOffset(12)]
        public uint ProductId;

        [FieldOffset(20)]
        public ushort UsagePage;

        [FieldOffset(22)]
        public ushort Usage;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct HidpCaps
    {
        [FieldOffset(4)]
        public ushort InputReportByteLength;

        [FieldOffset(46)]
        public ushort NumberInputButtonCaps;

        [FieldOffset(48)]
        public ushort NumberInputValueCaps;
    }

    /// <summary>HIDP_BUTTON_CAPS; <c>UsageMin</c> is <c>NotRange.Usage</c> when the entry is not a range.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    private struct ButtonCaps
    {
        [FieldOffset(0)]
        public ushort UsagePage;

        [FieldOffset(2)]
        public byte ReportId;

        [FieldOffset(3)]
        public byte IsAlias;

        [FieldOffset(12)]
        public byte IsRange;

        [FieldOffset(56)]
        public ushort UsageMin;

        [FieldOffset(58)]
        public ushort UsageMax;
    }

    /// <summary>HIDP_VALUE_CAPS; <c>UsageMin</c> is <c>NotRange.Usage</c> when the entry is not a range.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 72)]
    private struct ValueCaps
    {
        [FieldOffset(0)]
        public ushort UsagePage;

        [FieldOffset(2)]
        public byte ReportId;

        [FieldOffset(3)]
        public byte IsAlias;

        [FieldOffset(6)]
        public ushort LinkCollection;

        [FieldOffset(12)]
        public byte IsRange;

        [FieldOffset(40)]
        public int LogicalMin;

        [FieldOffset(44)]
        public int LogicalMax;

        [FieldOffset(56)]
        public ushort UsageMin;

        [FieldOffset(58)]
        public ushort UsageMax;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        [Out] RawInputDeviceList[]? list, ref uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfoW(nint device, uint command, ref RidDeviceInfo data, ref uint size);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfoW(nint device, uint command, [Out] char[]? data, ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfoW(nint device, uint command, [Out] byte[]? data, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(SafeFileHandle file, nint buffer, uint length, nint read, nint overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetOverlappedResult(SafeFileHandle file, nint overlapped, out uint read, bool wait);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIoEx(SafeFileHandle file, nint overlapped);

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid guid);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetInputReport(SafeFileHandle device, [In, Out] byte[] report, uint length);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(byte[] preparsed, out HidpCaps caps);

    [DllImport("hid.dll")]
    private static extern int HidP_GetButtonCaps(int type, [Out] ButtonCaps[] caps, ref ushort length, byte[] preparsed);

    [DllImport("hid.dll")]
    private static extern int HidP_GetValueCaps(int type, [Out] ValueCaps[] caps, ref ushort length, byte[] preparsed);

    [DllImport("hid.dll")]
    private static extern int HidP_MaxUsageListLength(int type, ushort usagePage, byte[] preparsed);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(
        int type,
        ushort usagePage,
        ushort linkCollection,
        [Out] ushort[] usages,
        ref uint length,
        byte[] preparsed,
        byte[] report,
        uint reportLength);

    [DllImport("hid.dll")]
    private static extern int HidP_GetUsageValue(
        int type,
        ushort usagePage,
        ushort linkCollection,
        ushort usage,
        out uint value,
        byte[] preparsed,
        byte[] report,
        uint reportLength);
}
