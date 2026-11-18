using D47.Core.Knowledge;
using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class RavenColonialSitesManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "raven-sites-hr-4464-full",
        document => RavenColonialResponse.ReadSites(document, []),
        [],
        new Dictionary<string, ReplyMapping>
        {
        ["[].bodyNum"] = To("[].BodyNum"),
        ["[].buildId"] = To("[].BuildId"),
        ["[].buildType"] = To("[].BuildType"),
        ["[].id"] = To("[].SiteId"),
        ["[].marketId"] = To("[].MarketId"),
        ["[].name"] = To("[].Name"),
        ["[].status"] = To("[].Status"),
        });
}
