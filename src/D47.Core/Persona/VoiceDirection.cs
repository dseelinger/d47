namespace D47.Core.Persona;

/// <summary>How a core should be performed, for a provider that can be told (#49).</summary>
public static class VoiceDirection
{
    /// <summary>The documented ceiling on the field.</summary>
    public const int MaximumCharacters = 4096;

    /// <summary>
    /// What to tell the provider about how this core sounds, or null when there is no core — with
    /// personality off there is nobody to perform, and an instruction would be a character note applied
    /// to a voice that is deliberately not in character.
    /// </summary>
    public static string? For(Persona? persona)
    {
        if (persona is not { VoiceHint.Description: { Length: > 0 } manner })
        {
            return null;
        }

        // The performance direction and nothing else.
        var said = $"Speak as {persona.Name}. {manner.Trim()}";

        // Bounded rather than trusted.
        return said.Length <= MaximumCharacters ? said : said[..MaximumCharacters];
    }
}
