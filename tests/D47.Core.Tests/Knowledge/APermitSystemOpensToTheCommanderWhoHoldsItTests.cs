using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public sealed class APermitSystemOpensToTheCommanderWhoHoldsItTests
{
    private static RankState Ranks(int empire, int federation) => new()
    {
        TakenAt = DateTimeOffset.UnixEpoch,
        Standings = [new RankStanding("Empire", empire), new RankStanding("Federation", federation)],
    };

    [Theory]
    [InlineData("Sol", 0, 3, true)]
    [InlineData("Sol", 0, 4, false)]
    [InlineData("Sol", 0, 9, false)]
    [InlineData("Sol", 4, 0, true)]
    [InlineData("Achenar", 3, 0, true)]
    [InlineData("Achenar", 4, 0, false)]
    [InlineData("Achenar", 0, 4, true)]
    public void ANavyRankOpensItsOwnPermitSystemAndNoOther(string system, int empire, int federation, bool closed) =>
        Assert.Equal(closed, PermitSystemTable.ClosedTo(system, Ranks(empire, federation), () => false));

    [Fact]
    public void AVisitedPermitSystemIsOpen() =>
        Assert.False(PermitSystemTable.ClosedTo("Shinrarta Dezhra", RankState.Empty, () => true));

    [Fact]
    public void AnUnvisitedPermitSystemIsClosed() =>
        Assert.True(PermitSystemTable.ClosedTo("Shinrarta Dezhra", RankState.Empty, () => false));

    [Fact]
    public void AVisitedStarterSystemIsStillClosed() =>
        Assert.True(PermitSystemTable.ClosedTo("HIP 97950", RankState.Empty, () => true));

    [Fact]
    public void ASystemWithoutAPermitIsNeverClosed() =>
        Assert.False(PermitSystemTable.ClosedTo("Oppi", RankState.Empty, () => false));

    [Fact]
    public void AnUnvisitedSystemIsNotLookedUpWhenARankOpensIt()
    {
        var looked = false;

        PermitSystemTable.ClosedTo("Sol", Ranks(0, 4), () => looked = true);

        Assert.False(looked);
    }
}
