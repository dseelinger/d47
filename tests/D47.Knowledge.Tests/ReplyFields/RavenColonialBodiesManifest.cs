using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class RavenColonialBodiesManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "raven-bodies-hr-4464-full",
        document => RavenColonialResponse.ReadBodies(document),
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["[].distLS"] = To("[].DistanceLs"),
        ["[].features[]"] = To("[].Features[]"),
        ["[].gravity"] = To("[].Gravity"),
        ["[].name"] = To("[].Name"),
        ["[].num"] = To("[].Num"),
        ["[].parents[]"] = To("[].Parents[]"),
        ["[].radius"] = To("[].Radius"),
        ["[].subType"] = To("[].SubType"),
        ["[].temp"] = To("[].Temperature"),
        ["[].type"] = To("[].Type"),
        });
}
