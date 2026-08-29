using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What leaves this machine, stated where the settings that cause it live (Phase 4,
/// "Say what each provider receives"). The disclosure rows are read-only: they are not
/// something the Commander sets, they are something d47 says, and saying it as a row means it
/// sits next to the toggle that changes it rather than in a document nobody opens.
/// </summary>
public static class PrivacyCapability
{
    public const string Id = "privacy";

    public const string UpdateCheckKey = "updates.checkOnStartup";

    /// <summary>The row carrying the one action that empties the memory store (Phase 31).</summary>
    public const string MemoryKey = "privacy.memory";

    /// <summary>
    /// The row carrying the wipe for retained audio
    /// (<a href="https://github.com/dseelinger/d47/issues/164">#164</a>). Registered only in a
    /// process that was asked to record, so on an ordinary run the recorder is absent from the
    /// surface rather than present and empty.
    /// </summary>
    public const string AudioFlightKey = "privacy.audioFlight";

    /// <summary>
    /// Throwing away what d47 worked out about the Commander from their journals (Phase 32).
    /// Beside the memory row, because a Commander wanting to be forgotten means both.
    /// </summary>

    /// <param name="searchAvailable">
    /// Whether the provider and model in use offer a server-side web search. Null is "assume it
    /// can", which is what a caller with no provider to ask is entitled to say — every test here
    /// is one, and so is first run. <b>A caller that has a provider must supply it</b>, or the
    /// web-search row describes searches at an endpoint that will never make one.
    /// </param>
    /// <param name="memories">
    /// What d47 remembers about the Commander (Phase 31). Null where nothing composed a
    /// store — under the designer and in tests that are not about it — and the row then says so
    /// rather than offering a button that erases nothing.
    /// </param>
    /// <param name="flight">
    /// The audio flight recorder's record, or null in a process that was not asked to record —
    /// which is every ordinary run. Null leaves the row out entirely rather than showing one
    /// that says nothing has been recorded, because a Commander who never turned this on should
    /// not have to read that d47 could have.
    /// </param>
    public static CapabilityDescriptor Create(
        SettingsService settings,
        Func<bool>? searchAvailable = null,
        Memory.MemoryBook? memories = null,

        // Appended, like every optional here: the composition root passes these positionally, so
        // a parameter added in the middle silently rebinds every argument after it.
        Diagnostics.Flight.FlightLog? flight = null)
    {
        var canSearch = searchAvailable ?? (() => true);

        // Presence of the key, never its value — the disclosure has to distinguish "configured
        // and sending" from "selected but inert", and that is the only bit it needs.
        bool KeyPresent() =>
            LlmProviderCatalog.Selected(settings.Current.Llm.Provider) is { KeySecretName: { } name }
            && settings.HasSecret(name);

        // Asked separately because it is the one destination decided by a secret alone: there is
        // no setting that turns Inara on, so the disclosure would read "nothing sent" forever if
        // it only had settings to go on.
        bool InaraKeyPresent() => settings.HasSecret(CommunityGoalCapability.KeySecretName);

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
                        ToolResult.Ok(EgressDisclosure.Describe(
                            settings.Current, KeyPresent(), InaraKeyPresent(), canSearch()))),
                },
            ],
            Settings = BuildSettingRows(KeyPresent, InaraKeyPresent, canSearch, memories, flight),
        };
    }

    private static IReadOnlyList<SettingRow> BuildSettingRows(
        Func<bool> keyPresent,
        Func<bool> inaraKeyPresent,
        Func<bool> searchAvailable,
        Memory.MemoryBook? memories,
        Diagnostics.Flight.FlightLog? flight)
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

        // Emptying the memory store, here rather than in its own section (Phase 31, item 3:
        // "which joins the existing privacy capability rather than inventing a second place to
        // look"). A Commander who wants d47 to forget them looks for that where the rest of "what
        // does this thing know" lives, and a second erase button somewhere else is one they would
        // find after the one they were looking for.
        //
        // Info with a Press, so SettingsService.Apply refuses it and nothing on the tool surface can
        // reach it — the same mechanism the introductions button uses, and the reason emptying needs
        // no protected flag of its own. It also has no router phrase: "forget everything about me"
        // is a sentence a transcriber can produce out of a misheard one, and this is the only
        // action in the phase that cannot be undone.
        rows.Add(new SettingRow
        {
            Key = MemoryKey,
            Label = "What D47 remembers about you",
            Help =
                "Facts D47 has kept between sessions — what you have told it, what it noticed in your "
                + "journal, and what it worked out for itself. Emptying is immediate and covers every "
                + "Commander in the file, not just the one currently aboard.",
            Kind = SettingKind.Info,
            DocsAnchor = "memory",
            PressLabel = memories is null ? null : "Forget everything",
            Press = memories is null ? null : () => memories.Store.Empty(),
            Binding = new SettingBinding
            {
                Read = _ => MemoryCapability.Summarise(memories),
            },
        });

        // Beside the memory row, and for the same reason it is there rather than in a section of
        // its own: a Commander who wants what d47 holds about them gone looks in one place, and
        // an erase button somewhere else is one they would find after the one they were looking
        // for (#164).
        //
        // Info with a Press, like the memory row above — so SettingsService.Apply refuses it and
        // nothing on the tool surface can reach it. No router phrase either, for the reason that
        // row has none: this cannot be undone, and a transcriber can produce the sentence that
        // would trigger it out of a misheard one.
        //
        // Registered only where something is recording. A run that was not asked to record has
        // no row here at all, which is the whole of what "absent from the surface unless enabled"
        // means.
        if (flight is not null)
        {
            rows.Add(new SettingRow
            {
                Key = AudioFlightKey,
                Label = "Recorded audio",
                Help =
                    "What the flight recorder has kept of this flight: the utterances handed to the "
                    + $"transcriber, and what left the speakers. At most {Diagnostics.Flight.FlightLog.CapBytes / (1024 * 1024)} MB "
                    + "is held, oldest dropped first, and nothing kept as a test case is dropped. It stays "
                    + "on this machine — it is never sent anywhere and never joins a donated excerpt, "
                    + "because voice is biometric. Deleting takes the kept test cases with it.",
                Kind = SettingKind.Info,

                // No DocsAnchor, like the coverage row it is a sibling of. This is a workbench
                // aid rather than something a Commander configures, so it gets no section in the
                // public capability page.
                PressLabel = "Delete every recording",
                Press = flight.Empty,
                Binding = new SettingBinding { Read = _ => flight.Summary() },
            });
        }

        // Beside the memory row, because "forget me" means both halves of what d47 knows about a
        // person and a Commander who found only one of them would reasonably assume they were done.
        // One row per destination, declared once from the closed id set. The text each one
        // reads is computed at render time, so the card always describes right now.
        rows.AddRange(EgressDisclosure.Ids.Select(id => new SettingRow
        {
            Key = $"egress.{id}",
            Advanced = true,
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
                    var entry = EgressDisclosure.Entry(
                        id, s, keyPresent(), inaraKeyPresent(), searchAvailable());
                    return $"{entry.Line}\n{entry.What}";
                },
            },
        }));

        return rows;
    }
}
