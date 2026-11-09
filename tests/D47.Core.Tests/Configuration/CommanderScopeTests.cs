using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// Several Commanders, one installation: the rows that are the Commander's rather than the
/// installation's are declared on the row, layered per Frontier id inside the one settings file, and
/// keep unset and deliberately blank apart.
/// </summary>
public class CommanderScopeTests
{
    private const string AboutMe = "llm.aboutMe";
    private const string CharacterSheet = "llm.characterSheet";

    /// <summary>The gate.</summary>
    [Fact]
    public void TheRowsDeclaredPerCommanderAreExactlyTheRowsTheOverlayReaches()
    {
        var install = new MemoryInstall();
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
                    Scenario = "a scenario",
                    ScenarioAudience = D47.Core.Conversation.ScenarioAudience.Public,
                    ShipCoreShip = 7,
                    HomeSystem = "Shinrarta Dezhra",
                    CombatRating = 80,
                    TradingRating = 80,
                    MiningRating = 80,
                    ExplorationRating = 80,
                    ExobiologyRating = 80,
                    PassengersRating = 80,
                    SearchAndRescueRating = 80,
                    CrimeRating = 80,
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
            .Where(row => row.Scope == SettingScope.Commander && row.Kind != SettingKind.Secret)
            .Select(row => row.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(declared);
        Assert.Equal(declared, reached);
    }

    /// <summary>d47 runs before Elite has said who is flying.</summary>
    [Fact]
    public void AWriteWithNobodyFlyingGoesToTheNextCommanderNamedAndNotTheInstallation()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        var result = surface.Settings.Apply(AboutMe, "Whoever sits here", SettingsCaller.Panel);
        Assert.Equal(SettingApplyStatus.Applied, result.Status);
        Assert.Equal("Whoever sits here", surface.Settings.Current.Llm.AboutMe);

        surface.Settings.UseCommander("F1", "Alice");
        Assert.Equal("Whoever sits here", surface.Settings.Current.Llm.AboutMe);

        surface.Settings.UseCommander("F2", "Bob");
        Assert.Null(surface.Settings.Current.Llm.AboutMe);

        var reloaded = TestSurface.For(install).Settings.Current;
        Assert.Null(reloaded.Llm.AboutMe);
        Assert.Equal("Whoever sits here", Assert.Single(reloaded.Commanders).AboutMe);
    }

    [Fact]
    public void ACommandersWriteLandsInTheirOverlayAndNoOneElseReadsIt()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        // Alice has set nothing, so she reads the default.
        surface.Settings.UseCommander("F1", "Alice");
        Assert.Null(surface.Settings.Current.Llm.AboutMe);

        surface.Settings.Apply(AboutMe, "Alice's story", SettingsCaller.Panel);
        Assert.Equal("Alice's story", surface.Settings.Current.Llm.AboutMe);

        // On disk: the installation's copy stays null, and Alice's is in her own entry, with her name
        // beside the id for a person reading it.
        var reloaded = TestSurface.For(install);
        Assert.Null(reloaded.Settings.Current.Llm.AboutMe);

        var overlay = Assert.Single(reloaded.Settings.Current.Commanders);
        Assert.Equal("F1", overlay.CommanderFid);
        Assert.Equal("Alice", overlay.CommanderName);
        Assert.Equal("Alice's story", overlay.AboutMe);
        Assert.Null(overlay.CharacterSheet);

        // Bob has set nothing and reads the default, not Alice's.
        reloaded.Settings.UseCommander("F2", "Bob");
        Assert.Null(reloaded.Settings.Current.Llm.AboutMe);

