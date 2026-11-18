using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class SpanshExobiologyRouteManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "spansh-exobiology-route-sol-full",
        document => SpanshResponse.ReadExobiology(document.RootElement.GetProperty("result"))!,
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["created_at"] = Excluded("The job envelope."),
        ["job"] = Excluded("The job envelope."),
        ["parameters.avoid_thargoids"] = Excluded("Echo of a request parameter."),
        ["parameters.loop"] = Excluded("Echo of a request parameter."),
        ["parameters.max_distance"] = Excluded("Echo of a request parameter."),
        ["parameters.max_results"] = Excluded("Echo of a request parameter."),
        ["parameters.min_value"] = Excluded("Echo of a request parameter."),
        ["parameters.radius"] = Excluded("Echo of a request parameter."),
        ["parameters.range"] = Excluded("Echo of a request parameter."),
        ["parameters.source"] = Excluded("Echo of a request parameter."),
        ["result[].bodies[].distance_to_arrival"] = To("Stops[].Bodies[].DistanceToArrival"),
        ["result[].bodies[].estimated_mapping_value"] = To("Stops[].Bodies[].MappingValue"),
        ["result[].bodies[].estimated_scan_value"] = To("Stops[].Bodies[].ScanValue"),
        ["result[].bodies[].id"] = Excluded("A row's own id; the same value as id64."),
        ["result[].bodies[].id64"] = To("Stops[].Bodies[].BodyAddress"),
        ["result[].bodies[].landmark_value"] = To("Stops[].Bodies[].LandmarkValue"),
        ["result[].bodies[].landmarks[].count"] = To("Stops[].Bodies[].Species[].Count"),
        ["result[].bodies[].landmarks[].subtype"] = To("Stops[].Bodies[].Species[].Name"),
        ["result[].bodies[].landmarks[].type"] = To("Stops[].Bodies[].Species[].Genus"),
        ["result[].bodies[].landmarks[].value"] = To("Stops[].Bodies[].Species[].Value"),
        ["result[].bodies[].name"] = To("Stops[].Bodies[].Name"),
        ["result[].bodies[].subtype"] = To("Stops[].Bodies[].Subtype"),
        ["result[].bodies[].type"] = To("Stops[].Bodies[].Type"),
        ["result[].id64"] = To("Stops[].SystemAddress"),
        ["result[].jumps"] = To("Stops[].Jumps"),
        ["result[].name"] = To("Stops[].System"),
        ["result[].x"] = To("Stops[].Position.X"),
        ["result[].y"] = To("Stops[].Position.Y"),
        ["result[].z"] = To("Stops[].Position.Z"),
        ["state"] = Excluded("The job envelope."),
        ["status"] = Excluded("The job envelope."),
        ["updated_at"] = Excluded("The job envelope."),
        });
}
