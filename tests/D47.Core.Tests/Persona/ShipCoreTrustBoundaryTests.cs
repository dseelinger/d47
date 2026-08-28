using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// The boundary this phase turns on (Phase 35, "The binding is the Commander's, and
/// unreachable from the model"): <b>the model may read a binding and never write one</b>.
/// <para>
/// Persona selection is protected because in-game comms and journal text are untrusted
/// (architecture.md §7) and "switch persona" is exactly the shape of thing a hostile message
/// would try. Binding is that same act with a delay on it — it changes who is speaking the next
/// time that ship is boarded, and every time after — so it does not become the way around the
/// rule.
/// </para>
/// </summary>
public class ShipCoreTrustBoundaryTests
{
    private static CapabilityRegistry Registry(TempInstall install) => TestSurface.For(install).Registry;

    [Theory]
    [InlineData("bind_ship_core")]
    [InlineData("forget_ship_core")]
    public async Task TheModelCannotBindACoreToAShip(string tool)
    {
        using var install = new TempInstall();

        var result = await Registry(install)
            .InvokeAsync(tool, ToolArguments.Empty, TestContext.Current.CancellationToken, ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.Contains("not something I can do on my own", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void NeitherHalfIsEverAdvertised()
    {
        using var install = new TempInstall();

        var advertised = ToolProfiles.All(Registry(install))
            .SelectMany(profile => profile.Tools)
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Not caution — arithmetic. A tool that would refuse every call it received still costs
        // the cached prefix on every turn of the session (architecture.md §6), and this phase was
        // handed under a hundred bytes of room.
        Assert.DoesNotContain("bind_ship_core", advertised);
        Assert.DoesNotContain("forget_ship_core", advertised);

        // What the model may do instead: read. The binding is one sentence inside a tool that was
        // already advertised, which is what makes reading free.
        Assert.Contains("describe_persona", advertised);
    }

    [Theory]
    [InlineData("remember this core for this ship", "bind_ship_core")]
    [InlineData("this ship flies with you", "bind_ship_core")]
    [InlineData("forget this ship's core", "forget_ship_core")]
    public void BothHalvesAreReachableByVoiceThroughTheModelFreeRouter(string phrase, string tool)
    {
        using var install = new TempInstall();

        // A protected tool is unreachable from the tool surface by design, so without a declared
        // phrase it could not be reached by voice at all — and nothing would report that.
        var match = new KeywordRouter(Registry(install)).MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.Equal(tool, match.ToolName);
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
