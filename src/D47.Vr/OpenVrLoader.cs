using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Valve.VR;

namespace D47.Vr;

/// <summary>
/// Finds <c>openvr_api.dll</c> and teaches the vendored binding where it is.
/// <para>
/// The DLL ships inside the SteamVR runtime and is not on <c>PATH</c>. Two other projects
/// solved this by copying Valve's binary next to their own executable, which works and costs
/// them a redistributed native library, a build step that silently does nothing when it
/// breaks, and a `0xC0000135` at first call when it does. Resolving it out of the installed
/// runtime instead means d47 ships no Valve binary at all, and the failure mode is the one we
/// want anyway: no runtime found is <see cref="Core.Vr.VrState.Unavailable"/>, which is a
/// first-class state rather than an error.
/// </para>
/// </summary>
public static class OpenVrLoader
{
    private static readonly Lock Gate = new();

    private static bool _registered;
    private static string? _resolved;

    /// <summary>
    /// The runtime path SteamVR published, or null if there is none. Reading it is the
    /// cheapest available answer to "is there a runtime on this machine at all" — cheaper
    /// than <c>VR_Init</c>, and it does not touch the compositor.
    /// </summary>
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
            // Parsed as the JSON it is. The spike scanned for quotes, which is fine for a
            // spike and wrong here: the file holds several arrays of Windows paths, every one
            // of them full of escaped backslashes, and a scanner that finds the wrong opening
            // bracket returns a plausible path to nothing.
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

    /// <summary>
    /// Registers the resolver, once. Safe to call from anywhere and any number of times;
    /// <see cref="NativeLibrary.SetDllImportResolver"/> throws on a second registration for
    /// the same assembly, which would turn a redundant call into a crash.
    /// </summary>
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
