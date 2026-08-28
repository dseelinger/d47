namespace D47.Core.Audio;

/// <summary>
/// What one voice provider offers and what talking to it costs in privacy. Declared as data
/// for the same reasons <see cref="Conversation.LlmProviderCatalog"/> is: the settings surface
/// shows the controls the selected provider actually has rather than a hardwired set, and the
/// egress disclosure has one place to read from (Phase 4).
/// </summary>
public sealed record TtsProviderInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>How the provider row labels it.</summary>
    public required string Label { get; init; }

    /// <summary>The secret store name for this provider's key, or null if it needs none.</summary>
    public string? KeySecretName { get; init; }

    /// <summary>
    /// Exactly what leaves the machine when this provider speaks. In the second person and in
    /// full, because a disclosure that summarises is a disclosure that omits.
    /// </summary>
    public required string Egress { get; init; }

    /// <summary>Where it goes, for the disclosure's one-line form.</summary>
    public required string Destination { get; init; }

    /// <summary>
    /// The narrowest and widest speaking rate this provider will accept, in d47's normalised
    /// units where 1.0 is the voice's natural pace. Declared rather than assumed because the
    /// providers disagree sharply — Edge takes a wide percentage offset, ElevenLabs a narrow
    /// multiplier — and this is what lets one settings row mean the same thing on both.
    /// </summary>
    public double MinimumRate { get; init; } = 0.5;

    public double MaximumRate { get; init; } = 2.0;

    /// <summary>
    /// Whether speaking through this provider costs the Commander money at all.
    /// <para>
    /// Declared rather than inferred from <see cref="NeedsKey"/>, because "free" and "billed at a
    /// rate d47 does not know" have to read differently: <c>$0.00</c> from Edge and <c>$0.00</c>
    /// from an ElevenLabs run nobody has priced are the same string for opposite reasons
    /// (Phase 19).
    /// </para>
    /// </summary>
    public bool Billed { get; init; }

    /// <summary>
    /// The published list price in US dollars per thousand characters, or null where the
    /// provider does not publish one. The default for the settings row, never a claim about what
    /// this Commander is actually paying — see <see cref="SpeechSpend"/> for why those differ.
    /// </summary>
    public decimal? ListDollarsPerThousandCharacters { get; init; }

    /// <summary>
    /// The same thing per minute of audio, for a provider whose bill is not a function of the
    /// characters handed over.
    /// <para>
    /// <b>Two providers genuinely bill in two units, and forcing one unit on both is what
    /// produced the gap this closes</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/63">#63</a>). A provider declares the
    /// unit it is billed in and the rate in that unit; <see cref="SpeechSpend"/> multiplies the
    /// matching measure. Exactly one of these two should be set — see <see cref="BilledByMinute"/>.
    /// </para>
    /// </summary>
    public decimal? ListDollarsPerMinute { get; init; }

    /// <summary>
    /// Which measure this provider's bill is a function of. Declared by which rate is set rather
    /// than by a third field that could disagree with both.
    /// </summary>
    public bool BilledByMinute => ListDollarsPerMinute is not null;

    /// <summary>
    /// Whether this provider's voice ids mean nothing to a person, so one must never be shown in
    /// place of a voice's name.
    /// <para>
    /// Edge names a voice <c>en-US-AndrewMultilingualNeural</c>: not pretty, but it says who it
    /// is, and falling back to it tells the Commander something. ElevenLabs names one
    /// <c>JBFqnCBsd6RMkjVDRZzb</c>, which says nothing whatever — and that string appeared in the
    /// Voice row, under a label promising "which voice the core aboard speaks in". A row that
    /// answers a question about a voice with twenty characters of base62 has not answered it.
    /// </para>
    /// <para>
    /// A property of the provider rather than a guess at the shape of the string, because that is
    /// where the fact lives: the same reason <see cref="Billed"/> is declared rather than inferred
    /// from <see cref="NeedsKey"/>.
    /// </para>
    /// </summary>
    public bool VoiceIdsAreOpaque { get; init; }

    /// <summary>
    /// Whether this provider can be told which language to speak (Phase 58).
    /// <para>
    /// <b>The property a slot carrying other people's text depends on.</b> Edge sends
    /// <c>xml:lang</c> in its SSML and ElevenLabs sends a <c>language_code</c>; both therefore
    /// read a line the way d47 asked for whatever the words look like. OpenAI has no such field,
    /// and — measured 2026-08-26 — sending one anyway is accepted with <c>200</c> and ignored,
    /// which is worse than a refusal because nothing can see it happen
    /// (docs/spikes/openai-tts-language-and-speed.md §2).
    /// </para>
    /// <para>
    /// A message from another Commander can be in any language at all, so a provider that cannot
    /// be told one would follow it — a French line read as French, in a voice the Commander chose
    /// for English. So the settings surface does not offer such a provider for those slots. False
    /// is the exception rather than the default, because the two providers that were here first
    /// both pin.
    /// </para>
    /// </summary>
    public bool LanguageCanBePinned { get; init; } = true;

    /// <summary>
    /// Whether the voice list is known without asking, so listing it proves nothing about a key
    /// (Phase 58).
    /// <para>
    /// True for a provider with no voices endpoint at all. The Check button lists voices to prove
    /// a credential everywhere else, and for such a provider that check would answer "accepted
    /// the key" for a key that had never been sent anywhere — which is the exact fault Phase 19
    /// fixed on the other side, arriving from the other direction.
    /// </para>
    /// </summary>
    public bool VoicesAreStatic { get; init; }

    /// <summary>
    /// Whether telling this provider to speak faster or slower actually changes the audio
    /// (Phase 60).
    /// <para>
    /// <b>The second thing a provider turned out not to be able to do, and it fails in a stranger
    /// way than the first.</b> OpenAI has no language field at all. Cartesia <em>has</em> a speed
    /// control, <em>validates</em> it to a precise range — <c>2.0</c> is a <c>400</c> naming the
    /// field and the bounds — and then does not act on it: three runs per setting put the largest
    /// difference between settings (1.19s) below the largest spread within one setting (2.14s),
    /// and <c>slowest</c> came out shorter than <c>normal</c>
    /// (docs/spikes/cartesia-voices-and-speed.md §3).
    /// </para>
    /// <para>
    /// So the rate row is not offered for such a provider, and <c>SpeechCapability.RateFor</c>
    /// answers 1.0 for it however a settings file was edited — the same two places
    /// <see cref="LanguageCanBePinned"/> is enforced in, because a rule living only in a dropdown
    /// is one a text editor walks straight past. <see cref="MinimumRate"/> and
    /// <see cref="MaximumRate"/> are not consulted while this is false; the declaration is here.
    /// </para>
    /// <para>
    /// A single-sample pass showed a tidy 26% monotonic spread across the enum and would have
    /// shipped a slider controlling nothing. Repeating the discriminating cases is the only reason
    /// this property reads the way it does.
    /// </para>
    /// </summary>
    public bool RateCanBeSet { get; init; } = true;

    /// <summary>
    /// Why d47 quotes no rate for a provider that charges, or null where it quotes one.
    /// <para>
    /// <b>Declared so that arriving unpriced is a sentence somebody wrote rather than a field
    /// nobody set.</b> <see cref="Billed"/> with neither rate is otherwise indistinguishable from
    /// an omission, which is exactly what the guard in <c>WhatTheVoicesCostTests</c> was built to
    /// catch — so the guard now asks for this instead of merely being widened past it.
    /// </para>
    /// </summary>
    public string? NoRateBecause { get; init; }

    public bool NeedsKey => KeySecretName is not null;

    /// <summary>
    /// Whether this provider can say anything right now. Both halves matter: a provider with no
    /// key is configured but off, which is a capability being off rather than a failure to
    /// handle (Phase 3).
    /// </summary>
    public bool Speaks => Id != TtsProviderCatalog.NoneId;
}

