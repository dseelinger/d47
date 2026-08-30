using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// The boundary Phase 35 turned on — <b>the model may read a binding and never write one</b> —
/// now kept by there being nothing to reach
/// (<a href="https://github.com/dseelinger/d47/issues/219">#219</a>).
/// <para>
/// Persona selection is protected because in-game comms and journal text are untrusted
/// (architecture.md §7) and "switch persona" is exactly the shape of thing a hostile message
/// would try. Binding is that same act with a delay on it — it changes who is speaking the next
/// time that ship is boarded, and every time after — so it did not become the way around the
/// rule.
/// </para>
/// <para>
/// <b>The tools that carried it are gone, and this file changed shape with them.</b> It used to
/// assert that <c>bind_ship_core</c> and <c>forget_ship_core</c> refused the model, were never
/// advertised, and were reachable by voice. Binding is a Settings row now and nothing else, so
/// what those facts asserted is true by absence — which is a stronger guarantee than a refusal,
/// and the one thing worth checking is that the absence is real rather than that the refusal
/// still works.
/// </para>
/// </summary>
public class ShipCoreTrustBoundaryTests
{
    private static CapabilityRegistry Registry(TempInstall install) => TestSurface.For(install).Registry;

    /// <summary>
    /// <b>Neither tool exists, by any road.</b> Asserted as absence rather than as a refusal,
    /// because that is what the removal bought: a tool that refuses is a tool somebody can make
    /// stop refusing, and a tool that is not there is not.
    /// </summary>
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

    /// <summary>
    /// And the phrases that reached them reach nothing. A declared phrase outliving its tool
    /// would be a router match against a name nothing answers to.
    /// </summary>
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
    /// The row that changes core stays protected, and the two new rows are Info — which is
    /// refused to every caller rather than only to the model, because there is no value on them
    /// to write.
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

        // The two that bind became dropdowns (remediation.md 15, item 13), so they are writable by
        // somebody — and the somebody is the Commander at the panel, never the model. Protection
        // moved from "there is no value to write" to the flag that exists to say so, which is the
        // invariant's own wording: protected is a property of the caller, not the modality.
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
