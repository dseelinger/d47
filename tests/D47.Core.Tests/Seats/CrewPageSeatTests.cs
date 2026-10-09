using D47.Core.Storage;
using D47.Core.Seats;
using Xunit;

namespace D47.Core.Tests.Seats;

public class TheCrewPageFillsTheShipFlownTests
{
    [Fact]
    public void AnAnacondaWithNothingFilledIsOfferedThreeSeats() =>
        Assert.Equal(3, CrewSeatRules.Offered("anaconda", 7, [], []).Count);

    [Fact]
    public void FilledSeatsAreNotTouched()
    {
        var mine = new CrewSeat("0000000a", CrewRole.Helm, null, "Rook");

        var seats = CrewSeatRules.Offered("anaconda", 7, [mine], []);

        Assert.Equal(3, seats.Count);
        Assert.Same(mine, seats[0]);
    }

    [Fact]
    public void ADefaultThatWouldRepeatARoleOrANameIsSkipped()
    {
        var offered = CrewDefaults.Offer("anaconda", 7);
        var held = new CrewSeat("0000000a", offered[0].Role, null, "Rook");

        var seats = CrewSeatRules.Offered("anaconda", 7, [held], [offered[1].Name]);

        Assert.Equal(offered[0].Role, seats[0].Role);
        Assert.DoesNotContain(seats.Skip(1), seat => seat.Role == held.Role || seat.Name == offered[1].Name);
    }

    [Fact]
    public void ASidewinderOffersNothing() =>
        Assert.Empty(CrewSeatRules.Offered("sidewinder", 7, [], []));

    [Fact]
    public void AHullWithoutAnswerLinesSaysWhichHull()
    {
        Assert.Equal("The Sidewinder has no seat besides yours.", CrewSeatRules.NoSeats("Sidewinder"));
        Assert.Equal("d47 does not know how many seats a Corsair has.", CrewSeatRules.UnknownSeats("Corsair"));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void SeatsOfferedAreStoredAndSurviveARestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"d47-seats-{Guid.NewGuid():N}.json");

        try
        {
            var store = new CrewSeatStore(path, new DiskFileSystem(), Microsoft.Extensions.Logging.Abstractions.NullLogger<CrewSeatStore>.Instance);
            var seats = CrewSeatRules.Offered("anaconda", 7, [], []);
            store.Set(new ShipSeats("F1", 7, "anaconda", seats));

            var again = new CrewSeatStore(path, new DiskFileSystem(), Microsoft.Extensions.Logging.Abstractions.NullLogger<CrewSeatStore>.Instance);
            again.Poll();

            Assert.Equal(3, again.For("F1", 7)?.Seats.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class ASeatNameMustBeItsOwnTests
{
    private static readonly CrewSeat Marlow = new("0000000a", CrewRole.FirstOfficer, null, "Marlow");

    private static CrewSeat Seat(CrewRole role, string name, string? title = null) =>
        new("0000000b", role, title, name);

    [Fact]
    public void ANameAnotherSeatHoldsIsRefused() =>
        Assert.NotNull(CrewSeatRules.Refusal(Seat(CrewRole.Helm, "marlow"), [Marlow], []));

    [Theory]
    [InlineData("Vance Ilo")]
    [InlineData("vance ilo")]
    public void ANameAHiredPilotHoldsIsRefused(string name) =>
        Assert.NotNull(CrewSeatRules.Refusal(Seat(CrewRole.Helm, name), [], ["Vance Ilo"]));

    [Fact]
    public void TheShipAIsOrTheCaptainsNameIsRefused()
    {
        Assert.NotNull(CrewSeatRules.Refusal(Seat(CrewRole.Helm, "Ava"), [], ["Ava", "Hollis"]));
        Assert.NotNull(CrewSeatRules.Refusal(Seat(CrewRole.Helm, "Hollis"), [], ["Ava", "Hollis"]));
    }

    [Fact]
    public void TwoSeatsMayNotShareAStandardRole() =>
        Assert.NotNull(CrewSeatRules.Refusal(Seat(CrewRole.FirstOfficer, "Okafor"), [Marlow], []));

    [Fact]
    public void TwoCustomSeatsMayShareTheWord() =>
        Assert.Null(CrewSeatRules.Refusal(
            Seat(CrewRole.Custom, "Okafor", "Cook"),
            [Marlow with { Role = CrewRole.Custom, Title = "Cook" }],
            []));

    [Fact]
    public void ASeatDoesNotCollideWithItself() =>
        Assert.Null(CrewSeatRules.Refusal(Marlow with { Voice = null }, [Marlow], []));

    [Fact]
    public void ACustomRoleNeedsATitle() =>
        Assert.NotNull(CrewSeatRules.Refusal(Seat(CrewRole.Custom, "Okafor"), [], []));

    [Fact]
    public void ABlankNameIsRefused() =>
        Assert.NotNull(CrewSeatRules.Refusal(Seat(CrewRole.Helm, "  "), [], []));
}