/// <summary>
/// The voice providers d47 ships. One list, read by the provider row, the key row, the rate
/// row, the egress disclosure and the app's provider construction — so a provider cannot exist
/// in one of those and be missing from another.
/// </summary>
public static class TtsProviderCatalog
{
    public const string NoneId = "none";

    public const string EdgeId = "edge";

    public const string ElevenLabsId = "elevenlabs";

    public const string OpenAiId = "openai";

    public const string CartesiaId = "cartesia";

    public const string KokoroId = "kokoro";

    public static TtsProviderInfo None { get; } = new()
    {
        Id = NoneId,
        Name = "None",
        Label = "None — do not speak",
        Destination = "nothing sent",
        Egress = "No voice provider is selected, so no text is sent anywhere to be spoken. "
                 + "Audio cues and the thinking bed still play; they are files on this machine.",
    };

    public static TtsProviderInfo Edge { get; } = new()
    {
        Id = EdgeId,
        Name = "Edge Neural",
        Label = "Edge Neural (free)",
        Destination = "speech.platform.bing.com",
        Egress = "The text of every line D47 speaks is sent to Microsoft's Edge Read Aloud service to "
                 + "be turned into audio. That includes re-voiced in-game messages when you have "
                 + "turned those on, which are written by other players. No game state, no journal "
                 + "content and no keys are sent, and no account is involved.",
    };