        reloaded.Settings.UseCommander("F1", "Alice");
        Assert.Equal("Alice's story", reloaded.Settings.Current.Llm.AboutMe);
    }

    [Fact]
    public void ACommanderReadsANullFieldAsItsDefaultNotTheInstallations()
    {
        var stored = new D47Settings
        {
            Llm = new LlmSettings { AboutMe = "old install story" },
            Callouts = new CalloutSettings { HomeSystem = "Sol" },
            Commanders = [new CommanderSettings { CommanderFid = "F1", CommanderName = "Alice" }],
        };

        foreach (var projected in new[] { CommanderScope.Project(stored, "F1"), CommanderScope.Project(stored, "F2") })
        {
            Assert.Null(projected.Llm.AboutMe);
            Assert.Null(projected.Callouts.HomeSystem);
        }
    }

    [Fact]
    public void AValueHeldWithNobodyFlyingDoesNotReplaceTheNextCommandersOwn()
    {
        var install = new MemoryInstall();

        var surface = TestSurface.For(install, settings: new D47Settings
        {
            Commanders = [new CommanderSettings { CommanderFid = "F1", AboutMe = "Alice's story" }],
        });

        surface.Settings.Apply(AboutMe, "Whoever sits here", SettingsCaller.Panel);
        surface.Settings.Apply(CharacterSheet, "A sheet", SettingsCaller.Panel);
        surface.Settings.UseCommander("F1", "Alice");

        Assert.Equal("Alice's story", surface.Settings.Current.Llm.AboutMe);
        Assert.Equal("A sheet", surface.Settings.Current.Llm.CharacterSheet);
    }

    [Fact]
    public void EachCommanderKeepsTheirOwnStoryGender()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        surface.Settings.UseCommander("F1", "Alice");
        surface.Settings.Replace("test", current => current with { CommanderGender = CommanderGender.Woman });

        surface.Settings.UseCommander("F2", "Bob");
        surface.Settings.Replace("test", current => current with { CommanderGender = CommanderGender.Man });

        Assert.Equal(CommanderGender.Man, surface.Settings.Current.CommanderGender);

        surface.Settings.UseCommander("F1", "Alice");
        Assert.Equal(CommanderGender.Woman, surface.Settings.Current.CommanderGender);
    }

    [Fact]
    public void AStoryGenderSetWithNobodyFlyingGoesToTheNextCommander()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        surface.Settings.Replace("test", current => current with { CommanderGender = CommanderGender.Woman });

        surface.Settings.UseCommander("F1", "Alice");
        Assert.Equal(CommanderGender.Woman, surface.Settings.Current.CommanderGender);
    }

    [Fact]
    public void EachCommanderKeepsTheirOwnHomeSystem()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        surface.Settings.Apply(CalloutCapability.HomeSystemKey, "Sol", SettingsCaller.Panel);

        surface.Settings.UseCommander("F1", "Alice");
        surface.Settings.Apply(CalloutCapability.HomeSystemKey, "Shinrarta Dezhra", SettingsCaller.Panel);
        Assert.Equal("Shinrarta Dezhra", surface.Settings.Current.Callouts.HomeSystem);

        surface.Settings.UseCommander("F2", "Bob");
        Assert.Null(surface.Settings.Current.Callouts.HomeSystem);

        surface.Settings.UseCommander("F1", "Alice");
        Assert.Equal("Shinrarta Dezhra", surface.Settings.Current.Callouts.HomeSystem);
    }

    [Fact]
    public void SwitchingCommanderAnnouncesTheHomeSystemToTheCalloutsRoute()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        surface.Settings.Apply(CalloutCapability.HomeSystemKey, "Sol", SettingsCaller.Panel);
        surface.Settings.UseCommander("F1", "Alice");
        surface.Settings.Apply(CalloutCapability.HomeSystemKey, "Shinrarta Dezhra", SettingsCaller.Panel);

        var keys = new List<string>();
        surface.Settings.Changed += change => keys.Add(change.Key);
        surface.Settings.UseCommander("F2", "Bob");

        Assert.Contains(CalloutCapability.HomeSystemKey, keys);
        Assert.Equal(SettingsSubsystem.Callouts, SettingsFanout.For(CalloutCapability.HomeSystemKey).Subsystem);
    }

    /// <summary>For About Me, empty is meaningful.</summary>
    [Fact]
    public void ClearingACommanderRowIsDeliberatelyBlankRatherThanUnset()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);
        surface.Settings.UseCommander("F1", "Alice");
        surface.Settings.Apply(AboutMe, "Alice's story", SettingsCaller.Panel);
        var cleared = surface.Settings.Apply(AboutMe, null, SettingsCaller.Panel);

        Assert.Equal(SettingApplyStatus.Applied, cleared.Status);
        Assert.Null(surface.Settings.Current.Llm.AboutMe);

        var reloaded = TestSurface.For(install);

        // Blank in the file, which is the distinction: null would read as the default.
        var overlay = Assert.Single(reloaded.Settings.Current.Commanders);
        Assert.Equal(string.Empty, overlay.AboutMe);

        reloaded.Settings.UseCommander("F1", "Alice");
        Assert.Null(reloaded.Settings.Current.Llm.AboutMe);

        reloaded.Settings.UseCommander("F2", "Bob");
        Assert.Null(reloaded.Settings.Current.Llm.AboutMe);
    }

    [Fact]
    public void AnInstallationRowWrittenWhileACommanderIsFlyingIsStillTheInstallations()
    {
        var install = new MemoryInstall();
        var surface = TestSurface.For(install);

        surface.Settings.UseCommander("F1", "Alice");
        surface.Settings.Apply(InterfaceCapability.ThemeKey, ThemeCatalog.ElitePaletteId, SettingsCaller.Panel);

        var reloaded = TestSurface.For(install).Settings.Current;
        Assert.Equal(ThemeCatalog.ElitePaletteId, reloaded.Ui.Theme);

        // No entry was created for a Commander who set nothing of their own.
        Assert.Empty(reloaded.Commanders);
    }

    /// <summary>
    /// A switch announces each Commander row whose effective value moved, under that row's own key, so
    /// the prompt re-reads About Me through the same fan-out an edit would use. A Commander-scoped secret
    /// row has no value to compare, so it is announced on every switch.
    /// </summary>
    [Fact]
    public void SwitchingCommanderAnnouncesTheRowsThatMovedUnderTheirOwnKeys()
    {
        var install = new MemoryInstall();

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
        Assert.Equal([ColonisationCapability.KeyRow, AboutMe], announced.Order(StringComparer.Ordinal));

        announced.Clear();
        surface.Settings.UseCommander("F2", "Bob");
        Assert.Equal([ColonisationCapability.KeyRow, AboutMe, CharacterSheet], announced.Order(StringComparer.Ordinal));

        announced.Clear();
        surface.Settings.UseCommander("F2", "Bob");
        Assert.Empty(announced);
    }

    /// <summary>
    /// The ship the core-binding rows point at is a ship id, and Elite's ship ids are per Commander —
    /// so it is per Commander too, and one Commander's selection does not point the other's rows at a
    /// ship that is not theirs.
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
