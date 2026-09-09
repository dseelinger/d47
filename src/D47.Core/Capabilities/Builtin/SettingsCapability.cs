using System.Text;
using D47.Core.Configuration;

namespace D47.Core.Capabilities.Builtin;

/// <summary>The model's view of the settings surface — deliberately smaller than the Commander's.</summary>
public static class SettingsCapability
{
    public const string Id = "settings";

    public static CapabilityDescriptor Create(SettingsService settings)
    {
        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Foundation",
            Name = "Settings",
            Summary = "List the settings D47 may change on your behalf, read one, or change one.",
            Examples =
            [
                "what settings can you change",
                "turn your personality off",
                "set the journal log level to debug",
            ],
            Keywords = ["list settings", "what settings", "which settings", "settings can you change"],
            Display = new CapabilityDisplay { PanelTitle = "Settings", Order = 60, ShowOnPanel = false },
            Tools =
            [
                new ToolDefinition
                {
                    Name = "list_settings",
                    Description =
                        "List the settings that can be changed through a tool call, with their current values. "
                        + "Settings that are protected or hold a secret are not listed and cannot be changed here.",
                    Handler = (_, _) => Task.FromResult(ToolResult.Ok(List(settings))),
                },
                new ToolDefinition
                {
                    Name = "get_setting",
                    Description = "Report the current value of one setting by key.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "key",
                            Type = ToolParameterType.String,
                            Description = "The setting key, as reported by list_settings.",
                            Required = true,
                        },
                    ],
                    Handler = (arguments, _) => Task.FromResult(Get(settings, arguments)),
                },
                new ToolDefinition
                {
                    Name = "set_setting",
                    Description =
                        "Change one setting. Takes effect immediately. Protected settings and secrets are refused: "
                        + "those are changed from the settings panel by the Commander.",
                    Parameters =
                    [
                        new ToolParameter
                        {
                            Name = "key",
                            Type = ToolParameterType.String,
                            Description = "The setting key, as reported by list_settings.",
                            Required = true,
                        },
                        new ToolParameter
                        {
                            Name = "value",
                            Type = ToolParameterType.String,
                            Description = "The new value. Omit it to clear the setting back to its default.",
                        },
                    ],
                    Handler = (arguments, _) => Task.FromResult(Set(settings, arguments)),
                },
            ],
        };
    }

    /// <summary>
    /// What the model is allowed to see: rows that apply right now, are not protected, and are not
    /// secrets.
    /// </summary>
    private static IEnumerable<SettingRow> Visible(SettingsService settings) =>
        settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => !row.Protected
                          && !row.PageOnly
                          && row.Kind is not (SettingKind.Secret or SettingKind.Info)
                          && row.Applies(settings.Current));

    private static string List(SettingsService settings)
    {
        var report = new StringBuilder();

        foreach (var row in Visible(settings))
        {
            var fallback = row.DefaultDisplayFor(settings.Current);
            var value = row.Binding!.Read(settings.Current)
                        ?? (fallback is null ? "(default)" : $"(default: {fallback})");
            report.AppendLine($"{row.Key} — {row.Label}: {value}");

            if (row.ChoicesFor(settings.Current) is { Count: > 0 } choices)
            {
                report.AppendLine($"    one of: {string.Join(", ", choices)}");
            }
        }

        return report.Length == 0
            ? "There are no settings I can change from here."
            : report.ToString().TrimEnd();
    }

    private static ToolResult Get(SettingsService settings, ToolArguments arguments)
    {
        if (!arguments.TryGetString("key", out var key))
        {
            return ToolResult.Error("A setting key is required.");
        }

        if (Visible(settings).FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase))
            is not { } row)
        {
            return ToolResult.Error(Unavailable(settings, key));
        }

        var value = row.Binding!.Read(settings.Current);

        return ToolResult.Ok(value is null
            ? $"{row.Label} is unset; the default applies ({row.DefaultDisplayFor(settings.Current) ?? "none"})."
            : $"{row.Label} is {value}.");
    }

    private static ToolResult Set(SettingsService settings, ToolArguments arguments)
    {
        if (!arguments.TryGetString("key", out var key))
        {
            return ToolResult.Error("A setting key is required.");
        }

        arguments.TryGetString("value", out var value);

        var result = settings.Apply(key, value, SettingsCaller.Model);

        return result.Ok ? ToolResult.Ok(result.Message) : ToolResult.Error(result.Message);
    }

    /// <summary>One message for "no such setting" and for "not yours to touch".</summary>
    private static string Unavailable(SettingsService settings, string key) =>
        settings.Find(key) is { } row
            ? $"'{row.Label}' is not something I can change. It lives in the settings panel."
            : $"There is no setting called '{key}'.";
}
