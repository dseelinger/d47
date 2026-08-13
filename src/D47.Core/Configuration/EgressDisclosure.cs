using D47.Core.Conversation;

namespace D47.Core.Configuration;

/// <summary>One destination d47 can send to, and what it sends there.</summary>
public sealed record EgressEntry(string Id, string Name, string Destination, string What, bool Active)
{
    /// <summary>A destination that is configured off, so nothing goes there right now.</summary>
    public static EgressEntry Silent(string id, string name, string what) =>
        new(id, name, "nothing sent", what, Active: false);

    public string Line => Active ? $"{Name} → {Destination}" : $"{Name} → nothing sent";
}

/// <summary>
/// What is leaving this machine, as of the current settings. Answerable at any time rather
/// than documented once and hoped for: the settings surface renders it, a tool reports it
/// aloud, and both read this (list.md Phase 4, "Say what each provider receives").
/// <para>
/// The entry <em>ids</em> are a closed set and the entry <em>contents</em> are computed from
/// settings. That split is what lets the disclosure rows be declared once and never mutated
/// like every other settings row, while still saying something true about right now.
/// </para>
/// <para>
/// The set is exhaustive by construction — every network destination d47 has belongs to an
/// entry here, and a phase that adds one adds its entry alongside. Local-only operation is
/// what you get when every entry reads inactive.
/// </para>
/// </summary>
public static class EgressDisclosure
{
    public const string LanguageModel = "llm";

    public const string UpdateCheck = "updates";

    public const string Diagnostics = "diagnostics";

    public const string JournalFiles = "journal";

    /// <summary>Fetching a speech model, which is the only transfer d47 makes on request.</summary>
    public const string SpeechModels = "models";

    /// <summary>
    /// Two hosts, because there are two transfers: the check asks api.github.com for a tag, and
    /// accepting an update fetches the build from github.com — which redirects to GitHub's asset
    /// storage, so the bytes land from objects.githubusercontent.com. Named in full rather than
    /// summarised as "GitHub": a disclosure that hides a host behind a brand is not a disclosure.
    /// </summary>
    public const string GitHubReleasesEndpoint =
        "api.github.com, and github.com if you accept an update";

    /// <summary>Every disclosure d47 makes, in a fixed order. Ids are stable; text is live.</summary>
    public static IReadOnlyList<string> Ids { get; } =
        [LanguageModel, UpdateCheck, SpeechModels, Diagnostics, JournalFiles];

    /// <summary>
    /// The heading for a disclosure. Fixed, because it labels a settings row and rows are
    /// declared once and never mutated — which provider is selected belongs in the text below
    /// the heading, where it can change without the row changing.
    /// </summary>
    public static string NameOf(string id) => id switch
    {
        LanguageModel => "Language model",
        UpdateCheck => "Update check",
        SpeechModels => "Speech model download",
        Diagnostics => "Diagnostics and logs",
        JournalFiles => "Journal files",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not an egress disclosure id."),
    };

    public static EgressEntry Entry(string id, D47Settings settings, bool llmKeyPresent) => id switch
    {
        LanguageModel => LanguageModelEntry(settings, llmKeyPresent),
        UpdateCheck => settings.Updates.CheckOnStartup
            ? new EgressEntry(
                UpdateCheck,
                NameOf(UpdateCheck),
                GitHubReleasesEndpoint,
                "One request for the latest release tag at startup. Nothing about you goes with it — no key, "
                + "no journal content, and no identifier beyond the request itself. Accepting an offered "
                + "update downloads that release from github.com and replaces D47 with it; nothing is "
                + "downloaded unless you ask for it.",
                Active: true)
            : EgressEntry.Silent(
                UpdateCheck, NameOf(UpdateCheck), "The startup update check is off, so nothing is requested."),

        // On demand and only with consent. The row reads active when a model is selected,
        // because that is the setting that can cause a transfer — not because one is happening
        // right now. A disclosure that only lit up mid-download would tell the Commander
        // nothing they could act on beforehand.
        SpeechModels => settings.Listening.Model == Listening.WhisperModels.NoneId
            ? EgressEntry.Silent(
                SpeechModels,
                NameOf(SpeechModels),
                "No speech model is selected, so nothing is downloaded and no request is made.")
            : new EgressEntry(
                SpeechModels,
                NameOf(SpeechModels),
                Listening.WhisperModels.Host,
                $"The {settings.Listening.Model} speech model is selected. If it is not already on disk, D47 "
                + "asks the host how big it is, shows you that figure, and downloads nothing until you agree. "
                + "Nothing about you goes with the request — no audio, no transcript, no key, no identifier. "
                + "Once downloaded, transcription runs entirely on this machine.",
                Active: true),

        // Stated rather than omitted. "No telemetry" is a claim worth being able to point at,
        // and a disclosure listing only what does send invites the question of what else exists.
        Diagnostics => EgressEntry.Silent(
            Diagnostics,
            NameOf(Diagnostics),
            "Logs are written beside the executable and never uploaded. There is no analytics endpoint, "
            + "no metrics endpoint and no crash reporter."),

        JournalFiles => EgressEntry.Silent(
            JournalFiles,
            NameOf(JournalFiles),
            "Your journal is read from disk and never uploaded. Facts drawn from it — system, body, station — "
            + "can reach the model as game state when one is configured; see the language model row."),

        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not an egress disclosure id."),
    };

    public static IReadOnlyList<EgressEntry> For(D47Settings settings, bool llmKeyPresent) =>
        [.. Ids.Select(id => Entry(id, settings, llmKeyPresent))];

    /// <summary>The same disclosure as prose, for the tool result and the spoken path.</summary>
    public static string Describe(D47Settings settings, bool llmKeyPresent)
    {
        var entries = For(settings, llmKeyPresent);
        var active = entries.Count(e => e.Active);

        var report = new System.Text.StringBuilder();

        report.AppendLine(active == 0
            ? "Nothing is leaving this machine right now."
            : $"{active} of {entries.Count} destinations are active right now.");

        foreach (var entry in entries)
        {
            report.AppendLine();
            report.AppendLine(entry.Line);
            report.AppendLine($"  {entry.What}");
        }

        return report.ToString().TrimEnd();
    }

    private static EgressEntry LanguageModelEntry(D47Settings settings, bool keyPresent)
    {
        var provider = LlmProviderCatalog.Selected(settings.Llm.Provider);

        if (provider.Id == LlmProviderCatalog.NoneId)
        {
            return EgressEntry.Silent(
                LanguageModel,
                NameOf(LanguageModel),
                "No provider is selected, so no turn text and no game state is sent anywhere.");
        }

        if (provider.NeedsKey && !keyPresent)
        {
            return EgressEntry.Silent(
                LanguageModel,
                NameOf(LanguageModel),
                $"{provider.Name} is selected but has no key stored, so no turn reaches it and nothing is sent.");
        }

        return new EgressEntry(
            LanguageModel,
            NameOf(LanguageModel),
            settings.Llm.Endpoint ?? provider.DefaultEndpoint ?? provider.Name,
            $"{provider.Name} is selected. {provider.Egress}",
            Active: true);
    }
}
