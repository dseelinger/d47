using System.Text;
using System.Text.Json;

namespace D47.Vr;

/// <summary>
/// The OpenVR action manifest, its per-controller bindings, and the <c>.vrmanifest</c> that names
/// this process to SteamVR — written to disk on demand and handed back as absolute paths.
/// <para>
/// <b>Written rather than shipped.</b> SteamVR reads all of these itself, from another process,
/// with a different working directory, so a relative path silently binds nothing. One of them has
/// to name whichever executable is running, which no checked-in file can. Writing all of them the
/// same way keeps it to one seam, and <c>data/</c> beside the executable is where everything d47
/// writes already goes.
/// </para>
/// </summary>
public static class VrActionManifest
{
    /// <summary>
    /// The application key SteamVR files bindings under. It has to be stable across releases: a
    /// Commander who rebinds the grab is rebinding it against this string, and changing it throws
    /// their binding away without saying so.
    /// </summary>
    public const string AppKey = "com.dseelinger.d47";

    public const string ActionSet = "/actions/panel";
    public const string GrabAction = "/actions/panel/in/grab";

    /// <summary>
    /// Back one level (list.md Phase 25, "Drill in, and find your way back").
    /// <para>
    /// One of the three routes that must agree - the breadcrumb, this, and a phrase - and the one
    /// a Commander with a controller in each hand reaches for without looking.
    /// </para>
    /// </summary>
    public const string BackAction = "/actions/panel/in/back";

    /// <summary>
    /// Every controller profile that gets a default binding.
    /// <para>
    /// <b><c>oculus_touch</c> and <c>rift</c> both, and that is not redundancy.</b> The Oculus
    /// driver asks for each profile in turn, and one missing binding disables input entirely —
    /// the only trace is a line in <c>vrserver.txt</c> reading "has no configured binding. Input
    /// will not be available". Index and Vive are here so the two other common headsets are not
    /// left to the same failure.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// A throwaway <c>.vrmanifest</c> naming this process, written beside the action manifest.
    /// <para>
    /// <b>Registering it is not optional, and its absence is silent.</b> SteamVR files bindings
    /// under an application key; a process it does not recognise has none, so there is nothing for
    /// a binding to attach to — the action manifest loads, the handles resolve, and the action
    /// stays bound to nothing forever. It does not appear under Manage Controller Bindings either,
    /// so it cannot be fixed by hand. This is the step two previous attempts at action input were
    /// missing, which is why both concluded the manifest route does not work.
    /// </para>
    /// </summary>
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

        // Two, and only two, and the count is about the controllers rather than about ambition:
        // the trigger and the grip are the only inputs that exist identically on all four
        // profiles below. The Vive wand has no face buttons at all and a trackpad where the
        // others have a stick, and Touch puts A and B on the right hand and X and Y on the left —
        // so a third action is per-profile work with a different answer for the wand, not one
        // more line here.
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

    /// <summary>
    /// The trigger on both hands, bound to the one action.
    /// <para>
    /// The trigger rather than the grip, for the reason that outlived every other attempt at this:
    /// it is the button a Commander already has a finger on, and binding both hands to one action
    /// means the panel does not care which hand takes it. Which hand it was is recovered from the
    /// ray, not from the button.
    /// </para>
    /// </summary>
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

                        // The grip, and it is the grip because it is the one input every profile
                        // here has. Touch and Index have face buttons under different names on
                        // each hand and the Vive wand has none at all, so a face button would be
                        // a binding that silently does not exist on somebody's controller - and a
                        // profile missing one binding disables input entirely, which is the
                        // failure the profile list above already exists to avoid.
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

    /// <summary>
    /// UTF-8 without a byte-order mark. SteamVR's JSON reader takes a BOM as the first character
    /// of the document and rejects the file, which presents as a manifest that loaded and bound
    /// nothing — indistinguishable from every other way that happens.
    /// </summary>
    private static void WriteFile(string path, object content) =>
        File.WriteAllText(path, JsonSerializer.Serialize(content, Json), new UTF8Encoding(false));
}
