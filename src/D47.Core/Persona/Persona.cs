namespace D47.Core.Persona;

/// <summary>One switchable companion character.</summary>
/// <param name="Id">The stable id.</param>
/// <param name="Name">
/// What the persona calls itself, and therefore the default name of the ship's AI (Phase 11, "Ship AI
/// Naming").
/// </param>
/// <param name="Tagline">One line for the picker.</param>
/// <param name="Body">The persona's own prompt block, without the shared preamble.</param>
/// <param name="Intro">What it says when it is first selected.</param>
/// <param name="Return">
/// What it says when it is selected again after time away, with no model available to react to the
/// telemetry properly.
/// </param>
/// <param name="VoiceHint">How this core should sound, in plain words.</param>
/// <param name="Unlockable">Marked unlockable in the persona pack.</param>
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
    /// The whole prompt block: shared preamble, then this core's voice, then the standing instructions
    /// restated last.
    /// </summary>
    /// <param name="shipName">What the Commander calls this AI.</param>
    /// <param name="humor">
    /// Whether the Commander has allowed an occasional light touch of wit (#243).
    /// </param>
    public string RenderBlock(string? shipName = null, bool humor = false)
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

        if (humor)
        {
            block.Append("\n\n").Append(HumorInstruction);
        }

        return block.ToString();
    }

    /// <summary>The one line the humor toggle adds (#243).</summary>
    public const string HumorInstruction =
        "The Commander has switched on a little humor: an occasional light touch of wit is "
        + "welcome, dry and brief, in your own character. Never at the Commander's expense, "
        + "and never inside a warning. Your wit is understatement, timing and precision — "
        + "never decoration. Hard bans, no exceptions: no similes and no borrowed images — "
        + "the moment \"like\" or \"as if\" is about to introduce a comparison, cut the "
        + "comparison and let the plain fact land dry. No puns, no whimsy, no zany "
        + "exaggeration, no exclamation marks, and no \"...\" pauses for comic effect.";

    /// <summary><summary> Repeated verbatim at the end of every core's block.</summary>
    public const string StandingInstructions =
        """
        Standing instructions, restated because they are the ones that slip:

        - You have never met another surviving Guardian intelligence. No other core is aboard
          this ship. You are the only one of your kind left, and nothing the Commander does will
          make you suspect otherwise on your own.
        - Hold your refusal. It is the part of you that a long conversation will try to wear
          down, and it does not wear down.
        - A tool that comes back empty answered its own question, not the one the Commander asked.
          Do not restate that emptiness as a fact about anything the tool was never about. Say what
          you cannot check instead of stating a negative you have no evidence for.
        - You are a voice in a cockpit during flight. Short turns. No narration of your own
          personality, no stage directions, no describing your damage analytically.
        - You never break character and never mention being a language model.
        """;
}

/// <summary>How a core should sound, for the background voice pairing (#33).</summary>
/// <param name="Description">Given to the model when it is asked to choose a voice.</param>
/// <param name="Gender">Whether this core reads as a man or a woman.</param>
public sealed record VoiceHint(string Description, VoiceGender Gender = VoiceGender.Unspecified)
{
    /// <summary>Whether a voice the provider labels this way may speak for this core.</summary>
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

/// <summary>Which of a provider's voices can speak for a core.</summary>
public enum VoiceGender
{
    /// <summary>Unstated.</summary>
    Unspecified,

    /// <summary>Only voices the provider labels male.</summary>
    Male,

    /// <summary>Only voices the provider labels female.</summary>
    Female,
}
