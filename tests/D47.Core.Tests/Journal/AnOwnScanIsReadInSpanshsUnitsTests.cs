using System.Globalization;
using D47.Core.Journal;
using D47.Core.Storage;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Each journal <c>Scan</c> field read back by <see cref="OwnScanIndex"/> in the units and words Spansh uses.</summary>
public sealed class AnOwnScanIsReadInSpanshsUnitsTests
{
    private const long System = 1000;

    private const string At = "2026-10-10T09:01:00Z";

    private const string Star =
        "{\"timestamp\":\"" + At + "\",\"event\":\"Scan\",\"ScanType\":\"Detailed\",\"BodyName\":\"Ocrinus A\",\"BodyID\":0,"
        + "\"StarSystem\":\"Ocrinus\",\"SystemAddress\":1000,\"DistanceFromArrivalLS\":0.0,\"StarType\":\"K_OrangeGiant\","
        + "\"Subclass\":2,\"StellarMass\":1.25,\"Radius\":5031422464.0,\"AbsoluteMagnitude\":0.5,\"Age_MY\":12136,"
        + "\"SurfaceTemperature\":4200.0,\"Luminosity\":\"III\",\"RotationPeriod\":864000.0,\"AxialTilt\":0.0,"
        + "\"Rings\":[{\"Name\":\"Ocrinus A A Belt\",\"RingClass\":\"eRingClass_Rocky\",\"MassMT\":1.5e13,\"InnerRad\":1.0e9,\"OuterRad\":2.0e9}]}";

    private const string Planet =
        "{\"timestamp\":\"" + At + "\",\"event\":\"Scan\",\"ScanType\":\"Detailed\",\"BodyName\":\"Ocrinus A 1\",\"BodyID\":3,"
        + "\"Parents\":[{\"Null\":1},{\"Star\":0}],\"StarSystem\":\"Ocrinus\",\"SystemAddress\":1000,\"DistanceFromArrivalLS\":760.25,"
        + "\"TidalLock\":false,\"TerraformState\":\"\",\"PlanetClass\":\"Rocky ice body\",\"Atmosphere\":\"thin neon atmosphere\","
        + "\"AtmosphereType\":\"Neon\",\"Volcanism\":\"\",\"MassEM\":0.05,\"Radius\":4133889.5,\"SurfaceGravity\":3.04782,"
        + "\"SurfaceTemperature\":80.5,\"SurfacePressure\":1.13172,\"Landable\":true,"
        + "\"Materials\":[{\"Name\":\"sulphur\",\"Percent\":23.5},{\"Name\":\"iron\",\"Percent\":16.25}],"
        + "\"Composition\":{\"Ice\":0.5,\"Rock\":0.25,\"Metal\":0.25},\"SemiMajorAxis\":299195741400.0,\"Eccentricity\":0.01,"
        + "\"OrbitalInclination\":1.5,\"Periapsis\":117.45,\"OrbitalPeriod\":172800.0,\"AscendingNode\":20.25,\"MeanAnomaly\":292.75,"
        + "\"RotationPeriod\":43200.0,\"AxialTilt\":3.141592653589793,"
        + "\"Rings\":[{\"Name\":\"Ocrinus A 1 A Ring\",\"RingClass\":\"eRingClass_MetalRich\",\"MassMT\":2.5e9,\"InnerRad\":1.0e7,\"OuterRad\":2.0e7}]}";

    private const string Signals =
        "{\"timestamp\":\"2026-10-10T09:02:00Z\",\"event\":\"SAASignalsFound\",\"BodyName\":\"Ocrinus A 1\",\"BodyID\":3,\"SystemAddress\":1000,"
        + "\"Signals\":[{\"Type\":\"$SAA_SignalType_Biological;\",\"Type_Localised\":\"Biological\",\"Count\":1}],"
        + "\"Genuses\":[{\"Genus\":\"$Codex_Ent_Tussocks_Genus_Name;\",\"Genus_Localised\":\"Tussock\"}]}";

    private const string GiantWithRawFields =
        "{\"timestamp\":\"" + At + "\",\"event\":\"Scan\",\"ScanType\":\"Detailed\",\"BodyName\":\"Ocrinus A 2\",\"BodyID\":4,"
        + "\"Parents\":[{\"Star\":0}],\"StarSystem\":\"Ocrinus\",\"SystemAddress\":1000,\"TerraformState\":\"Terraformable\","
        + "\"PlanetClass\":\"High metal content body\",\"Atmosphere\":\"\",\"AtmosphereType\":\"None\","
        + "\"Volcanism\":\"minor rocky magma volcanism\",\"Radius\":1000.0}";

    private static readonly DateTimeOffset ScannedAt = DateTimeOffset.Parse(At, CultureInfo.InvariantCulture);

    private static readonly OwnSystem Read = ReadBack();

    private static BodyProfile StarBody => Read.Bodies.Single(body => body.BodyId == 0);

    private static BodyProfile PlanetBody => Read.Bodies.Single(body => body.BodyId == 3);

    private static BodyProfile Terraformable => Read.Bodies.Single(body => body.BodyId == 4);

