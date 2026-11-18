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
        ["[].avgStars"] = Pending,
        ["[].callsign"] = Pending,
        ["[].categoryId"] = Pending,
        ["[].categoryId2"] = Pending,
        ["[].coordinates[]"] = To("[].Position"),
        ["[].curation"] = Pending,
        ["[].descriptionHtml"] = Pending,
        ["[].descriptionMardown"] = Pending,
        ["[].galMapSearch"] = To("[].System"),
        ["[].galMapUrl"] = Pending,
        ["[].id"] = Pending,
        ["[].id64"] = To("[].SystemAddress"),
        ["[].latitude"] = Pending,
        ["[].longitude"] = Pending,
        ["[].mainImage"] = Pending,
        ["[].name"] = To("[].Name"),
        ["[].poiUrl"] = Pending,
        ["[].rare"] = Pending,
        ["[].rating"] = To("[].Rating"),
        ["[].region"] = Pending,
        ["[].solDistance"] = Pending,
        ["[].source"] = Pending,
        ["[].summary"] = To("[].Summary"),
        ["[].type"] = To("[].Type"),
        ["[].type2"] = Pending,
        ["[].votes"] = Pending,
        });
}
