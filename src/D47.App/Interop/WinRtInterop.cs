using System.Runtime.InteropServices;

namespace D47.App.Interop;

/// <summary>The WinRT string and activation imports from <c>combase.dll</c>.</summary>
internal static class WinRtInterop
{
    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    public static extern int WindowsCreateString(string source, int length, out nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    public static extern int WindowsDeleteString(nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    public static extern int RoGetActivationFactory(nint className, ref Guid iid, out nint factory);
}
