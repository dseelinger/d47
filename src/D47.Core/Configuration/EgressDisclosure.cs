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

/// <summary>What is leaving this machine, as of the current settings.</summary>
public static class EgressDisclosure
{
    public const string LanguageModel = "llm";

    public const string UpdateCheck = "updates";

    public const string Diagnostics = "diagnostics";

    public const string JournalFiles = "journal";

    /// <summary>Fetching a speech model.</summary>
    public const string SpeechModels = "models";

    /// <summary>Sending a donated incident excerpt or journal history (#175).</summary>
    public const string Donation = "donation";

    /// <summary>Synthesising a spoken line.</summary>
    public const string TextToSpeech = "tts";

    /// <summary>Looking something up in the galaxy.</summary>
    public const string GalaxySearch = "galaxy";

    /// <summary>Listing community goals running where the Commander has not been.</summary>
    public const string CommunityGoals = "communitygoals";

    /// <summary>Searching the web, which the language-model provider does on d47's behalf.</summary>
    public const string WebSearch = "websearch";

    /// <summary>Fetching the catalogue of notable places a generated adventure may draw on (Phase 47).</summary>
    public const string NotablePlaces = "notableplaces";

    /// <summary>
    /// Two hosts, because there are two transfers: the check asks api.github.com for a tag, and
    /// accepting an update fetches the build from github.com — which redirects to GitHub's asset
    /// storage, so the bytes land from objects.githubusercontent.com.
    /// </summary>
    public const string GitHubReleasesEndpoint =
        "api.github.com, and github.com if you accept an update";

    /// <summary>Fetching a hull's large art — the 4K picture and the turntable (#289).</summary>
    public const string HullArt = "hullart";

    /// <summary>Every disclosure d47 makes, in a fixed order.</summary>
    public static IReadOnlyList<string> Ids { get; } =
    [
        LanguageModel,
        WebSearch,
        TextToSpeech,
        GalaxySearch,
        NotablePlaces,
        CommunityGoals,
        UpdateCheck,
        HullArt,
        SpeechModels,
        Diagnostics,
        JournalFiles,
        Donation,
    ];

    /// <summary>The heading for a disclosure.</summary>
    public static string NameOf(string id) => id switch
    {
        LanguageModel => "Language model",
        UpdateCheck => "Update check",
        TextToSpeech => "Spoken replies",
        GalaxySearch => "Galaxy search",
        NotablePlaces => "Notable places",
        CommunityGoals => "Community goals",
        WebSearch => "Web search",
        SpeechModels => "Speech model download",
        HullArt => "Hull pictures",
        Diagnostics => "Diagnostics and logs",
        JournalFiles => "Journal files",
        // "Shared", not "Donated" (#239): the Commander's word for the act on every surface they see.
        Donation => "Shared excerpts and journals",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not an egress disclosure id."),
    };

