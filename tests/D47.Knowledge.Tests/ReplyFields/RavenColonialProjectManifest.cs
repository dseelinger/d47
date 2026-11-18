using D47.Core.Knowledge;
using static D47.Knowledge.Tests.ReplyFields.ReplyMapping;

namespace D47.Knowledge.Tests.ReplyFields;

internal static class RavenColonialProjectManifest
{
    private const string Person = "Names or links to another person.";

    public static ReplyManifest Manifest { get; } = new(
        "raven-project-hr-4464-full",
        document => RavenColonialResponse.ReadProject(new PlannedSite("site", "Site", PlanStatus.Build), document, null),
        ["commodities", "commanders"],
        new Dictionary<string, ReplyMapping>
        {
        ["architectName"] = Excluded(Person),
        ["bodyFeatures[]"] = To("BodyFeatures[]"),
        ["bodyName"] = To("BodyName"),
        ["bodyNum"] = To("BodyNum"),
        ["bodyType"] = To("BodyType"),
        ["buildId"] = To("BuildId"),
        ["buildName"] = To("BuildName"),
        ["buildType"] = To("BuildType"),
        ["commanders"] = Excluded("Names another person; counted in Commanders."),
        ["commodities"] = To("Needed"),
        ["complete"] = To("Complete"),
        ["discordLink"] = Excluded(Person),
        ["eTag"] = To("ETag"),
        ["factionName"] = To("FactionName"),
        ["isPrimaryPort"] = To("IsPrimaryPort"),
        ["linkedFC[].displayName"] = Excluded(Person),
        ["linkedFC[].marketId"] = To("Carriers[].MarketId"),
        ["linkedFC[].name"] = To("Carriers[].Name"),
        ["marketId"] = To("MarketId"),
        ["maxNeed"] = To("MaxNeed"),
        ["notes"] = Excluded(Person),
        ["reserveLevel"] = To("ReserveLevel"),
        ["starPos[]"] = To("StarPos[]"),
        ["sumNeed"] = To("SumNeed"),
        ["systemAddress"] = To("SystemAddress"),
        ["systemFeatures"] = To("SystemFeatures"),
        ["systemName"] = To("SystemName"),
        ["timeCompleted"] = To("Completed"),
        ["timeDue"] = To("Due"),
        ["timestamp"] = To("Timestamp"),
        ["timestarted"] = To("Started"),
        });
}
