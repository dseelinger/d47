using D47.Core.Hotas;
using Xunit;

namespace D47.App.Tests;

/// <summary>The window runs an action for every slot <see cref="ActionButtons"/> raises, and for nothing else (#1550).</summary>
public sealed class EveryActionButtonHasAnActionTests
{
    [Fact]
    public void TheWindowDispatchesExactlyTheNineActionSlots()
    {
        Assert.Equal(
            ActionButtons.Slots.Select(slot => slot.Key).Order(StringComparer.Ordinal),
            MainWindow.ButtonActions.Keys.Order(StringComparer.Ordinal));
    }
}
