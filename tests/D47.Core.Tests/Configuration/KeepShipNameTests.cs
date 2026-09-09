using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>Whether a name the Commander gave the ship's AI survives a change of core.</summary>
public class KeepShipNameTests
{
    private static TestSurface Named(TempInstall install, string name, bool keep)
    {
        var surface = TestSurface.For(install);

        surface.Settings.Apply(PersonaCapability.ShipNameKey, name, SettingsCaller.Panel);
        surface.Settings.Apply(PersonaCapability.KeepShipNameKey, keep ? "true" : "false", SettingsCaller.Panel);

        return surface;
    }

    [Fact]
    public void OnByDefault()
    {
        // The behaviour every existing install already has: a name outlives the core it was given under,
        // because a Commander who named their ship's AI named the ship's AI.
        Assert.True(D47Settings.Defaults.Persona.KeepShipName);
    }

    [Fact]
    public void OnTheNameOutlivesTheSwitch()
    {
        using var install = new TempInstall();
        var surface = Named(install, "Fred", keep: true);

        surface.Settings.Apply(PersonaCapability.PersonaKey, "cora", SettingsCaller.Panel);

        Assert.Equal("Fred", surface.Settings.Current.Persona.ShipName);
        Assert.Equal("cora", surface.Settings.Current.Persona.Id);
    }

    [Fact]
    public void OffTheSwitchClearsIt()
    {
        using var install = new TempInstall();
        var surface = Named(install, "Fred", keep: false);

        surface.Settings.Apply(PersonaCapability.PersonaKey, "cora", SettingsCaller.Panel);

        // Cleared rather than kept and ignored: a row showing "Fred" while the answer is "I am Cora" is the
        // row-and-behaviour disagreement this codebase has already fixed once.
        Assert.Null(surface.Settings.Current.Persona.ShipName);
        Assert.Equal("cora", surface.Settings.Current.Persona.Id);
    }

    [Fact]
    public void WritingTheCoreThatIsAlreadyAboardIsNotASwitch()
    {
        // Otherwise an unrelated settings edit that rewrites the same core would silently rename the
        // Commander's companion.
        using var install = new TempInstall();
        var surface = Named(install, "Fred", keep: false);
        var write = surface.Settings.Find(PersonaCapability.PersonaKey)!.Binding!.Write!;

        var unchanged = write(surface.Settings.Current, "warden");

        Assert.Equal("Fred", unchanged.Persona.ShipName);

        // And the same write for a different core does clear it, so the guard above is the core comparison
        // and not the rule being off altogether.
        Assert.Null(write(surface.Settings.Current, "cora").Persona.ShipName);
    }

    [Fact]
    public void TheRowIsOnlyOfferedWhileThereIsANameToKeep()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var row = surface.Settings.Find(PersonaCapability.KeepShipNameKey);

        Assert.NotNull(row);
        Assert.False(row.Applies(surface.Settings.Current));

        surface.Settings.Apply(PersonaCapability.ShipNameKey, "Fred", SettingsCaller.Panel);
        Assert.True(row.Applies(surface.Settings.Current));

        // And a name of nothing but spaces is no name, on this row as on the answer to "who are you".
        surface.Settings.Apply(PersonaCapability.ShipNameKey, "   ", SettingsCaller.Panel);
        Assert.False(row.Applies(surface.Settings.Current));
    }

    [Fact]
    public void ClearingTheNameByHandIsStillTheCommandersToDo()
    {
        // The toggle governs what a switch does to the name.
        using var install = new TempInstall();
        var surface = Named(install, "Fred", keep: true);

        surface.Settings.Apply(PersonaCapability.ShipNameKey, null, SettingsCaller.Panel);
        surface.Settings.Apply(PersonaCapability.PersonaKey, "kex", SettingsCaller.Panel);

        Assert.Null(surface.Settings.Current.Persona.ShipName);
    }
}
