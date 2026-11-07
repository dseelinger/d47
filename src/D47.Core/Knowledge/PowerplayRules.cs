using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>One merit modifier from Frontier's update notes.</summary>
public sealed record PowerplayRule(int Percent)
{
    /// <summary>The modifier as written in speech and text: "-35%", "+15%".</summary>
    public string Signed => Percent < 0 ? $"\u2212{-Percent}%" : $"+{Percent}%";
}

/// <summary>What the journal says about the merit modifier where the Commander is.</summary>
public enum MeritSituation
{
    None,

    /// <summary>The pledged Power's system, with nothing undermined this cycle.</summary>
    OwnNotUndermined,

    /// <summary>The pledged Power's system, undermined this cycle.</summary>
    OwnUndermined,

    /// <summary>The pledged Power's system, with no undermining figure in the journal.</summary>
    OwnUnderminingUnknown,

    /// <summary>Another Power's system.</summary>
    Rival,
}

/// <summary>Powerplay merit modifiers from Frontier's update notes.</summary>
public static class PowerplayRules
{
    /// <summary>Reinforcing the Commander's own Power's system.</summary>
    public static readonly PowerplayRule OwnReinforcement = new(-35);

    /// <summary>Reinforcing, in addition, when nothing undermined the system in the past 24 hours.</summary>
    public static readonly PowerplayRule OwnReinforcementNotUndermined = new(-20);

    /// <summary>Reinforcing, in addition, at the most undermined end of the sliding scale.</summary>
    public static readonly PowerplayRule OwnReinforcementMostUndermined = new(30);

    /// <summary>Undermining a rival Power's system.</summary>
    public static readonly PowerplayRule RivalUndermining = new(15);

    /// <summary>Acquiring an unoccupied system.</summary>
    public static readonly PowerplayRule UnoccupiedAcquisition = new(0);

    /// <summary>Undermining, in addition, in a system the galaxy map marks for undermining.</summary>
    public static readonly PowerplayRule MarkedUndermining = new(25);

    /// <summary>Reinforcing, in addition, in a system the galaxy map marks for reinforcement.</summary>
    public static readonly PowerplayRule MarkedReinforcement = new(35);

    /// <summary>Salvage collected in the Commander's own Power's system earns reinforcement merits only when handed in at a Power contact in that system; escape pods earn none.</summary>
    public static readonly PowerplayRule OwnSalvageHandIn = new(0);

    /// <summary>Which rule applies where the pledged Commander is; None for an unpledged Commander or a state the rules do not cover.</summary>
    public static MeritSituation Classify(PowerplayPledge pledge, JournalLocation location)
    {
        if (!pledge.IsPledged
            || location.ControllingPower is not { Length: > 0 } controlling
            || location.PowerplayState is not ("Exploited" or "Fortified" or "Stronghold"))
        {
            return MeritSituation.None;
        }

        if (!string.Equals(controlling, pledge.Power, StringComparison.OrdinalIgnoreCase))
        {
            return MeritSituation.Rival;
        }

        return location.PowerplayStateUndermining switch
        {
            null => MeritSituation.OwnUnderminingUnknown,
            0 => MeritSituation.OwnNotUndermined,
            _ => MeritSituation.OwnUndermined,
        };
    }

    /// <summary>The situation-line sentence for where the Commander is; null when there is none.</summary>
    public static string? SituationLine(PowerplayPledge pledge, JournalLocation location)
    {
        var reinforcing = $"reinforcing {OwnReinforcement.Signed}";
        var marked = $"{MarkedReinforcement.Signed} more if the galaxy map marks it for reinforcement";

        return Classify(pledge, location) switch
        {
            MeritSituation.OwnNotUndermined =>
                $"Merits here: {reinforcing}, and {OwnReinforcementNotUndermined.Signed} more because nobody has undermined this system this cycle; {marked}.",
            MeritSituation.OwnUndermined =>
                $"Merits here: {reinforcing}, and between {OwnReinforcementNotUndermined.Signed} and {OwnReinforcementMostUndermined.Signed} more by how much this system was undermined in the past 24 hours; {marked}.",
            MeritSituation.OwnUnderminingUnknown =>
                $"Merits here: {reinforcing}; {marked}.",
            MeritSituation.Rival =>
                $"Merits here: undermining {RivalUndermining.Signed}; {MarkedUndermining.Signed} more if the galaxy map marks it for undermining.",
            _ => null,
        };
    }
}
