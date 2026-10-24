using D47.Core.Characters;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Characters;

internal static class Characters
{
    public const string Location = @"C:\d47\data\characters.json";

    public static CharacterStore Store(IFileSystem files) =>
        new(Location, files, NullLogger<CharacterStore>.Instance);

    public static CustomCharacter Aboard(string name, string? notes = null) =>
        new(CustomCharacter.NewId(), name, null, notes, Aboard: true, System: null);

    public static CustomCharacter Away(string name, string? system) =>
        new(CustomCharacter.NewId(), name, null, null, Aboard: false, system);
}

public class ACharacterIsKeptAcrossARestartTests
{
    [Fact]
    public void EveryFieldReadsBackInANewStore()
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);
        var vex = new CustomCharacter(
            CustomCharacter.NewId(), "Vex", new CharacterVoice("elevenlabs", "v-123"), "Dry, never hurried.", Aboard: false, "Shinrarta Dezhra");
        var orla = Characters.Aboard("Orla");

        Assert.Null(store.Set("F1", vex));
        Assert.Null(store.Set("F1", orla));
        Assert.Null(store.SetNotes("F1", CarrierCharacter.Captain, "Gruff, fond of the crew."));
        Assert.Null(store.SetNotes("F1", CarrierCharacter.Tower, "By the book."));
        Assert.Null(store.SetCastNotes("F1", "the-long-dark", "mara", "Softer with me."));

        var reread = Characters.Store(files);

        Assert.True(reread.Poll());
        Assert.Equal([vex, orla], reread.CharactersOf("F1"));
        Assert.Equal("Gruff, fond of the crew.", reread.NotesFor("F1", CarrierCharacter.Captain));
        Assert.Equal("By the book.", reread.NotesFor("F1", CarrierCharacter.Tower));
        Assert.Equal("Softer with me.", reread.CastNotes("F1", "the-long-dark", "mara"));
        Assert.Null(reread.CastNotes("F1", "the-long-dark", "someone-else"));
        Assert.Empty(reread.Problems);
    }

    [Fact]
    public void ACharactersPictureIsNamedAfterItsId()
    {
        var character = Characters.Aboard("Vex") with { Id = "0a1b2c3d" };

        Assert.Equal("character.0a1b2c3d", character.Picture);
    }

    [Fact]
    public void SavingTheSameIdReplacesTheCharacterInPlace()
    {
        var store = Characters.Store(new MemoryFileSystem());
        var vex = Characters.Aboard("Vex");
        var orla = Characters.Aboard("Orla");

        store.Set("F1", vex);
        store.Set("F1", orla);
        Assert.Null(store.Set("F1", vex with { Name = "Vexley", Aboard = false, System = "Sol" }));

        Assert.Equal(["Vexley", "Orla"], store.CharactersOf("F1").Select(character => character.Name));
        Assert.Equal("Sol", store.Find("F1", vex.Id)?.System);
    }

    [Fact]
    public void ASystemIsKeptWhileTheCharacterIsAboard()
    {
        var store = Characters.Store(new MemoryFileSystem());
        var vex = Characters.Away("Vex", "Sol");

        store.Set("F1", vex);
        store.Set("F1", vex with { Aboard = true });

        Assert.Equal("Sol", store.Find("F1", vex.Id)?.System);
    }

    [Fact]
    public void NamesNotesAndSystemsAreTrimmedAndEmptyNotesAreNone()
    {
        var store = Characters.Store(new MemoryFileSystem());
        var vex = new CustomCharacter(CustomCharacter.NewId(), "  Vex ", null, "   ", Aboard: false, " Sol ");

        store.Set("F1", vex);

        Assert.Equal(new CustomCharacter(vex.Id, "Vex", null, null, false, "Sol"), store.Find("F1", vex.Id));
    }

    [Fact]
    public void EmptyNotesClearWhatWasThere()
    {
        var store = Characters.Store(new MemoryFileSystem());

        store.SetNotes("F1", CarrierCharacter.Captain, "Gruff.");
        store.SetCastNotes("F1", "story", "mara", "Softer.");
        store.SetNotes("F1", CarrierCharacter.Captain, " ");
        store.SetCastNotes("F1", "story", "mara", null);

        Assert.Null(store.NotesFor("F1", CarrierCharacter.Captain));
        Assert.Null(store.CastNotes("F1", "story", "mara"));
    }

    [Fact]
    public void ARemovedCharacterIsGoneAfterARestart()
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);
        var vex = Characters.Aboard("Vex");
        var orla = Characters.Aboard("Orla");

        store.Set("F1", vex);
        store.Set("F1", orla);

        Assert.True(store.Remove("F1", vex.Id));
        Assert.False(store.Remove("F1", vex.Id));

        var reread = Characters.Store(files);
        reread.Poll();

        Assert.Equal([orla], reread.CharactersOf("F1"));
    }

    [Fact]
    public void AnotherWritersChangeIsPickedUpByPoll()
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);
        var other = Characters.Store(files);
        var changed = 0;
        store.Changed += () => changed++;

        store.Poll();
        other.Set("F1", Characters.Aboard("Vex"));

        Assert.True(store.Poll());
        Assert.False(store.Poll());
        Assert.Equal(["Vex"], store.CharactersOf("F1").Select(character => character.Name));
        Assert.Equal(1, changed);
    }
}

