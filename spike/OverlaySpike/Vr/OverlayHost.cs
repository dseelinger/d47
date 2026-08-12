using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Valve.VR;

namespace OverlaySpike.Vr;

/// Thin wrapper over IVROverlay. Throwaway spike code.
sealed class OverlayHost : IDisposable
{
    public ulong Handle;
    CVRSystem _system;

    static OverlayHost()
    {
        // openvr_api.dll is not on PATH. Resolve it out of the SteamVR runtime.
        NativeLibrary.SetDllImportResolver(typeof(OpenVR).Assembly, (name, asm, path) =>
        {
            if (!name.StartsWith("openvr_api", StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
            foreach (var candidate in RuntimeDllCandidates())
                if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var h)) return h;
            return IntPtr.Zero;
        });
    }

    static string[] RuntimeDllCandidates()
    {
        var vrpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openvr", "openvrpaths.vrpath");
        string runtime = null;
        if (File.Exists(vrpath))
        {
            // deliberately dumb parse - spike
            var txt = File.ReadAllText(vrpath);
            var i = txt.IndexOf("\"runtime\"", StringComparison.Ordinal);
            if (i >= 0)
            {
                var a = txt.IndexOf('"', txt.IndexOf('[', i) + 1);
                var b = txt.IndexOf('"', a + 1);
                if (a > 0 && b > a) runtime = txt.Substring(a + 1, b - a - 1).Replace("\\\\", "\\");
            }
        }
        return new[]
        {
            runtime == null ? null : Path.Combine(runtime, "bin", "win64", "openvr_api.dll"),
            @"C:\Program Files (x86)\Steam\steamapps\common\SteamVR\bin\win64\openvr_api.dll",
        };
    }

    public string Init(string key, string name)
    {
        var err = EVRInitError.None;
        _system = OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
        if (err != EVRInitError.None)
            return $"VR_Init failed: {err} ({OpenVR.GetStringForHmdError(err)})";

        var ov = OpenVR.Overlay;
        if (ov == null) return "IVROverlay interface unavailable";

        var e = ov.CreateOverlay(key, name, ref Handle);
        if (e != EVROverlayError.None) return $"CreateOverlay failed: {e}";

        ov.SetOverlayWidthInMeters(Handle, 0.55f);
        ov.SetOverlayAlpha(Handle, 1.0f);
        ov.SetOverlayColor(Handle, 1, 1, 1);
        ov.SetOverlayInputMethod(Handle, VROverlayInputMethod.None);

        // 1.1 m in front of the HMD, slightly down. Head-locked so it is visible
        // no matter where the Commander is looking.
        var m = new HmdMatrix34_t
        {
            m0 = 1, m1 = 0, m2 = 0, m3 = 0.00f,
            m4 = 0, m5 = 1, m6 = 0, m7 = -0.10f,
            m8 = 0, m9 = 0, m10 = 1, m11 = -1.10f,
        };
        e = ov.SetOverlayTransformTrackedDeviceRelative(Handle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref m);
        if (e != EVROverlayError.None) return $"SetOverlayTransform failed: {e}";

        e = ov.ShowOverlay(Handle);
        if (e != EVROverlayError.None) return $"ShowOverlay failed: {e}";
        return null;
    }

    public EVROverlayError SubmitD3D11(IntPtr texturePtr)
    {
        var t = new Texture_t { handle = texturePtr, eType = ETextureType.DirectX, eColorSpace = EColorSpace.Auto };
        return OpenVR.Overlay.SetOverlayTexture(Handle, ref t);
    }

    public EVROverlayError SubmitRaw(IntPtr buffer, int w, int h)
        => OpenVR.Overlay.SetOverlayRaw(Handle, buffer, (uint)w, (uint)h, 4);

    /// Which DXGI adapter does SteamVR itself render on? A shared texture created
    /// on any other adapter is a cross-adapter share.
    public int PreferredAdapterIndex
    {
        get { int i = -1; _system?.GetDXGIOutputInfo(ref i); return i; }
    }

    public bool IsVisible => OpenVR.Overlay.IsOverlayVisible(Handle);

    public void PumpEvents()
    {
        var ev = new VREvent_t();
        var size = (uint)Marshal.SizeOf<VREvent_t>();
        while (OpenVR.Overlay.PollNextOverlayEvent(Handle, ref ev, size)) { }
    }

    public void Dispose()
    {
        if (Handle != 0) { OpenVR.Overlay?.DestroyOverlay(Handle); Handle = 0; }
        OpenVR.Shutdown();
    }
}
