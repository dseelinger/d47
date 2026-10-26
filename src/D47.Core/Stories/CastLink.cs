using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>Where the Commander is relative to their own fleet carrier; null light years when unknown.</summary>
public sealed record CarrierFix(bool Docked, double? LightYears)
{
    /// <summary>
    /// The fix for <paramref name="carrier"/> with the Commander in <paramref name="system"/> at <paramref name="here"/>: 0 light
    /// years in the same system, else the distance to the carrier system's position as <paramref name="positionOf"/> gives it.
    /// Null for a carrier the Commander does not own, or a squadron carrier.
    /// </summary>
    public static CarrierFix? Of(CarrierState? carrier, string? system, StarPosition? here, Func<string, StarPosition?> positionOf)
    {
        ArgumentNullException.ThrowIfNull(positionOf);

        if (carrier is not { Owned: true, IsSquadron: false })
        {
            return null;
        }

        if (carrier.StarSystem is not { Length: > 0 } carrierSystem)
        {
            return new(carrier.DockedAtOwnCarrier, null);
        }

        if (string.Equals(system, carrierSystem, StringComparison.OrdinalIgnoreCase))
        {
            return new(carrier.DockedAtOwnCarrier, 0);
        }

        return new(carrier.DockedAtOwnCarrier, here is { } from && positionOf(carrierSystem) is { } to ? from.DistanceTo(to) : null);
    }
}

/// <summary>How a story's cast member is heard: in person, over the carrier's link, or over their own.</summary>
public static class CastLink
{
    /// <summary>The weakest link a member on the carrier is heard through, however far away.</summary>
    public const double Weakest = 0.3;

    /// <summary>
    /// The link <paramref name="member"/> is heard through: null in person, else a strength 0 to 1. On the ship, or on the carrier
    /// with the Commander docked there, in person; on the carrier at a known distance, the carrier's link; otherwise the member's
    /// own <see cref="StorySpeaker.Link"/>.
    /// </summary>
    public static double? Of(StorySpeaker member, bool onShip, bool onCarrier, CarrierFix? carrier)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (onShip || (onCarrier && carrier is { Docked: true }))
        {
            return null;
        }

        return Away(onShip, onCarrier, carrier) is { } away ? Strength(away) : member.Link;
    }

    /// <summary>
    /// The light years to the carrier when a member is heard over its link: on the carrier and not the ship, the Commander not
    /// docked there, the distance known. Null otherwise.
    /// </summary>
    public static double? Away(bool onShip, bool onCarrier, CarrierFix? carrier) =>
        !onShip && onCarrier && carrier is { Docked: false, LightYears: { } away } ? away : null;

    /// <summary>The carrier link's strength at <paramref name="away"/> light years, never under <see cref="Weakest"/>.</summary>
    public static double Strength(double away) => Math.Max(LinkSignal.Strength(away), Weakest);
}
