using System.Runtime.InteropServices;
using D47.XrLayer;

namespace D47.Vr;

/// <summary>The memory block the OpenXR layer creates in Elite, as d47 maps it.</summary>
public interface IXrBlockMemory
{
    /// <summary>A view of the whole block, or zero when no layer has created it. Returns the same view until <see cref="Close"/>.</summary>
    IntPtr Open();

    /// <summary>Unmaps the view. Safe to call when nothing is open.</summary>
    void Close();
}

/// <summary>The real <see cref="IXrBlockMemory"/>: opens the layer's named file mapping and never creates it.</summary>
public sealed unsafe class XrSharedBlock : IXrBlockMemory
{
    private const uint FileMapAllAccess = 0xF001F;

    private nint _mapping;
    private void* _view;

    public IntPtr Open()
    {
        if (_view is not null)
        {
            return (IntPtr)_view;
        }

        _mapping = OpenFileMappingW(FileMapAllAccess, false, XrBlock.Name);

        if (_mapping == 0)
        {
            return 0;
        }

        _view = MapViewOfFile(_mapping, FileMapAllAccess, 0, 0, (nuint)sizeof(XrBlock));

        if (_view is null)
        {
            Close();
            return 0;
        }

        return (IntPtr)_view;
    }

    public void Close()
    {
        if (_view is not null)
        {
            UnmapViewOfFile(_view);
            _view = null;
        }

        if (_mapping != 0)
        {
            CloseHandle(_mapping);
            _mapping = 0;
        }
    }

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern nint OpenFileMappingW(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern void* MapViewOfFile(nint mapping, uint access, uint offsetHigh, uint offsetLow, nuint bytes);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(void* view);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
