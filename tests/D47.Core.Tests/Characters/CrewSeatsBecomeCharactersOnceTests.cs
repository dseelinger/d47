using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Characters;

public class CrewSeatsBecomeCharactersOnceTests
{
    private const string SeatsPath = @"C:\d47\data\crew-seats.json";

    private const string MigratedPath = @"C:\d47\data\" + SeatMigration.MigratedName;

    /// <summary>Two ships for F1, one sharing a name across both, and one ship for F2.</summary>
    private const string TwoShips = """
        {
          "ships": [
            {
              "commanderFid": "F1",
              "shipId": 7,
              "hull": "python",
              "seats": [
                { "id": "0000000a", "role": "Helm", "name": "Vasquez", "voice": { "provider": "kokoro", "voiceId": "am_adam" } },
                { "id": "0000000b", "role": "Custom", "title": "Quartermaster", "name": "Teague" }
              ]
            },
            {
              "commanderFid": "F1",
              "shipId": 9,
              "hull": "anaconda",
              "seats": [
                { "id": "0000000c", "role": "Navigation", "name": "vasquez" },
                { "id": "0000000d", "role": "ScienceOfficer", "name": "Imani" }
              ]
            },
            {
              "commanderFid": "F2",
              "shipId": 3,
              "hull": "asp",
              "seats": [
                { "id": "0000000e", "role": "FirstOfficer", "name": "Marlow" }
              ]
            }
          ]
        }
        """;

    private static readonly IReadOnlyDictionary<string, string> RoleVoices =
        new Dictionary<string, string> { ["scienceOfficer"] = "bf_emma" };

    private static int Migrate(IFileSystem files, CharacterStore store) =>
        SeatMigration.Run(files, SeatsPath, store, "kokoro", RoleVoices, NullLogger.Instance);

    private static MemoryFileSystem Seats(string text)
    {
        var files = new MemoryFileSystem();
        files.WriteText(SeatsPath, text);
        return files;
    }

    [Fact]
    public void SeatsOnTwoShipsBecomeOneCharacterPerName()
    {
        var read = SeatMigration.Read(TwoShips, "kokoro", RoleVoices);

        Assert.Equal(
            [
                new CustomCharacter("0000000a", "Vasquez", new CharacterVoice("kokoro", "am_adam"), "Helm. Navigation.", Aboard: true, System: null),
                new CustomCharacter("0000000b", "Teague", null, "Quartermaster.", Aboard: true, System: null),
                new CustomCharacter("0000000d", "Imani", new CharacterVoice("kokoro", "bf_emma"), "Science officer.", Aboard: true, System: null),
            ],
            read["F1"]);

        Assert.Equal("Marlow", Assert.Single(read["F2"]).Name);
    }

    [Fact]
    public void TheMigrationRunsOnceAndRenamesTheFile()
    {
        var files = Seats(TwoShips);
        var store = Characters.Store(files);

        Assert.Equal(4, Migrate(files, store));
        Assert.Equal(0, Migrate(files, store));

        Assert.Equal(["Vasquez", "Teague", "Imani"], store.CharactersOf("F1").Select(character => character.Name));
        Assert.Equal(["Marlow"], store.CharactersOf("F2").Select(character => character.Name));
        Assert.Null(files.Stat(SeatsPath));
        Assert.Equal(TwoShips, files.ReadText(MigratedPath));
    }

    [Fact]
    public void ANameTheCommanderAlreadyHasIsNotAddedAgain()
    {
        var files = Seats(TwoShips);
        var store = Characters.Store(files);
        var own = Characters.Aboard("VASQUEZ", "My own.");

        Assert.Null(store.Set("F1", own));
        Assert.Equal(3, Migrate(files, store));

        Assert.Equal(own, store.CharactersOf("F1").Single(character => character.Name == "VASQUEZ"));
        Assert.DoesNotContain(store.CharactersOf("F1"), character => character.Name == "Vasquez");
    }

    [Fact]
    public void NoSeatFileChangesNothing()
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);

        Assert.Equal(0, Migrate(files, store));
        Assert.Empty(store.CharactersOf("F1"));
        Assert.Null(files.Stat(MigratedPath));
    }

    [Fact]
    public void ANavigationCalloutIsSpokenByTheCoreWithAMigratedHelmAboard()
    {
        var files = Seats(TwoShips);
        var store = Characters.Store(files);
        Migrate(files, store);

        Assert.Contains(store.CharactersOf("F1"), character => character.Aboard && character.Notes!.StartsWith("Helm.", StringComparison.Ordinal));

        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance)
            .Add(new FixedCallout("route", new Announcement("route.progress", "Three jumps left.")));

        engine.Tick(new CalloutContext(new DateTimeOffset(3311, 4, 1, 12, 0, 0, TimeSpan.Zero), false, null, GameStatus.Unknown, NavRoute.None, []));

        var said = Assert.Single(engine.Drain());

        Assert.Equal(VoiceRole.ShipAi, said.Voice);
        Assert.Null(said.Speaker);
    }

    private sealed class FixedCallout(string id, Announcement announcement) : ICallout
    {
        public string Id => id;

        public IEnumerable<Announcement> Examine(CalloutContext context) => [announcement];
    }
}