    private static OwnSystem ReadBack()
    {
        var files = new MemoryFileSystem();
        files.WriteLines("j/Journal.2026-10-10T000000.01.log",
        [
            "{\"timestamp\":\"2026-10-10T09:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"Jameson\"}",
            Star,
            Planet,
            GiantWithRawFields,
            Signals,
        ]);

        var index = new OwnScanIndex(files, NullLogger.Instance);
        index.FoldHistory(["j/Journal.2026-10-10T000000.01.log"]);

        return index.Read("F1", System, CancellationToken.None)!;
    }

    [Fact]
    public void APlanetsRadiusIsInKilometres() => Assert.Equal(4133.8895, PlanetBody.Radius!.Value, 6);

    [Fact]
    public void AStarsRadiusIsInSolarRadii() => Assert.Equal(7.2343, StarBody.SolarRadius!.Value, 4);

    [Fact]
    public void GravityIsInG() => Assert.Equal(0.31079, PlanetBody.Gravity!.Value, 5);

    [Fact]
    public void PressureIsInAtmospheres() => Assert.Equal(1.11692e-5, PlanetBody.SurfacePressure!.Value, 9);

    [Fact]
    public void TheSemiMajorAxisIsInAstronomicalUnits() => Assert.Equal(2.0, PlanetBody.SemiMajorAxis!.Value, 9);

    [Fact]
    public void PeriodsAreInDays() =>
        Assert.Equal((2.0, 0.5), (PlanetBody.OrbitalPeriod!.Value, PlanetBody.RotationalPeriod!.Value));

    [Fact]
    public void TheAxialTiltIsInDegrees() => Assert.Equal(180.0, PlanetBody.AxialTilt!.Value, 9);

    [Fact]
    public void TheOrbitAnglesAreKeptAndTheMeanAnomalyIsStampedWithTheScan() =>
        Assert.Equal(
            (1.5, 117.45, 20.25, 292.75, (DateTimeOffset?)ScannedAt),
            (PlanetBody.OrbitalInclination!.Value, PlanetBody.ArgOfPeriapsis!.Value, PlanetBody.AscendingNode!.Value, PlanetBody.MeanAnomaly!.Value, PlanetBody.MeanAnomalyAt));

    [Fact]
    public void TheArrivalDistanceIsKeptAndStampedWithTheScan() =>
        Assert.Equal((760.25, (DateTimeOffset?)ScannedAt), (PlanetBody.DistanceToArrival!.Value, PlanetBody.DistanceToArrivalAt));

    [Fact]
    public void TheCompositionIsInPercentages() =>
        Assert.Equal(new Dictionary<string, double> { ["Ice"] = 50, ["Rock"] = 25, ["Metal"] = 25 }, PlanetBody.SolidComposition);

    [Fact]
    public void MaterialNamesAreCapitalised() =>
        Assert.Equal(new Dictionary<string, double> { ["Sulphur"] = 23.5, ["Iron"] = 16.25 }, PlanetBody.Materials);

    [Fact]
    public void AStarTypeAndSubclassAreSpanshsSubTypeAndSpectralClass() =>
        Assert.Equal(("K (Yellow-Orange giant) Star", "K2"), (StarBody.SubType, StarBody.SpectralClass));

    [Fact]
    public void AStarsMassAgeMagnitudeAndLuminosityAreKept() =>
        Assert.Equal((1.25, 12136.0, 0.5, "III"), (StarBody.SolarMasses!.Value, StarBody.Age!.Value, StarBody.AbsoluteMagnitude!.Value, StarBody.Luminosity));

    [Fact]
    public void APlanetClassIsSpanshsSubType() => Assert.Equal("Rocky Ice world", PlanetBody.SubType);

    [Fact]
    public void TheAtmosphereIsInSpanshsWords() => Assert.Equal("Thin Neon", PlanetBody.Atmosphere);

    [Fact]
    public void NoVolcanismAndNoTerraformingAreSaidAsSpanshSaysThem() =>
        Assert.Equal(("No volcanism", "Not terraformable"), (PlanetBody.Volcanism, PlanetBody.TerraformingState));

    [Fact]
    public void VolcanismAndATerraformingStateAreInSpanshsWords() =>
        Assert.Equal(("Minor Rocky Magma", "Terraformable"), (Terraformable.Volcanism, Terraformable.TerraformingState));

    [Fact]
    public void ARingClassIsSpanshsRingTypeAndABeltIsABelt() =>
        Assert.Equal(("Metal Rich", "Rocky"), (Assert.Single(PlanetBody.Rings).Type, Assert.Single(StarBody.Belts).Type));

    [Fact]
    public void TheParentIsTheNearestBodyPastABarycentre() =>
        Assert.Equal((0, 2), (PlanetBody.ParentId!.Value, PlanetBody.Parents.Count));

    [Fact]
    public void SignalKeysAreTheRawSignalTypes() =>
        Assert.Equal(new Dictionary<string, int> { ["$SAA_SignalType_Biological;"] = 1 }, PlanetBody.Signals);

    [Fact]
    public void GeneraAreTheCodexGenusNames() => Assert.Equal(["Tussock"], PlanetBody.Genera);

    [Fact]
    public void TheMainStarIsMarkedAndEveryBodyIsStampedWithItsScan() =>
        Assert.Equal((true, false, (DateTimeOffset?)ScannedAt), (StarBody.IsMainStar, PlanetBody.IsMainStar, PlanetBody.ReportedAt));
}
