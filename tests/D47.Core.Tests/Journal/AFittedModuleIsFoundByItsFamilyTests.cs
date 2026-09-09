using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// <see cref="ShipLoadout.Fitted"/>: does this ship carry a module of that symbol family, and what does
/// it answer when it cannot tell.
/// </summary>
public class AFittedModuleIsFoundByItsFamilyTests
{
    private static ShipLoadout LoadoutWith(string modules) => ShipLoadout.Unknown.Apply(
        Parsed($$"""
            {"timestamp":"3311-01-01T00:00:00Z","event":"Loadout","Ship":"Python","Modules":[{{modules}}]}
            """));

    private static JournalEvent Parsed(string json)
    {
        Assert.True(JournalEvent.TryParse(json, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));
        return parsed!;
    }

    [Fact]
    public void TheSizeAndClassOnTheEndOfASymbolDoNotHideTheFamily()
    {
        var loadout = LoadoutWith(
            """{"Slot":"Slot03_Size6","Item":"int_fuelscoop_size6_class5","On":true,"Health":1.0}""");

        Assert.True(loadout.Fitted("int_fuelscoop"));
    }

    [Fact]
    public void AShipCarryingNoneOfThatFamilyAnswersFalse()
    {
        var loadout = LoadoutWith(
            """{"Slot":"Slot03_Size6","Item":"int_cargorack_size6_class1","On":true,"Health":1.0}""");

        Assert.False(loadout.Fitted("int_fuelscoop"));
    }

    [Fact]
    public void ALoadoutThatCannotAnswerSaysSoRatherThanSayingNo()
    {
        // Nothing read yet, and a Loadout that carried no module list.
        Assert.Null(ShipLoadout.Unknown.Fitted("int_fuelscoop"));
        Assert.Null(LoadoutWith(string.Empty).Fitted("int_fuelscoop"));
    }
}