public class ACharacterBreakingARuleIsNotSavedTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ACharacterNeedsAName(string name) =>
        Refused(Characters.Aboard(name), CharacterField.Name, "Give this character a name.");

    [Fact]
    public void ANameIsAtMostThirtyTwoCharacters()
    {
        Refused(Characters.Aboard(new string('a', 33)), CharacterField.Name, "Keep the name to 32 characters.");

        Assert.Null(Characters.Store(new MemoryFileSystem()).Set("F1", Characters.Aboard(new string('a', 32))));
    }

    [Fact]
    public void ANameIsUsedOnceByEachCommanderWhateverItsCase()
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);
        store.Set("F1", Characters.Aboard("Vex"));
        var before = files.ReadText(Characters.Location);

        var refusal = store.Set("F1", Characters.Aboard("VEX"));

        Assert.Equal(new CharacterRefusal(CharacterField.Name, "You already have a character named VEX."), refusal);
        Assert.Equal(before, files.ReadText(Characters.Location));
        Assert.Single(store.CharactersOf("F1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ACharacterAwayNamesTheirSystem(string? system) =>
        Refused(Characters.Away("Vex", system), CharacterField.System, "Name the system they are in, or check Aboard.");

    [Fact]
    public void NotesAreAtMostSixHundredCharacters()
    {
        var store = Characters.Store(new MemoryFileSystem());
        var refusal = new CharacterRefusal(CharacterField.Notes, "Keep the notes to 600 characters.");
        var tooLong = new string('n', 601);

        Refused(Characters.Aboard("Vex", tooLong), CharacterField.Notes, refusal.Reason);
        Assert.Equal(refusal, store.SetNotes("F1", CarrierCharacter.Tower, tooLong));
        Assert.Equal(refusal, store.SetCastNotes("F1", "story", "mara", tooLong));
        Assert.Null(store.NotesFor("F1", CarrierCharacter.Tower));
        Assert.Null(store.CastNotes("F1", "story", "mara"));
        Assert.Null(store.Set("F1", Characters.Aboard("Vex", new string('n', 600))));
    }

    [Fact]
    public void CheckGivesTheSameRefusalWithoutWriting()
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);

        Assert.Equal(CharacterField.System, store.Check("F1", Characters.Away("Vex", null))?.Field);
        Assert.Null(store.Check("F1", Characters.Aboard("Vex")));
        Assert.Null(files.Stat(Characters.Location));
    }

    [Fact]
    public void AnIdThatIsNotEightHexCharactersIsAnError()
    {
        var store = Characters.Store(new MemoryFileSystem());

        Assert.Throws<ArgumentException>(() => store.Set("F1", Characters.Aboard("Vex") with { Id = "VEX" }));
    }

    [Fact]
    public void AHandEditedFileKeepsTheGoodEntriesAndNamesTheBadOnes()
    {
        var files = new MemoryFileSystem();
        files.WriteText(Characters.Location, """
            {
              "commanders": [
                {
                  "frontierId": "F1",
                  "characters": [
                    { "id": "00000001", "name": "Vex", "aboard": true },
                    { "id": "00000002", "name": "vex", "aboard": true },
                    { "id": "nothex!!", "name": "Orla", "aboard": true },
                    { "id": "00000003", "name": "Kade", "aboard": false },
                    { "id": "00000004", "name": "", "aboard": true },
                    { "id": "00000001", "name": "Tam", "aboard": true }
                  ],
                  "towerNotes": "TOO_LONG",
                  "cast": [
                    { "storyId": "story", "notes": "No cast id." },
                    { "storyId": "story", "castId": "mara", "notes": "Kept." },
                    { "storyId": "story", "castId": "mara", "notes": "Twice." }
                  ]
                }
              ]
            }
            """.Replace("TOO_LONG", new string('n', 601), StringComparison.Ordinal));
        var store = Characters.Store(files);

        store.Poll();

        Assert.Equal(["Vex"], store.CharactersOf("F1").Select(character => character.Name));
        Assert.Null(store.NotesFor("F1", CarrierCharacter.Tower));
        Assert.Equal("Kept.", store.CastNotes("F1", "story", "mara"));
        Assert.Equal(8, store.Problems.Count);
    }

    [Fact]
    public void AFileThatIsNotJsonIsOneProblemAndNoCharacters()
    {
        var files = new MemoryFileSystem();
        files.WriteText(Characters.Location, "{ not json");
        var store = Characters.Store(files);

        store.Poll();

        Assert.Empty(store.CharactersOf("F1"));
        Assert.Single(store.Problems);
    }

    private static void Refused(CustomCharacter character, CharacterField field, string reason)
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);

        Assert.Equal(new CharacterRefusal(field, reason), store.Set("F1", character));
        Assert.Empty(store.CharactersOf("F1"));
        Assert.Null(files.Stat(Characters.Location));
    }
}