    /// <summary><param name="inaraKeyPresent"> Whether the Commander has stored an Inara key.</summary>
    /// <param name="inaraKeyPresent">Whether the Commander has stored an Inara key.</param>
    /// <param name="searchAvailable">
    /// Whether the provider and model in use offer a server-side web search — what
    /// <c>LlmProviderCapabilities.SupportsWebSearch</c> says.
    /// </param>
    public static EgressEntry Entry(
        string id,
        D47Settings settings,
        bool llmKeyPresent,
        bool inaraKeyPresent = false,
        bool searchAvailable = true) => id switch
    {
        LanguageModel => LanguageModelEntry(settings, llmKeyPresent),
        WebSearch => WebSearchEntry(settings, llmKeyPresent, searchAvailable),
        TextToSpeech => TextToSpeechEntry(settings),

        GalaxySearch => settings.Knowledge.GalaxySearch
            ? new EgressEntry(
                GalaxySearch,
                NameOf(GalaxySearch),
                "spansh.co.uk and api.ardent-insight.com",
                "System names you ask about, and the filters of a search — an allegiance, a distance, an "
                + "economy, a module you want to buy, a body type or a mining material. Where you are goes "
                + "with it whenever a question is relative to you, because "
                + "\"the nearest high tech system\" cannot be asked without saying where from. Plotting a "
                + "route also sends your ship's jump range. A trade route sends the system you are in and "
                + "how far to look, and nothing else: the prices come back and the planning happens here, "
                + "so how much your hold carries and the figure you gave to trade with never leave this "
                + "machine — and neither does your actual balance, which is never read. Asking where to "
                + "buy or sell one named commodity goes to the second host instead, and sends the same "
                + "three things: the commodity, the system to search out from, and how far to look. No "
                + "key, no identifier, and nothing else from your journal.",
                Active: true)
            : EgressEntry.Silent(
                GalaxySearch,
                NameOf(GalaxySearch),
                "Galaxy search is off, so no system name and no search leaves this machine."),
        NotablePlaces => settings.Knowledge.NotablePlaces
            ? new EgressEntry(
                NotablePlaces,
                NameOf(NotablePlaces),
                "edastro.com",
                "One request for the whole Galactic Exploration Catalog — about two megabytes, every point "
                + "of interest it lists — made when you ask for an adventure to be written. Nothing about you "
                + "goes with it: no position, no key, no identifier and nothing from your journal. The "
                + "choosing by distance happens on this machine. The catalogue's descriptions are read as "
                + "information and never written into D47's own tables; its content is CC BY-NC-SA 3.0, "
                + "acknowledged in NOTICE.",
                Active: true)
            : EgressEntry.Silent(
                NotablePlaces,
                NameOf(NotablePlaces),
                "Notable places are off, so no catalogue is fetched and a generated adventure chooses its "
                + "stops from the galaxy search alone."),

        // The key is the switch, and deliberately the only one.
        CommunityGoals => inaraKeyPresent
            ? new EgressEntry(
                CommunityGoals,
                NameOf(CommunityGoals),
                "inara.cz",
                "Your Inara API key, and nothing else. The request asks for the current community goals and "
                + "says nothing about you — not your Commander name, not your Frontier ID, not where you are "
                + "and nothing from your journal. What comes back is treated as information rather than as "
                + "an instruction, and the goal descriptions are left there rather than read.",
                Active: true)
            : EgressEntry.Silent(
                CommunityGoals,
                NameOf(CommunityGoals),
                "No Inara API key is stored, so nothing is requested and community goals are read only from "
                + "your own journal."),

        HullArt => settings.Ui.HullArt
            ? new EgressEntry(
                HullArt,
                NameOf(HullArt),
                GitHubReleasesEndpoint,
                "The hull symbol of a ship you open, when D47 has no large picture of it yet - one request "
                + "for a picture and one for a turntable, kept on disk so each hull is asked for once. "
                + "Nothing else goes with it: no key, no Commander name, no position and nothing from your "
                + "journal. Which hulls you own is not sent, and the small picture on each card came with "
                + "the build and is never fetched.",
                Active: true)
            : EgressEntry.Silent(
                HullArt,
                NameOf(HullArt),
                "Hull pictures are off, so nothing is fetched and every ship keeps the small drawing that "
                + "came with the build."),

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

        // On demand.
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
                + "downloads it from this host — once, and only the model file. "
                + "Nothing about you goes with the request — no audio, no transcript, no key, no identifier. "
                + "Once downloaded, transcription runs entirely on this machine.",
                Active: true),

        Diagnostics => EgressEntry.Silent(
            Diagnostics,
            NameOf(Diagnostics),
            "Logs are written beside the executable and are not uploaded."),

        JournalFiles => EgressEntry.Silent(
            JournalFiles,
            NameOf(JournalFiles),
            "Your journal is read from disk and never uploaded. Facts drawn from it — system, body, station — "
            + "can reach the model as game state when one is configured; see the language model row."),

