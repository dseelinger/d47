using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

[Trait("Category", "Integration")]
public sealed class ASquadronsFactionSurvivesARestartTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-squadron-factions-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private SquadronFactions Open()
    {
        var store = new SquadronFactions(
            Path.Combine(_folder, "squadron-factions.json"), new DiskFileSystem(), NullLogger<SquadronFactions>.Instance);

        store.Load();
        return store;
    }

    private static SquadronState Squadron(string line)
    {
        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
        return SquadronState.None.Apply(parsed!);
    }

    [Fact]
    public void AStoredFactionIsHeldAfterTheStoreIsOpenedAgain()
    {
        var squadron = Squadron("""{ "timestamp":"2026-10-19T12:00:00Z", "event":"SquadronStartup", "SquadronName":"GREYBEARD DELTA", "SquadronID":1234 }""");

        Assert.True(Open().Set("F1", squadron, "Hutton Truckers"));

        var reopened = Open();

        Assert.Equal("Hutton Truckers", reopened.For("F1", squadron));
        Assert.Null(reopened.For("F2", squadron));
    }
}
