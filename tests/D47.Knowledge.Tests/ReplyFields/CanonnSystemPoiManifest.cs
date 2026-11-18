using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class CanonnSystemPoiManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "canonn-systempoi-synuefe-full",
        CanonnBiologyService.ReadRecord,
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["SAAsignals[].body"] = To("Signals[].Body"),
        ["SAAsignals[].count"] = To("Signals[].Count"),
        ["SAAsignals[].english_name"] = To("Signals[].Name"),
        ["SAAsignals[].hud_category"] = To("Signals[].Category"),
        ["ScanOrganic[].body"] = To("OrganicScans[].Body"),
        ["ScanOrganic[].english_name"] = To("OrganicScans[].Name"),
        ["ScanOrganic[].entryid"] = To("OrganicScans[].EntryId"),
        ["ScanOrganic[].hud_category"] = To("OrganicScans[].Category"),
        ["ScanOrganic[].index_id"] = To("OrganicScans[].IndexId"),
        ["ScanOrganic[].latitude"] = To("OrganicScans[].Latitude"),
        ["ScanOrganic[].longitude"] = To("OrganicScans[].Longitude"),
        ["ScanOrganic[].scanned"] = To("OrganicScans[].Scanned"),
        ["cmdrName"] = Excluded("Echo of a request parameter d47 does not send; null in every reply."),
        ["codex[].body"] = To("Codex[].Body"),
        ["codex[].english_name"] = To("Codex[].Name"),
        ["codex[].entryid"] = To("Codex[].EntryId"),
        ["codex[].hud_category"] = To("Codex[].Category"),
        ["codex[].index_id"] = To("Codex[].IndexId"),
        ["codex[].latitude"] = To("Codex[].Latitude"),
        ["codex[].longitude"] = To("Codex[].Longitude"),
        ["codex[].scanned"] = To("Codex[].Scanned"),
        ["odyssey"] = Excluded("Echo of a request parameter d47 does not send; null in every reply."),
        ["system"] = Excluded("The name asked for."),
        });
}
