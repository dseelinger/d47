using D47.Core.Journal;
using D47.Core.Logbook;
using D47.Core.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Logbook;

public sealed class AShipIsNamedFromTheHullTableTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private static string Rendered(string symbol, string? shipName = null)
    {
        var corpus = new JournalCorpus();
        var name = shipName is null ? string.Empty : $",\"ShipName\":\"{shipName}\"";

        corpus.Journal(
            Evening,
            JournalCorpus.Event(Evening, "LoadGame", $"\"Name\":\"Jameson\",\"FID\":\"F1\",\"Ship\":\"{symbol}\"{name},\"Credits\":1000000"),
            JournalCorpus.Event(Evening.AddMinutes(1), "Docked", "\"StationName\":\"Seedbox\",\"StarSystem\":\"ICZ JX-T b3-6\""));

        return new LogDigestBuilder(corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance)
            .Build(corpus.Files, new LogRange
            {
                Span = LogSpan.Session,
                From = Evening.AddMinutes(-1),
                To = Evening.AddDays(1),
                Label = "the last session",
            })
            .Render();
    }

    [Theory]
    [InlineData("cobramkv", "a Cobra Mk V")]
    [InlineData("explorer_nx", "a Caspian Explorer")]
    [InlineData("anacondamkii", "an Anaconda Mk II")]
    public void ASymbolWithoutALocalisedNameIsSaidAsTheTableNamesIt(string symbol, string expected) =>
        Assert.Contains(expected, Rendered(symbol), StringComparison.Ordinal);

    [Fact]
    public void ANamedShipKeepsItsName() =>
        Assert.Contains("the Cobra Mk V \"Insti-taneous\"", Rendered("cobramkv", "Insti-taneous"), StringComparison.Ordinal);

    [Theory]
    [InlineData("", "you were flying a Cobra Mk V.")]
    [InlineData(",\"ShipName\":\"Reaper\"", "you were flying Reaper, a Cobra Mk V.")]
    public void TheMemoryFactNamesTheHullFromTheTable(string name, string expected)
    {
        var store = new GameStateStore();
        store.Apply(Parsed("""{"timestamp":"3311-04-01T08:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        store.Apply(Parsed("{\"timestamp\":\"3311-04-01T08:31:00Z\",\"event\":\"Loadout\",\"Ship\":\"cobramkv\"" + name + ",\"ShipID\":7,\"Modules\":[]}"));
        store.Apply(Parsed("""{"timestamp":"3311-04-01T08:32:00Z","event":"Location","StarSystem":"Deciat","SystemAddress":1}"""));

        Assert.Equal("Cobra Mk V", store.Active!.Ship.TypeSaid);
        Assert.Equal(expected, MemoryObserver.ShipFact(store.Active!));
    }

    private static JournalEvent Parsed(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
