using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The one field of Status.json read for the galaxy-map macro (2026-08-21): <c>GuiFocus</c>,
/// which is how d47 knows the map is showing before it types into it, and that it has closed
/// again afterwards.
/// </summary>
public class GameStatusReaderTests
{
    private static GameStatus Read(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), "d47-status-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, GameStatusReader.FileName), json);

            var reader = new GameStatusReader(directory, NullLogger.Instance);
            Assert.True(reader.Poll());

            return reader.Current;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TheGalaxyMapIsReadOffGuiFocus()
    {
        var status = Read("""{ "timestamp":"2026-08-21T21:11:02Z", "event":"Status", "Flags":16777224, "Flags2":0, "GuiFocus":6 }""");

        Assert.Equal(GuiFocus.GalaxyMap, status.GuiFocus);
        Assert.True(status.InShip);
    }

    /// <summary>Absent is the cockpit, which is what Elite writes as zero as well.</summary>
    [Fact]
    public void NoGuiFocusIsTheCockpit()
    {
        var status = Read("""{ "timestamp":"2026-08-21T21:11:02Z", "event":"Status", "Flags":16777224, "Flags2":0 }""");

        Assert.Equal(GuiFocus.None, status.GuiFocus);
    }
}
