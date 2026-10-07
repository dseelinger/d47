using D47.Core.Input;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Named copies of Elite's control bindings, saved and loaded by voice (#80).</summary>
public static class BindingProfilesCapability
{
    public const string Id = "binding-profiles";

    public const string ListKey = "binding-profiles.list";

    public const string SaveTool = "save_binding_profile";

    public const string LoadTool = "load_binding_profile";

    /// <param name="profiles">The saved profiles, or null under the designer and in tests that are not about them.</param>
    public static CapabilityDescriptor Create(BindingProfiles? profiles = null) => new()
    {
        Id = Id,
        Group = "Acting on the game",
        Name = "Binding profiles",
        Summary = "Save Elite's control bindings under a name and put them back when the hardware changes.",
        Examples = ["save these bindings as sim pit", "load the sim pit bindings", "list my binding profiles"],
        Keywords = ["list my binding profiles", "what binding profiles do i have"],
        Display = new CapabilityDisplay { PanelTitle = "Binding profiles", Order = 67 },
        Settings = [ListRow(profiles)],
        Tools =
        [
            // Argument-free and first, so the keywords above reach it with no model in the path.
            new ToolDefinition
            {
                Name = "list_binding_profiles",
                Description = "Report the names of the Commander's saved binding profiles.",
                Handler = (_, _) => Task.FromResult(ToolResult.Ok(Summarise(profiles))),
            },

            new ToolDefinition
            {
                Name = SaveTool,
                Description =
                    "Save Elite's current control bindings under a name, replacing a profile of that name. "
                    + "Refused while Elite is running.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "What to call the profile: letters, digits, spaces, hyphens and underscores.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(Run(profiles, arguments, (store, name) => store.Save(name))),
            },

            new ToolDefinition
            {
                Name = LoadTool,
                Description =
                    "Copy a saved binding profile back into Elite's bindings folder, keeping the set it "
                    + "replaces as \"" + BindingProfiles.BeforeLastLoad + "\". Elite reads them when it "
                    + "next starts. Refused while Elite is running.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "name",
                        Type = ToolParameterType.String,
                        Description = "Which saved profile.",
                        Required = true,
                    },
                ],
                Handler = (arguments, _) => Task.FromResult(Run(profiles, arguments, (store, name) => store.Load(name))),
            },
        ],
    };

    /// <summary>The arguments the save and load tools take.</summary>
    public static ToolArguments ArgumentsFor(string name) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = name });

    private static SettingRow ListRow(BindingProfiles? profiles) => new()
    {
        Key = ListKey,
        Label = "Saved binding profiles",
        Help = "Named copies of Elite's bindings. Say \"save these bindings as\" and a name, or \"load the\" "
               + "name \"bindings\", with Elite closed.",
        Kind = SettingKind.Info,
        DocsAnchor = "deleting-one",
        Binding = new SettingBinding { Read = _ => Summarise(profiles) },
    };

    private static ToolResult Run(
        BindingProfiles? profiles,
        ToolArguments arguments,
        Func<BindingProfiles, string, BindingProfileOutcome> act)
    {
        if (profiles is null)
        {
            return ToolResult.Error("Binding profiles are not available here.");
        }

        if (!arguments.TryGetString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return ToolResult.Error("A binding profile needs a name.");
        }

        var outcome = act(profiles, name);

        return outcome.Done ? ToolResult.Ok(outcome.Reply) : ToolResult.Error(outcome.Reply);
    }

    private static string Summarise(BindingProfiles? profiles) =>
        profiles?.Names is { Count: > 0 } names
            ? $"{names.Count} saved: {string.Join(", ", names)}."
            : "No binding profiles saved yet.";
}
