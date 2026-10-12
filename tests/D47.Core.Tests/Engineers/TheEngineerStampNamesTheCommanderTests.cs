using D47.Core.Engineers;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Engineers;

public class TheEngineerStampNamesTheCommanderTests
{
    [Fact]
    public void TwoCommandersAtTheSameSpotHaveDifferentStamps()
    {
        var first = new CommanderGameState(new CommanderIdentity("F100", "One"));
        var second = new CommanderGameState(new CommanderIdentity("F200", "Two"));

        Assert.NotEqual(UnlockPlanner.Stamp(first), UnlockPlanner.Stamp(second));
    }
}
