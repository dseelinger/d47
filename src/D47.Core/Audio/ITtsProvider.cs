namespace D47.Core.Audio;

/// <summary>One voice a provider offers. What the searchable picker renders.</summary>
public sealed record VoiceInfo(string Id, string Name, string Locale, string? Gender = null)
{
    /// <summary>How the picker labels it. The id stays the value, as everywhere else.</summary>
    public string Label => Gender is null ? $"{Name} ({Locale})" : $"{Name} — {Gender}, {Locale}";
}

/// <summary>
/// A voice, plus how fast to say it. Rate lives here rather than in settings-shaped form
/// because providers disagree about its units and its range, and normalising at the seam is
/// one conversion instead of one per caller (list.md Phase 11).
/// </summary>
public sealed record VoiceSelection(string? VoiceId, double Rate = 1.0)
{
    /// <summary>
    /// No voice chosen, so the provider picks its own. Named argument because a positional
    /// `new(null)` binds the record's copy constructor rather than this one.
    /// </summary>
    public static readonly VoiceSelection Default = new(VoiceId: null);
}

/// <summary>
/// Why a voice list is the length it is. Four different situations used to arrive as one empty
/// list, and the Commander was shown the same nothing for all of them — which reads as "d47 is
/// broken" whichever one it actually was (docs/spikes/elevenlabs-voice-sources.md §3).
/// </summary>
public enum VoiceListing
{
    /// <summary>The provider answered. However many voices that was, including none.</summary>
    Listed,

    /// <summary>
    /// Nothing was asked. The provider needs a credential and none is stored, so an empty list
    /// here is a capability being off rather than anything having failed.
    /// </summary>
    NoKey,

    /// <summary>It was asked and refused the credential.</summary>
    KeyRejected,

    /// <summary>It was asked and did not answer. Network, outage, a proxy in the way.</summary>
    Unreachable,
}

/// <summary>
/// What a provider can say, and why that is all of it.
/// <para>
/// The reason this is a record rather than a bare list is measured: on 2026-08-16 every one of
/// no-key, wrong-key, no-network and genuinely-empty produced <c>[]</c> and a picker with
/// nothing in it, and the picker had nothing to say beyond "there is nothing to offer here".
/// The distinction exists at the seam — the service answers 401 with a different <c>status</c>
/// for each — and it was being thrown away one line above where it arrived.
/// </para>
/// </summary>
public sealed record VoiceCatalogue(IReadOnlyList<VoiceInfo> Voices, VoiceListing Listing, string? Detail = null)
{
    /// <summary>The provider answered, with whatever it had.</summary>
    public static VoiceCatalogue Of(IReadOnlyList<VoiceInfo> voices) => new(voices, VoiceListing.Listed);

    public static VoiceCatalogue NoKey(string detail) => new([], VoiceListing.NoKey, detail);

    public static VoiceCatalogue KeyRejected(string detail) => new([], VoiceListing.KeyRejected, detail);

    public static VoiceCatalogue Unreachable(string detail) => new([], VoiceListing.Unreachable, detail);

    /// <summary>Nothing was fetched and no provider is selected. Not a failure.</summary>
    public static readonly VoiceCatalogue Silent = new([], VoiceListing.Listed);

    public int Count => Voices.Count;

    /// <summary>
    /// What a picker with nothing in it should say, or null when there is something in it.
    /// <para>
    /// One sentence per situation, in the second person, each naming the thing the Commander
    /// can actually do about it. <see cref="VoiceListing.Listed"/> with no voices is the one
    /// that is genuinely the account's answer, and it says so rather than implying a fault.
    /// </para>
    /// </summary>
    public string? WhyEmpty(string providerName) => Voices.Count > 0 ? null : Listing switch
    {
        VoiceListing.NoKey =>
            $"{providerName} needs an API key before it will list its voices. Add one in the row above.",

        VoiceListing.KeyRejected =>
            $"{providerName} refused the stored key, so its voices cannot be listed{Because}",

        VoiceListing.Unreachable =>
            $"{providerName} could not be reached, so its voices cannot be listed{Because}",

        // The account really does have nothing. Worth saying outright, because it is the one
        // case where waiting, retrying or re-pasting the key changes nothing.
        _ => $"{providerName} answered, and has no voices on this account. "
             + "Adding one on their site makes it appear here.",
    };

    private string Because => Detail is { Length: > 0 } said ? $" — {said}." : ".";
}

/// <summary>
/// What kind of thing went wrong, where the difference changes what d47 should do about it.
/// <para>
/// Deliberately not a mapping of every provider's error taxonomy. The one distinction that
/// earns a type is <see cref="VoiceRejected"/>, because it is the only failure d47 can repair
/// by itself — everything else is for the Commander to read and act on.
/// </para>
/// </summary>
public enum TtsFault
{
    /// <summary>Something went wrong and the message is the whole of what is known.</summary>
    Unknown,

    /// <summary>
    /// The provider will not accept the voice it was given. Either it never issued that id — a
    /// voice chosen while a different provider was selected — or the voice has since been
    /// removed from the account. Both mean the id is worth forgetting rather than retrying.
    /// </summary>
    VoiceRejected,
}

public sealed class TtsException(string message, Exception? inner = null, TtsFault fault = TtsFault.Unknown)
    : Exception(message, inner)
{
    public TtsFault Fault { get; } = fault;
}

/// <summary>
/// Text to audio. The seam exists for the same reason <see cref="Conversation.ILlmProvider"/>
/// does: a paid provider arrives in Phase 11 and nothing above this line should notice
/// (architecture.md §2).
/// </summary>
public interface ITtsProvider
{
    string Id { get; }

    string Name { get; }

    /// <summary>
    /// What this provider can say, and why that is all of it. Network-bound for every provider
    /// d47 has, which is why it is async and why an empty list is a supported answer rather than
    /// an error — the picker still lets the Commander keep the current value or type one
    /// (list.md Phase 4).
    /// <para>
    /// It answers a <see cref="VoiceCatalogue"/> rather than a list because "empty" was four
    /// situations wearing one face, and the one that means "pay attention to the row above" was
    /// indistinguishable from the one that means "wait and try again"
    /// (list.md Phase 19; docs/spikes/elevenlabs-voice-sources.md §3).
    /// </para>
    /// </summary>
    Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One sentence, rendered to <see cref="AudioFormat.Standard"/>. Sentence-sized rather
    /// than reply-sized on purpose: the whole latency win is that this is called per sentence
    /// while the previous one is still playing.
    /// </summary>
    Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What this provider will actually put on the wire for <paramref name="text"/>, which for
    /// most of them is the text itself.
    /// <para>
    /// It exists so the spend counter stays true. A provider that rewrites a line before sending
    /// it — ElevenLabs writes numerals out as words, because a bare numeral is what makes a
    /// multilingual voice change language mid-sentence — is billed for the rewritten length, and
    /// a meter counting what it handed down would under-report every line with a figure in it.
    /// </para>
    /// <para>
    /// Only ever a character count. Nothing reads this to decide what to say.
    /// </para>
    /// </summary>
    string Billable(string text) => text;
}
