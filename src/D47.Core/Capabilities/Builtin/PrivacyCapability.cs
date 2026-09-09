using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Diagnostics.Donation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What leaves this machine, stated where the settings that cause it live (Phase 4, "Say what each
/// provider receives").
/// </summary>
public static class PrivacyCapability
{
    public const string Id = "privacy";

    public const string UpdateCheckKey = "updates.checkOnStartup";

    /// <summary>The row carrying the one action that empties the memory store (Phase 31).</summary>
    public const string MemoryKey = "privacy.memory";

    /// <summary>The row carrying the wipe for retained audio (#164).</summary>
    public const string AudioRecordingKey = "privacy.audioFlight";

    /// <summary>Where a donation is posted (#175).</summary>

    /// <summary>
    /// The random per-installation identifier donations are grouped under, and the one press that
    /// forgets it (#176).
    /// </summary>
    public const string DonorKey = "privacy.donor";

    /// <summary>Throwing away what d47 worked out about the Commander from their journals (Phase 32).</summary>

    /// <summary>
    /// <param name="searchAvailable"> Whether the provider and model in use offer a server-side web
    /// search.
    /// </summary>
    /// <param name="searchAvailable">
    /// Whether the provider and model in use offer a server-side web search.
    /// </param>
    /// <param name="memories">What d47 remembers about the Commander (Phase 31).</param>
    /// <param name="recording">
    /// The audio recorder's record, or null in a process that was not asked to record — which is every
    /// ordinary run.
    /// </param>
    /// <param name="donorTokenFile">
    /// Where the donation identifier lives, or null where nothing composed a data folder — under the
    /// designer and in tests that are not about it.
    /// </param>
    /// <param name="forgetDonations">
    /// Withdrawal that reaches the store as well as this machine (#167), or null where nothing composed
    /// a network — the designer, and every test that is not about it.
    /// </param>
    public static CapabilityDescriptor Create(
        SettingsService settings,
        Func<bool>? searchAvailable = null,
        Memory.MemoryBook? memories = null,

        // Appended, like every optional here: the composition root passes these positionally, so a parameter
        // added in the middle silently rebinds every argument after it.
        Diagnostics.Recording.RecordingLog? recording = null,
        string? donorTokenFile = null,

        // Appended, like every optional here and for the reason the parameter above records: the composition
        // root passes these positionally.
        LongPress? forgetDonations = null)
    {
        var canSearch = searchAvailable ?? (() => true);

        // Presence of the key, never its value — the disclosure has to distinguish "configured and sending"
        // from "selected but inert", and that is the only bit it needs.
        bool KeyPresent() =>
            LlmProviderCatalog.Selected(settings.Current.Llm.Provider) is { KeySecretName: { } name }
            && settings.HasSecret(name);

        // Asked separately because it is the one destination decided by a secret alone: there is no setting
        // that turns Inara on, so the disclosure would read "nothing sent" forever if it only had settings to
        // go on.
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
            // Last on the surface, below even Diagnostics.
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
            Settings = BuildSettingRows(
                KeyPresent, InaraKeyPresent, canSearch, memories, recording, donorTokenFile, forgetDonations),
        };
    }

    private static IReadOnlyList<SettingRow> BuildSettingRows(
        Func<bool> keyPresent,
        Func<bool> inaraKeyPresent,
        Func<bool> searchAvailable,
        Memory.MemoryBook? memories,
        Diagnostics.Recording.RecordingLog? recording,
        string? donorTokenFile,
        LongPress? forgetDonations)
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
                // Protected: this row decides whether anything leaves at all.
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

        // Emptying the memory store, here rather than in its own section (Phase 31, item 3: "which joins the
        // existing privacy capability rather than inventing a second place to look").
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

        // Beside the memory row, and for the same reason it is there rather than in a section of its own: a
        // Commander who wants what d47 holds about them gone looks in one place, and an erase button
        // somewhere else is one they would find after the one they were looking for (#164).
        if (recording is not null)
        {
            rows.Add(new SettingRow
            {
                Key = AudioRecordingKey,
                Label = "Recorded audio",
                Help =
                    "What the audio recorder has kept of this recording: the utterances handed to the "
                    + $"transcriber, and what left the speakers. At most {Diagnostics.Recording.RecordingLog.CapBytes / (1024 * 1024)} MB "
                    + "is held, oldest dropped first, and nothing kept as a test case is dropped. It stays "
                    + "on this machine — it is never sent anywhere and never joins a donated excerpt, "
                    + "because voice is biometric. Deleting takes the kept test cases with it.",
                Kind = SettingKind.Info,

                // No DocsAnchor, like the coverage row it is a sibling of.
                PressLabel = "Delete every recording",
                Press = recording.Empty,
                Binding = new SettingBinding { Read = _ => recording.Summary() },
            });
        }

        // Donation.
        rows.Add(new SettingRow
        {
            Key = DonorKey,
            Advanced = true,
            Label = "Your donation identifier",
            Help = forgetDonations is null
                ? "A random number made on this machine the first time you donate, so a journal "
                  + "history you add to can be added to rather than piling up as unrelated blobs. "
                  + "It is not derived from your Commander name or anything else about you, and it "
                  + "is used for donations and nothing else. Forgetting it stops future donations "
                  + "joining the ones already sent — it does not reach back, and what has already "
                  + "gone has to be deleted at the store."

                // The withdrawal sentence (#167).
                : "A random number made on this machine the first time you donate, so a journal "
                  + "history you add to can be added to rather than piling up as unrelated blobs. "
                  + "It is not derived from your Commander name or anything else about you, and it "
                  + "is used for donations and nothing else. Forgetting it asks the store to delete "
                  + "every donation sent under it, and then forgets it here — you do not have to "
                  + "post anywhere or ask anybody. A record of what was deleted is written to "
                  + "data\\donations. What a donation was used for stays: a defect it found stays "
                  + "fixed, and a released build never moves.",
            Kind = SettingKind.Info,
            DocsAnchor = "donor-token",

            // Info with a Press, like the memory row above: SettingsService.Apply refuses that shape, so
            // nothing on the tool surface can reach it and it needs no protected flag of its own.
            PressLabel = donorTokenFile is null
                ? null
                : forgetDonations is null ? "Forget it" : "Forget it, and delete what was sent",
            Press = donorTokenFile is null || forgetDonations is not null
                ? null
                : () => DonorToken.Forget(donorTokenFile),
            PressAsync = donorTokenFile is null ? null : forgetDonations,
            Binding = new SettingBinding
            {
                Read = _ => donorTokenFile is null
                    ? "No donation identifier exists on this installation. One is created the "
                      + "first time you donate, and never before."
                    : DonorToken.Summarise(DonorToken.Read(donorTokenFile)),
            },
        });

        // Beside the memory row, because "forget me" means both halves of what d47 knows about a person and a
        // Commander who found only one of them would reasonably assume they were done.
        rows.AddRange(EgressDisclosure.Ids.Select(id => new SettingRow
        {
            Key = $"egress.{id}",
            Advanced = true,
            Label = EgressDisclosure.NameOf(id),
            // The heading below says "read-only" once for all of them.
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
