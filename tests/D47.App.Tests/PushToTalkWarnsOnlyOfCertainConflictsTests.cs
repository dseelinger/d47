using D47.App.Voice;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Hotas;
using D47.Core.Input;
using Xunit;

namespace D47.App.Tests;

/// <summary>The startup log names a push-to-talk button only when Elite certainly binds the same one (#2382).</summary>
public sealed class PushToTalkWarnsOnlyOfCertainConflictsTests
{
    private const string Throttle = "{wgi/nrid/throttle}";

    private static readonly EliteBinds Binds = new()
    {
        PresetName = "Custom",
        SourceFile = "Custom.4.2.binds",
        Bindings = [new EliteBinding("ShipSpotLightToggle", "Secondary", "4098BD65", "Joy_9")],
    };

    private static HotasReading Reading(string id) => new()
    {
        Id = id,
        VendorId = 0x4098,
        ProductId = 0xBD65,
        Buttons = new bool[32],
    };

    private static IReadOnlyList<string> Logged(IReadOnlyList<HotasReading> readings)
    {
        var settings = TestSurface.Settings();
        var rows = new[]
            {
                ListeningCapability.PushToTalkKeyKey, ListeningCapability.PushToTalkButtonKey,
                ListeningCapability.CommsPushToTalkKeyKey, ListeningCapability.CommsPushToTalkButtonKey,
            }
            .Select(settings.Find)
            .OfType<SettingRow>()
            .ToList();
        var current = settings.Current with
        {
            Listening = settings.Current.Listening with { PushToTalkButton = new HotasButton(Throttle, 8).ToString() },
        };
        var logger = new RecordingLogger();

        Listener.WarnIfEliteBindsIt(logger, current, rows, Binds, readings);

        return logger.Lines;
    }

    [Fact]
    public void AButtonOnAControllerShowingOneInterfaceIsLogged()
    {
        var line = Assert.Single(Logged([Reading(Throttle)]));

        Assert.Contains("Push-to-talk button 9", line, StringComparison.Ordinal);
        Assert.Contains("ShipSpotLightToggle", line, StringComparison.Ordinal);
    }

    [Fact]
    public void AButtonOnAControllerShowingFourInterfacesIsNotLogged() =>
        Assert.Empty(Logged(
        [
            Reading("{wgi/nrid/throttle-block-0}"),
            Reading(Throttle),
            Reading("{wgi/nrid/throttle-block-2}"),
            Reading("{wgi/nrid/throttle-block-3}"),
        ]));

    [Fact]
    public void AButtonOnAControllerThatIsNotAttachedIsNotLogged() => Assert.Empty(Logged([]));
}
