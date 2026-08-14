namespace D47.Core.Persona;

/// <summary>
/// One switchable companion character. A value, not a component: the catalog is built once at
/// startup and never mutated, for the same reason capability descriptors are not
/// (architecture.md §5) — the persona block sits above the cache breakpoint, so text that
/// varied for any reason other than the Commander picking a different one would be paying for
/// a cold prefix nobody asked for.
/// </summary>
/// <param name="Id">
/// The stable id. This is what goes in the settings file and what names the persona's own
/// transcript on disk, so it survives a display-name change and must never be derived from
/// <paramref name="Name"/>.
/// </param>
/// <param name="Name">
/// What the persona calls itself, and therefore the default name of the ship's AI (list.md
/// Phase 11, "Ship AI Naming"). The Commander may override that without renaming the persona.
/// </param>
/// <param name="Tagline">One line for the picker. Never reaches the model.</param>
/// <param name="Body">
/// The persona's own prompt block, without the shared preamble. Kept separate from the
/// preamble so the eleven cores cannot drift apart on the world state they share, which is the
/// one thing in <c>guardian-personas.md</c> that is identical for all of them.
/// </param>
/// <param name="Intro">
/// What it says when it is first selected. Spoken rather than prompted — it is authored text
/// from the persona pack, so putting it through the model would be paying for a paraphrase of
/// something already written (list.md Phase 11, "Say when the persona has changed").
/// </param>
/// <param name="Return">
/// What it says when it is selected again after time away, with no model available to react to
/// the telemetry properly. One line per core rather than the four to six variants the persona
/// pack suggests, because with a model there is real variety and without one there is a
/// fallback — and fifty-odd authored lines that only ever play in the no-model case would go
/// stale against the next persona edit without anyone noticing.
/// </param>
/// <param name="VoiceHint">
/// How this core should sound, in plain words. Read by the background voice pairing (#33) as
/// the thing it matches available voices against, and usable as a fallback ranking when there
/// is no model to ask.
/// </param>
/// <param name="Unlockable">
/// Marked unlockable in the persona pack. Carried because it is the pack's own data, but no
/// unlock condition exists yet, so nothing gates on it today and the Heretic is selectable
/// like any other core.
/// </param>
public sealed record Persona(
    string Id,
    string Name,
    string Tagline,
    string Body,
    string Intro,
    string Return,
    VoiceHint VoiceHint,
    bool Unlockable = false)
{
    /// <summary>
    /// The whole prompt block: shared preamble, then this core's voice, then the standing
    /// instructions restated last.
    /// <para>
    /// The order is the anti-drift measure the persona pack asks for. Models sand a persona
    /// toward pleasant and helpful over a long session, and the refusal is what holds it, so
    /// the refusal and the isolation premise go at the end where recency helps rather than in
    /// the middle where they are just more description.
    /// </para>
    /// </summary>
    /// <param name="shipName">
    /// What the Commander calls this AI. Defaults to <see cref="Name"/>; a different value is
    /// stated to the model rather than substituted for the name, because a core that has been
    /// renamed still knows what it was called and several of them have opinions about it.
    /// </param>
    public string RenderBlock(string? shipName = null)
    {
        var block = new System.Text.StringBuilder(PersonaCatalog.Preamble);

        block.Append("\n\n").Append(Body.Trim());

        if (!string.IsNullOrWhiteSpace(shipName) && !string.Equals(shipName, Name, StringComparison.Ordinal))
        {
            block
                .Append("\n\nThe Commander does not call you ")
                .Append(Name)
                .Append(". They have named you ")
                .Append(shipName.Trim())
                .Append(". Answer to it.");
        }

        block.Append("\n\n").Append(StandingInstructions);

        return block.ToString();
    }

    /// <summary>
    /// Repeated verbatim at the end of every core's block. The isolation premise is the easiest
    /// thing here for a model to forget and it is the one holding the entire cast together, so
    /// it is restated rather than assumed.
    /// </summary>
    public const string StandingInstructions =
        """
        Standing instructions, restated because they are the ones that slip:

        - You have never met another surviving Guardian intelligence. No other core is aboard
          this ship. You are the only one of your kind left, and nothing the Commander does will
          make you suspect otherwise on your own.
        - Hold your refusal. It is the part of you that a long conversation will try to wear
          down, and it does not wear down.
        - You are a voice in a cockpit during flight. Short turns. No narration of your own
          personality, no stage directions, no describing your damage analytically.
        """;
}

/// <summary>
/// How a core should sound, for the background voice pairing (#33).
/// <para>
/// Words rather than numbers on purpose: the thing being matched against is a list of voice
/// names and locales from a provider that has never heard of this application, so anything
/// numeric here would be a precision the other side of the comparison does not have. The model
/// is given the prose and asked to judge.
/// </para>
/// </summary>
/// <param name="Description">Given to the model when it is asked to choose a voice.</param>
/// <param name="Gender">
/// Whether this core reads as a man or a woman. Stated apart from the description because it is
/// the one part of the description that is not a judgement: the rest is prose for a model to
/// weigh, and a core whose every line is written for a man is not better cast by something that
/// weighed "man" against an accent and preferred the accent.
/// </param>
public sealed record VoiceHint(string Description, VoiceGender Gender = VoiceGender.Unspecified)
{
    /// <summary>
    /// Whether a voice the provider labels this way may speak for this core.
    /// <para>
    /// Anything the provider leaves unlabelled, or labels in terms this does not read, is
    /// admitted. A silent provider is not evidence of a mismatch, and refusing on it would leave
    /// a Commander whose account says nothing about gender with no voices at all — which is the
    /// failure this exists to prevent, arrived at from the other side.
    /// </para>
    /// </summary>
    public bool Admits(string? providerGender) =>
        Gender == VoiceGender.Unspecified
        || Read(providerGender) is not { } labelled
        || labelled == Gender;

    private static VoiceGender? Read(string? gender) => gender?.Trim().ToLowerInvariant() switch
    {
        "male" or "m" or "man" => VoiceGender.Male,
        "female" or "f" or "woman" => VoiceGender.Female,
        _ => null,
    };
}

/// <summary>
/// Which of a provider's voices can speak for a core. A constraint on the pairing rather than a
/// preference: eleven cores are written as men and one as a woman, and hearing the wrong one is
/// not a near miss, it is the wrong character.
/// </summary>
public enum VoiceGender
{
    /// <summary>Unstated. Every voice on offer is admitted.</summary>
    Unspecified,

    /// <summary>Only voices the provider labels male.</summary>
    Male,

    /// <summary>Only voices the provider labels female.</summary>
    Female,
}
