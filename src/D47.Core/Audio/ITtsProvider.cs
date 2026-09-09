namespace D47.Core.Audio;

/// <summary>One voice a provider offers.</summary>
public sealed record VoiceInfo(string Id, string Name, string Locale, string? Gender = null)
{
    /// <summary>How the picker labels it.</summary>
    public string Label => Gender is null ? $"{Name} ({Locale})" : $"{Name} — {Gender}, {Locale}";
}

/// <summary>A voice, plus how fast to say it.</summary>
public sealed record VoiceSelection(string? VoiceId, double Rate = 1.0)
{
    /// <summary>What the voice is called, where the caller could find out.</summary>
    public string? Name { get; init; }

    /// <summary>No voice chosen, so the provider picks its own.</summary>
    public static readonly VoiceSelection Default = new(VoiceId: null);
}

/// <summary>Why a voice list is the length it is.</summary>
public enum VoiceListing
{
    /// <summary>The provider answered.</summary>
    Listed,

    /// <summary>Nothing was asked.</summary>
    NoKey,

    /// <summary>It was asked and refused the credential.</summary>
    KeyRejected,

    /// <summary>It was asked and did not answer.</summary>
    Unreachable,
}

/// <summary>What a provider can say, and why that is all of it.</summary>
public sealed record VoiceCatalogue(IReadOnlyList<VoiceInfo> Voices, VoiceListing Listing, string? Detail = null)
{
    /// <summary>The provider answered, with whatever it had.</summary>
    public static VoiceCatalogue Of(IReadOnlyList<VoiceInfo> voices) => new(voices, VoiceListing.Listed);

    public static VoiceCatalogue NoKey(string detail) => new([], VoiceListing.NoKey, detail);

    public static VoiceCatalogue KeyRejected(string detail) => new([], VoiceListing.KeyRejected, detail);

    public static VoiceCatalogue Unreachable(string detail) => new([], VoiceListing.Unreachable, detail);

    /// <summary>Nothing was fetched and no provider is selected.</summary>
    public static readonly VoiceCatalogue Silent = new([], VoiceListing.Listed);

    public int Count => Voices.Count;

    /// <summary>
    /// How one voice id is shown to the Commander — the Voice rows, their tooltips and the picker all
    /// end up here.
    /// </summary>
    public string LabelFor(string id, TtsProviderInfo provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (Voices.FirstOrDefault(voice => string.Equals(voice.Id, id, StringComparison.OrdinalIgnoreCase))
            is { } known)
        {
            return known.Label;
        }

        if (!provider.VoiceIdsAreOpaque)
        {
            return id;
        }

        return Count == 0
            ? $"(a {provider.Name} voice — the list has not been fetched yet)"
            : $"(a voice not in {provider.Name}'s list)";
    }

    /// <summary>What a picker with nothing in it should say, or null when there is something in it.</summary>
    public string? WhyEmpty(string providerName) => Voices.Count > 0 ? null : Listing switch
    {
        VoiceListing.NoKey =>
            $"{providerName} needs an API key before it will list its voices. Add one in the row above.",

        VoiceListing.KeyRejected =>
            $"{providerName} refused the stored key, so its voices cannot be listed{Because}",

        VoiceListing.Unreachable =>
            $"{providerName} could not be reached, so its voices cannot be listed{Because}",

        // The account really does have nothing.
        _ => $"{providerName} answered, and has no voices on this account. "
             + "Adding one on their site makes it appear here.",
    };

    private string Because => Detail is { Length: > 0 } said ? $" — {said}." : ".";
}

/// <summary>What kind of thing went wrong, where the difference changes what d47 should do about it.</summary>
public enum TtsFault
{
    /// <summary>Something went wrong and the message is the whole of what is known.</summary>
    Unknown,

    /// <summary>The provider will not accept the voice it was given.</summary>
    VoiceRejected,

    /// <summary>The credential was refused, or there is none stored.</summary>
    KeyRejected,

    /// <summary>It was asked and did not answer: network, outage, a proxy, a rate limit.</summary>
    Unreachable,
}

public sealed class TtsException(string message, Exception? inner = null, TtsFault fault = TtsFault.Unknown)
    : Exception(message, inner)
{
    public TtsFault Fault { get; } = fault;
}

/// <summary>Text to audio.</summary>
public interface ITtsProvider
{
    string Id { get; }

    string Name { get; }

    /// <summary>What this provider can say, and why that is all of it.</summary>
    Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>One sentence, rendered to <see cref="AudioFormat.Standard"/>.</summary>
    Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many characters this provider would rather be handed at once, or zero for one sentence at a
    /// time.
    /// </summary>
    int GroupsSentencesUpTo => 0;

    /// <summary>
    /// Whether this provider performs bracketed delivery direction rather than reading it aloud (#291).
    /// </summary>
    bool ReadsAudioTags => false;

    /// <summary>
    /// What this provider will actually put on the wire for <paramref name="text"/>, which for most of
    /// them is the text itself.
    /// </summary>
    string Billable(string text) => text;

    /// <summary>
    /// What this provider will actually be given to pronounce, where that is phonemes rather than the
    /// text itself.
    /// </summary>
    string? Phonemes(string text, VoiceSelection voice) => null;
}
