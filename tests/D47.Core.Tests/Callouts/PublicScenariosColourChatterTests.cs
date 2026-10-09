using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Callouts;

[Trait("Category", "Integration")]
public class PublicScenariosColourChatterTests
{
    private const string Scenario = "Hauling refugees out of the Pleiades.";

    [Fact]
    public void ChatterWithoutAScenarioIsTheInstructionItAlwaysWas()
    {
        var plain = NpcChatter.Instruction(NpcChatterKind.Passersby);

        Assert.Equal(plain, NpcChatter.Instruction(NpcChatterKind.Passersby, scenario: null));
        Assert.Equal(plain, NpcChatter.Instruction(NpcChatterKind.Passersby, scenario: "  "));
        Assert.DoesNotContain("pilots around the Commander", plain);
    }

    [Fact]
    public void AScenarioAppearsOnceAndMayColourAnExchangeWithoutBeingRequired()
    {
        var text = NpcChatter.Instruction(NpcChatterKind.Passersby, scenario: Scenario);

        Assert.Equal(1, text.Split(Scenario).Length - 1);
        Assert.StartsWith(NpcChatter.Instruction(NpcChatterKind.Passersby), text);
        Assert.Contains("may be coloured by this where it fits; most exchanges do not mention it", text);
        Assert.Contains("nobody asks the Commander about it", text);
    }

    [Theory]
    [InlineData(ScenarioAudience.Aboard, false)]
    [InlineData(ScenarioAudience.Carrier, false)]
    [InlineData(ScenarioAudience.Public, true)]
    public void OnlyAPublicScenarioReachesChatter(ScenarioAudience audience, bool reaches)
    {
        Assert.Equal(reaches ? Scenario : null, NpcChatter.ScenarioFor(audience, Scenario));
    }

    [Fact]
    public void APublicAudienceWithNoScenarioSendsNothing()
    {
        Assert.Null(NpcChatter.ScenarioFor(ScenarioAudience.Public, " "));
    }
}
