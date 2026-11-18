using System.Runtime.InteropServices;

namespace D47.XrLayer;

/// <summary>
/// Enables XR_KHR_composition_layer_cylinder on Elite's instance when the runtime lists it. Any failure creates
/// the instance as Elite asked for it.
/// </summary>
internal static unsafe class CylinderExtension
{
    public const string Name = "XR_KHR_composition_layer_cylinder";

    private const int MostExtensions = 1024;

    /// <summary>The name, null-terminated.</summary>
    private static ReadOnlySpan<byte> TerminatedName => "XR_KHR_composition_layer_cylinder\0"u8;

    /// <summary>
    /// The extensions to create the instance with, or null to create it with <paramref name="requested"/> unchanged:
    /// the requested ones and the cylinder extension, in an Elite process whose runtime lists it and which did not ask for it.
    /// </summary>
    public static IReadOnlyList<string>? Extended(bool inElite, IReadOnlyList<string> requested, IReadOnlyCollection<string> listed)
    {
        if (!inElite || requested.Contains(Name) || !listed.Contains(Name))
        {
            return null;
        }

        return [.. requested, Name];
    }

    /// <summary>
    /// Creates the instance through <paramref name="next"/>, with the cylinder extension added where
    /// <see cref="Extended"/> says, and again unchanged if that create fails. <paramref name="enabled"/> is true when
    /// the instance was created with the extension on.
    /// </summary>
    public static int Create(
        bool inElite,
        ApiLayerNextInfo* next,
        XrInstanceCreateInfo* createInfo,
        ApiLayerCreateInfo* forwarded,
        ulong* instance,
        out bool enabled)
    {
        enabled = false;
        var asked = false;
        var adding = false;
        try
        {
            if (inElite && createInfo is not null && Requested(createInfo) is { } requested)
            {
                asked = requested.Contains(Name);
                adding = Extended(inElite, requested, Listed(next->NextGetInstanceProcAddr)) is not null;
            }
        }
        catch (Exception)
        {
            adding = false;
        }

        if (adding)
        {
            var count = (int)createInfo->EnabledExtensionCount;
            var names = stackalloc byte*[count + 1];
            for (var i = 0; i < count; i++)
            {
                names[i] = createInfo->EnabledExtensionNames[i];
            }

            fixed (byte* name = TerminatedName)
            {
                names[count] = name;
                var withCylinders = *createInfo;
                withCylinders.EnabledExtensionCount = (uint)count + 1;
                withCylinders.EnabledExtensionNames = names;
                var copy = *forwarded;
                var result = next->NextCreateApiLayerInstance(&withCylinders, &copy, instance);
                if (result >= XrResult.Success)
                {
                    enabled = true;
                    return result;
                }
            }
        }

        var unchanged = *forwarded;
        var created = next->NextCreateApiLayerInstance(createInfo, &unchanged, instance);
        enabled = asked && created >= XrResult.Success;
        return created;
    }

    /// <summary>The extensions Elite asked for, or null when its list cannot be read.</summary>
    private static List<string>? Requested(XrInstanceCreateInfo* createInfo)
    {
        if (createInfo->EnabledExtensionCount >= MostExtensions
            || (createInfo->EnabledExtensionCount > 0 && createInfo->EnabledExtensionNames is null))
        {
            return null;
        }

        var names = new List<string>();
        for (var i = 0; i < createInfo->EnabledExtensionCount; i++)
        {
            var name = createInfo->EnabledExtensionNames[i];
            if (name is null)
            {
                return null;
            }

            names.Add(Marshal.PtrToStringUTF8((nint)name) ?? string.Empty);
        }

        return names;
    }

    /// <summary>The extensions the next layer and the runtime list, asked with a null instance. Empty when they cannot be read.</summary>
    private static HashSet<string> Listed(delegate* unmanaged<ulong, byte*, nint*, int> getInstanceProcAddr)
    {
        var listed = new HashSet<string>(StringComparer.Ordinal);
        nint function = 0;
        fixed (byte* functionName = "xrEnumerateInstanceExtensionProperties\0"u8)
        {
            if (getInstanceProcAddr(0, functionName, &function) < XrResult.Success || function == 0)
            {
                return listed;
            }
        }

        var enumerate = (delegate* unmanaged<byte*, uint, uint*, XrExtensionProperties*, int>)function;
        uint count = 0;
        if (enumerate(null, 0, &count, null) < XrResult.Success || count is 0 or > MostExtensions)
        {
            return listed;
        }

        var properties = new XrExtensionProperties[count];
        for (var i = 0; i < count; i++)
        {
            properties[i].Type = XrType.ExtensionProperties;
        }

        fixed (XrExtensionProperties* filled = properties)
        {
            if (enumerate(null, count, &count, filled) < XrResult.Success)
            {
                return listed;
            }

            for (var i = 0; i < count && i < properties.Length; i++)
            {
                var name = new ReadOnlySpan<byte>(filled[i].ExtensionName, XrConstant.MaxExtensionNameSize);
                var end = name.IndexOf((byte)0);
                listed.Add(System.Text.Encoding.UTF8.GetString(end < 0 ? name : name[..end]));
            }
        }

        return listed;
    }
}
