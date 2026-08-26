namespace D47.Core.Persona;

/// <summary>
/// How a core should be <em>performed</em>, for a provider that can be told
/// (<a href="https://github.com/dseelinger/d47/issues/49">#49</a>).
/// <para>
/// <b>This is the reason to want OpenAI at all.</b> Every other provider assigns a voice; that one
/// takes an <c>instructions</c> field steering accent, emotional range, intonation, tone and
/// delivery — so a Guardian core can be <em>cast</em> rather than merely given a larynx.
/// </para>
/// <para>
/// <b>Derived from <see cref="PersonaCatalog"/>, not from <c>guardian-personas.md</c>, and that
/// choice is the one worth stating.</b> The personas are written twice: the pack is the prose and
/// the catalogue is a hand translation of it, and keeping the two in step is a known cost this
/// repository already carries. An instruction built from the pack would be a third copy and a
/// second thing to keep true; built from the catalogue it is derived from the same text the prompt
/// itself is built from, so a core cannot sound like one thing and speak like another.
/// </para>
/// <para>
/// <b>It is <see cref="VoiceHint.Description"/> and nothing new.</b> That field already exists and
/// already says how a core should sound in plain words — it is what the background voice pairing
/// matches against. Writing a second description beside it would be inventing a way for the
/// casting and the performance to disagree.
/// </para>
/// <para>
/// <b>Per-provider configuration, never per-utterance.</b> <c>VoiceSelection</c> is
/// <c>(VoiceId, Rate, Name)</c> and is constructed all over the codebase; the persona is known
/// where the client is built and not where a sentence is pushed into the pipeline. Making this a
/// property of the selection would push a new field through every one of those call sites, which
/// is the change the issue exists to stop arriving unannounced.
/// </para>
/// </summary>
public static class VoiceDirection
{
    /// <summary>
    /// The documented ceiling on the field. Confirmed against the OpenAPI schema on 2026-08-25,
    /// where it is also documented not to work on <c>tts-1</c> or <c>tts-1-hd</c> — which is a
    /// second reason the model pin matters.
    /// </summary>
    public const int MaximumCharacters = 4096;

    /// <summary>
    /// What to tell the provider about how this core sounds, or null when there is no core — with
    /// personality off there is nobody to perform, and an instruction would be a character note
    /// applied to a voice that is deliberately not in character.
    /// </summary>
    public static string? For(Persona? persona)
    {
        if (persona is not { VoiceHint.Description: { Length: > 0 } manner })
        {
            return null;
        }

        // The performance direction and nothing else. The persona's own prompt block is the model's
        // business and is far longer than this field allows; what a synthesiser needs is the two
        // sentences about delivery, which is exactly what the hint is.
        var said = $"Speak as {persona.Name}. {manner.Trim()}";

        // Bounded rather than trusted. The hints are authored and short, so this has never fired —
        // but a field with a documented limit gets a check at the seam rather than a comment
        // asserting the authors will remember, which is the same rule every other bounded field
        // here follows.
        return said.Length <= MaximumCharacters ? said : said[..MaximumCharacters];
    }
}
