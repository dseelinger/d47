using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class SpanshRouteManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "spansh-route-sol-colonia-full",
        document => SpanshResponse.ReadRoute(document.RootElement.GetProperty("result"))!,
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["created_at"] = Excluded("The job envelope."),
        ["job"] = Excluded("The job envelope."),
        ["parameters.efficiency"] = Excluded("Echo of a request parameter."),
        ["parameters.from"] = Excluded("Echo of a request parameter."),
        ["parameters.range"] = Excluded("Echo of a request parameter."),
        ["parameters.to"] = Excluded("Echo of a request parameter."),
        ["result.destination_system"] = To("Destination"),
        ["result.distance"] = To("TotalDistance"),
        ["result.efficiency"] = Excluded("Echo of a request parameter."),
        ["result.job"] = Excluded("The job id, echoed."),
        ["result.range"] = Excluded("Echo of a request parameter."),
        ["result.source_system"] = To("Origin"),
        ["result.system_jumps[].distance_jumped"] = To("Waypoints[].DistanceJumped"),
        ["result.system_jumps[].distance_left"] = To("Waypoints[].DistanceLeft"),
        ["result.system_jumps[].id64"] = To("Waypoints[].SystemAddress"),
        ["result.system_jumps[].jumps"] = To("Waypoints[].Jumps"),
        ["result.system_jumps[].neutron_star"] = To("Waypoints[].IsNeutron"),
        ["result.system_jumps[].system"] = To("Waypoints[].System"),
        ["result.system_jumps[].x"] = To("Waypoints[].Position.X"),
        ["result.system_jumps[].y"] = To("Waypoints[].Position.Y"),
        ["result.system_jumps[].z"] = To("Waypoints[].Position.Z"),
        ["result.total_jumps"] = To("ReportedJumps"),
        ["state"] = Excluded("The job envelope."),
        ["status"] = Excluded("The job envelope."),
        ["updated_at"] = Excluded("The job envelope."),
        });
}
