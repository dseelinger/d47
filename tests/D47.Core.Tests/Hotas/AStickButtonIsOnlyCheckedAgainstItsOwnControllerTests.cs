using D47.Core.Hotas;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Hotas;

/// <summary>
/// A push-to-talk button checked against Elite's bindings for the controller it is on, not every
/// controller on the desk.
/// </summary>
public class AStickButtonIsOnlyCheckedAgainstItsOwnControllerTests
{
    private const string Throttle = "{wgi/nrid/throttle-block-1}";

    /// <summary>
    /// The report: a WinWing stick (4098BEA1) firing on Joy_9, and the four-block WinWing throttle
    /// (4098BD65) lighting on Joy_9 of its block 3.
    /// </summary>
    private static readonly EliteBinds Binds = new()
    {
        PresetName = "Custom",
        SourceFile = "Custom.4.2.binds",
        Bindings =
        [
            new EliteBinding("PrimaryFire", "Primary", "4098BEA1", "Joy_9"),
            new EliteBinding("ShipSpotLightToggle", "Secondary", "4098BD65", "Joy_9"),
        ],
    };

    private static HotasReading Reading(string id, ushort product) => new()
    {
        Id = id,
        VendorId = 0x4098,
        ProductId = product,
        Buttons = new bool[32],
    };

    private static readonly HotasButton ThumbButton = new(Throttle, 8);

    [Fact]
    public void TheSticksBindingIsNotReportedForTheThrottle()
    {
        var sharing = Binds.SharingButton(ThumbButton, [Reading(Throttle, 0xBD65), Reading("{wgi/nrid/stick}", 0xBEA1)]);

        Assert.Equal(["ShipSpotLightToggle"], sharing.Bindings.Select(binding => binding.Action));
        Assert.True(sharing.Certain);
    }

    [Fact]
    public void AControllerWindowsShowsAsSeveralDevicesIsNotCertain()
    {
        var sharing = Binds.SharingButton(
            ThumbButton,
            [
                Reading("{wgi/nrid/throttle-block-0}", 0xBD65),
                Reading(Throttle, 0xBD65),
                Reading("{wgi/nrid/throttle-block-2}", 0xBD65),
                Reading("{wgi/nrid/throttle-block-3}", 0xBD65),
                Reading("{wgi/nrid/stick}", 0xBEA1),
            ]);

        Assert.Equal(["ShipSpotLightToggle"], sharing.Bindings.Select(binding => binding.Action));
        Assert.Equal(4, sharing.Interfaces);
        Assert.False(sharing.Certain);
    }

    [Fact]
    public void AControllerThatIsNotConnectedIsCheckedAgainstEveryDevice()
    {
        var sharing = Binds.SharingButton(ThumbButton, [Reading("{wgi/nrid/stick}", 0xBEA1)]);

        Assert.Equal(2, sharing.Bindings.Count);
        Assert.Equal(0, sharing.Interfaces);
        Assert.False(sharing.Certain);
    }

    [Fact]
    public void AButtonElitesBindingsDoNotUseIsClearOnItsController()
    {
        var sharing = Binds.SharingButton(new HotasButton(Throttle, 9), [Reading(Throttle, 0xBD65)]);

        Assert.Empty(sharing.Bindings);
    }
}
