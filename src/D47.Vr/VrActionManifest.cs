using System.Text.Json;
using D47.Core.Storage;

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

    public const string PrimaryAction = "/actions/panel/in/primary";
    public const string SecondaryAction = "/actions/panel/in/secondary";
    public const string MenuAction = "/actions/panel/in/menu";
    public const string StickClickAction = "/actions/panel/in/stickclick";

    /// <summary>The stick, or the trackpad on a Vive controller.</summary>
    public const string StickAction = "/actions/panel/in/stick";

    /// <summary>Every controller profile that gets a default binding.</summary>
    private static readonly string[] Profiles =
        ["oculus_touch", "rift", "knuckles", "vive_controller"];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Where the action manifest ended up, having written it and its bindings.</summary>
    public static string Write(IFileSystem files, string folder)
    {
        foreach (var profile in Profiles)
        {
            WriteFile(files, Path.Combine(folder, BindingFile(profile)), Binding(profile));
        }

        var manifest = Path.Combine(folder, "actions.json");
        WriteFile(files, manifest, Actions());
        return manifest;
    }

    /// <summary>A throwaway <c>.vrmanifest</c> naming this process, written beside the action manifest.</summary>
    public static string WriteAppManifest(IFileSystem files, string folder, string actionManifest)
    {
        var path = Path.Combine(folder, "d47.vrmanifest");

        WriteFile(files, path, new
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

        actions = new[]
        {
            new { name = GrabAction, requirement = "suggested", type = "boolean" },
            new { name = BackAction, requirement = "suggested", type = "boolean" },
            new { name = PrimaryAction, requirement = "suggested", type = "boolean" },
            new { name = SecondaryAction, requirement = "suggested", type = "boolean" },
            new { name = MenuAction, requirement = "suggested", type = "boolean" },
            new { name = StickClickAction, requirement = "suggested", type = "boolean" },
            new { name = StickAction, requirement = "suggested", type = "vector2" },
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
                [PrimaryAction] = "Primary button (A or X)",
                [SecondaryAction] = "Secondary button (B or Y)",
                [MenuAction] = "Menu button",
                [StickClickAction] = "Stick click",
                [StickAction] = "Stick",
            },
        },
    };

    /// <summary>One physical input on one or both hands and the actions its parts output to.</summary>
    private sealed record Source(string[] Hands, string Input, string Mode, (string Part, string Action)[] Outputs);

    private static readonly string[] BothHands = ["left", "right"];

    private static Source Button(string[] hands, string input, string action) =>
        new(hands, input, "button", [("click", action)]);

    private static Source Stick(string input, string mode) =>
        new(BothHands, input, mode, [("position", StickAction), ("click", StickClickAction)]);

    /// <summary>What each profile's controllers have, by the names SteamVR gives their inputs.</summary>
    private static IEnumerable<Source> Sources(string profile)
    {
        yield return Button(BothHands, "trigger", GrabAction);
        yield return Button(BothHands, "grip", BackAction);

        switch (profile)
        {
            case "oculus_touch":
            case "rift":
                yield return Button(["left"], "x", PrimaryAction);
                yield return Button(["left"], "y", SecondaryAction);
                yield return Button(["left"], "application_menu", MenuAction);
                yield return Button(["right"], "a", PrimaryAction);
                yield return Button(["right"], "b", SecondaryAction);
                yield return Stick("joystick", "joystick");
                break;

            case "knuckles":
                yield return Button(BothHands, "a", PrimaryAction);
                yield return Button(BothHands, "b", SecondaryAction);
                yield return Stick("thumbstick", "joystick");
                break;

            case "vive_controller":
                yield return Button(BothHands, "application_menu", MenuAction);
                yield return Stick("trackpad", "trackpad");
                break;
        }
    }

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
                sources = Sources(profile)
                    .SelectMany(source => source.Hands.Select(hand => (object)new
                    {
                        path = $"/user/hand/{hand}/input/{source.Input}",
                        mode = source.Mode,
                        inputs = source.Outputs.ToDictionary(
                            output => output.Part,
                            output => new { output = output.Action }),
                    }))
                    .ToArray(),
            },
        },
    };

    private static string BindingFile(string profile) => $"binding_{profile}.json";

    private static void WriteFile(IFileSystem files, string path, object content) =>
        files.WriteText(path, JsonSerializer.Serialize(content, Json));
}
