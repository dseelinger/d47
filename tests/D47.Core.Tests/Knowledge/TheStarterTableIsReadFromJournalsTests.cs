using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public sealed class TheStarterTableIsReadFromJournalsTests
{
    [Fact]
    public void TheShippedTableHoldsHip97950() => Assert.True(StarterSystemTable.Contains("HIP 97950"));

    [Fact]
    public void AnOpenSystemIsNotAStarterSystem() => Assert.False(StarterSystemTable.Contains("Sol"));

    [Fact]
    public void EveryStarterSystemIsPermitLocked() =>
        Assert.All(StarterSystemTable.Names, name => Assert.True(PermitSystemTable.Locked(name), name));

    [Fact]
    public void TheTableTextIsReadWithoutCommentsOrCase()
    {
        var names = StarterSystemTable.Parse("# header\nHIP 97950\n\nWolf 397\n");

        Assert.Equal(2, names.Count);
        Assert.Contains("hip 97950", names);
        Assert.False(StarterSystemTable.Contains(null));
    }
}