    public static TtsProviderInfo ElevenLabs { get; } = new()
    {
        Id = ElevenLabsId,
        Name = "ElevenLabs",
        Label = "ElevenLabs (paid — needs a key)",
        KeySecretName = "elevenlabs.apiKey",
        Destination = "api.elevenlabs.io",

        // "JBFqnCBsd6RMkjVDRZzb" is a real one, and it is what the Voice row showed.
        VoiceIdsAreOpaque = true,
        Egress = "The text of every line D47 speaks is sent to ElevenLabs to be turned into audio, "
                 + "along with your API key. That includes re-voiced in-game messages when you have "
                 + "turned those on, which are written by other players. No journal content, game "
                 + "state or other keys are sent.",

        // ElevenLabs rejects a speed outside this outright rather than clamping, so the range is
        // declared here and the settings row narrows to it while this provider is selected.
        MinimumRate = 0.7,
        MaximumRate = 1.2,

        Billed = true,

        // The published API list price for eleven_flash_v2_5, which is the model d47 pins —
        // $0.05 per 1,000 characters, half the Multilingual 2 rate this used to read. From
        // elevenlabs.io/pricing/api, read on 2026-08-16 for Turbo 2.5 and again on 2026-08-25
        // when the pin moved to Flash 2.5, which ElevenLabs bills at the same rate. Neither move
        // was made for price: the first was language enforcement and the second was Turbo being
        // deprecated. The figure has not changed through either.
        //
        // A list price and not a bill. A subscription burns bundled credits instead — 121,000 a
        // month for $22 on Creator, so an effective $0.18 per thousand until the bundle runs out
        // and nothing at the margin before that — and the API reports neither the tier nor the
        // arrangement. So this is the row's default and the row is editable, in the same spirit
        // as the model price table declining to model introductory pricing it cannot date.
        ListDollarsPerThousandCharacters = 0.05m,
    };

