using D47.Core.Adventures;
using D47.Core.Capabilities.Builtin;
using D47.Core.Persona;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>The story block tells the model what the Commander can say when an objective will not complete.</summary>
public sealed class AStuckCommanderIsToldHowToChangeTheObjectiveTests
{
    private static readonly DateTimeOffset Now = Accepted.AddHours(1);

    private static string Block(Adventure adventure) =>
        AdventureContext.Describe([AdventureFold.Start(adventure)], _ => null, Now)!;

    private static Adventure Chapter(AdventureTrigger? first = null)
    {
        var route = LanternRoute(Accepted) with { StoryId = "a-stock-story" };

        return first is null
            ? route
            : route with { Beats = [route.Beats[0] with { Trigger = first }, .. route.Beats.Skip(1)] };
    }

    [Fact]
    public void ARunningChapterNamesBothPhrasesAndSaysTheModelCannotMoveIt()
    {
        var block = Block(Chapter());

        Assert.Contains($"\"{AdventureCapability.ChangeObjectivePhrase}\"", block);
        Assert.Contains($"\"{AdventureCapability.RefuseBeatPhrase}\"", block);
        Assert.Contains("Change objective", block);
        Assert.Contains("Never ask this", block);
        Assert.Contains("you cannot complete, skip or change one", block);
    }

    [Fact]
    public void ABeaconScanNamesNeitherPhraseAndSaysTheScanIsNeeded()
    {
        var beacon = new AdventureTrigger { Kind = TriggerKind.Beacon, SystemAddress = GuardianCores.Beacons.Keys.First(), System = "A beacon" };
        var block = Block(Chapter(beacon));

        Assert.DoesNotContain(AdventureCapability.ChangeObjectivePhrase, block);
        Assert.DoesNotContain(AdventureCapability.RefuseBeatPhrase, block);
        Assert.Contains("the scan itself is the only way past it", block);
    }

    [Fact]
    public void AShortStoryNamesNoVoicePhrase()
    {
        var block = Block(LanternRoute(Accepted));

        Assert.DoesNotContain(AdventureCapability.ChangeObjectivePhrase, block);
        Assert.DoesNotContain(AdventureCapability.RefuseBeatPhrase, block);
        Assert.Contains("no voice phrase", block);
    }

    [Theory]
    [InlineData(AdventureCapability.ChangeObjectivePhrase, AdventureCapability.ChangeObjectiveTool)]
    [InlineData(AdventureCapability.RefuseBeatPhrase, AdventureCapability.RefuseBeatTool)]
    public void EveryPhraseTheBlockNamesIsAPhraseOfItsTool(string phrase, string tool)
    {
        var descriptor = AdventureCapability.Create();
        var definition = descriptor.Tools.Single(candidate => candidate.Name == tool);

        Assert.Contains(definition.Commands, command => command.Phrase == phrase);
        Assert.Contains($"\"{phrase}\"", Block(Chapter()));
    }
}
