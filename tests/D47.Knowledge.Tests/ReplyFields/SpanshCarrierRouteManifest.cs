using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class SpanshCarrierRouteManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "spansh-fleetcarrier-route-sol-colonia-sol",
        document => SpanshResponse.ReadCarrier(document.RootElement.GetProperty("result"))!,
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["created_at"] = Excluded("The job envelope."),
        ["job"] = Excluded("The job envelope."),
        ["parameters.calculate_starting_fuel"] = Excluded("Echo of a request parameter."),
        ["parameters.capacity"] = Excluded("Echo of a request parameter."),
        ["parameters.capacity_used"] = Excluded("Echo of a request parameter."),
        ["parameters.current_fuel"] = Excluded("Echo of a request parameter."),
        ["parameters.destination_systems[]"] = Excluded("Echo of a request parameter."),
        ["parameters.mass"] = Excluded("Echo of a request parameter."),
        ["parameters.source_system"] = Excluded("Echo of a request parameter."),
        ["parameters.tritium_amount"] = Excluded("Echo of a request parameter."),
        ["result.calculate_starting_fuel"] = Excluded("Echo of a request parameter."),
        ["result.capacity"] = Excluded("Echo of a request parameter."),
        ["result.capacity_used"] = Excluded("Echo of a request parameter."),
        ["result.destinations[]"] = Excluded("Echo of a request parameter."),
        ["result.fuel_loaded"] = Excluded("Echo of a request parameter."),
        ["result.jumps[].distance"] = To("Waypoints[].Distance"),
        ["result.jumps[].distance_to_destination"] = To("Waypoints[].DistanceToDestination"),
        ["result.jumps[].fuel_in_tank"] = To("Waypoints[].FuelInTank"),
        ["result.jumps[].fuel_used"] = To("Waypoints[].FuelUsed"),
        ["result.jumps[].has_icy_ring"] = To("Waypoints[].HasIcyRing"),
        ["result.jumps[].id64"] = To("Waypoints[].SystemAddress"),
        ["result.jumps[].is_desired_destination"] = To("Waypoints[].IsDestination"),
        ["result.jumps[].is_system_pristine"] = To("Waypoints[].IsSystemPristine"),
        ["result.jumps[].must_restock"] = To("Waypoints[].MustRestock"),
        ["result.jumps[].name"] = To("Waypoints[].Name"),
        ["result.jumps[].restock_amount"] = To("Waypoints[].RestockAmount"),
        ["result.jumps[].tritium_in_market"] = To("Waypoints[].ReserveInHold"),
        ["result.jumps[].x"] = To("Waypoints[].Position.X"),
        ["result.jumps[].y"] = To("Waypoints[].Position.Y"),
        ["result.jumps[].z"] = To("Waypoints[].Position.Z"),
        ["result.mass"] = To("Mass"),
        ["result.source"] = Excluded("Echo of a request parameter."),
        ["result.tritium_stored"] = Excluded("Echo of a request parameter."),
        ["state"] = Excluded("The job envelope."),
        ["status"] = Excluded("The job envelope."),
        ["updated_at"] = Excluded("The job envelope."),
        });
}
