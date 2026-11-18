using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class RavenColonialCarriersManifest
{
    public static ReplyManifest Manifest { get; } = new(
        "raven-project-fc-hr-4464-full",
        document => RavenColonialResponse.ReadCarrierCargo(document),
        [""],
        new Dictionary<string, ReplyMapping>
        {
        [""] = To("{}"),
        });
}
