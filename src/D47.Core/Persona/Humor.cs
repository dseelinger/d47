using D47.Core.Audio;
using D47.Core.Configuration;

namespace D47.Core.Persona;

/// <summary>Whose lines one pair of humor settings governs.</summary>
public enum HumorGroup
{
    /// <summary>The ship's AI.</summary>
    Cores,

    /// <summary>Invented people and every other NPC.</summary>
    Npcs,

    /// <summary>The Commander's carrier captain and tower.</summary>
    Carrier,
}

/// <summary>How funny one group may be (0 to 10) and on what share of its lines (0 to 100).</summary>
public sealed record HumorDial(int Level, int Percent)
{
    /// <summary>No humor.</summary>
    public static readonly HumorDial Off = new(0, 0);
}

/// <summary>The graded humor instruction, and which group a speaker belongs to.</summary>
public static class Humor
{
    /// <summary>The highest level.</summary>
    public const int Top = 10;

    /// <summary>The highest level that keeps <see cref="Bans"/>.</summary>
    public const int DryUpTo = 6;

    /// <summary>What levels 1 to 6 forbid.</summary>
    public const string Bans =
        "The wit is understatement, timing and precision — never decoration. Hard bans, no "
        + "exceptions: no similes and no borrowed images — the moment \"like\" or \"as if\" is about "
        + "to introduce a comparison, cut the comparison and let the plain fact land dry. No puns, no "
        + "whimsy, no zany exaggeration, no exclamation marks, and no \"...\" pauses for comic effect.";

    /// <summary>Added on a hit when the voice performs delivery notes.</summary>
    public const string Laughter =
        "Where a line is funny, a delivery note can carry the laugh: [laughs], [chuckles], "
        + "[stifles a laugh].";

    /// <summary>Holds for every level.</summary>
    private const string Limits =
        "Never at the Commander's expense. Every fact stays exactly true.";

    /// <summary>What the model may do at one level, without framing, or null at 0.</summary>
    public static string? Describe(int level, bool canBeDirected)
    {
        if (level <= 0)
        {
            return null;
        }

        var grade = Math.Min(level, Top) switch
        {
            1 or 2 => "At most one dry word or understatement; the rest is plain.",
            3 or 4 => "An occasional light touch of wit, dry and brief.",
            5 or 6 => "Wit is welcome: one clear dry joke, well timed.",
            7 or 8 =>
                "Openly funny. Puns, comparisons and absurd exaggeration are allowed; one or two jokes.",
            _ =>
                "As funny as a stand-up comedian: aim to make the Commander laugh out loud. Puns, "
                + "comparisons, absurdity and comic timing are all allowed.",
        };

        var text = $"{grade} {Limits}";

        if (level <= DryUpTo)
        {
            text += " " + Bans;
        }

        if (canBeDirected)
        {
            text += " " + Laughter;
        }

        return text;
    }

    /// <summary>The instruction for one line spoken by one speaker, or null at 0.</summary>
    public static string? ForLine(int level, bool canBeDirected) =>
        Describe(level, canBeDirected) is { } text
            ? $"Humor for this line, level {Math.Min(level, Top)} of {Top}: {text}"
            : null;

    /// <summary>The group a voice role speaks for, outside the ship.</summary>
    public static HumorGroup GroupOf(VoiceRole? role) =>
        role is VoiceRole.CarrierCaptain or VoiceRole.TowerControl ? HumorGroup.Carrier : HumorGroup.Npcs;

    /// <summary>One group's settings.</summary>
    public static HumorDial DialFor(PersonaSettings settings, HumorGroup group)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return group switch
        {
            HumorGroup.Cores => new HumorDial(settings.CoreHumor, settings.CoreHumorPercent),
            HumorGroup.Npcs => new HumorDial(settings.NpcHumor, settings.NpcHumorPercent),
            _ => new HumorDial(settings.CarrierHumor, settings.CarrierHumorPercent),
        };
    }
}

/// <summary>Decides, line by line, whether a group's line carries humor.</summary>
public sealed class HumorRoll(Random? choice = null)
{
    private readonly Random _choice = choice ?? Random.Shared;

    /// <summary>True on a hit. A dial at level 0 never hits and does not roll.</summary>
    public bool Hits(HumorDial dial)
    {
        ArgumentNullException.ThrowIfNull(dial);

        return dial.Level > 0 && _choice.Next(100) < Math.Clamp(dial.Percent, 0, 100);
    }

    /// <summary>The instruction for one line, or null on a miss.</summary>
    public string? ForLine(HumorDial dial, bool canBeDirected) =>
        Hits(dial) ? Humor.ForLine(dial.Level, canBeDirected) : null;
}
