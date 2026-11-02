using System.Text.Json;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

/// <summary>Reads one journal line into its <see cref="EventReading"/> and picks rows out of it.</summary>
public static class EventReadingFixtures
{
    public static EventReading Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var kind = document.RootElement.GetProperty("event").GetString()!;
        var log = new JournalLog();

        log.Add([new JournalEvent(DateTimeOffset.Parse("2025-07-04T00:00:00Z"), kind, document.RootElement.Clone())]);

        return EventReadings.For(Assert.Single(log.Read(noise: true)));
    }

    public static ReadingRow Row(EventReading reading, string label) =>
        Assert.Single(reading.Rows, row => row.Label == label);

    public static string Joined(EventReading reading, string label) =>
        string.Join(" · ", Row(reading, label).Values.Select(value => value.Text));
}
