using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Tests.Knowledge;

/// <summary>Reads a recorded Spansh carrier route into a <see cref="CarrierRoute"/>.</summary>
public static class CarrierRouteFixture
{
    public static CarrierRoute Read(string name, bool withPosition)
    {
        using var document = EmbeddedFixture.Json(name);

        return new CarrierRoute([.. document.RootElement.GetProperty("result").GetProperty("jumps").EnumerateArray()
            .Select(jump => new CarrierWaypoint(
                jump.GetProperty("name").GetString()!,
                jump.GetProperty("distance").GetDouble(),
                jump.GetProperty("distance_to_destination").GetDouble(),
                jump.GetProperty("fuel_in_tank").GetInt32(),
                jump.GetProperty("fuel_used").GetInt32(),
                jump.GetProperty("must_restock").GetInt32() != 0,
                jump.GetProperty("restock_amount").GetInt32(),
                jump.GetProperty("tritium_in_market").GetInt32(),
                jump.GetProperty("has_icy_ring").GetBoolean(),
                jump.GetProperty("is_system_pristine").GetBoolean(),
                jump.GetProperty("is_desired_destination").GetInt32() != 0)
            {
                Position = withPosition
                    ? new StarPosition(
                        jump.GetProperty("x").GetDouble(),
                        jump.GetProperty("y").GetDouble(),
                        jump.GetProperty("z").GetDouble())
                    : null,
            })]);
    }
}
