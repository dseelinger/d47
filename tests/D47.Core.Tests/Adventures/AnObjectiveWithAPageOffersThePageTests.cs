using D47.Core.Adventures;
using Xunit;

namespace D47.Core.Tests.Adventures;

public sealed class AnObjectiveWithAPageOffersThePageTests
{
    [Fact]
    public void AnEngineerObjectiveShowsThatEngineer()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Engineer, Engineer = "Felicity Farseer", Stage = "Unlocked" };

        var button = ShipObjective.For(trigger, []);

        Assert.NotNull(button);
        Assert.Equal(ShipObjectiveKind.ShowEngineer, button.Kind);
        Assert.Equal("Show Felicity Farseer", button.Label);
        Assert.Equal("Felicity Farseer", button.Engineer?.Name);
    }

    [Fact]
    public void AnEngineerTheDirectoryDoesNotKnowHasNoButton()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.Engineer, Engineer = "Nobody Atall", Stage = "Unlocked" };

        Assert.Null(ShipObjective.For(trigger, []));
    }

    [Fact]
    public void ADataSaleObjectiveShowsUnsoldData()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.DataSale, Count = 20_000_000, Organic = true };

        var button = ShipObjective.For(trigger, []);

        Assert.NotNull(button);
        Assert.Equal(ShipObjectiveKind.ShowUnsold, button.Kind);
        Assert.Equal("Show unsold data", button.Label);
    }

    [Fact]
    public void ASuitModObjectiveShowsSuits()
    {
        var trigger = new AdventureTrigger { Kind = TriggerKind.SuitMod, Count = 2 };

        var button = ShipObjective.For(trigger, []);

        Assert.NotNull(button);
        Assert.Equal(ShipObjectiveKind.ShowSuits, button.Kind);
        Assert.Equal("Show suits", button.Label);
    }

    [Fact]
    public void ACountedActivityObjectiveHasNoButton()
    {
        Assert.Null(ShipObjective.For(new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 5 }, []));
    }
}
