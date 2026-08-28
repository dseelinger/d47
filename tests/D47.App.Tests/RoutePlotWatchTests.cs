using D47.App.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The route half of "did the galaxy map macro work" (Phase 10, "Galaxy Map").
/// <para>
/// Reported 2026-08-21: the Commander plotted a route by hand, asked for the same one by voice,
/// and heard "course plotted" for a macro that had plotted nothing — the check read the file
/// after the keys and found a route that was already there. So the watch is opened before the
/// keys and only a newer write counts.
/// </para>
/// </summary>
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

            var watch = new RoutePlotWatch(reader, NullLogger.Instance, Quick);

            Assert.False(await watch.ConfirmAsync("Scorpii Sector BB-O a6-2", TestContext.Current.CancellationToken));
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

            var watch = new RoutePlotWatch(reader, NullLogger.Instance, Quick);

            // The plot lands while the watch is waiting.
            Write(directory, Route("2026-08-22T00:29:31Z", "Oppi", "Scorpii Sector BB-O a6-2"), new DateTime(2026, 8, 22, 0, 29, 31, DateTimeKind.Utc));

            Assert.True(await watch.ConfirmAsync("Scorpii Sector BB-O a6-2", TestContext.Current.CancellationToken));
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
            var watch = new RoutePlotWatch(reader, NullLogger.Instance, Quick);

            Write(directory, Route("2026-08-22T00:29:31Z", "Oppi", "HR 7169"), new DateTime(2026, 8, 22, 0, 29, 31, DateTimeKind.Utc));

            Assert.False(await watch.ConfirmAsync("Scorpii Sector BB-O a6-2", TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>No file at all is "cannot tell", which sends the Commander somewhere different from "did not work".</summary>
    [Fact]
    public async Task NoRouteFileAtAllIsCannotTell()
    {
        var (directory, reader) = Folder();

        try
        {
            var watch = new RoutePlotWatch(reader, NullLogger.Instance, Quick);

            Assert.Null(await watch.ConfirmAsync("Colonia", TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
