using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class GecAllManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "edastro-gec-all-full",
        document => GecNotablePlacesService.Read(document),
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["[].avgStars"] = To("[].AvgStars"),
        ["[].callsign"] = To("[].Callsign"),
        ["[].categoryId"] = To("[].CategoryId"),
        ["[].categoryId2"] = To("[].CategoryId2"),
        ["[].coordinates[]"] = To("[].Position"),
        ["[].curation"] = To("[].Curation"),
        ["[].descriptionHtml"] = To("[].DescriptionHtml"),
        ["[].descriptionMardown"] = To("[].DescriptionMarkdown"),
        ["[].galMapSearch"] = To("[].System"),
        ["[].galMapUrl"] = To("[].GalMapUrl"),
        ["[].id"] = To("[].Id"),
        ["[].id64"] = To("[].SystemAddress"),
        ["[].latitude"] = To("[].Latitude"),
        ["[].longitude"] = To("[].Longitude"),
        ["[].mainImage"] = To("[].MainImage"),
        ["[].name"] = To("[].Name"),
        ["[].poiUrl"] = To("[].PoiUrl"),
        ["[].rare"] = To("[].Rare"),
        ["[].rating"] = To("[].Rating"),
        ["[].region"] = To("[].Region"),
        ["[].solDistance"] = To("[].SolDistance"),
        ["[].source"] = To("[].Source"),
        ["[].summary"] = To("[].Summary"),
        ["[].type"] = To("[].Type"),
        ["[].type2"] = To("[].Type2"),
        ["[].votes"] = To("[].Votes"),
        });
}