public class EachCommanderKeepsTheirOwnCharactersTests
{
    [Fact]
    public void TwoCommandersSeeOnlyTheirOwn()
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);

        store.Set("F1", Characters.Aboard("Vex"));
        store.SetNotes("F1", CarrierCharacter.Captain, "One's captain.");
        store.SetCastNotes("F1", "story", "mara", "One's Mara.");
        store.Set("F2", Characters.Aboard("Orla"));

        var reread = Characters.Store(files);
        reread.Poll();

        Assert.Equal(["Vex"], reread.CharactersOf("F1").Select(character => character.Name));
        Assert.Equal(["Orla"], reread.CharactersOf("F2").Select(character => character.Name));
        Assert.Null(reread.NotesFor("F2", CarrierCharacter.Captain));
        Assert.Null(reread.CastNotes("F2", "story", "mara"));
        Assert.Equal("One's captain.", reread.NotesFor("F1", CarrierCharacter.Captain));
    }

    [Fact]
    public void TwoCommandersMayEachHaveACharacterOfTheSameName()
    {
        var store = Characters.Store(new MemoryFileSystem());

        Assert.Null(store.Set("F1", Characters.Aboard("Vex")));
        Assert.Null(store.Set("F2", Characters.Aboard("Vex")));
    }

    [Fact]
    public void RemovingOneCommandersCharacterLeavesTheOtherAlone()
    {
        var store = Characters.Store(new MemoryFileSystem());
        var vex = Characters.Aboard("Vex");

        store.Set("F1", vex);
        store.Set("F2", Characters.Aboard("Orla"));

        Assert.False(store.Remove("F2", vex.Id));
        Assert.True(store.Remove("F1", vex.Id));
        Assert.Single(store.CharactersOf("F2"));
    }

    [Fact]
    public void NoCommanderIsAKeyOfItsOwn()
    {
        var store = Characters.Store(new MemoryFileSystem());

        store.Set(null, Characters.Aboard("Vex"));

        Assert.Single(store.CharactersOf(CharacterStore.NoCommander));
        Assert.Empty(store.CharactersOf("F1"));
    }

    [Fact]
    public void AnIdHeldByAnotherCommanderIsAnError()
    {
        var store = Characters.Store(new MemoryFileSystem());
        var vex = Characters.Aboard("Vex");

        store.Set("F1", vex);

        Assert.Throws<ArgumentException>(() => store.Set("F2", vex));
        Assert.Empty(store.CharactersOf("F2"));
    }

    [Fact]
    public void AnIdTwiceInTheFileIsKeptOnlyTheFirstTime()
    {
        var files = new MemoryFileSystem();
        files.WriteText(Characters.Location, """
            {
              "commanders": [
                { "frontierId": "F1", "characters": [ { "id": "0000000a", "name": "Vex", "aboard": true } ] },
                { "frontierId": "F2", "characters": [ { "id": "0000000a", "name": "Orla", "aboard": true } ] }
              ]
            }
            """);
        var store = Characters.Store(files);

        store.Poll();

        Assert.Single(store.CharactersOf("F1"));
        Assert.Empty(store.CharactersOf("F2"));
        Assert.Single(store.Problems);
    }
}

public class AMissingCharacterFileReadsAsEmptyTests
{
    [Fact]
    public void NoFileIsNoCharactersAndNoProblems()
    {
        var store = Characters.Store(new MemoryFileSystem());

        Assert.False(store.Poll());
        Assert.Empty(store.CharactersOf("F1"));
        Assert.Null(store.NotesFor("F1", CarrierCharacter.Captain));
        Assert.Null(store.CastNotes("F1", "story", "mara"));
        Assert.Empty(store.Problems);
    }

    [Fact]
    public void AFileDeletedAfterReadingEmptiesTheStore()
    {
        var files = new MemoryFileSystem();
        var store = Characters.Store(files);
        store.Set("F1", Characters.Aboard("Vex"));

        files.Delete(Characters.Location);

        Assert.True(store.Poll());
        Assert.Empty(store.CharactersOf("F1"));
    }
}
