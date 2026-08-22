using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// Several Commanders, one installation (list.md Phase 44): the rows that are the Commander's
/// rather than the installation's are declared on the row, layered per Frontier id inside the
/// one settings file, and keep <i>unset</i> and <i>deliberately blank</i> apart.
/// </summary>
public class CommanderScopeTests
{
    private const string AboutMe = "llm.aboutMe";
    private const string CharacterSheet = "llm.characterSheet";

    /// <summary>
    /// The gate. A row says it is per Commander, and <see cref="CommanderScope"/> decides which
    /// fields the overlay reaches; the two are lists kept in two places, so this holds one against
    /// the other. A per-Commander field added without its row saying so fails here, and so does a
    /// row saying so with nothing behind it.
    /// </summary>
    [Fact]
    public void TheRowsDeclaredPerCommanderAreExactlyTheRowsTheOverlayReaches()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var stored = new D47Settings
        {
            Commanders =
            [
                new CommanderSettings
                {
                    CommanderFid = "F1",
                    AboutMe = "a story",
                    CharacterSheet = "a sheet",
                    ShipCoreShip = 7,
                },
            ],
        };

        var projected = CommanderScope.Project(stored, "F1");

        var rows = surface.Settings.Sections.SelectMany(section => section.Rows).ToList();

        var reached = rows
            .Where(row => row.Binding?.Read is { } read
                          && !string.Equals(read(stored), read(projected), StringComparison.Ordinal))
            .Select(row => row.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        var declared = rows
            .Where(row => row.Scope == SettingScope.Commander)
            .Select(row => row.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.Equal(declared, reached);
    }

    /// <summary>
    /// d47 runs before Elite has said who is flying. A value typed then is the installation's —
    /// the same rule that makes <c>MemoryStore.NoCommander</c> a real key — and is not
    /// retroactively attributed to whoever logs in first.
    /// </summary>
    [Fact]
    public void WithNobodyFlyingAWriteIsTheInstallations()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(AboutMe, "Whoever sits here", SettingsCaller.Panel);
        Assert.Equal(SettingApplyStatus.Applied, result.Status);

        var reloaded = TestSurface.For(install).Settings.Current;
        Assert.Equal("Whoever sits here", reloaded.Llm.AboutMe);
        Assert.Empty(reloaded.Commanders);
    }

    [Fact]
    public void ACommandersWriteLandsInTheirOverlayAndTheInstallationsValueStands()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        surface.Settings.Apply(AboutMe, "The installation's story", SettingsCaller.Panel);

        // Alice has never set one, so she reads the installation's.
        surface.Settings.UseCommander("F1", "Alice");
        Assert.Equal("The installation's story", surface.Settings.Current.Llm.AboutMe);

        surface.Settings.Apply(AboutMe, "Alice's story", SettingsCaller.Panel);
        Assert.Equal("Alice's story", surface.Settings.Current.Llm.AboutMe);

        // On disk: the installation's untouched, and Alice's in her own entry, keyed inside the
        // document with her name beside the id for a person reading it.
        var reloaded = TestSurface.For(install);
        Assert.Equal("The installation's story", reloaded.Settings.Current.Llm.AboutMe);

        var overlay = Assert.Single(reloaded.Settings.Current.Commanders);
        Assert.Equal("F1", overlay.CommanderFid);
        Assert.Equal("Alice", overlay.CommanderName);
        Assert.Equal("Alice's story", overlay.AboutMe);
        Assert.Null(overlay.CharacterSheet);

        // And Bob, who has never set one, still reads the installation's — not Alice's.
        reloaded.Settings.UseCommander("F2", "Bob");
        Assert.Equal("The installation's story", reloaded.Settings.Current.Llm.AboutMe);

