using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What leaves this machine, stated where the settings that cause it live (list.md Phase 4,
/// "Say what each provider receives"). The disclosure rows are read-only: they are not
/// something the Commander sets, they are something d47 says, and saying it as a row means it
/// sits next to the toggle that changes it rather than in a document nobody opens.
/// </summary>
public static class PrivacyCapability
{
    public const string Id = "privacy";

    public const string UpdateCheckKey = "updates.checkOnStartup";

    public static CapabilityDescriptor Create(SettingsService settings)
    {
        // Presence of the key, never its value — the disclosure has to distinguish "configured
        // and sending" from "selected but inert", and that is the only bit it needs.
        bool KeyPresent() =>
            LlmProviderCatalog.Selected(settings.Current.Llm.Provider) is { KeySecretName: { } name }
            && settings.HasSecret(name);

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Foundation",
            Name = "Privacy",
            Summary = "State exactly what D47 is sending off this machine right now, and to whom.",
            Examples =
            [
                "what are you sending",
                "what leaves this machine",
                "are you sending anything to the internet",
            ],
            Keywords =
            [
                "what are you sending",
                "what leaves this machine",
                "what data leaves",
                "data egress",
                "what do you send",
                "privacy report",
            ],
            // Last on the surface, below even Diagnostics. Not because it matters least — it is
            // the section that answers what leaves the machine — but because it is a section a
            // Commander goes to *deliberately* and reads, rather than one they pass through on
            // the way to something else. Sitting it between Headset and Acting on its own put a
            // page of reading in the middle of the rows people actually adjust.
            Display = new CapabilityDisplay { PanelTitle = "Privacy and egress", Order = 95 },
            Tools =
            [
                new ToolDefinition
                {
                    Name = "get_data_egress",
                    Description =
                        "List every destination D47 can send to, whether it is active with the current settings, "
                        + "and exactly what is sent there.",
                    Handler = (_, _) => Task.FromResult(
                        ToolResult.Ok(EgressDisclosure.Describe(settings.Current, KeyPresent()))),
                },
            ],
            Settings = BuildSettingRows(KeyPresent),
        };
    }

    private static IReadOnlyList<SettingRow> BuildSettingRows(Func<bool> keyPresent)
    {
        var rows = new List<SettingRow>
        {
            new()
            {
                Key = UpdateCheckKey,
                Label = "Check for updates at startup",
                Help = "One request to GitHub for the latest release tag. Off means D47 makes no network call of its own.",
                Kind = SettingKind.Toggle,
                DefaultDisplay = "on",
                DocsAnchor = "update-check",
                // Protected: this row decides whether anything leaves at all. A model that can
                // switch egress back on is a model that can be told to by untrusted text.
                Protected = true,
                Commands =
                [
                    new SettingCommandPhrase("stop checking for updates", "false"),
                    new SettingCommandPhrase("turn off update checks", "false"),
                    new SettingCommandPhrase("start checking for updates", "true"),
                    new SettingCommandPhrase("turn on update checks", "true"),
                ],
                Binding = new SettingBinding
                {
                    Read = s => s.Updates.CheckOnStartup ? "true" : "false",
                    Write = (s, v) => s with { Updates = s.Updates with { CheckOnStartup = v is not "false" } },
                },
            },
        };

        // One row per destination, declared once from the closed id set. The text each one
        // reads is computed at render time, so the card always describes right now.
        rows.AddRange(EgressDisclosure.Ids.Select(id => new SettingRow
        {
            Key = $"egress.{id}",
            Label = EgressDisclosure.NameOf(id),
            // The heading below says "read-only" once for all of them. Repeating it per row is
            // how a disclosure starts reading as boilerplate instead of as a statement.
            Help = string.Empty,
            Group = "What leaves this machine",
            GroupHelp =
                "Read-only, and computed from the settings as they stand right now — not a "
                + "description of what D47 could do in general.",
            Kind = SettingKind.Info,
            DocsAnchor = $"egress-{id}",
            Binding = new SettingBinding
            {
                Read = s =>
                {
                    var entry = EgressDisclosure.Entry(id, s, keyPresent());
                    return $"{entry.Line}\n{entry.What}";
                },
            },
        }));

        return rows;
    }
}
