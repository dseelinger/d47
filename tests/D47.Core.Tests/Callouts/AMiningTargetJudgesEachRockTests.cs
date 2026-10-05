using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Mining;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>With a mining target set, the prospector names only the target and judges the rock (#607).</summary>
public class AMiningTargetJudgesEachRockTests
{
    private static CalloutContext Context(bool priming, params string[] lines) =>
        new(
            DateTimeOffset.UnixEpoch,
            IsPriming: priming,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [.. lines.Select(line =>
            {
                Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
                return parsed!;
            })]);

    private static string Rock(string materials, double remaining = 100, int minute = 0) =>
        "{\"timestamp\":\"2026-08-16T10:" + minute.ToString("00") + ":00Z\",\"event\":\"ProspectedAsteroid\","
        + "\"Materials\":[" + materials + "],"
        + "\"Content\":\"$AsteroidMaterialContent_Low;\",\"Content_Localised\":\"Material Content: Low\","
        + "\"Remaining\":" + remaining.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";

    private static readonly string Platinum58 = Rock("""{"Name":"Platinum","Proportion":58.255268}""");

    private static ProspectorCallout Targeting(string material, double? percent) =>
        new() { Target = () => new MiningTarget(material, percent) };

    private static string Said(ProspectorCallout callout, string rock) =>
        Assert.Single(callout.Examine(Context(false, rock))).Text;

    [Fact]
    public void ARockAboveTheTargetSaysSo() =>
        Assert.Equal("Platinum, 58.3%. Above your target.", Said(Targeting("Platinum", 25), Platinum58));

    [Fact]
    public void ARockBelowTheTargetSaysSo() =>
        Assert.Equal(
            "Platinum, 18%. Below your target.",
            Said(Targeting("Platinum", 25), Rock("""{"Name":"Platinum","Proportion":18.0}""")));

    [Fact]
    public void ARockExactlyAtTheTargetIsAboveIt() =>
        Assert.Equal(
            "Platinum, 25%. Above your target.",
            Said(Targeting("Platinum", 25), Rock("""{"Name":"Platinum","Proportion":25.0}""")));

    [Fact]
    public void ARockWithoutTheTargetSaysOnlyThat() =>
        Assert.Equal(
            "No platinum.",
            Said(
                Targeting("Platinum", 25),
                Rock("""{"Name":"tritium","Proportion":23.4},{"Name":"water","Proportion":9.1}""")));

    [Fact]
    public void OtherMaterialsAreNotReadOut() =>
        Assert.Equal(
            "Platinum, 30%. Above your target.",
            Said(
                Targeting("Platinum", 25),
                Rock("""{"Name":"Osmium","Proportion":40.0},{"Name":"Platinum","Proportion":30.0}""")));

    [Fact]
    public void ATargetWithNoPercentageKeepsTheSessionBest()
    {
        var callout = Targeting("Platinum", null);

        Assert.Equal("Platinum, 31%.", Said(callout, Rock("""{"Name":"Platinum","Proportion":31.0}""", minute: 0)));
        Assert.Equal(
            "Platinum, 40%. Best you have found this session.",
            Said(
                callout,
                Rock("""{"Name":"Osmium","Proportion":50.0},{"Name":"Platinum","Proportion":40.0}""", minute: 1)));
    }

    [Fact]
    public void AMinedOutRockSaysSoWithATarget() =>
        Assert.Equal(
            "Platinum, 58.3%. Above your target. Already mined.",
            Said(Targeting("Platinum", 25), Rock("""{"Name":"Platinum","Proportion":58.255268}""", remaining: 0)));

    [Fact]
    public void AMinedOutRockSaysSoWithoutATarget() =>
        Assert.Equal(
            "Tritium, 23.1%, plus Liquid oxygen at 7.8%. Already mined.",
            Said(
                new ProspectorCallout(),
                Rock(
                    """{"Name":"tritium","Proportion":23.144218},{"Name":"liquidoxygen","Name_Localised":"Liquid oxygen","Proportion":7.832518}""",
                    remaining: 0)));

    [Fact]
    public void APartlyMinedRockIsAlreadyMined() =>
        Assert.EndsWith(
            "Already mined.",
            Said(new ProspectorCallout(), Rock("""{"Name":"Platinum","Proportion":30.0}""", remaining: 52.5)),
            StringComparison.Ordinal);

    /// <summary>The journal and the hotspot catalogue spell some materials differently.</summary>
    [Theory]
    [InlineData("Low Temperature Diamonds", """{"Name":"LowTemperatureDiamond","Name_Localised":"Low Temp. Diamonds","Proportion":12.0}""")]
    [InlineData("Void Opal", """{"Name":"Opal","Name_Localised":"Void Opal","Proportion":12.0}""")]
    [InlineData("Liquid oxygen", """{"Name":"LiquidOxygen","Name_Localised":"Liquid oxygen","Proportion":12.0}""")]
    [InlineData("Methane Clathrate", """{"Name":"MethaneClathrate","Name_Localised":"Methane Clathrate","Proportion":12.0}""")]
    [InlineData("Gold", """{"Name":"gold","Proportion":12.0}""")]
    public void TheTargetIsFoundUnderTheJournalsSpelling(string material, string row) =>
        Assert.Equal($"{material}, 12%. Above your target.", Said(Targeting(material, 10), Rock(row)));

    [Fact]
    public void ThePrimingBacklogSaysNothingWithATarget() =>
        Assert.Empty(Targeting("Platinum", 25).Examine(Context(true, Platinum58)));
}
