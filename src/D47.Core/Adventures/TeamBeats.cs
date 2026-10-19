using System.Globalization;
using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.Core.Adventures;

/// <summary>Which carrier, squadron and team beats a chapter may ask for, from what the Commander owns, belongs to and holds.</summary>
public static class TeamBeats
{
    /// <summary>The kinds this class decides; every other kind is always allowed.</summary>
    public static readonly IReadOnlyList<TriggerKind> Kinds =
        [TriggerKind.CarrierBuy, TriggerKind.CarrierJump, TriggerKind.Wing, TriggerKind.Multicrew, TriggerKind.Squadron, TriggerKind.SquadronFound, TriggerKind.Pledge, TriggerKind.PowerRank];

    /// <summary>Why a beat of this kind cannot stand for this Commander, or null when it can.</summary>
    public static string? Why(TriggerKind kind, bool ownsCarrier, bool inSquadron, long? credits, PowerplayPledge? pledge = null) => kind switch
    {
        TriggerKind.CarrierBuy when ownsCarrier => "the Commander already owns a fleet carrier",
        TriggerKind.CarrierBuy when !Holds(credits, ChapterFit.CarrierBuyNeeded) => Short(ChapterFit.CarrierBuyNeeded),
        TriggerKind.CarrierJump when !ownsCarrier => "the Commander owns no fleet carrier",
        TriggerKind.Squadron when inSquadron => "the Commander is already in a squadron",
        TriggerKind.SquadronFound when inSquadron => "the Commander is already in a squadron",
        TriggerKind.SquadronFound when !Holds(credits, ChapterFit.SquadronFoundNeeded) => Short(ChapterFit.SquadronFoundNeeded),
        TriggerKind.Pledge when pledge is not { IsKnown: true } => "d47 has not read whether the Commander is pledged to a Power",
        TriggerKind.Pledge when pledge.IsPledged => "the Commander is already pledged to a Power",
        TriggerKind.PowerRank when pledge is not { IsPledged: true } => "the Commander is not pledged to a Power",
        _ => null,
    };

    private static bool Holds(long? credits, long threshold) => credits >= threshold;

    private static string Short(long threshold) =>
        $"the Commander has fewer than {threshold.ToString("N0", CultureInfo.InvariantCulture)} credits at the last load";
}
