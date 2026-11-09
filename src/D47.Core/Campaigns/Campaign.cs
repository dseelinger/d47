using System.Collections.Immutable;
using D47.Core.Journal;

namespace D47.Core.Campaigns;

/// <summary>What the Commander means to do with the campaign faction in one system.</summary>
public enum SystemIntent
{
    Hold,
    Grow,
    ExpandFromHere,
    StayUnder75,
    TakeControl,
    LeaveAlone,
}

/// <summary>The Commander's BGS work of one kind on a resolved front: signed marks for missions, credits otherwise.</summary>
public sealed record ChronicleWork(BgsWorkKind Kind, long Amount);

/// <summary>
/// A resolved front: what kind it was, where, against whom, how it ended, the ticks it spanned and the
/// Commander's own BGS work on it.
/// </summary>
public sealed record ChronicleEntry(
    string Kind,
    string System,
    string? Opponent,
    string Outcome,
    IReadOnlyList<DateTimeOffset> Ticks,
    IReadOnlyList<ChronicleWork> Work)
{
    /// <summary>The Chronicler's passage for this entry, written once and kept; null until written.</summary>
    public string? Legend { get; init; }
}

/// <summary>The one front the Commander is working: where it is, when it was taken and the BGS ticks it has spanned.</summary>
public sealed record ChosenFront(FrontKind Kind, string System, DateTimeOffset Since, IReadOnlyList<DateTimeOffset> Ticks)
{
    /// <summary>The BGS tick the Liaison last gave its dispatch for, or null.</summary>
    public DateTimeOffset? Reported { get; init; }
}

/// <summary>A kind of campaign arc.</summary>
public enum ArcKind
{
    Nemesis,
}

/// <summary>
/// A campaign arc under way: its kind, the rival faction it is about, the leader invented for that rival, when it began,
/// and how many chronicle entries it has already answered.
/// </summary>
public sealed record CampaignArc(ArcKind Kind, string Rival, string Leader, DateTimeOffset Since, int Answered);

/// <summary>
/// The Ideology arc under way: the rival whose government differs from the faction's, the two governments when it opened,
/// the systems whose fronts it has broadcast on, the elections it has framed (system and state) and how many chronicle
/// entries it has answered.
/// </summary>
public sealed record IdeologyArc(
    string Rival,
    string OurGovernment,
    string TheirGovernment,
    DateTimeOffset Since,
    int Answered,
    IReadOnlyList<string> Fronts,
    IReadOnlyList<string> Elections);

/// <summary>Which of the two councillors a front suits.</summary>
public enum CouncilSide
{
    Hawk,
    Dove,
}

/// <summary>A front the Commander chose, which councillor it sided with and when.</summary>
public sealed record CouncilSiding(string System, CouncilSide Sided, DateTimeOffset At);

/// <summary>
/// The faction's council: the Hawk and the Dove, invented once; when it opened; the Since of the last front chosen that has
/// been answered; and every siding so far.
/// </summary>
public sealed record CampaignCouncil(string Hawk, string Dove, DateTimeOffset Since, DateTimeOffset? Noted, IReadOnlyList<CouncilSiding> Sidings);

/// <summary>A bad state active in a system the faction controls: where, which state, the rival blamed, when it landed, and whether the rival's exploit has been broadcast.</summary>
public sealed record OpenCrisis(string System, string State, string? Blamed, DateTimeOffset Since, bool Exploited);

/// <summary>One Commander's campaign: the faction, an intent per system, the front being worked, the chronicle of resolved fronts and the arc.</summary>
public sealed record Campaign
{
    public static readonly Campaign None = new();

    /// <summary>The faction the Commander named, or null to follow the squadron's faction.</summary>
    public string? Faction { get; init; }

    /// <summary>The intents the Commander has set, by system name, case ignored.</summary>
    public ImmutableDictionary<string, SystemIntent> Intents { get; init; } =
        ImmutableDictionary.Create<string, SystemIntent>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The front being worked, or null.</summary>
    public ChosenFront? Chosen { get; init; }

    public IReadOnlyList<ChronicleEntry> Chronicle { get; init; } = [];

    /// <summary>The arc under way, or null.</summary>
    public CampaignArc? Arc { get; init; }

    /// <summary>The Informant and the leaks given, or null before the first leak.</summary>
    public CampaignInformant? Informant { get; init; }

    /// <summary>The Ideology arc under way, or null.</summary>
    public IdeologyArc? Ideology { get; init; }

    /// <summary>The Hawk and the Dove and the front choices they have answered, or null before two fronts were open.</summary>
    public CampaignCouncil? Council { get; init; }

    /// <summary>The crises open now, one per system and state.</summary>
    public IReadOnlyList<OpenCrisis> Crises { get; init; } = [];

    /// <summary>The faction named, else <paramref name="squadronFaction"/>.</summary>
    public string? FactionOr(string? squadronFaction) =>
        Faction is { Length: > 0 } named ? named : squadronFaction is { Length: > 0 } ? squadronFaction : null;

    /// <summary>The intent set for <paramref name="system"/>, else Hold where the faction controls it and LeaveAlone where it does not.</summary>
    public SystemIntent IntentFor(string system, bool controls) =>
        Intents.TryGetValue(system, out var set) ? set : DefaultIntent(controls);

    /// <summary>The intent of a system with none set: Hold where the faction controls it, else LeaveAlone.</summary>
    public static SystemIntent DefaultIntent(bool controls) => controls ? SystemIntent.Hold : SystemIntent.LeaveAlone;

    public bool IsEmpty => Faction is null && Intents.IsEmpty && Chosen is null && Chronicle.Count == 0 && Arc is null && Informant is null && Ideology is null && Council is null && Crises.Count == 0;
}
