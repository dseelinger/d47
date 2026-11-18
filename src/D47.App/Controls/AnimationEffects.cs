using System.Runtime.InteropServices;

namespace D47.App.Controls;

/// <summary>Windows' Animation effects setting (Accessibility › Visual effects).</summary>
public static class AnimationEffects
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    /// <summary>True when Windows has animation effects on; true when the setting cannot be read.</summary>
    public static bool AreOn()
    {
        try
        {
            return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var on, 0) || on;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);
}
