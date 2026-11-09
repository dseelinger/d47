namespace D47.Core.Journal;

/// <summary>The ship whose jump range sets a <see cref="SpanReach"/>.</summary>
/// <param name="Where">The system a stored ship is in; the current system when it is aboard; null when unknown.</param>
public sealed record SpanShip(int? ShipId, string? Hull, string? Name, string? Where, bool Aboard);

/// <summary>How far a chapter that travels in the black may reach, from the fleet's best jump range.</summary>
/// <param name="LightYears">Straight-line light years.</param>
/// <param name="Ship">The ship that gives <paramref name="LightYears"/>; null when the carrier floor gives it.</param>
public sealed record SpanReach(double LightYears, SpanShip? Ship)
{
    public const double JumpsPerSpan = 100;

    /// <summary>Jumps of the best jump range in a session's flying.</summary>
    public const double JumpsPerSession = 33;

    public const double CarrierFloorLightYears = 5000;

    /// <summary>The best jump range in the fleet; null when no ship has a known one.</summary>
    public double? JumpRange { get; init; }

    /// <summary>How far a session flies: <see cref="JumpsPerSession"/> jumps of <see cref="JumpRange"/>, or that share of <see cref="LightYears"/> when no range is known.</summary>
    public double SessionLightYears => JumpRange is { } range ? range * JumpsPerSession : LightYears * JumpsPerSession / JumpsPerSpan;

    /// <summary>Null when no ship has a known jump range and there is no carrier.</summary>
    public static SpanReach? Of(CommanderGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var aboard = state.Ship is { IsKnown: true, MaxJumpRange: > 0 } ? state.Ship : null;

        var best = aboard;

        foreach (var remembered in state.Loadouts.Ships.Values.Select(ship => ship.Loadout))
        {
            if (remembered.MaxJumpRange is not > 0 || (aboard is not null && remembered.ShipId == aboard.ShipId))
            {
                continue;
            }

            if (best is null || remembered.MaxJumpRange > best.MaxJumpRange)
            {
                best = remembered;
            }
        }

        var carrier = state.Carrier.Owned;

        if (best?.MaxJumpRange is not { } range)
        {
            return carrier ? new SpanReach(CarrierFloorLightYears, null) : null;
        }

        var reach = range * JumpsPerSpan;

        if (carrier && reach < CarrierFloorLightYears)
        {
            return new SpanReach(CarrierFloorLightYears, null) { JumpRange = range };
        }

        var isAboard = ReferenceEquals(best, aboard);
        var stored = isAboard ? null : state.Fleet.Ships.FirstOrDefault(ship => ship.ShipId == best.ShipId);
        var where = isAboard ? state.Location.StarSystem : stored is { HasSystem: true } ? stored.StarSystem : null;

        return new SpanReach(reach, new SpanShip(best.ShipId, best.TypeSaid, best.Name, where, isAboard)) { JumpRange = range };
    }
}
