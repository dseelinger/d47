using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The mini panel carries a square avatar beside its text, so its default size and quad grow (#778).</summary>
public class TheMiniPanelMakesRoomForItsAvatarTests
{
    [Fact]
    public void TheDefaultIsTheAvatarsSquareBesideTheOldTextColumn()
    {
        Assert.Equal((280 + 512, 280), PanelResolution.Mini);
    }

    /// <summary>Wider by the same ratio as the pixels, so the text keeps its apparent size in the headset.</summary>
    [Fact]
    public void TheQuadWidensInProportion()
    {
        Assert.Equal(0.34 * 792 / 512, D47Settings.Defaults.Vr.Mini.Width, 3);
        Assert.Equal(0.526, VrSurfaceSettings.Mini().Width);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AnInstallAtTheOldDefaultIsWidened()
    {
        var loaded = Load("""{ "vr": { "mini": { "width": 0.34, "pixels": "" } } }""");

        Assert.Equal(VrSurfaceSettings.MiniWidth, loaded.Vr.Mini.Width);
        Assert.True(loaded.Vr.MiniWidened > 0);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void AWidthTheCommanderChoseIsLeftAlone()
    {
        Assert.Equal(0.45, Load("""{ "vr": { "mini": { "width": 0.45 } } }""").Vr.Mini.Width);
    }

    /// <summary>A resize drag stores pixels with its width; that pair is the Commander's, even at 0.34.</summary>
    [Trait("Category", "Integration")]
    [Fact]
    public void AResizedPanelIsLeftAlone()
    {
        var loaded = Load("""{ "vr": { "mini": { "width": 0.34, "pixels": "640x280" } } }""");

        Assert.Equal(0.34, loaded.Vr.Mini.Width);
        Assert.Equal("640x280", loaded.Vr.Mini.Pixels);
    }

    /// <summary>Stamped, so a Commander who later sets 0.34 on purpose keeps it.</summary>
    [Trait("Category", "Integration")]
    [Fact]
    public void TheRepairHappensOnce()
    {
        var loaded = Load("""{ "vr": { "miniWidened": 1, "mini": { "width": 0.34 } } }""");

        Assert.Equal(0.34, loaded.Vr.Mini.Width);
    }

    private static D47Settings Load(string json)
    {
        using var install = new TempInstall();

        File.WriteAllText(install.Paths.SettingsFile, json);

        return new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance).Load();
    }
}