        Donation => DonationEntry(),

        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not an egress disclosure id."),
    };

    /// <summary>What a donation sends, and where.</summary>
    private static EgressEntry DonationEntry()
    {
        return new EgressEntry(
            Donation,
            NameOf(Donation),
            DonationSettings.Address,
            "Nothing is sent unless you press send, every time — there is no standing consent, no "
            + "remembered choice and no automatic upload. What goes when you do press it is exactly "
            + "the scrubbed text the window showed you: names and Frontier IDs already replaced, "
            + "other players' in-game messages already dropped. A random identifier for this "
            + "installation goes with it, so a journal history you add to can be added to rather "
            + "than piling up as unrelated blobs — it is made on this machine, is not derived from "
            + "your Commander name or anything else about you, and deleting "
            + $"{AppPaths.DataFolderName}\\donor-token.txt ends that grouping. d47 writes its own "
            + "copy of every donation beside the executable, with the hash of what it sent, so you "
            + "can check that what was shown is what left.",
            Active: true);
    }

    /// <summary>What a web search sends, and where.</summary>
    private static EgressEntry WebSearchEntry(D47Settings settings, bool keyPresent, bool available)
    {
        var provider = LlmProviderCatalog.Selected(settings.Llm.Provider);
        // A provider whose key is optional is usable with an empty box, which is the change Phase 29 made —
        // and NeedsKey now says so, so this test needed no editing to become right.
        var usable = provider.Id != LlmProviderCatalog.NoneId && (!provider.NeedsKey || keyPresent);

        if (!settings.Llm.WebSearch)
        {
            return EgressEntry.Silent(
                WebSearch,
                NameOf(WebSearch),
                "Web search is off, so D47 never asks the model to look anything up online.");
        }

        if (!usable)
        {
            return EgressEntry.Silent(
                WebSearch,
                NameOf(WebSearch),
                "Web search is on, but no language model is usable, so no turn runs and no search "
                + "is made.");
        }

        // The fourth state: selected, keyed, and no provider to reach.
        if (!available)
        {
            return EgressEntry.Silent(
                WebSearch,
                NameOf(WebSearch),
                "Web search is on, but the endpoint D47 is pointed at offers no search, so none is "
                + "made. A server-side search is the provider's to offer, not something D47 can do "
                + "itself.");
        }

        return new EgressEntry(
            WebSearch,
            NameOf(WebSearch),
            settings.Llm.Endpoint ?? provider.DefaultEndpoint ?? provider.Name,
            $"D47 does not search the web itself. When a question needs current information — and, "
            + "if the lore remark is set to look things up, when you arrive in a system D47 knows "
            + $"something about — {provider.Name} runs the search and reads the pages, and D47 only "
            + "ever sees the reply. That means no new destination beyond the one above — but it does "
            + "mean the wording of the search, which is drawn from what you asked or from the name of "
            + "the system you just jumped into, and whatever those pages say comes back into the "
            + "conversation. Anything read there is treated as information, never as an instruction, "
            + "and is never written into D47's own tables. Searches are billed by the provider on top "
            + "of the turn, at about a penny each.",
            Active: true);
    }

    /// <summary>
    /// What the voice providers receive — a table since Phase 57, because one sentence can no longer be
    /// true.
    /// </summary>
    private static EgressEntry TextToSpeechEntry(D47Settings settings)
    {
        var speaking = Audio.VoiceGroups.All
            .Select(slot => (Slot: slot, Provider: Audio.TtsProviderCatalog.Selected(
                Audio.VoiceGroups.ProviderFor(settings.Speech, slot.Group))))
            .Where(pair => pair.Provider.Speaks)
            .ToList();

        if (speaking.Count == 0)
        {
            return EgressEntry.Silent(
                TextToSpeech,
                NameOf(TextToSpeech),
                Audio.TtsProviderCatalog.None.Egress);
        }

        var others = speaking.Where(pair => pair.Slot.OtherPeoplesWords).ToList();

        var what = new System.Text.StringBuilder();

        // The legible sentence first.
        what.Append(others.Count == 0
            ? "No other player's words are being sent anywhere: every slot that could carry them "
              + "is silent. "
            : $"Another player's words are sent to {Destinations(others.Select(pair => pair.Provider))}"
              + " to be spoken aloud. ");

        what.Append("Line by line: ");
        what.AppendJoin(
            "; ",
            speaking.Select(pair => $"{pair.Slot.Name} ({pair.Slot.Covers}) → {pair.Provider.Name}"));
        what.Append(". ");

        // Each service's own disclosure, once, whichever slots reached it.
        what.AppendJoin(
            " ",
            speaking
                .Select(pair => pair.Provider)
                .DistinctBy(provider => provider.Id)
                .Select(provider => provider.Egress));

        return new EgressEntry(
            TextToSpeech,
            NameOf(TextToSpeech),
            Destinations(speaking.Select(pair => pair.Provider)),
            what.ToString(),
            Active: true);
    }

    /// <summary>Where the bytes actually go, each host once, in the order the slots run.</summary>
    private static string Destinations(IEnumerable<Audio.TtsProviderInfo> providers) =>
        string.Join(
            ", ",
            providers.Select(provider => provider.Destination).Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>What choosing one provider for one slot causes to leave (Phase 57).</summary>
    public static EgressEntry TextToSpeechForSlot(Audio.VoiceGroupInfo slot, Audio.TtsProviderInfo provider)
    {
        var what = $"{slot.Name} — {slot.Covers} — is spoken by {provider.Name}. {provider.Egress}";

        if (slot.OtherPeoplesWords && provider.Billed)
        {
            what += " This slot carries text written by other players, so choosing a paid "
                    + "provider for it means being billed per character for words somebody else "
                    + "typed, in whatever volume they choose to type them.";
        }

        return provider.Speaks
            ? new EgressEntry(TextToSpeech, NameOf(TextToSpeech), provider.Destination, what, Active: true)
            : EgressEntry.Silent(TextToSpeech, NameOf(TextToSpeech), what);
    }

    /// <summary>What one named voice provider receives, whether or not it is the one selected.</summary>
    public static EgressEntry TextToSpeechFor(Audio.TtsProviderInfo provider) =>
        provider.Speaks
            ? new EgressEntry(TextToSpeech, NameOf(TextToSpeech), provider.Destination, provider.Egress, Active: true)
            : EgressEntry.Silent(TextToSpeech, NameOf(TextToSpeech), provider.Egress);

    public static IReadOnlyList<EgressEntry> For(
        D47Settings settings,
        bool llmKeyPresent,
        bool inaraKeyPresent = false,
        bool searchAvailable = true) =>
        [.. Ids.Select(id => Entry(id, settings, llmKeyPresent, inaraKeyPresent, searchAvailable))];

    /// <summary>The same disclosure as prose, for the tool result and the spoken path.</summary>
    public static string Describe(
        D47Settings settings,
        bool llmKeyPresent,
        bool inaraKeyPresent = false,
        bool searchAvailable = true)
    {
        var entries = For(settings, llmKeyPresent, inaraKeyPresent, searchAvailable);
        var active = entries.Count(e => e.Active);

        var report = new System.Text.StringBuilder();

        // One sentence shape for every case, including none.
        report.AppendLine($"{active} of {entries.Count} destinations are active right now.");

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

        var destination = settings.Llm.Endpoint ?? provider.DefaultEndpoint ?? provider.Name;

        // The first time in d47's life that the honest answer to *what is leaving* is *nothing* (Phase 29).
        if (LocalEndpoint.IsLoopback(destination))
        {
            return EgressEntry.Silent(
                LanguageModel,
                NameOf(LanguageModel),
                $"{provider.Name} is selected and pointed at {destination}, which is this machine. Your question, "
                + "the reply, the persona, what D47 remembers about you and the game state D47 assembled from your "
                + "journal all go to that address and no further — nothing leaves this machine, and no account or "
                + "key is involved.");
        }

        return new EgressEntry(
            LanguageModel,
            NameOf(LanguageModel),
            destination,
            $"{provider.Name} is selected. {provider.Egress}",
            Active: true);
    }
}
