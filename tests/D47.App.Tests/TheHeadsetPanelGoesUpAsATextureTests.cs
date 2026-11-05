using Avalonia.Headless.XUnit;
using D47.App.Headset;
using D47.App.Panel;
using D47.Vr;
using Xunit;

namespace D47.App.Tests;

/// <summary>The panel takes the texture route, as the avatar clip does, so its redraws do not flicker in the headset.</summary>
public sealed class TheHeadsetPanelGoesUpAsATextureTests
{
    [AvaloniaFact]
    public void ThePanelGoesUpAsATexture()
    {
        var (settings, _, _) = TestSurface.Create();
        using var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null);

        Assert.True(((IVrSurfaceSource)surface).UploadsAsTexture);
    }
}
