using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class EveryInterdictionOfTheCommanderIsCountedTests
{
    private static int InterdictionsAfter(string kind, string fields)
    {
        var line = $$"""{"timestamp":"3311-04-02T19:00:00Z","event":"{{kind}}",{{fields}}}""";
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed), line);
        return SessionSummary.Empty.Apply(parsed!).Interdictions;
    }

    [Fact]
    public void ASubmittedInterdictionCounts() =>
        Assert.Equal(1, InterdictionsAfter("Interdicted", "\"Submitted\":true,\"Interdictor\":\"Pirate\",\"IsPlayer\":false,\"Faction\":\"Pirates\""));

    [Fact]
    public void AFoughtInterdictionCounts() =>
        Assert.Equal(1, InterdictionsAfter("Interdicted", "\"Submitted\":false,\"Interdictor\":\"Pirate\",\"IsPlayer\":false,\"Faction\":\"Pirates\""));

    [Fact]
    public void AnEscapedInterdictionDoesNotCount() =>
        Assert.Equal(0, InterdictionsAfter("EscapeInterdiction", "\"Interdictor\":\"Pirate\",\"IsPlayer\":false"));

    [Fact]
    public void TheCommanderInterdictingAnotherShipDoesNotCount() =>
        Assert.Equal(0, InterdictionsAfter("Interdiction", "\"Success\":true,\"Interdicted\":\"Trader\",\"IsPlayer\":false,\"Faction\":\"Traders\""));
}
