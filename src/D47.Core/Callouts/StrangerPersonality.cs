namespace D47.Core.Callouts;

/// <summary>The brief for a stranger's personality, and what of the model's answer may be kept.</summary>
public static class StrangerPersonality
{
    /// <summary>Longest personality kept, in characters.</summary>
    public const int MostLength = 400;

    /// <summary>How long the model has to write one before the reply goes ahead without it.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    /// <summary>Asks for two sentences about one NPC from what D47 knows of them.</summary>
    public static string Instruction(NpcHeard npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        var known = npc.Kind == NpcKind.Comms
            ? $"{npc.Name} is a real comms contact" + (npc.Family is { Length: > 0 } family ? $" of the kind \"{family}\"." : ".")
            : $"{npc.Name} is a made-up pilot or crew member heard on the comms channel.";

        return $"""
                Write a two-sentence personality for a person in Elite Dangerous. {known}
                Give how they talk and what they care about, as a description to be played by someone else.
                A manner is how they talk, never where they are from: name no nationality, language or accent.
                Never name or imitate a real person. Answer with the two sentences only: no heading, no quotation marks.
                """;
    }

    /// <summary>The answer trimmed to what may be kept, or null when it is empty, too long or talks about itself.</summary>
    public static string? Kept(string? answer)
    {
        var text = answer?.Trim();

        return text is { Length: > 0 and <= MostLength } && FlavourBriefs.MayBeSpoken(text) ? text : null;
    }
}
