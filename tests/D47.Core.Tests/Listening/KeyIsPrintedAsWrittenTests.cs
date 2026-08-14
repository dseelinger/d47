using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The status says the key the Commander pressed, not the enum name behind it.
/// <para>
/// Reported from a running build: the panel answered "Push-to-talk: Oem4 (hold)" for a key
/// bound to <kbd>[</kbd>. The settings row already printed it correctly — several of these
/// values carry two names in the key enum and <c>ToString</c> picks whichever it finds first —
/// but the printable form belongs to the input toolkit, which Core does not reference, so the
/// status text had nothing to ask and printed the stored form.
/// </para>
/// </summary>
public class KeyIsPrintedAsWrittenTests
{
    private static ListeningCapability.ListeningSurface Surface(Func<string, string>? label) => new()
    {
        InputDevices = () => [],
        DeviceLabel = id => id,
        CaptureState = () => (true, null),
        TranscriberState = () => (false, null, "No speech model is selected."),
        // Known, with nothing bound, so the collision line is produced and can be read.
        Binds = () => new D47.Core.Input.EliteBinds
        {
            PresetName = "KeyboardMouseOnly",
            SourceFile = "KeyboardMouseOnly.binds",
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
        // The unconditional inventory, which is where the key and the all-clear now live;
        // Describe answers a Commander's question and says neither when nothing is wrong.
        var text = ListeningCapability.DescribeInDetail(Bound(), Surface(key => key == "Oem4" ? "[" : key));

        Assert.Contains("Push-to-talk: [ (hold).", text, StringComparison.Ordinal);

        // The collision line too. Being told that "Oem4" is clear is being told about a key the
        // Commander has to translate before they can act on it.
        Assert.Contains(
            "No Elite binding uses [ in the KeyboardMouseOnly preset.",
            text,
            StringComparison.Ordinal);

        Assert.DoesNotContain("Oem4", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// With nothing to ask, the stored form is still better than nothing — a replay harness and
    /// a test construct this surface without an input toolkit behind it.
    /// </summary>
    [Fact]
    public void WithNoLabellerItFallsBackToWhatIsStored()
    {
        Assert.Contains(
            "Push-to-talk: Oem4 (hold).",
            ListeningCapability.DescribeInDetail(Bound(), Surface(label: null)),
            StringComparison.Ordinal);
    }
}
