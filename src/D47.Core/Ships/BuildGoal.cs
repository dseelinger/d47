namespace D47.Core.Ships;

/// <summary>What a build is for.</summary>
public enum BuildRole
{
    /// <summary>Survivability and sustained damage, mixed by <see cref="CombatProfile"/>.</summary>
    Combat,

    /// <summary>Laden jump range.</summary>
    Exploration,

    /// <summary>Tons of hold.</summary>
    Cargo,
}

/// <summary>Which fight a combat build is for, which decides the damage mix it defends against.</summary>
public enum CombatProfile
{
    Bounty,
    ConflictZone,

    /// <summary>Caustic and thermal damage taken; only AX and Guardian weapons count toward damage.</summary>
    Thargoid,
}

/// <summary>One figure a floor can be set on.</summary>
public enum BuildFigure
{
    LadenJump,
    UnladenJump,
    Cargo,
    Shields,
    Hull,
    SustainedDps,
}

/// <summary>How far a goal reaches for its <see cref="BuildGoal.Target"/>.</summary>
public enum GoalMode
{
    /// <summary>The weighted sum of every role's score.</summary>
    Weighted,

    /// <summary>The target alone, keeping only the stated must-haves.</summary>
    Only,

    /// <summary>The target, with every other figure held at least at the start build's.</summary>
    KeepRest,

    /// <summary>The fewest slot changes, then the lowest cost, that raise the target at all.</summary>
    AnyGain,
}

/// <summary>A hard lower limit on one figure.</summary>
public sealed record BuildFloor(BuildFigure Figure, double AtLeast);

/// <summary>What a Commander wants a build to do, as <see cref="BuildSearch"/> reads it.</summary>
/// <param name="Weights">Each role's share of the score; normalised to sum to 1.</param>
/// <param name="Floors">Hard limits; a weight never becomes one.</param>
/// <param name="MustHaves">Module names as <c>EliteSpecifications</c> spells them, one entry per module.</param>
/// <param name="Credits">What may be spent, or null for no limit.</param>
/// <param name="Engineering">Whether a move may give a module a blueprint and an experimental.</param>
/// <param name="Target">The role <see cref="GoalMode.Only"/>, <see cref="GoalMode.KeepRest"/> and <see cref="GoalMode.AnyGain"/> raise; required by them.</param>
public sealed record BuildGoal(
    IReadOnlyDictionary<BuildRole, double> Weights,
    CombatProfile Combat,
    IReadOnlyList<BuildFloor> Floors,
    IReadOnlyList<string> MustHaves,
    long? Credits,
    bool Engineering,
    GoalMode Mode = GoalMode.Weighted,
    BuildRole? Target = null);

/// <summary>The build <see cref="BuildSearch.Find"/> found, against the one it started from.</summary>
/// <param name="Plan">The start build with every change applied.</param>
/// <param name="Changes">One row per slot whose plan differs from the start's, in slot-layout order.</param>
/// <param name="Credits">What the changed modules cost to buy; nothing is credited for what they replace.</param>
/// <param name="Unmet">The first floor the plan falls short of, or null where every floor is met.</param>
/// <param name="Missing">Must-haves the plan does not carry, one entry per missing module.</param>
public sealed record BuildResult(
    ShipBuild Plan,
    BuildGauges Gauges,
    BuildGauges StartGauges,
    IReadOnlyList<SlotChange> Changes,
    long Credits,
    BuildFloor? Unmet,
    IReadOnlyList<string> Missing);
