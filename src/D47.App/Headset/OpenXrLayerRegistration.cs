using D47.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace D47.App.Headset;

/// <summary>The value names under the current user's OpenXR implicit-layer key.</summary>
public interface IImplicitLayerStore
{
    IReadOnlyList<string> ValueNames();

    void Add(string manifestPath);

    void Remove(string manifestPath);
}

/// <summary>The real registry. A value of 0 means the layer is enabled.</summary>
public sealed class RegistryImplicitLayerStore : IImplicitLayerStore
{
    private const string KeyPath = @"SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit";

    public IReadOnlyList<string> ValueNames()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValueNames() ?? [];
    }

    public void Add(string manifestPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(manifestPath, 0, RegistryValueKind.DWord);
    }

    public void Remove(string manifestPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(manifestPath, throwOnMissingValue: false);
    }
}

/// <summary>
/// Keeps the implicit-layer registry value in step with the settings: present while Show D47 in the headset and
/// Show D47 without SteamVR are both on, whether or not d47 is running.
/// </summary>
public sealed class OpenXrLayerRegistration(
    IImplicitLayerStore store,
    string manifestPath,
    ILogger<OpenXrLayerRegistration> logger)
{
    public const string ManifestName = "XR_APILAYER_D47_panel.json";

    private readonly object _gate = new();

    public static string ManifestPathBeside(string exeFolder) =>
        Path.Combine(exeFolder, "runtimes", "openxr", ManifestName);

    public static bool Wanted(VrSettings vr) => vr.Enabled && vr.OpenXrLayer;

    /// <summary>Adds or removes the value, and removes any other value naming a file called the same.</summary>
    public void Apply(VrSettings vr)
    {
        lock (_gate)
        {
            Try(() =>
            {
                foreach (var name in store.ValueNames())
                {
                    if (!string.Equals(name, manifestPath, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(Path.GetFileName(name), ManifestName, StringComparison.OrdinalIgnoreCase))
                    {
                        store.Remove(name);
                    }
                }

                if (Wanted(vr))
                {
                    store.Add(manifestPath);
                }
                else
                {
                    store.Remove(manifestPath);
                }
            });
        }
    }

    private void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            logger.LogWarning(ex, "Could not update the OpenXR layer registration");
        }
    }
}
