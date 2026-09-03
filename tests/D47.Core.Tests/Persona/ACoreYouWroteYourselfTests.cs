using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// A core the Commander wrote (remediation.md 11, item 9).
/// <para>
/// Eleven Guardian cores ship. This is the twelfth onwards: a name, the character in the
/// Commander's own words, and how it should sound. Everything else about the frame is supplied,
/// because a Commander should get a working companion out of a name and a paragraph rather than
/// out of seven boxes.
/// </para>
/// </summary>
public class ACoreYouWroteYourselfTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-own-personas", Guid.NewGuid().ToString("n"));

    private OwnPersonaStore Store()
    {
        Directory.CreateDirectory(_folder);

        var store = new OwnPersonaStore(
            Path.Combine(_folder, "personas.json"),
            NullLogger<OwnPersonaStore>.Instance);

        PersonaCatalog.Own = () => [.. store.Cores.Select(core => core.AsPersona())];

        return store;
    }

    public void Dispose()
    {
        // The catalogue's source is static, so a test that left one pointing at a deleted folder
        // would hand every later test somebody else's cores.
        PersonaCatalog.Own = null;

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static OwnPersona Written(string name, string body = "You are terse and you like mining.") =>
        new(OwnPersonaStore.IdFor(name, []), name, body);

    [Fact]
    public void ItJoinsThePickerBesideTheOnesThatShip()
    {
        var store = Store();

        store.Save([Written("Rusty")]);

        Assert.Contains(PersonaCatalog.All, persona => persona.Name == "Rusty");

        // The shipped eleven come first, so writing a twelfth does not move them.
        Assert.Equal("warden", PersonaCatalog.All[0].Id);
        Assert.Equal(PersonaCatalog.Shipped.Count + 1, PersonaCatalog.All.Count);
    }

    [Fact]
    public void ItCanBeSelectedByItsId()
    {
        var store = Store();
        var core = Written("Rusty");

        store.Save([core]);

        Assert.True(PersonaCatalog.Knows(core.Id));
        Assert.Equal("Rusty", PersonaCatalog.Resolve(core.Id).Name);
    }

    /// <summary>
    /// The frame is supplied and only the character is the Commander's. The shared preamble and
    /// the standing instructions wrap it exactly as they wrap a shipped core — they are what hold
    /// the cast together and what stops a model sanding a persona toward pleasant over a session.
    /// </summary>
    [Fact]
    public void TheFrameIsStillWrappedAroundIt()
    {
        var block = Written("Rusty", "You are terse and you like mining.").AsPersona().RenderBlock();

        Assert.Contains(PersonaCatalog.Preamble, block, StringComparison.Ordinal);
        Assert.Contains(D47.Core.Persona.Persona.StandingInstructions, block, StringComparison.Ordinal);
        Assert.Contains("you like mining", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// A shipped id can never be shadowed by one somebody wrote, so "warden" is still Warden
    /// whatever a Commander calls their own core.
    /// </summary>
    [Fact]
    public void AShippedCoreCannotBeShadowed()
    {
        var store = Store();

        store.Save([new OwnPersona("warden", "Not Warden", "You are an impostor.")]);

        Assert.Equal("Warden", PersonaCatalog.Resolve("warden").Name);
    }

    /// <summary>And the ids it mints are prefixed, which is what makes that true.</summary>
    [Fact]
    public void MintedIdsCannotCollideWithAShippedOne()
    {
        Assert.StartsWith("own.", OwnPersonaStore.IdFor("Warden", []), StringComparison.Ordinal);
    }

    /// <summary>Two cores with the same name still get one id each.</summary>
    [Fact]
    public void TwoCoresWithOneNameGetDifferentIds()
    {
        var first = Written("Rusty");
        var second = OwnPersonaStore.IdFor("Rusty", [first]);

        Assert.NotEqual(first.Id, second);
    }

    /// <summary>
    /// The id does not follow the name, so correcting a typo does not leave the Commander talking
    /// to Warden again.
    /// </summary>
    [Fact]
    public void RenamingDoesNotChangeWhichCoreIsSelected()
    {
        var store = Store();
        var core = Written("Rsuty");

        store.Save([core]);
        store.Save([core with { Name = "Rusty" }]);

        Assert.Equal("Rusty", PersonaCatalog.Resolve(core.Id).Name);
    }

    /// <summary>A bad entry is reported and the rest of the file still loads.</summary>
    [Fact]
    public void OneBadCoreDoesNotCostTheOthers()
    {
        var store = Store();

        store.Save(
        [
            Written("Rusty"),
            new OwnPersona("own.empty", "Hollow", string.Empty),
        ]);

        Assert.Equal("Rusty", Assert.Single(store.Cores).Name);

        var problem = Assert.Single(store.Problems);

        Assert.Contains("Hollow", problem.Which, StringComparison.Ordinal);
    }

    /// <summary>A hand edit is live without a restart, like every other file d47 polls.</summary>
    [Fact]
    public void AHandEditIsLiveWithNoRestart()
    {
        var store = Store();

        File.WriteAllText(
            store.Path,
            """
            { "cores": [ { "id": "own.rusty", "name": "Rusty", "body": "You are terse." } ] }
            """);

        Assert.True(store.Poll());
        Assert.Equal("Rusty", Assert.Single(store.Cores).Name);
    }

    /// <summary>
    /// The voice description reaches the pairing, which is the whole reason it is a field rather
    /// than a picker: a provider's catalogue is its own and a description outlives it.
    /// </summary>
    [Fact]
    public void TheVoiceDescriptionIsCarried()
    {
        var core = new OwnPersona("own.rusty", "Rusty", "You are terse.", "A gravelly old miner.");

        Assert.Equal("A gravelly old miner.", core.AsPersona().VoiceHint.Description);
    }

    /// <summary>And a core written without one still says something a pairing can work with.</summary>
    [Fact]
    public void AVoicelessCoreStillDescribesItself()
    {
        Assert.Contains("Rusty", Written("Rusty").AsPersona().VoiceHint.Description, StringComparison.Ordinal);
    }

    /// <summary>
    /// The picker still offers a drop-down rather than a search window. Declaring a choice source
    /// so custom cores appear made the row an "open vocabulary" and flipped it to the searchable
    /// picker — eleven named cores are a list somebody picks from, not a haystack.
    /// </summary>
    [Fact]
    public void ThePickerIsStillAClosedList()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Single(candidate => candidate.Key == D47.Core.Capabilities.Builtin.PersonaCapability.PersonaKey);

        Assert.False(row.IsOpenVocabulary);
        Assert.NotEmpty(row.Choices);
    }

    /// <summary>And it offers the Commander's own cores as well as the eleven.</summary>
    [Fact]
    public void ThePickerOffersWhatTheCommanderWrote()
    {
        var store = Store();

        store.Save([Written("Rusty")]);

        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Single(candidate => candidate.Key == D47.Core.Capabilities.Builtin.PersonaCapability.PersonaKey);

        Assert.Contains(
            row.ChoicesFor(surface.Settings.Current),
            id => PersonaCatalog.Resolve(id).Name == "Rusty");
    }

    /// <summary>
    /// And the panel offers the way in. The store, the editor window and the catalogue wiring
    /// were all built and the row that reaches them was never declared — a key with nothing
    /// carrying it — so the only way to write a core was to write the file by hand and restart.
    /// A test that asked what the picker offered could not see that, because the picker is fed
    /// by the catalogue and the missing row is on the panel.
    /// </summary>
    [Fact]
    public void ThePanelOffersTheWayIntoWritingOne()
    {
        var store = Store();

        store.Save([Written("Rusty")]);

        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var row = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Single(candidate => candidate.Key == D47.Core.Capabilities.Builtin.PersonaCapability.OwnKey);

        // Info, so the model cannot write it, and it states what is there before it is pressed.
        Assert.Equal(D47.Core.Capabilities.SettingKind.Info, row.Kind);
        Assert.Contains("Rusty", row.Binding!.Read(surface.Settings.Current));
    }
}
