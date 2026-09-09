using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Valve.VR;

namespace D47.Vr;

/// <summary>Finds <c>openvr_api.dll</c> and teaches the vendored binding where it is.</summary>
public static class OpenVrLoader
{
    private static readonly Lock Gate = new();

    private static bool _registered;
    private static string? _resolved;

    /// <summary>The runtime path SteamVR published, or null if there is none.</summary>
    public static string? RuntimePath()
    {
        var vrpath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "openvr",
            "openvrpaths.vrpath");

        if (!File.Exists(vrpath))
        {
            return null;
        }

        try
        {
            // Parsed as the JSON it is.
            using var document = JsonDocument.Parse(File.ReadAllText(vrpath));

            if (!document.RootElement.TryGetProperty("runtime", out var runtimes)
                || runtimes.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var candidate in runtimes.EnumerateArray())
            {
                if (candidate.GetString() is { } path && Directory.Exists(path))
                {
                    return path;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A machine with no usable runtime, which is a state and not a failure.
            return null;
        }

        return null;
    }

    /// <summary>The DLL this machine would load, or null if there is nothing to load.</summary>
    public static string? Locate()
    {
        if (RuntimePath() is not { } runtime)
        {
            return null;
        }

        var dll = Path.Combine(runtime, "bin", "win64", "openvr_api.dll");
        return File.Exists(dll) ? dll : null;
    }

    /// <summary>Registers the resolver, once.</summary>
    public static bool Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return _resolved is not null;
            }

            _registered = true;
            _resolved = Locate();

            if (_resolved is null)
            {
                return false;
            }

            NativeLibrary.SetDllImportResolver(typeof(OpenVR).Assembly, Resolve);
            return true;
        }
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (!name.StartsWith("openvr_api", StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        return _resolved is not null && NativeLibrary.TryLoad(_resolved, out var handle)
            ? handle
            : IntPtr.Zero;
    }
}
