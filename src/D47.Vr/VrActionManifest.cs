using System.Text;
using System.Text.Json;

namespace D47.Vr;

/// <summary>
/// The OpenVR action manifest, its per-controller bindings, and the <c>.vrmanifest</c> that names this
/// process to SteamVR — written to disk on demand and handed back as absolute paths.
/// </summary>
public static class VrActionManifest
{
    /// <summary>The application key SteamVR files bindings under.</summary>
    public const string AppKey = "com.dseelinger.d47";

    public const string ActionSet = "/actions/panel";
    public const string GrabAction = "/actions/panel/in/grab";

    /// <summary>Back one level (Phase 25, "Drill in, and find your way back").</summary>
    public const string BackAction = "/actions/panel/in/back";

    /// <summary>Every controller profile that gets a default binding.</summary>
    private static readonly string[] Profiles =
        ["oculus_touch", "rift", "knuckles", "vive_controller"];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Where the action manifest ended up, having written it and its bindings.</summary>
    public static string Write(string folder)
    {
        Directory.CreateDirectory(folder);

        foreach (var profile in Profiles)
        {
            WriteFile(Path.Combine(folder, BindingFile(profile)), Binding(profile));
        }

        var manifest = Path.Combine(folder, "actions.json");
        WriteFile(manifest, Actions());
        return manifest;
    }

    /// <summary>A throwaway <c>.vrmanifest</c> naming this process, written beside the action manifest.</summary>
    public static string WriteAppManifest(string folder, string actionManifest)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "d47.vrmanifest");

        WriteFile(path, new
        {
            source = "builtin",
            applications = new[]
            {
                new
                {
                    app_key = AppKey,
                    launch_type = "binary",
                    binary_path_windows = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0],
                    is_dashboard_overlay = true,
                    action_manifest_path = actionManifest,
                    strings = new
                    {
                        en_us = new
                        {
                            name = "d47",
                            description = "Point at the d47 panel and carry it with the trigger.",
                        },
                    },
                },
            },
        });

        return path;
    }

    private static object Actions() => new
    {
        action_sets = new[] { new { name = ActionSet, usage = "leftright" } },

        // Two, and only two, and the count is about the controllers rather than about ambition: the trigger
        // and the grip are the only inputs that exist identically on all four profiles below.
        actions = new[]
        {
            new { name = GrabAction, requirement = "suggested", type = "boolean" },
            new { name = BackAction, requirement = "suggested", type = "boolean" },
        },
        default_bindings = Profiles
            .Select(profile => new { controller_type = profile, binding_url = BindingFile(profile) })
            .ToArray(),
        localization = new[]
        {
            new Dictionary<string, string>
            {
                ["language_tag"] = "en_US",
                [ActionSet] = "d47 panel",
                [GrabAction] = "Carry the panel",
                [BackAction] = "Go back a level",
            },
        },
    };

    /// <summary>The trigger on both hands, bound to the one action.</summary>
    private static object Binding(string profile) => new
    {
        action_manifest_version = 0,
        controller_type = profile,
        description = "Hold the trigger to carry the d47 panel.",
        name = "d47 panel",
        bindings = new Dictionary<string, object>
        {
            [ActionSet] = new
            {
                sources = new[] { "left", "right" }
                    .SelectMany(hand => new object[]
                    {
                        new
                        {
                            path = $"/user/hand/{hand}/input/trigger",
                            mode = "button",
                            inputs = new { click = new { output = GrabAction } },
                        },

                        // The grip, and it is the grip because it is the one input every profile here has.
                        new
                        {
                            path = $"/user/hand/{hand}/input/grip",
                            mode = "button",
                            inputs = new { click = new { output = BackAction } },
                        },
                    })
                    .ToArray(),
            },
        },
    };

    private static string BindingFile(string profile) => $"binding_{profile}.json";

    /// <summary>UTF-8 without a byte-order mark.</summary>
    private static void WriteFile(string path, object content) =>
        File.WriteAllText(path, JsonSerializer.Serialize(content, Json), new UTF8Encoding(false));
}
