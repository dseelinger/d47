using D47.Core.Knowledge;
using D47.Core.Seats;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Seats;

public class TheCommanderTakesOneSeatTests
{
    [Theory]
    [InlineData("anaconda", 3)]
    [InlineData("krait_mkii", 2)]
    [InlineData("cobramkiii", 1)]
    [InlineData("sidewinder", 0)]
    public void AHullOffersItsCrewFigureLessOne(string hull, int seats) =>
        Assert.Equal(seats, CrewSeats.CountFor(hull));

    [Fact]
    public void AHullWithNoCrewFigureIsUnknownRatherThanZero()
    {
        Assert.Null(CrewSeats.CountFor("corsair"));
        Assert.Null(CrewSeats.CountFor("explorer_nx"));
        Assert.Null(CrewSeats.CountFor("mediumtransport01"));
        Assert.Null(CrewSeats.CountFor("smallcombat01_nx"));
    }

    [Fact]
    public void AHullTheTableDoesNotKnowIsUnknown() =>
        Assert.Null(CrewSeats.CountFor("not_a_ship"));
}

public class DefaultsFollowWhatTheHullIsForTests
{
    [Fact]
    public void ACombatHullIsOfferedSecurityHelmAndNavigation() =>
        Assert.Equal(
            [CrewRole.SecurityOfficer, CrewRole.Helm, CrewRole.Navigation],
            Roles("federation_corvette", 1));

    [Fact]
    public void ATraderIsOfferedFirstOfficerNavigationAndComms() =>
        Assert.Equal(
            [CrewRole.FirstOfficer, CrewRole.Navigation, CrewRole.Comms],
            Roles("type9", 1));

    [Fact]
    public void AHullWithOneSeatIsOfferedTheFirstRoleOnly() =>
        Assert.Equal([CrewRole.FirstOfficer], Roles("cobramkiii", 1));

    [Fact]
    public void AHullWithNoSeatsIsOfferedNothing()
    {
        Assert.Empty(CrewDefaults.Offer("sidewinder", 1));
        Assert.Empty(CrewDefaults.Offer("corsair", 1));
    }

    [Fact]
    public void AHullOutsideThePurposeTableIsMultipurpose() =>
        Assert.Equal(HullPurpose.Multipurpose, CrewDefaults.PurposeOf("not_a_ship"));

