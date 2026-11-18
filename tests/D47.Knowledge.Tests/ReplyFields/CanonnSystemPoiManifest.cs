using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class CanonnSystemPoiManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "canonn-systempoi-synuefe-full",
        document => CanonnBiologyService.Read("Synuefe XO-P c22-17", document),
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["SAAsignals[].body"] = Pending,
        ["SAAsignals[].count"] = Pending,
        ["SAAsignals[].english_name"] = Pending,
        ["SAAsignals[].hud_category"] = Pending,
        ["ScanOrganic[].body"] = Pending,
        ["ScanOrganic[].english_name"] = Pending,
        ["ScanOrganic[].entryid"] = Pending,
        ["ScanOrganic[].hud_category"] = Pending,
        ["ScanOrganic[].index_id"] = Pending,
        ["ScanOrganic[].latitude"] = Pending,
        ["ScanOrganic[].longitude"] = Pending,
        ["ScanOrganic[].scanned"] = Pending,
        ["cmdrName"] = Excluded("Echo of a request parameter d47 does not send; null in every reply."),
        ["codex[].body"] = To("[].Body"),
        ["codex[].english_name"] = To("[].Name"),
        ["codex[].entryid"] = To("[].EntryId"),
        ["codex[].hud_category"] = Excluded("Only Biology rows are kept."),
        ["codex[].index_id"] = Pending,
        ["codex[].latitude"] = To("[].Latitude"),
        ["codex[].longitude"] = To("[].Longitude"),
        ["codex[].scanned"] = Pending,
        ["odyssey"] = Excluded("Echo of a request parameter d47 does not send; null in every reply."),
        ["system"] = Excluded("The name asked for."),
        });
}
