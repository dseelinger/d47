using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>The journal kept for the Commander to read (https://github.com/dseelinger/d47/issues/51).</summary>
public class TheJournalIsReadableTests
{
    private static JournalEvent Event(string kind, string json, int second = 0) =>
        new(new DateTimeOffset(2026, 8, 27, 12, 3, second, TimeSpan.Zero),
            kind,
            JsonDocument.Parse(json).RootElement);

    [Fact]
    public void AnEventBecomesALineAndItsFields()
    {
        var log = new JournalLog();

        log.Add([Event("FSDTarget", """{"event":"FSDTarget","Name":"Kusauts","StarClass":"K"}""")]);

        var entry = Assert.Single(log.Read());

        Assert.Equal("FSDTarget", entry.Kind);
        Assert.Contains("Kusauts", entry.Said);

        // The fields, indented, exactly as Elite wrote them.
        Assert.Contains("\"StarClass\"", entry.Raw);
        Assert.Contains("Kusauts", entry.Raw);
    }

    [Fact]
    public void TheNewestLineIsFirst()
    {
        var log = new JournalLog();

        log.Add([
            Event("Docked", """{"event":"Docked"}""", 1),
            Event("Undocked", """{"event":"Undocked"}""", 2),
        ]);

        Assert.Equal("Undocked", log.Read()[0].Kind);
    }

    /// <summary>Noise is kept and marked, never dropped.</summary>
    [Fact]
    public void NoiseIsHiddenByDefaultAndCanBeAskedFor()
    {
        var log = new JournalLog();

        log.Add([
            Event("ShipLocker", """{"event":"ShipLocker"}"""),
            Event("Docked", """{"event":"Docked"}"""),
        ]);

        Assert.Equal(2, log.Count);
        Assert.Equal("Docked", Assert.Single(log.Read()).Kind);
        Assert.Equal(2, log.Read(noise: true).Count);
    }

    /// <summary>Bounded, because the file is not.</summary>
    [Fact]
    public void TheOldestFallOffTheEnd()
    {
        var log = new JournalLog(keep: 3);

        for (var i = 0; i < 10; i++)
        {
            log.Add([Event("Docked", $$"""{"event":"Docked","n":{{i}}}""")]);
        }

        Assert.Equal(3, log.Count);
        Assert.Contains("\"n\": 9", log.Read()[0].Raw);
    }

    /// <summary>
    /// An event nobody wrote a sentence for still lists, and its fields are exactly as complete as any
    /// other's — which is the property that made this design win over a pure reformatter.
    /// </summary>
    [Fact]
    public void AnEventWithNoSentenceStillReads()
    {
        var log = new JournalLog();

        log.Add([Event("SomeEventFrontierAddedYesterday", """{"event":"SomeEventFrontierAddedYesterday","X":1}""")]);

        var entry = Assert.Single(log.Read());

        Assert.False(string.IsNullOrWhiteSpace(entry.Said));
        Assert.Contains("\"X\"", entry.Raw);
    }

    /// <summary>The line carries the time, because "when" is half of reading a log.</summary>
    [Fact]
    public void ALineLeadsWithItsTime()
    {
        var log = new JournalLog();

        log.Add([Event("Docked", """{"event":"Docked"}""", 15)]);

        Assert.Matches(@"^\d\d:\d\d:15  ", log.Read()[0].Line);
    }
}