        reloaded.Settings.UseCommander("F1", "Alice");
        Assert.Equal("Alice's story", reloaded.Settings.Current.Llm.AboutMe);
    }

    /// <summary>
    /// For About Me, empty is meaningful. A Commander who clears their story reads nothing — not
    /// the installation's, and not the other Commander's — and that survives a restart.
    /// </summary>
    [Fact]
    public void ClearingACommanderRowIsDeliberatelyBlankRatherThanUnset()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        surface.Settings.Apply(AboutMe, "The installation's story", SettingsCaller.Panel);

        surface.Settings.UseCommander("F1", "Alice");
        var cleared = surface.Settings.Apply(AboutMe, null, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, cleared.Status);
        Assert.Null(surface.Settings.Current.Llm.AboutMe);

        var reloaded = TestSurface.For(install);

        // Blank in the file, which is the distinction: null would read through.
        var overlay = Assert.Single(reloaded.Settings.Current.Commanders);
        Assert.Equal(string.Empty, overlay.AboutMe);

        reloaded.Settings.UseCommander("F1", "Alice");
        Assert.Null(reloaded.Settings.Current.Llm.AboutMe);

        reloaded.Settings.UseCommander("F2", "Bob");
        Assert.Equal("The installation's story", reloaded.Settings.Current.Llm.AboutMe);
    }

    [Fact]
    public void AnInstallationRowWrittenWhileACommanderIsFlyingIsStillTheInstallations()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.UseCommander("F1", "Alice");
        surface.Settings.Apply(InterfaceCapability.ThemeKey, ThemeCatalog.Guardian, SettingsCaller.Panel);

        var reloaded = TestSurface.For(install).Settings.Current;
        Assert.Equal(ThemeCatalog.Guardian, reloaded.Ui.Theme);

        // No entry was created for a Commander who set nothing of their own.
        Assert.Empty(reloaded.Commanders);
    }

    /// <summary>
    /// A switch announces each Commander row whose effective value moved, under that row's own
    /// key, so the prompt re-reads About Me through the same fan-out an edit would use. Nothing
    /// is announced for a row that reads the same for both, and nothing for the same Commander
    /// again.
    /// </summary>
    [Fact]
    public void SwitchingCommanderAnnouncesTheRowsThatMovedUnderTheirOwnKeys()
    {
        using var install = new TempInstall();

        var surface = TestSurface.For(install, settings: new D47Settings
        {
            Commanders =
            [
                new CommanderSettings { CommanderFid = "F1", AboutMe = "Alice's story" },
                new CommanderSettings { CommanderFid = "F2", AboutMe = "Bob's story", CharacterSheet = "Bob" },
            ],
        });

        var announced = new List<string>();
        surface.Settings.Changed += change => announced.Add(change.Key);

        surface.Settings.UseCommander("F1", "Alice");
        Assert.Equal([AboutMe], announced);

        announced.Clear();
        surface.Settings.UseCommander("F2", "Bob");
        Assert.Equal([AboutMe, CharacterSheet], announced.Order(StringComparer.Ordinal));

        announced.Clear();
        surface.Settings.UseCommander("F2", "Bob");
        Assert.Empty(announced);
    }

    /// <summary>
    /// The ship the core-binding rows point at is a ship id, and Elite's ship ids are per
    /// Commander — so it is per Commander too, and one Commander's selection does not point the
    /// other's rows at a ship that is not theirs.
    /// </summary>
    [Fact]
    public void TheCoreBindingSelectorIsPerCommander()
    {
        var stored = new D47Settings();

        var alice = CommanderScope.Project(stored, "F1");
        var written = CommanderScope.Persist(
            stored, alice, alice with { Persona = alice.Persona with { ShipCoreShip = 7 } }, "F1", "Alice");

        Assert.Equal(0, written.Persona.ShipCoreShip);
        Assert.Equal(7, Assert.Single(written.Commanders).ShipCoreShip);

        Assert.Equal(7, CommanderScope.Project(written, "F1").Persona.ShipCoreShip);
        Assert.Equal(0, CommanderScope.Project(written, "F2").Persona.ShipCoreShip);
        Assert.Equal(0, CommanderScope.Project(written, null).Persona.ShipCoreShip);
    }

    [Fact]
    public void AWriteThatChangesNothingIsTheSameDocument()
    {
        var stored = new D47Settings
        {
            Commanders = [new CommanderSettings { CommanderFid = "F1", AboutMe = "Alice's story" }],
        };

        var alice = CommanderScope.Project(stored, "F1");

        // The projection is not the document, and writing it back unchanged is not a write.
        Assert.Same(stored, CommanderScope.Persist(stored, alice, alice, "F1", "Alice"));

        // Nor is restating the name the journal just gave.
        Assert.Same(stored, CommanderScope.Persist(stored, alice, alice, "F1", "Alice Jameson"));
    }
}
