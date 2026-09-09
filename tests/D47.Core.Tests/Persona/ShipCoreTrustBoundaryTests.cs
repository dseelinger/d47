using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// The boundary Phase 35 turned on — the model may read a binding and never write one — now kept by
/// there being nothing to reach.
/// </summary>
public class ShipCoreTrustBoundaryTests
{
    private static CapabilityRegistry Registry(TempInstall install) => TestSurface.For(install).Registry;

    /// <summary>Neither tool exists, by any road.</summary>
    [Theory]
    [InlineData("bind_ship_core")]
    [InlineData("forget_ship_core")]
    public void NeitherHalfIsATooAtAllAnyMore(string gone)
    {
        using var install = new TempInstall();

        var declared = Registry(install).All
            .SelectMany(capability => capability.Descriptor.Tools)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(gone, declared);
    }

    [Theory]
    [InlineData("remember this core for this ship")]
    [InlineData("this ship flies with you")]
    [InlineData("forget this ship's core")]
    public void ThePhrasesThatReachedThemReachNothing(string phrase)
    {
        using var install = new TempInstall();

        Assert.Null(new KeywordRouter(Registry(install)).MatchToolCommand(phrase));
    }

    /// <summary>
    /// Reading is untouched, and it is the half that was always allowed: the binding arrives in
    /// <c>describe_persona</c>'s output rather than as a tool of its own.
    /// </summary>
    [Fact]
    public void TheModelMayStillReadWhatAShipFliesWith()
    {
        using var install = new TempInstall();

        var advertised = ToolProfiles.All(Registry(install))
            .SelectMany(profile => profile.Tools)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("describe_persona", advertised);
    }

    /// <summary>
    /// The row that changes core stays protected, and the two new rows are Info — which is refused to
    /// every caller rather than only to the model, because there is no value on them to write.
    /// </summary>
    [Fact]
    public void TheRowsCannotBeWrittenEither()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        var persona = surface.Settings.Find(D47.Core.Capabilities.Builtin.PersonaCapability.PersonaKey);
        Assert.True(persona!.Protected);

        // The read-only row keeps its shape: nothing to write and nothing to press wrongly.
        var listing = surface.Settings.Find(
            D47.Core.Capabilities.Builtin.PersonaCapability.ShipCoresKey);

        Assert.NotNull(listing);
        Assert.Equal(SettingKind.Info, listing!.Kind);

 // The two that bind became dropdowns, so they are writable by somebody —
        // and the somebody is the Commander at the panel, never the model.
        foreach (var key in new[]
                 {
                     D47.Core.Capabilities.Builtin.PersonaCapability.ShipCoreKey,
                     D47.Core.Capabilities.Builtin.PersonaCapability.ShipCoreShipKey,
                     D47.Core.Capabilities.Builtin.PersonaCapability.ShipCoresKey,
                 })
        {
            var row = surface.Settings.Find(key);

            Assert.NotNull(row);

            var applied = surface.Settings.Apply(key, "sentinel", SettingsCaller.Model);

            Assert.Equal(SettingApplyStatus.Refused, applied.Status);
        }
    }
}
