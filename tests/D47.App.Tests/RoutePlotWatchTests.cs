using D47.App.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The route half of "did the galaxy map macro work".</summary>
public class RoutePlotWatchTests
{
    private static readonly TimeSpan Quick = TimeSpan.FromMilliseconds(400);

    private static string Route(string timestamp, params string[] systems) =>
        "{ \"timestamp\":\"" + timestamp + "\", \"event\":\"NavRoute\", \"Route\":[ "
        + string.Join(", ", systems.Select((system, index) =>
            "{ \"StarSystem\":\"" + system + "\", \"SystemAddress\":" + (1000 + index)
            + ", \"StarPos\":[0,0," + index + "], \"StarClass\":\"K\" }"))
        + " ] }";

    private static (string Directory, NavRouteReader Reader) Folder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "d47-plotwatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return (directory, new NavRouteReader(directory, NullLogger.Instance));
    }

    private static void Write(string directory, string json, DateTime writtenUtc)
    {
        var path = Path.Combine(directory, NavRouteReader.FileName);
        File.WriteAllText(path, json);
        File.SetLastWriteTimeUtc(path, writtenUtc);
    }

    [Fact]
    public async Task ARouteThatWasAlreadyThereIsNotEvidenceTheKeysDidAnything()
    {
        var (directory, reader) = Folder();

        try
        {
            Write(directory, Route("2026-08-22T00:20:00Z", "Oppi", "Scorpii Sector BB-O a6-2"), new DateTime(2026, 8, 22, 0, 20, 0, DateTimeKind.Utc));
            reader.Poll();

            var watch = new RoutePlotWatch(reader, NullLogger.Instance, () => null, Quick);

            Assert.False((await watch.ConfirmAsync("Scorpii Sector BB-O a6-2", TestContext.Current.CancellationToken)).Confirmed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ARouteWrittenAfterTheWatchOpenedCounts()
    {
        var (directory, reader) = Folder();

        try
        {
            Write(directory, Route("2026-08-22T00:20:00Z", "Oppi", "HR 7169"), new DateTime(2026, 8, 22, 0, 20, 0, DateTimeKind.Utc));
            reader.Poll();

            var watch = new RoutePlotWatch(reader, NullLogger.Instance, () => null, Quick);

            // The plot lands while the watch is waiting.
            Write(directory, Route("2026-08-22T00:29:31Z", "Oppi", "Scorpii Sector BB-O a6-2"), new DateTime(2026, 8, 22, 0, 29, 31, DateTimeKind.Utc));

            Assert.True((await watch.ConfirmAsync("Scorpii Sector BB-O a6-2", TestContext.Current.CancellationToken)).Confirmed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ANewRouteToSomewhereElseIsNotTheOneAskedFor()
    {
        var (directory, reader) = Folder();

        try
        {
            var watch = new RoutePlotWatch(reader, NullLogger.Instance, () => null, Quick);

            Write(directory, Route("2026-08-22T00:29:31Z", "Oppi", "HR 7169"), new DateTime(2026, 8, 22, 0, 29, 31, DateTimeKind.Utc));

            Assert.False((await watch.ConfirmAsync("Scorpii Sector BB-O a6-2", TestContext.Current.CancellationToken)).Confirmed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// No file at all is "cannot tell", which sends the Commander somewhere different from "did not
    /// work".
    /// </summary>
    [Fact]
    public async Task NoRouteFileAtAllIsCannotTell()
    {
        var (directory, reader) = Folder();

        try
        {
            var watch = new RoutePlotWatch(reader, NullLogger.Instance, () => null, Quick);

            Assert.Null((await watch.ConfirmAsync("Colonia", TestContext.Current.CancellationToken)).Confirmed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A confirmed plot counts the jumps still ahead of the Commander, not the hops in the file (#120).</summary>
    [Fact]
    public async Task AConfirmedPlotCountsJumpsAheadRatherThanHopsInTheFile()
    {
        var (directory, reader) = Folder();

        try
        {
            var watch = new RoutePlotWatch(reader, NullLogger.Instance, () => "Oppi", Quick);

            Write(
                directory,
                Route("2026-08-22T00:29:31Z", "Oppi", "HR 7169", "Scorpii Sector BB-O a6-2"),
                new DateTime(2026, 8, 22, 0, 29, 31, DateTimeKind.Utc));

            var confirmation = await watch.ConfirmAsync("Scorpii Sector BB-O a6-2", TestContext.Current.CancellationToken);

            Assert.True(confirmation.Confirmed);
            Assert.Equal(2, confirmation.JumpsRemaining);
            Assert.NotNull(confirmation.DistanceRemaining);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A leg of unknown length blanks the distance without hiding the jump count.</summary>
    [Fact]
    public async Task AConfirmedPlotWithAnUnmeasuredLegGivesNoDistance()
    {
        var (directory, reader) = Folder();

        try
        {
            var watch = new RoutePlotWatch(reader, NullLogger.Instance, () => "Oppi", Quick);

            var json = "{ \"timestamp\":\"2026-08-22T00:29:31Z\", \"event\":\"NavRoute\", \"Route\":[ "
                + "{ \"StarSystem\":\"Oppi\", \"SystemAddress\":1000, \"StarPos\":[0,0,0], \"StarClass\":\"K\" }, "
                + "{ \"StarSystem\":\"HR 7169\", \"SystemAddress\":1001, \"StarClass\":\"K\" }, "
                + "{ \"StarSystem\":\"Scorpii Sector BB-O a6-2\", \"SystemAddress\":1002, \"StarPos\":[0,0,2], \"StarClass\":\"K\" } "
                + "] }";

            Write(directory, json, new DateTime(2026, 8, 22, 0, 29, 31, DateTimeKind.Utc));

            var confirmation = await watch.ConfirmAsync("Scorpii Sector BB-O a6-2", TestContext.Current.CancellationToken);

            Assert.True(confirmation.Confirmed);
            Assert.Equal(2, confirmation.JumpsRemaining);
            Assert.Null(confirmation.DistanceRemaining);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
