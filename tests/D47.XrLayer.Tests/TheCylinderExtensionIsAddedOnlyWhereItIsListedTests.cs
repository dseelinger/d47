using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

/// <summary>
/// Elite's instance gets XR_KHR_composition_layer_cylinder added only when the runtime lists it and Elite did not
/// ask for it; outside Elite the list is never touched.
/// </summary>
public sealed class TheCylinderExtensionIsAddedOnlyWhereItIsListedTests
{
    private static readonly string[] Elites = ["XR_KHR_D3D11_enable", "XR_EXT_hand_tracking"];

    [Fact]
    public void AListedExtensionEliteDidNotAskForIsAppendedToElitesList()
    {
        var extended = CylinderExtension.Extended(true, Elites, [CylinderExtension.Name, "XR_KHR_D3D11_enable"]);

        Assert.Equal(["XR_KHR_D3D11_enable", "XR_EXT_hand_tracking", "XR_KHR_composition_layer_cylinder"], extended);
    }

    [Fact]
    public void AnExtensionTheRuntimeDoesNotListIsNotAdded() =>
        Assert.Null(CylinderExtension.Extended(true, Elites, ["XR_KHR_D3D11_enable"]));

    [Fact]
    public void AnExtensionEliteAskedForIsNotAddedTwice() =>
        Assert.Null(CylinderExtension.Extended(true, [.. Elites, CylinderExtension.Name], [CylinderExtension.Name]));

    [Fact]
    public void OutsideEliteTheListIsLeftAlone() =>
        Assert.Null(CylinderExtension.Extended(false, Elites, [CylinderExtension.Name]));
}
