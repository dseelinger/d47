using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class ARescueObjectiveNamesItsItemsTests
{
    private static AdventureTrigger Rescue(string? filter = null) =>
        new() { Kind = TriggerKind.Rescue, Count = 1, Filter = filter };

    private static string Context(AdventureTrigger rescue)
    {
        var route = LanternRoute(Accepted);
        var beat = route.Beats[0] with { Steps = [new BeatStep { Trigger = rescue }] };
        var standing = AdventureFold.Start(route with { Beats = [beat, .. route.Beats.Skip(1)] });
        return AdventureContext.Describe([standing], _ => null, Accepted.AddHours(1))!;
    }

    [Fact]
    public void AnUnfilteredHandOffNamesEveryItemAndTheContact()
    {
        var text = Rescue().HandOff();

        Assert.Contains("escape pods", text);
        Assert.Contains("wreckage components", text);
        Assert.Contains("black boxes", text);
        Assert.Contains("Search and Rescue contact", text);
    }

    [Fact]
    public void AFilteredHandOffNamesOnlyItsItem()
    {
        var text = Rescue("wreckagecomponents").HandOff();

        Assert.Contains("Wreckage components", text);
        Assert.DoesNotContain("black box", text);
        Assert.DoesNotContain("escape pod", text);
    }

    [Fact]
    public void ABlackBoxNamesDegradedEmissionsAndWreckageComponentsDoNot()
    {
        Assert.Contains("Degraded Emissions", Rescue("usscargoblackbox").HandOff());
        Assert.Contains("Power Wreckage Signature", Rescue("wreckagecomponents").HandOff());
        Assert.DoesNotContain("Degraded Emissions", Rescue("wreckagecomponents").HandOff());
    }

    [Fact]
    public void TheStatusTheModelReadsExplainsTheRescueObjective()
    {
        var block = Context(Rescue());

        Assert.Contains("Search and Rescue contact", block);
        Assert.Contains("Power Wreckage Signature", block);
        Assert.Contains("Degraded Emissions", block);
    }

    [Fact]
    public void AStepHandOffSaysNextOnce()
    {
        var route = LanternRoute(Accepted);
        var beat = route.Beats[0] with { Steps = [new BeatStep { Trigger = Rescue() }, new BeatStep { Trigger = Rescue() }] };
        var moment = new AdventureMoment("F1", route with { Beats = [beat, .. route.Beats.Skip(1)] }, 0, Accepted, Step: 0);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(moment.HandOff!, "Next:"));
    }
}
