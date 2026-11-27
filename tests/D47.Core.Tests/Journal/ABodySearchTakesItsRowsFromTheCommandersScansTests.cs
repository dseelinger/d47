using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><see cref="OwnBodySearch.Apply"/> corrects a body search's rows by the Commander's own newer scans and drops those that no longer match.</summary>
public sealed class ABodySearchTakesItsRowsFromTheCommandersScansTests
{
    private const long Address = 396459183;

    private static readonly DateTimeOffset Reported = new(2018, 8, 28, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Scanned = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AStarTheScanShowsIsAnotherKindIsLeftOut()
    {
        var result = Result(Row("Gru Eop AA-A h47", 1, "A (Blue-White super giant) Star", "Ia0"), Row("Other", 2, "A (Blue-White super giant) Star", "Ia0"));

        var answer = OwnBodySearch.Apply(result, Query(new BodyRequest { Subtype = "A (Blue-White super giant) Star", Luminosity = "Hypergiant" }), ReadBlueSuperGiant, TestContext.Current.CancellationToken);

        Assert.Equal(["Other"], answer.Bodies.Select(body => body.Name));
        Assert.Equal(1, answer.Total);
    }

    [Fact]
    public void AStarTheScanConfirmsStaysAndTakesTheScansFields()
    {
        var result = Result(Row("Gru Eop AA-A h47", 1, "B (Blue-White super giant) Star", "Ia"));

        var answer = OwnBodySearch.Apply(result, Query(new BodyRequest { Subtype = "B (Blue-White super giant) Star" }), ReadBlueSuperGiant, TestContext.Current.CancellationToken);

        var body = Assert.Single(answer.Bodies);
        Assert.Equal("Ia0", body.Luminosity);
        Assert.Equal(26014, body.SurfaceTemperature);
        Assert.Equal(Scanned, body.ReportedAt);
        Assert.Equal(1, answer.Total);
    }

    [Fact]
    public void ABodyWhoseScanIsOlderThanTheReportKeepsTheReport()
    {
        var result = Result(Row("Gru Eop AA-A h47", 1, "A (Blue-White super giant) Star", "Ia0") with { ReportedAt = Scanned.AddDays(1) });

        var answer = OwnBodySearch.Apply(result, Query(new BodyRequest { Subtype = "A (Blue-White super giant) Star" }), ReadBlueSuperGiant, TestContext.Current.CancellationToken);

        var body = Assert.Single(answer.Bodies);
        Assert.Equal("A (Blue-White super giant) Star", body.Subtype);
        Assert.Equal(1, answer.Total);
    }

    [Fact]
    public void AFilterTheScanDoesNotHoldIsLeftAsTheServiceAnswered()
    {
        var result = Result(Row("Gru Eop AA-A h47", 1, "B (Blue-White super giant) Star", "Ia0") with { Materials = [("Iron", 12.5)] });

        var answer = OwnBodySearch.Apply(result, Query(new BodyRequest { Material = "Polonium" }), ReadBlueSuperGiant, TestContext.Current.CancellationToken);

        Assert.Single(answer.Bodies);
        Assert.Equal(1, answer.Total);
    }

    [Fact]
    public void ASystemIsReadOnceAndOnlyForABodyWithAnAddress()
    {
        var reads = new List<long>();
        var result = Result(
            Row("A", 1, "A (Blue-White super giant) Star", "Ia0"),
            Row("B", 2, "A (Blue-White super giant) Star", "Ia0"),
            Row("C", 3, "A (Blue-White super giant) Star", "Ia0") with { SystemAddress = null });

        OwnBodySearch.Apply(result, Query(new BodyRequest { Landable = true }), (address, _) => { reads.Add(address); return null; }, TestContext.Current.CancellationToken);

        Assert.Equal([Address], reads);
    }

    [Fact]
    public void ASystemWithNoScansLeavesTheAnswerAsItWas()
    {
        var result = Result(Row("A", 1, "A (Blue-White super giant) Star", "Ia0"));

        Assert.Same(result, OwnBodySearch.Apply(result, Query(new BodyRequest { Landable = true }), (_, _) => null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AReadThatThrowsCountsAsNoScans()
    {
        var result = Result(Row("A", 1, "A (Blue-White super giant) Star", "Ia0"));

        var answer = OwnBodySearch.Apply(result, Query(new BodyRequest { Landable = true }), (_, _) => throw new IOException("locked"), TestContext.Current.CancellationToken);

        Assert.Same(result, answer);
    }

    [Fact]
    public void ACancelledReadIsNotSwallowed()
    {
        var result = Result(Row("A", 1, "A (Blue-White super giant) Star", "Ia0"));

        Assert.Throws<OperationCanceledException>(
            () => OwnBodySearch.Apply(result, Query(new BodyRequest { Landable = true }), (_, _) => throw new OperationCanceledException(), TestContext.Current.CancellationToken));
    }

    private static OwnSystem? ReadBlueSuperGiant(long address, CancellationToken cancellation) => address != Address
        ? null
        : new OwnSystem(Address)
        {
            Name = "Gru Eop AA-A h47",
            Bodies =
            [
                new BodyProfile
                {
                    BodyId = 1,
                    Name = "Gru Eop AA-A h47",
                    Type = "Star",
                    SubType = "B (Blue-White super giant) Star",
                    Luminosity = "Ia0",
                    SurfaceTemperature = 26014,
                    ReportedAt = Scanned,
                },
            ],
            Own = new Dictionary<int, OwnBody> { [1] = new OwnBody(Scanned) },
            LatestAt = Scanned,
        };

    private static BodyQuery Query(BodyRequest request)
    {
        Assert.True(BodyQuery.TryParse(request with { ReferenceSystem = "Sol" }, out var query, out var failure), failure);

        return query;
    }

    private static BodySearchResult Result(params BodySummary[] bodies) => new("Sol", bodies.Length, bodies);

    private static BodySummary Row(string name, int bodyId, string subtype, string luminosity) => new()
    {
        Name = name,
        SystemName = "Gru Eop AA-A h47",
        SystemAddress = Address,
        BodyId = bodyId,
        Type = "Star",
        Subtype = subtype,
        Luminosity = luminosity,
        ReportedAt = Reported,
    };
}
