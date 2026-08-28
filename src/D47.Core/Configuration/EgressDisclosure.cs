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
/// aloud, and both read this (Phase 4, "Say what each provider receives").
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
    /// Synthesising a spoken line. Added in Phase 11 alongside the second provider, and it
    /// should have been here from Phase 5 — every voice provider d47 has is a network service,
    /// so a set that claimed to be exhaustive while omitting the one that sends every word D47
    /// says was not exhaustive. architecture.md §7 names it and Phase 4 asks for it by
    /// name ("reply text to a paid TTS").
    /// </summary>
    public const string TextToSpeech = "tts";

    /// <summary>
    /// Looking something up in the galaxy. Added in Phase 14 with the first capability whose
    /// answers come from off this machine.
    /// </summary>
    public const string GalaxySearch = "galaxy";

    /// <summary>
    /// Listing community goals running where the Commander has not been. Added in Phase 14 with
    /// the item that needs it, and the only destination d47 reaches with a credential of the
    /// Commander's own rather than one they bought from a provider.
    /// </summary>
    public const string CommunityGoals = "communitygoals";

    /// <summary>
    /// Searching the web, which the language-model provider does on d47's behalf.
    /// <para>
    /// A separate entry even though it adds no new host, and that is the point worth making
    /// rather than eliding: the destination is the same endpoint the language-model row already
    /// names, but what happens there is different in kind. Folding it into that row would let a
    /// Commander read "the model gets your question" and not learn that the model can now go and
    /// fetch arbitrary pages about it. A disclosure organised by host rather than by what is
    /// actually done is a disclosure that hides things behind an address.
    /// </para>
    /// </summary>
    public const string WebSearch = "websearch";

    /// <summary>
    /// Fetching the catalogue of notable places a generated adventure may draw on (Phase 47). A
    /// different host from the galaxy search, so its own entry — the disclosure lists
    /// destinations by where the bytes go.
    /// </summary>
    public const string NotablePlaces = "notableplaces";

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
    [
        LanguageModel,
        WebSearch,
        TextToSpeech,
        GalaxySearch,
        NotablePlaces,
        CommunityGoals,
        UpdateCheck,
        SpeechModels,
        Diagnostics,
        JournalFiles,
    ];

    /// <summary>
    /// The heading for a disclosure. Fixed, because it labels a settings row and rows are
    /// declared once and never mutated — which provider is selected belongs in the text below
    /// the heading, where it can change without the row changing.
    /// </summary>
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
        Diagnostics => "Diagnostics and logs",
        JournalFiles => "Journal files",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not an egress disclosure id."),
    };

    /// <param name="inaraKeyPresent">
    /// Whether the Commander has stored an Inara key. Optional and last, because it is the only
    /// destination whose row is decided by a secret rather than by a setting — a caller that has
    /// no secret store to ask reports it silent, which is what a machine with no key is.
    /// </param>
    /// <param name="searchAvailable">
    /// Whether the provider and model in use offer a server-side web search — what
    /// <c>LlmProviderCapabilities.SupportsWebSearch</c> says. Optional and last, and defaulting
    /// to <c>true</c>, because a caller with no provider to ask is describing what the settings
    /// <em>would</em> cause rather than what this endpoint can do, and the default endpoint can
    /// search. A caller that has a provider must pass the real answer: without it this row says
    /// searches are being made at a gateway that will never make one.
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
                "spansh.co.uk",
                "System names you ask about, and the filters of a search — an allegiance, a distance, an "
                + "economy, a module you want to buy, a body type or a mining material. Where you are goes "
                + "with it whenever a question is relative to you, because "
                + "\"the nearest high tech system\" cannot be asked without saying where from. Plotting a "
                + "route also sends your ship's jump range. A trade route sends the system you are in and "
                + "how far to look, and nothing else: the prices come back and the planning happens here, "
                + "so how much your hold carries and the figure you gave to trade with never leave this "
                + "machine — and neither does your actual balance, which is never read. No key, no "
                + "identifier, and nothing else from your journal.",
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

        // The key is the switch, and deliberately the only one. Every other third-party
        // destination needs a toggle because it could otherwise be reached by a fresh install
        // that never asked for it; this one cannot be reached at all until somebody pastes in a
        // credential, which is a clearer act of consent than a checkbox. Clearing the key is how
        // it is turned off, and the capability still answers from the journal either way.
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

        // On demand. The row reads active when a model is selected,
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
                + "downloads it from this host — once, and only the model file. "
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

    /// <summary>
    /// What a web search sends, and where.
    /// <para>
    /// Three conditions, all of which have to hold before anything can happen: a provider that
    /// is not "none", a key for it, and the setting on. Reporting the setting alone would say
    /// "active" on a machine with no key, where no turn runs and therefore no search can.
    /// </para>
    /// </summary>
    private static EgressEntry WebSearchEntry(D47Settings settings, bool keyPresent, bool available)
    {
        var provider = LlmProviderCatalog.Selected(settings.Llm.Provider);
        // A provider whose key is optional is usable with an empty box, which is the change
        // Phase 29 made — and NeedsKey now says so, so this test needed no editing to
        // become right. It is spelled out because it silently changed meaning.
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

        // The fourth state, and the one this row used to get wrong. A server-side search is the
        // provider's to offer, so pointing `llm.endpoint` at a gateway turns it off whatever the
        // setting says — and until this branch existed the row went on to describe searches being
        // made, pages being read and a penny being billed, none of which could happen. Claiming
        // egress that does not occur is the safe direction to be wrong in, but it is still wrong,
        // and Phase 4's rule is that a disclosure which summarises is a disclosure that omits.
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
    /// What the voice providers receive — a table since Phase 57, because one sentence can no
    /// longer be true.
    /// <para>
    /// Phase 4 requires stating exactly what leaves for the selected provider, and there is no
    /// longer a selected provider: there are six slots and up to three services. So the entry
    /// leads with the one thing a Commander most needs to know — whether anybody else's words are
    /// leaving, and to whom — and then lists the slots that are actually going somewhere.
    /// </para>
    /// <para>
    /// <b>Free is not the same as private, and the wording must not let it read that way.</b>
    /// Edge costs nothing and still sends every line to <c>speech.platform.bing.com</c>. That is
    /// the mistake a Commander would make unaided, and the whole reason the cost half and the
    /// egress half of Phase 57 are two different sentences.
    /// </para>
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

        // The legible sentence first. Four rows a Commander has to combine is not a disclosure of
        // the thing they care about, which is whether another player's text is leaving and where
        // it is going.
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

        // Each service's own disclosure, once, whichever slots reached it. Two slots on ElevenLabs
        // do not send it two different things.
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

    /// <summary>
    /// What choosing one provider for <em>one slot</em> causes to leave (Phase 57).
    /// <para>
    /// The slot row's own question, and it is not the one the privacy section asks. A Commander
    /// on that row is deciding whether a particular set of voices should go to a particular
    /// service, and the two facts that decide it are what the service does with the text and
    /// whose text it is.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// What <em>one named</em> voice provider receives, whether or not it is the one selected.
    /// <para>
    /// The selection is the right question almost everywhere: the privacy section is asking what
    /// is leaving right now, and the answer to that is about whoever is configured. It is the
    /// wrong question on a provider's own key row, where the Commander is deciding whether to
    /// hand <em>that</em> provider a credential. Resolved by selection, ElevenLabs' key row
    /// described Microsoft's Edge Read Aloud service and named <c>speech.platform.bing.com</c> as
    /// the destination — true of the current setting, and false about every consequence of the
    /// thing being pasted. A disclosure that is wrong on the one screen where trust is being
    /// decided is worse than no disclosure, because it is believed.
    /// </para>
    /// <para>
    /// Still computed from <see cref="Audio.TtsProviderCatalog"/> rather than written out beside
    /// the row: the reason the disclosures live in the catalog is that a second hand-written copy
    /// is the one that goes stale.
    /// </para>
    /// </summary>
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

        // One sentence shape for every case, including none. The zero case used to get its own
        // line — "Nothing is leaving this machine right now" — and a blanket claim is a worse
        // answer than a count even when it is true: it invites being read as a property of d47
        // rather than of these settings at this moment, and it is the sentence a future feature
        // then has to be designed around rather than merely disclosed in. The rows below say
        // what each destination is doing; that is the disclosure, and it does not need a slogan
        // on top of it.
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

        // The first time in d47's life that the honest answer to *what is leaving* is *nothing*
        // (Phase 29). Phase 4's amendment made local-only a matter of the enumeration
        // being truthful rather than of a mode being promised, and this is what being truthful
        // looks like when the endpoint is on this machine.
        //
        // Silent rather than active, because the row's whole job is to say what leaves and the
        // answer here is nothing. The address is still named: a Commander should be able to read
        // *why* it says that and check the address themselves.
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
