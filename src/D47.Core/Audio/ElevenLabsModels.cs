namespace D47.Core.Audio;

/// <summary>
/// Which ElevenLabs model speaks, and what each one can do
/// (<a href="https://github.com/dseelinger/d47/issues/291">#291</a>).
/// <para>
/// <b>Here rather than in the client, because two of the three answers are needed before any
/// speaking happens.</b> The settings surface has to hide the rate row for a model with no rate,
/// and prompt assembly has to know whether the model about to speak performs audio tags — and
/// both of those live in Core, which depends on nothing and cannot ask <c>D47.Tts</c>. This is the
/// same split <see cref="TtsProviderCatalog"/> already makes: the catalogue is what is true about a
/// provider, the client is how it is reached.
/// </para>
/// <para>
/// <b>Two models, and there is no mechanism for a third.</b> <c>eleven_v3</c> — the plain one, not
/// the conversational one below — lists at twice the price for the same 74 languages and the same
/// tags, so it is an upgrade only in name. And Multilingual 2 cannot hold a language: it infers one
/// per line, which is the behaviour it is built for and which read a material milestone half in
/// German. A list of exactly these two keeps that model off the surface by never naming it, which
/// was one of the three costs that made a picker unwelcome the first time it was asked for.
/// </para>
/// <para>
/// <b>The history this list replaces.</b> d47 pinned one model from Phase 11 and had no setting for
/// it by design (change-requests.md 41, declined 2026-08-25): a picker would have needed a per-model
/// price in the spend ledger, a per-model speed range in the rate row, and a standing rule keeping
/// Multilingual 2 off the list, so a Commander could choose between one right model and several
/// wrong ones. The pin moved once, Turbo 2.5 to Flash 2.5 on 2026-08-25, when ElevenLabs deprecated
/// Turbo and named Flash its replacement — <i>"functionally equivalent … except the latency on the
/// Flash models is lower on average"</i>. That was the same ruling on the model that still existed,
/// not a second opinion about it.
/// </para>
/// <para>
/// <b>Two of those three costs are now gone and the third is worse than expected.</b> Both models
/// list at $0.05 per thousand characters, so the ledger needs no per-model price; this list keeps
/// Multilingual 2 unnameable. The rate row was expected to <em>narrow</em> per model — but v3 has no
/// rate at all, so it is hidden rather than narrowed. See <see cref="ReadsRate"/>.
/// </para>
/// </summary>
public static class ElevenLabsModels
{
    /// <summary>
    /// The expressive model, and the default. <c>_conversational</c> rather than plain
    /// <c>eleven_v3</c>: same tags, half the list price.
    /// </summary>
    public const string V3 = "eleven_v3_conversational";

    /// <summary>
    /// The fast model, and what d47 pinned before v3 existed. Kept because 313 ms against
    /// 2,060 ms on a routing line is a difference a Commander may feel in a fight and not mind in
    /// the hangar, and that is a preference rather than a fact d47 can settle for them.
    /// </summary>
    public const string Flash = "eleven_flash_v2_5";

    /// <summary>
    /// What speaks when nobody has chosen.
    /// <para>
    /// v3, on the maintainer's ear rather than on the specification: it was judged plainly better
    /// than Flash <em>even on untagged lines</em>, which is the comparison that describes d47 as it
    /// is, since nothing writes a tag unless the model chooses to. The tags are what it can do
    /// beyond that.
    /// </para>
    /// </summary>
    public const string Default = V3;

    /// <summary>
    /// The models offered, in the order the row lists them. Each label says what the choice buys,
    /// because "v3" and "Flash 2.5" mean nothing to somebody who has not read the spike — and the
    /// timings are the measured round trip on a routing line, not the published time-to-first-byte
    /// figures, which describe a streaming caller d47 is not.
    /// </summary>
    public static readonly IReadOnlyList<(string Id, string Label)> All =
    [
        (V3, "v3 Conversational — more expressive, about 2 seconds"),
        (Flash, "Flash 2.5 — fastest, about 0.3 seconds"),
    ];

    /// <summary>
    /// A stored name, or <see cref="Default"/> where it is missing or is one d47 no longer offers.
    /// <para>
    /// Every question below goes through this, so a hand-edited <c>settings.json</c> naming
    /// <c>eleven_multilingual_v2</c> gets the default rather than a model the language rule
    /// excludes. Settings are append-only and the file is one a Commander reads and edits; a rule
    /// living only in a dropdown is one a text editor walks straight past.
    /// </para>
    /// </summary>
    public static string Named(string? model) =>
        All.Any(offered => offered.Id == model) ? model! : Default;

    /// <summary>
    /// Whether the model honours <c>voice_settings.speed</c>. <b>Only Flash does.</b>
    /// <para>
    /// v3 accepts 0.5 through 2.0 — a four-fold span — and returns the same eight and a half
    /// seconds of audio throughout, with the spread within one setting wider than the spread across
    /// all eleven. That is the same failure Cartesia's speed control had and it is handled the same
    /// way: the row is not offered, rather than offered and inert
    /// (docs/spikes/elevenlabs-v3-conversational.md §3).
    /// </para>
    /// </summary>
    public static bool ReadsRate(string? model) => Named(model) == Flash;

    /// <summary>
    /// Whether the model performs bracketed delivery direction rather than reading it aloud.
    /// <b>Only v3 does.</b> Flash transcribes back as <i>"Whispers, cutting the drives"</i> — every
    /// tag, every time (§7).
    /// </summary>
    public static bool ReadsTags(string? model) => Named(model) == V3;

    /// <summary>
    /// How much text the model would rather be handed at once, or zero for one sentence at a time.
    /// <para>
    /// <b>300 characters for v3, and the number comes from both ends.</b> ElevenLabs encourages
    /// over 250 for a tag to land consistently, and <see cref="SentenceSplitter"/>'s soft cap is
    /// 320, so a larger budget could not be filled by a single sentence anyway. Flash asks for
    /// nothing: it performs no tags, so grouping would spend its whole advantage — the reason a
    /// Commander picks it — on a capability it does not have.
    /// </para>
    /// </summary>
    public static int GroupsSentencesUpTo(string? model) => ReadsTags(model) ? 300 : 0;
}
