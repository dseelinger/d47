using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><see cref="OwnScansMerge.Into"/> joins the Commander's own scans with Spansh's record of the same system.</summary>
public sealed class OwnScansJoinTheSystemRecordTests
{
    private const long Address = 1000;

    private static readonly DateTimeOffset Older = new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Newer = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Latest = new(2026, 10, 10, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void BothMissingIsNull()
    {
        Assert.Null(OwnScansMerge.Into(null, null));
    }

    [Fact]
    public void ASpanshRecordAloneIsUnchanged()
    {
        var record = Record([Planet(3, Older)]);

        Assert.Same(record, OwnScansMerge.Into(record, null));
    }

    [Fact]
    public void OwnScansAloneBuildTheProfile()
    {
        var centre = new BarycentreProfile { BodyId = 1, SemiMajorAxis = 2.5 };
        var own = Own([Star(0, Newer), Planet(3, Newer)]) with
        {
            Position = new StarPosition(1, 2, 3),
            BodyCount = 7,
            Barycentres = [centre],
        };

        var merged = OwnScansMerge.Into(null, own);

        Assert.NotNull(merged);
        Assert.Equal("Ocrinus", merged.Name);
        Assert.Equal(Address, merged.SystemAddress);
        Assert.Equal(new StarPosition(1, 2, 3), merged.Position);
        Assert.Equal(7, merged.BodyCount);
        Assert.Equal([0, 3], merged.Bodies.Select(body => body.BodyId));
        Assert.Same(centre, Assert.Single(merged.Barycentres));
        Assert.Equal(Latest, merged.ReportedAt);
        Assert.Empty(merged.Stations);
        Assert.Empty(merged.Factions);
        Assert.Null(merged.Powerplay);
        Assert.Null(merged.ControllingFaction);
    }

    [Fact]
    public void ANewerSpanshReportWinsAndIsFilledFromTheOwnScan()
    {
        var spansh = Planet(3, Newer) with { Gravity = 0.31, Volcanism = null, SubType = "Icy body" };
        var scanned = Planet(3, Older) with { Gravity = 0.28, Volcanism = "Minor Water Geysers", SubType = "Rocky ice body" };

        var body = MergedBody(spansh, scanned);

        Assert.Equal(spansh.Gravity, body.Gravity);
        Assert.Equal("Icy body", body.SubType);
        Assert.Equal("Minor Water Geysers", body.Volcanism);
        Assert.Equal(Newer, body.ReportedAt);
    }

    [Fact]
    public void ANewerOwnScanWinsAndIsFilledFromSpansh()
    {
        var spansh = Planet(3, Older) with { Gravity = 0.31, ReserveLevel = "Pristine", SubType = "Icy body" };
        var scanned = Planet(3, Newer) with { Gravity = 0.28, ReserveLevel = null, SubType = "Rocky ice body" };

        var body = MergedBody(spansh, scanned);

        Assert.Equal(scanned.Gravity, body.Gravity);
        Assert.Equal("Rocky ice body", body.SubType);
        Assert.Equal("Pristine", body.ReserveLevel);
        Assert.Equal(Newer, body.ReportedAt);
    }

    [Fact]
    public void ASpanshReportWithNoDateIsOlder()
    {
        var spansh = Planet(3, null) with { Gravity = 0.31 };
        var scanned = Planet(3, Older) with { Gravity = 0.28 };

        Assert.Equal(scanned.Gravity, MergedBody(spansh, scanned).Gravity);
    }

    [Fact]
    public void ANewerOwnScanWithAnOlderOrbitKeepsSpanshsOrbitWhole()
    {
        var spansh = Orbit(Planet(3, Older), 10, 1.5, Newer) with { OrbitalPeriod = null };
        var scanned = Orbit(Planet(3, Newer), 200, 1.6, Older) with { OrbitalPeriod = 2 };

        var body = MergedBody(spansh, scanned);

        Assert.Equal(10, body.MeanAnomaly);
        Assert.Equal(Newer, body.MeanAnomalyAt);
        Assert.Equal(1.5, body.SemiMajorAxis);
        Assert.Equal(0.01, body.OrbitalEccentricity);
        Assert.Null(body.OrbitalPeriod);
    }

    [Fact]
    public void ANewerSpanshReportWithAnOlderOrbitTakesTheOwnOrbitWhole()
    {
        var spansh = Orbit(Planet(3, Newer), 10, 1.5, Older) with { OrbitalPeriod = 2 };
        var scanned = Orbit(Planet(3, Older), 200, 1.6, Newer) with { OrbitalPeriod = null };

        var body = MergedBody(spansh, scanned);

        Assert.Equal(200, body.MeanAnomaly);
        Assert.Equal(Newer, body.MeanAnomalyAt);
        Assert.Equal(1.6, body.SemiMajorAxis);
        Assert.Null(body.OrbitalPeriod);
    }

    [Fact]
    public void TheDistanceToArrivalGoesWithItsOwnStamp()
    {
        var spansh = Planet(3, Older) with { DistanceToArrival = 761, DistanceToArrivalAt = Newer };
        var scanned = Planet(3, Newer) with { DistanceToArrival = 750, DistanceToArrivalAt = Older };

        var body = MergedBody(spansh, scanned);

        Assert.Equal(761, body.DistanceToArrival);
        Assert.Equal(Newer, body.DistanceToArrivalAt);
    }

    [Fact]
    public void TheRingsComeAsOneListFromTheWinningBody()
    {
        var spansh = Planet(3, Older) with { Rings = [Ring("Ocrinus A 1 A Ring"), Ring("Ocrinus A 1 B Ring")] };
        var scanned = Planet(3, Newer) with { Rings = [Ring("Ocrinus A 1 A Ring")] };

        Assert.Same(scanned.Rings, MergedBody(spansh, scanned).Rings);
    }

    [Fact]
    public void ABodyOnlyTheCommanderScannedIsAddedUnderItsParent()
    {
        var record = Record([Star(0, Older)]);
        var moon = Planet(5, Newer) with { ParentId = 0, Parents = [("Star", 0)] };

        var merged = OwnScansMerge.Into(record, Own([moon]));

        Assert.NotNull(merged);
        Assert.Equal([0, 5], merged.Bodies.Select(body => body.BodyId));
        Assert.Same(moon, merged.Bodies[1]);
        Assert.Equal(0, merged.Bodies[1].ParentId);
    }

    [Fact]
    public void AnOwnWinnerWithNoParentsTakesBothParentFieldsFromSpansh()
    {
        var spansh = Planet(3, Older) with { ParentId = 0, Parents = [("Null", 1), ("Star", 0)] };
        var scanned = Planet(3, Newer) with { ParentId = null, Parents = [] };

        var body = MergedBody(spansh, scanned);

        Assert.Equal(0, body.ParentId);
        Assert.Same(spansh.Parents, body.Parents);
    }

    [Fact]
    public void OwnSurfaceScanCountsAndGeneraReplaceSpanshs()
    {
        var spansh = Planet(3, Newer) with
        {
            Signals = new Dictionary<string, int> { ["$SAA_SignalType_Biological;"] = 1 },
            SignalsSeen = Newer,
            Genera = ["Bacterium"],
        };
        var scanned = Planet(3, Older) with
        {
            Signals = new Dictionary<string, int> { ["$SAA_SignalType_Biological;"] = 3 },
            SignalsSeen = Older,
            Genera = ["Bacterium", "Fonticulua", "Stratum"],
        };

        var body = MergedBody(spansh, scanned, new OwnBody(Older) { SignalsFromSurfaceScan = true, SignalsAt = Older });

        Assert.Same(scanned.Signals, body.Signals);
        Assert.Same(scanned.Genera, body.Genera);
        Assert.Equal(Older, body.SignalsSeen);
    }

    [Fact]
    public void OwnFssCountsFillOnlyABodySpanshGivesNoSignalsFor()
    {
        var fss = new OwnBody(Newer) { SignalsAt = Newer };
        var counted = new Dictionary<string, int> { ["$SAA_SignalType_Geological;"] = 2 };
        var scanned = Planet(3, Newer) with { Signals = counted, SignalsSeen = Newer };

        var filled = MergedBody(Planet(3, Older) with { Genera = ["Bacterium"] }, scanned, fss);

        Assert.Same(counted, filled.Signals);
        Assert.Equal(Newer, filled.SignalsSeen);
        Assert.Equal(["Bacterium"], filled.Genera);

        var spanshCounts = new Dictionary<string, int> { ["$SAA_SignalType_Geological;"] = 4 };
        var kept = MergedBody(Planet(3, Older) with { Signals = spanshCounts, SignalsSeen = Older }, scanned, fss);

        Assert.Same(spanshCounts, kept.Signals);
        Assert.Equal(Older, kept.SignalsSeen);
    }

    [Fact]
    public void ANewerOwnScanWithNoCountsKeepsSpanshsSignals()
    {
        var counts = new Dictionary<string, int> { ["$SAA_SignalType_Biological;"] = 2 };
        var spansh = Planet(3, Older) with { Signals = counts, SignalsSeen = Older, Genera = ["Tussock"] };

        var body = MergedBody(spansh, Planet(3, Newer), new OwnBody(Newer));

        Assert.Same(counts, body.Signals);
        Assert.Equal(Older, body.SignalsSeen);
        Assert.Equal(["Tussock"], body.Genera);
    }

    [Theory]
    [InlineData(12, 9, 12)]
    [InlineData(9, 12, 12)]
    [InlineData(null, 12, 12)]
    [InlineData(12, null, 12)]
    public void TheBodyCountIsTheLarger(int? spansh, int? own, int expected)
    {
        var merged = OwnScansMerge.Into(Record([]) with { BodyCount = spansh }, Own([]) with { BodyCount = own });

        Assert.Equal(expected, merged?.BodyCount);
    }

    [Fact]
    public void StationsFactionsAndPowerplayComeFromSpanshUntouched()
    {
        var record = Record([Planet(3, Older)]) with
        {
            Stations = [new StationProfile { Name = "Ocrinus Port" }],
            Factions = [new FactionStanding("Ocrinus Union", "Democracy", "Federation", 0.5, [], [])],
            Powerplay = new PowerplayStanding { ControllingPower = "Felicia Winters" },
            ControllingFaction = "Ocrinus Union",
            ReportedAt = Older,
        };

        var merged = OwnScansMerge.Into(record, Own([Planet(3, Newer)]));

        Assert.NotNull(merged);
        Assert.Same(record.Stations, merged.Stations);
        Assert.Same(record.Factions, merged.Factions);
        Assert.Same(record.Powerplay, merged.Powerplay);
        Assert.Equal("Ocrinus Union", merged.ControllingFaction);
        Assert.Equal(Older, merged.ReportedAt);
    }

    [Fact]
    public void BarycentresJoinByTheLaterOrbit()
    {
        var record = Record([]) with
        {
            Barycentres =
            [
                new BarycentreProfile { BodyId = 1, MeanAnomaly = 10, MeanAnomalyAt = Older, SemiMajorAxis = 2 },
                new BarycentreProfile { BodyId = 2, MeanAnomaly = 20, MeanAnomalyAt = Newer, SemiMajorAxis = 3 },
            ],
        };
        var own = Own([]) with
        {
            Barycentres =
            [
                new BarycentreProfile { BodyId = 1, MeanAnomaly = 110, MeanAnomalyAt = Newer, SemiMajorAxis = 2.1 },
                new BarycentreProfile { BodyId = 2, MeanAnomaly = 120, MeanAnomalyAt = Older, SemiMajorAxis = 3.1 },
                new BarycentreProfile { BodyId = 4, MeanAnomaly = 140, MeanAnomalyAt = Older },
            ],
        };

        var merged = OwnScansMerge.Into(record, own);

        Assert.NotNull(merged);
        Assert.Equal([1, 2, 4], merged.Barycentres.Select(centre => centre.BodyId));
        Assert.Equal(110, merged.Barycentres[0].MeanAnomaly);
        Assert.Equal(2.1, merged.Barycentres[0].SemiMajorAxis);
        Assert.Equal(20, merged.Barycentres[1].MeanAnomaly);
        Assert.Equal(3, merged.Barycentres[1].SemiMajorAxis);
        Assert.Equal(140, merged.Barycentres[2].MeanAnomaly);
    }

    [Fact]
    public void ARecordOfAnotherSystemIsUnchanged()
    {
        var record = Record([Planet(3, Older)]);

        Assert.Same(record, OwnScansMerge.Into(record, new OwnSystem(Address + 1) { Bodies = [Planet(3, Newer)] }));
    }

    private static BodyProfile MergedBody(BodyProfile spansh, BodyProfile scanned, OwnBody? record = null)
    {
        var own = Own([scanned]) with
        {
            Own = new Dictionary<int, OwnBody> { [scanned.BodyId] = record ?? new OwnBody(scanned.ReportedAt!.Value) },
        };

        var merged = OwnScansMerge.Into(Record([spansh]), own);

        Assert.NotNull(merged);
        return Assert.Single(merged.Bodies);
    }

    private static StarSystemProfile Record(IReadOnlyList<BodyProfile> bodies) => new()
    {
        Name = "Ocrinus",
        SystemAddress = Address,
        Bodies = bodies,
    };

    private static OwnSystem Own(IReadOnlyList<BodyProfile> bodies) => new(Address)
    {
        Name = "Ocrinus",
        Bodies = bodies,
        Own = bodies.ToDictionary(body => body.BodyId, body => new OwnBody(body.ReportedAt ?? Latest)),
        LatestAt = Latest,
    };

    private static BodyProfile Star(int bodyId, DateTimeOffset? reportedAt) => new()
    {
        BodyId = bodyId,
        Name = "Ocrinus A",
        Type = "Star",
        IsMainStar = bodyId == 0,
        ReportedAt = reportedAt,
    };

    private static BodyProfile Planet(int bodyId, DateTimeOffset? reportedAt) => new()
    {
        BodyId = bodyId,
        Name = "Ocrinus A " + bodyId,
        Type = "Planet",
        ParentId = 0,
        Parents = [("Star", 0)],
        ReportedAt = reportedAt,
    };

    private static BodyProfile Orbit(BodyProfile body, double meanAnomaly, double semiMajorAxis, DateTimeOffset at) => body with
    {
        MeanAnomaly = meanAnomaly,
        MeanAnomalyAt = at,
        SemiMajorAxis = semiMajorAxis,
        OrbitalEccentricity = 0.01,
    };

    private static RingProfile Ring(string name) => new(name, "Icy", new Dictionary<string, int>());
}