    public static TtsProviderInfo OpenAi { get; } = new()
    {
        Id = OpenAiId,
        Name = "OpenAI",
        Label = "OpenAI (paid — needs a key)",

        // The same secret the language-model provider uses. One account, one credential: asking a
        // Commander to paste the same key twice charges them for an implementation detail, and
        // two copies of one secret is a rotation that half-works.
        KeySecretName = "openai.apiKey",
        Destination = "api.openai.com",
        Egress = "The text of every line D47 speaks through this slot is sent to OpenAI to be turned "
                 + "into audio, along with your API key — the same key the language model uses if you "
                 + "have set one. No journal content, game state or other keys are sent.",

        // It cannot be told a language, so it is never offered for a slot carrying somebody
        // else's words. See the property's own note; this is the whole reason it is declared.
        LanguageCanBePinned = false,

        // Thirteen built-ins and no voices endpoint, so the list needs no key and proves nothing
        // about one.
        VoicesAreStatic = true,

        // Measured 2026-08-26 across the documented range, and honoured
        // (docs/spikes/openai-tts-language-and-speed.md §3).
        MinimumRate = 0.25,
        MaximumRate = 4.0,

        Billed = true,

        // Not priced by the character, and that is still the finding rather than an omission
        // (Phase 58). Measured on the spike's own clips, plain prose runs at 951
        // characters a minute and a line of system names and numerals at 671 — a spread of about
        // 40% with *content* — so no character-to-minute conversion exists and any figure derived
        // that way would be wrong by a different amount on every line.
        //
        // What changed is that the conversion is not needed: d47 has the audio, so it knows each
        // clip's length to the sample, and duration is a measurement rather than an estimate
        // (#63).
        ListDollarsPerThousandCharacters = null,

        // $0.015 per minute of audio. **This is a proxy and is recorded as one**, which is the
        // honest half of #63 and the half worth reading before trusting the figure.
        //
        // OpenAI publishes no per-minute rate. From developers.openai.com/api/docs/pricing, read
        // on 2026-08-26: gpt-4o-mini-tts is $12.00 per 1M audio *output tokens* and $0.60 per 1M
        // text input tokens, and no minute appears anywhere on the page. The commonly quoted
        // $0.015 a minute is the equivalent third parties arrive at, not a rate OpenAI states.
        //
        // So duration is a proxy for the billed quantity rather than the billed quantity itself.
        // It is a far better proxy than characters — audio tokens track the length of the audio,
        // which is exactly what is being measured, rather than tracking content the way a
        // character count does — and it is the closest thing to ground truth available, because
        // /v1/audio/speech returns audio bytes and **no usage object**, so there is nothing to
        // read the real token count back from.
        //
        // A good proxy stated as one is honest; a proxy presented as the bill is not. The row is
        // editable for the same reason ElevenLabs' is, and the spend dialog says once, at the top,
        // that every figure in it is an estimate.
        ListDollarsPerMinute = 0.015m,
    };

    public static TtsProviderInfo Cartesia { get; } = new()
    {
        Id = CartesiaId,
        Name = "Cartesia",
        Label = "Cartesia (paid — needs a key)",
        KeySecretName = "cartesia.apiKey",
        Destination = "api.cartesia.ai",
        Egress = "The text of every line D47 speaks through this slot is sent to Cartesia to be "
                 + "turned into audio, along with your API key. That includes re-voiced in-game "
                 + "messages when you have turned those on, which are written by other players. "
                 + "No journal content, game state or other keys are sent.",

        // Opaque, the way ElevenLabs' are: measured 2026-08-26 across all 924
        // (docs/spikes/cartesia-voices-and-speed.md §1).
        VoiceIdsAreOpaque = true,

        // It takes a `language` field and holds it, so unlike OpenAI it is eligible for the four
        // slots carrying other people's words — the second provider after ElevenLabs that can be.
        // A capability rather than a preference, which is why it is stated here rather than
        // argued at the row.
        LanguageCanBePinned = true,

        // Measured, not read: `speed` validates to [-1.0, 1.0] inside
        // `voice.__experimental_controls` and moves nothing beyond the instrument's own noise.
        // See RateCanBeSet, which is where the finding lives.
        RateCanBeSet = false,

        Billed = true,

        // Not published, and not for the reason OpenAI's is not. OpenAI publishes a rate in a
        // unit that had to be converted; here d47 does not know the *unit*. Four account
        // endpoints — /balance, /usage, /subscriptions/current, /account — are all 404, so there
        // is no rate and no balance to read (docs/spikes/cartesia-voices-and-speed.md §2), and
        // the price page is a fact about a web page to be read by a person and recorded rather
        // than scraped.
        //
        // So the row takes the treatment OpenAI's took before #63: "(not published — no price
        // will be quoted)", a character count with no dollars beside it, and an editable row for
        // a Commander who has read the page. Both rates stay null until somebody has.
        ListDollarsPerThousandCharacters = null,
        NoRateBecause =
            "Cartesia's API publishes neither a rate nor a balance — /balance, /usage, "
            + "/subscriptions/current and /account are all 404 — and the price page has not been "
            + "read. Set the rate in Settings once it has.",
    };

