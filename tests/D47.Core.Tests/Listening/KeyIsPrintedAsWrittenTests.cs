using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>The status says the key the Commander pressed, not the enum name behind it.</summary>
public class KeyIsPrintedAsWrittenTests
{
    private static ListeningCapability.ListeningSurface Surface(Func<string, string>? label) => new()
    {
        InputDevices = () => [],
        DeviceLabel = id => id,
        CaptureState = () => (true, null),
        TranscriberState = () => (false, null, "No speech model is selected."),
        // Known, with the key bound to an action, so the collision line is produced and can be read.
        Binds = () => new D47.Core.Input.EliteBinds
        {
            PresetName = "KeyboardMouseOnly",
            SourceFile = "KeyboardMouseOnly.binds",
            Bindings = [new D47.Core.Input.EliteBinding("SelectTarget", "Primary", "Keyboard", "Key_Oem4")],
        },
        InstalledModels = () => [],
        KeyLabel = label,
    };

    private static D47Settings Bound() => new()
    {
        Listening = new ListeningSettings { PushToTalkKey = "Oem4" },
    };

    [Fact]
    public void ThePushToTalkKeyIsReportedInItsPrintableForm()
    {
        var text = ListeningCapability.Describe(Bound(), Surface(key => key == "Oem4" ? "[" : key));

        Assert.Contains("Warning: [ is also bound in Elite (KeyboardMouseOnly)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Oem4", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// With nothing to ask, the stored form is still better than nothing — a replay harness and a test
    /// construct this surface without an input toolkit behind it.
    /// </summary>
    [Fact]
    public void WithNoLabellerItFallsBackToWhatIsStored()
    {
        Assert.Contains(
            "Warning: Oem4 is also bound in Elite",
            ListeningCapability.Describe(Bound(), Surface(label: null)),
            StringComparison.Ordinal);
    }
}
