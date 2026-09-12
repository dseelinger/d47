using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AJournalEntryKnowsItsSystemTests
{
    private static JournalEntry Entry(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        var log = new JournalLog();

        log.Add([parsed!]);

        return Assert.Single(log.Entries);
    }

    [Theory]
    [InlineData("""{ "timestamp":"2026-02-10T09:05:00Z", "event":"FSDJump", "StarSystem":"LHS 3447", "SystemAddress":5306465653474 }""")]
    [InlineData("""{ "timestamp":"2026-02-10T09:00:00Z", "event":"Location", "Docked":false, "StarSystem":"LHS 3447" }""")]
    [InlineData("""{ "timestamp":"2026-02-10T09:10:00Z", "event":"Docked", "StationName":"Worlidge Terminal", "StarSystem":"LHS 3447" }""")]
    [InlineData("""{ "timestamp":"2026-02-10T09:15:00Z", "event":"CarrierJump", "Docked":true, "StarSystem":"LHS 3447" }""")]
    public void AnEventWithASystemCarriesIt(string line)
    {
        Assert.Equal("LHS 3447", Entry(line).StarSystem);
    }

    [Fact]
    public void AnEventWithoutOneCarriesNull()
    {
        Assert.Null(Entry("""{ "timestamp":"2026-02-10T09:20:00Z", "event":"Music", "MusicTrack":"Exploration" }""").StarSystem);
    }
}