    /// <summary>
    /// The local voice (Phase 59), and the only entry here that sends nothing anywhere.
    /// <para>
    /// <b>It exists to close Phase 57's last open item</b> — <em>no other player's text has to
    /// leave the machine</em> — which has stood since v0.72.0. The cost half was settled then,
    /// because every slot carrying another player's words defaults to Edge and Edge is free. The
    /// egress half could not be: Edge is free and <em>not local</em>, so those words still reached
    /// Microsoft.
    /// </para>
    /// </summary>
    public static TtsProviderInfo Kokoro { get; } = new()
    {
        Id = KokoroId,
        Name = "Kokoro",
        Label = "Kokoro (free — runs on this machine)",
        Destination = "nothing sent",
        Egress =
            "Nothing is sent anywhere. The voice runs on this computer, so the text D47 speaks — "
            + "including re-voiced in-game messages written by other players — never leaves it. The "
            + "model is downloaded once, from huggingface.co, and after that this slot needs no "
            + "network at all.",

        // Free, and free in the way None is rather than the way Edge is: there is no service.
        Billed = false,

        // The voices are files on disk, so listing them proves nothing about a credential — there
        // is no credential. Same reasoning as OpenAI's static list, arriving from further along.
        VoicesAreStatic = true,

        // The ids say who they are — af_heart, bm_george — so they are not the opaque kind that
        // must never be shown in place of a name.
        VoiceIdsAreOpaque = false,

        // <b>Eligible for the slots carrying other people's words, and the flag needs its
        // reasoning stated rather than copied.</b> The rule exists because a provider that cannot
        // be told a language FOLLOWS the text's language, so a French message would be read as
        // French in a voice chosen for English. Kokoro cannot be told a language because it only
        // speaks one: everything reaching it is phonemes worked out by d47's own English rules, so
        // the language is pinned permanently rather than not at all. The risk the flag guards
        // against cannot arise here, and excluding it would exclude the only provider that makes
        // those slots private, which is the whole point of the phase.
        LanguageCanBePinned = true,

        // The model takes a speed input and the spike measured it moving the audio, unlike
        // Cartesia's.
        RateCanBeSet = true,
    };

    /// <summary>Every provider, in the order the row offers them. "None" last, like the LLM row.</summary>
    public static IReadOnlyList<TtsProviderInfo> All { get; } =
        [Edge, Kokoro, ElevenLabs, OpenAi, Cartesia, None];

    /// <summary>
    /// The providers that may speak for one slot. Everything, except that a slot carrying other
    /// people's words is not offered a provider that cannot be told a language (Phase 58).
    /// <para>
    /// Read by the settings row and by <see cref="VoiceGroups.ProviderFor"/>, so what the picker
    /// offers and what the app will actually use cannot disagree — which is the failure mode a
    /// filtered list invites if only one of the two knows about the filter.
    /// </para>
    /// </summary>
    public static IReadOnlyList<TtsProviderInfo> For(VoiceGroupInfo slot) =>
        slot.OtherPeoplesWords ? [.. All.Where(provider => provider.LanguageCanBePinned)] : All;

    /// <summary>
    /// The provider this id names, or <see cref="Edge"/> if it names none. Unknown rather than
    /// invalid, like the persona row: a settings file naming a provider d47 no longer ships
    /// should start the app with a voice, not fail to start.
    /// </summary>
    public static TtsProviderInfo Selected(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Edge;
}
