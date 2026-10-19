using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheSignalIndexIsReadFromItsDailyFileTests
{
    private static string Tsv(params string[] rows) => string.Join("\n", [SignalIndex.Header, .. rows]) + "\n";

    private static SignalIndex Read(string text) => SignalIndex.Read(new StringReader(text));

    [Fact]
    public void ThreeRowsAreThreeSightingsWithTheirKinds()
    {
        var index = Read(Tsv(
            "1001\tAlpha\t1.5\t-2.25\t3\tNavBeacon\tNav Beacon\t2026-10-15",
            "1001\tAlpha\t1.5\t-2.25\t3\tResourceExtraction\t$MULTIPLAYER_SCENARIO14_TITLE;\t2026-10-14",
            "1004\tDelta\t-100.125\t20\t30000.5\tMegaship\tKolo Gryphon\t2026-10-13"));

        Assert.Equal(3, index.Count);
        Assert.Equal(0, index.Malformed);

        Assert.Equal(
            new SignalSighting(
                1001, "Alpha", new StarPosition(1.5, -2.25, 3), "NavBeacon", "Nav Beacon", SignalKind.NavBeacon,
                new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero)),
            index[0]);

        Assert.Equal(
            new SignalSighting(
                1001, "Alpha", new StarPosition(1.5, -2.25, 3), "ResourceExtraction", "Resource extraction site",
                SignalKind.ResourceExtractionSite, new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero)),
            index[1]);

        Assert.Equal(
            new SignalSighting(
                1004, "Delta", new StarPosition(-100.125, 20, 30000.5), "Megaship", "Kolo Gryphon", SignalKind.Megaship,
                new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero)),
            index[2]);
    }

    [Fact]
    public void AFileWithNoHeaderIsRefused()
    {
        var text = "1001\tAlpha\t1.5\t-2.25\t3\tNavBeacon\tNav Beacon\t2026-10-15\n";

        Assert.Throws<InvalidDataException>(() => Read(text));
        Assert.Throws<InvalidDataException>(() => Read(string.Empty));
    }

    [Fact]
    public void AMalformedRowIsSkippedAndCounted()
    {
        var index = Read(Tsv(
            "1001\tAlpha\t1.5\t-2.25\t3\tNavBeacon\tNav Beacon\t2026-10-15",
            "1002\tBeta\tnorth\t0\t0\tInstallation\tOld Depot\t2026-10-15",
            "1003\tGamma\t0\t0\t0\tInstallation\tOld Depot",
            "1004\tDelta\t0\t0\t0\tInstallation\tOld Depot\t15/10/2026"));

        Assert.Equal(3, index.Malformed);
        Assert.Equal("Alpha", Assert.Single(index).SystemName);
    }

    [Fact]
    public void ADollarCodeWithNoKindIsLeftOutRatherThanCountedOrShown()
    {
        var index = Read(Tsv(
            "1002\tBeta\t1.5\t-2.25\t3\tGeneric\t$Warzone_Low;\t2026-10-15",
            "1003\tGamma\t1.5\t-2.25\t3\tInstallation\tMining Platform\t2026-10-15"));

        Assert.Equal(0, index.Malformed);
        Assert.Equal("Mining Platform", Assert.Single(index).SignalName);
    }

    [Fact]
    public void ARowsOwnNameIsKeptWhereItHasNoKind()
    {
        var sighting = Assert.Single(Read(Tsv("1003\tGamma\t0\t0\t0\tGeneric\tAbandoned Probe\t2026-10-15")));

        Assert.Null(sighting.Kind);
        Assert.Equal("Abandoned Probe", sighting.SignalName);
    }

    [Fact]
    public void PositionsAreReadTheSameUnderACommaDecimalCulture()
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            var sighting = Assert.Single(Read(Tsv("1001\tAlpha\t1.5\t-2.25\t3\tNavBeacon\tNav Beacon\t2026-10-15")));

            Assert.Equal(new StarPosition(1.5, -2.25, 3), sighting.Position);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void ALineLongerThanTheLimitRefusesTheFile()
    {
        var text = Tsv("1001\tAlpha\t1.5\t-2.25\t3\tInstallation\t" + new string('x', SignalIndex.MaxLineLength) + "\t2026-10-15");

        Assert.Throws<InvalidDataException>(() => Read(text));
    }
}