    [Fact]
    public void TwoShipsOfOneHullAreOfferedDifferentNames()
    {
        var first = CrewDefaults.Offer("anaconda", 12).Select(seat => seat.Name);
        var second = CrewDefaults.Offer("anaconda", 27).Select(seat => seat.Name);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ANameIsOfferedOncePerRoster()
    {
        foreach (var hull in CrewDefaults.Hulls)
        {
            var names = CrewDefaults.Offer(hull, 5).Select(seat => seat.Name).ToArray();

            Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void AnOfferedSeatIsAValidSeat()
    {
        foreach (var seat in CrewDefaults.Offer("anaconda", 3))
        {
            Assert.True(CrewSeat.IsId(seat.Id));
            Assert.InRange(seat.Name.Length, 1, CrewSeat.MaxName);
            Assert.Null(seat.Title);
        }
    }

    [Fact]
    public void EveryHullWithASeatIsInThePurposeTable()
    {
        var missing = EliteSpecifications.Ships
            .Where(ship => CrewSeats.CountFor(ship.Symbol) is > 0)
            .Select(ship => ship.Symbol)
            .Where(symbol => !CrewDefaults.Hulls.Contains(symbol))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryHullInThePurposeTableIsInTheSpecsTable()
    {
        var symbols = EliteSpecifications.Ships.Select(ship => ship.Symbol).ToHashSet();

        Assert.All(CrewDefaults.Hulls, hull => Assert.Contains(hull, symbols));
    }

    private static CrewRole[] Roles(string hull, int shipId) =>
        [.. CrewDefaults.Offer(hull, shipId).Select(seat => seat.Role)];
}

[Trait("Category", "Integration")]
public class ASeatFileCannotOverfillAHullTests
{
    [Fact]
    public void FourSeatsOnAnAnacondaKeepThreeAndReportOne()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Save([new ShipSeats("F1", 1, "anaconda",
        [
            Seat(CrewRole.FirstOfficer, "A"),
            Seat(CrewRole.Helm, "B"),
            Seat(CrewRole.Comms, "C"),
            Seat(CrewRole.Navigation, "D"),
        ])]);

        Assert.Equal(3, store.For("F1", 1)!.Seats.Count);
        Assert.Equal("D", Assert.Single(store.Problems).Where.Split(", ")[1]);
    }

    [Fact]
    public void TwoNavigationSeatsKeepTheFirst()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Save([new ShipSeats("F1", 1, "anaconda",
        [
            Seat(CrewRole.Navigation, "First"),
            Seat(CrewRole.Navigation, "Second"),
        ])]);

        Assert.Equal("First", Assert.Single(store.For("F1", 1)!.Seats).Name);
        Assert.Contains("Navigation", Assert.Single(store.Problems).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoCustomSeatsMayShareARole()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Save([new ShipSeats("F1", 1, "anaconda",
        [
            Seat(CrewRole.Custom, "Gunner", "Gunner"),
            Seat(CrewRole.Custom, "Cook", "Cook"),
        ])]);

        Assert.Equal(2, store.For("F1", 1)!.Seats.Count);
        Assert.Empty(store.Problems);
    }

    [Fact]
    public void TwoSeatsWithOneNameKeepTheFirst()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Save([new ShipSeats("F1", 1, "anaconda",
        [
            Seat(CrewRole.Helm, "Quill"),
            Seat(CrewRole.Comms, "quill"),
        ])]);

        Assert.Single(store.For("F1", 1)!.Seats);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void AnEmptyOrOverlongNameOrTitleIsRefused()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Save([new ShipSeats("F1", 1, "anaconda",
        [
            Seat(CrewRole.Helm, " "),
            Seat(CrewRole.Comms, new string('n', CrewSeat.MaxName + 1)),
            Seat(CrewRole.Custom, "Cook", new string('t', CrewSeat.MaxTitle + 1)),
            Seat(CrewRole.Custom, "Gunner", null),
        ])]);

        Assert.Empty(store.For("F1", 1)!.Seats);
        Assert.Equal(4, store.Problems.Count);
    }

    [Fact]
    public void AHullWithNoSeatsRefusesEverySeat()
    {
        using var folder = new TempFolder();
        var store = Store(folder);

        store.Save([
            new ShipSeats("F1", 1, "sidewinder", [Seat(CrewRole.Helm, "A")]),
            new ShipSeats("F1", 2, "corsair", [Seat(CrewRole.Helm, "B")]),
        ]);

        Assert.Empty(store.For("F1", 1)!.Seats);
        Assert.Empty(store.For("F1", 2)!.Seats);
        Assert.Equal(2, store.Problems.Count);
    }

    [Fact]
    public void ASeatSurvivesARestartWithTheSameId()
    {
        using var folder = new TempFolder();
        var offered = CrewDefaults.Offer("anaconda", 7);

        Store(folder).Set(new ShipSeats("F1", 7, "anaconda", offered));

        var reopened = Store(folder);
        reopened.Poll();

        Assert.Equal(offered, reopened.For("F1", 7)!.Seats);
        Assert.Empty(reopened.Problems);
    }

    [Fact]
    public void ACustomSeatKeepsItsTitle()
    {
        using var folder = new TempFolder();
        var seat = Seat(CrewRole.Custom, "Gunner", "Gunnery chief");

        Store(folder).Set(new ShipSeats("F1", 7, "anaconda", [seat]));

        var reopened = Store(folder);
        reopened.Poll();

        Assert.Equal(seat, Assert.Single(reopened.For("F1", 7)!.Seats));
    }

    [Fact]
    public void ChangedIsRaisedWhoeverWroteTheFile()
    {
        using var folder = new TempFolder();
        var store = Store(folder);
        var changes = 0;
        store.Changed += () => changes++;

        store.Set(new ShipSeats("F1", 7, "anaconda", []));
        File.WriteAllText(folder.File, """{ "ships": [] }""");

        Assert.True(store.Poll());
        Assert.Equal(2, changes);
    }

    [Fact]
    public void AFileThatIsNotJsonIsOneProblemAndNoSeats()
    {
        using var folder = new TempFolder();
        File.WriteAllText(folder.File, "not json");

        var store = Store(folder);
        store.Poll();

        Assert.Empty(store.Ships);
        Assert.Single(store.Problems);
    }

    private static CrewSeat Seat(CrewRole role, string name, string? title = null) =>
        new(CrewSeat.NewId(), role, title, name);

    private static CrewSeatStore Store(TempFolder folder) =>
        new(folder.File, NullLogger<CrewSeatStore>.Instance);

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), $"d47-crew-seats-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string File => Path.Combine(Root, "crew-seats.json");

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
