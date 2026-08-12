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

public sealed class TtsException(string message, Exception? inner = null) : Exception(message, inner);

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
    /// What this provider can say. Network-bound for every provider d47 has, which is why it
    /// is async and why an empty list is a supported answer rather than an error — the picker
    /// still lets the Commander keep the current value or type one (list.md Phase 4).
    /// </summary>
    Task<IReadOnlyList<VoiceInfo>> ListVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One sentence, rendered to <see cref="AudioFormat.Standard"/>. Sentence-sized rather
    /// than reply-sized on purpose: the whole latency win is that this is called per sentence
    /// while the previous one is still playing.
    /// </summary>
    Task<AudioClip> SynthesizeAsync(
        string text,
        VoiceSelection voice,
        CancellationToken cancellationToken = default);
}
