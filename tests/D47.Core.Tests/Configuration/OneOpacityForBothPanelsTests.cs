using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// Opacity was one of the six settings each headset surface kept its own copy of, and is now one
/// knob for both (asked for 2026-08-24).
/// <para>
/// It is the odd one out among those six. Mini exists to be smaller and further out of the way, so
/// its distance, size and drop have to differ from the big panel's — but how see-through the glass
/// is is one preference about how much cockpit shows through d47, and a Commander who wants it at
/// half never means <i>half, in one of the two modes</i>. Two copies made <i>"set the opacity to
/// 0.5"</i> a question with two answers, and it was answered with the surface that was not on
/// screen.
/// </para>
/// </summary>
public class OneOpacityForBothPanelsTests
{
    /// <summary>
    /// The reported file, exactly: the big panel set to 0.5 by a Commander who then saw nothing,
    /// and mini still at the default. The decision is carried up rather than reset — the whole
    /// point of a repair over a default is that somebody who set it should not have to set it
    /// again.
    /// </summary>
    [Fact]
    public void AValueTheCommanderChoseBecomesTheSharedOne()
    {
        var loaded = Load("""{ "vr": { "panel": { "opacity": 0.5 }, "mini": { "opacity": 0.95 } } }""");

        Assert.Equal(0.5, loaded.Vr.Opacity);
    }

    /// <summary>Whichever surface it was set on, if only one was ever touched.</summary>
    [Fact]
    public void AValueSetOnTheMiniPanelIsJustAsMuchADecision()
    {
        var loaded = Load("""{ "vr": { "panel": { "opacity": 0.95 }, "mini": { "opacity": 0.3 } } }""");

        Assert.Equal(0.3, loaded.Vr.Opacity);
    }

    /// <summary>
    /// The big panel wins a disagreement: it is the surface the settings page leads with and the
    /// one a spoken "set the opacity" reached, so a deliberate value is likeliest to be there.
    /// </summary>
    [Fact]
    public void TwoDeliberateValuesResolveToTheBigPanels()
    {
        var loaded = Load("""{ "vr": { "panel": { "opacity": 0.4 }, "mini": { "opacity": 0.8 } } }""");

        Assert.Equal(0.4, loaded.Vr.Opacity);
    }

    [Fact]
    public void AFileNobodyTouchedKeepsTheDefault()
    {
        Assert.Equal(0.95, Load("{}").Vr.Opacity);
    }

    /// <summary>
    /// Stamped, so a Commander who then sets the shared knob back to the old default does not have
    /// the repair reach in and re-decide it on the next start.
    /// </summary>
    [Fact]
    public void TheRepairHappensOnceAndSaysSo()
    {
        var loaded = Load("""{ "vr": { "panel": { "opacity": 0.5 } } }""");

        Assert.True(loaded.Vr.OpacityShared > 0);

        var again = Load("""{ "vr": { "opacity": 0.95, "opacityShared": 1, "panel": { "opacity": 0.5 } } }""");

        Assert.Equal(0.95, again.Vr.Opacity);
    }

    /// <summary>
    /// <b>The old properties stay on disk</b> — the settings file is append-only, and a property
    /// that disappears is a file an older build cannot read. They are simply no longer consulted.
    /// </summary>
    [Fact]
    public void ThePerSurfaceCopiesAreStillReadableAndNoLongerConsulted()
    {
        var loaded = Load("""{ "vr": { "opacity": 0.6, "opacityShared": 1, "panel": { "opacity": 0.1 } } }""");

        Assert.Equal(0.1, loaded.Vr.Panel.Opacity);
        Assert.Equal(0.6, loaded.Vr.Opacity);

        // What the headset is actually told, for either surface, is the shared one.
        Assert.Equal(0.6f, loaded.Vr.Panel.ToPlacement(loaded.Vr.Opacity).Opacity, 3);
        Assert.Equal(0.6f, loaded.Vr.Mini.ToPlacement(loaded.Vr.Opacity).Opacity, 3);
    }

    /// <summary>One knob on the settings surface too, or the panel would still ask twice.</summary>
    [Fact]
    public void ThereIsOneOpacityRowAndItBelongsToNeitherSurface()
    {
        using var install = new TempInstall();

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        var keys = VrCapability
            .Create(
                settings,
                new VrCapability.HeadsetSurface
                {
                    Report = () => (D47.Core.Vr.VrState.Unavailable, "No runtime in a test."),
                    Reanchor = () => 0,
                })
            .Settings
            .Select(row => row.Key)
            .ToList();

        Assert.Contains(VrCapability.OpacityKey, keys);
        Assert.DoesNotContain("vr.panel.opacity", keys);
        Assert.DoesNotContain("vr.mini.opacity", keys);

        // The rest of the placement rows are genuinely per-surface and stay that way.
        Assert.Contains("vr.panel.distance", keys);
        Assert.Contains("vr.mini.distance", keys);
    }

    private static D47Settings Load(string json)
    {
        using var install = new TempInstall();

        File.WriteAllText(install.Paths.SettingsFile, json);

        return new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance).Load();
    }
}
