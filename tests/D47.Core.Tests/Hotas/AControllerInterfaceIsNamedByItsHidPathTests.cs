using D47.Core.Hotas;
using Xunit;

namespace D47.Core.Tests.Hotas;

public sealed class AControllerInterfaceIsNamedByItsHidPathTests
{
    private const string Throttle =
        @"\?\HID#VID_4098&PID_BD65&Col01#6&8f76149&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";

    private static string One(string path, int buttons) =>
        HidIdentity.Build([new HidInterface(path, buttons)])[0];

    [Fact]
    public void TheIdentityComesFromThePathAndButtonCountOnly()
    {
        Assert.Equal("hid:VID_4098&PID_BD65&COL01:b32", One(Throttle, 32));
        Assert.Equal(One(Throttle, 32), One(Throttle, 32));
        Assert.DoesNotContain("{wgi/nrid/", One(Throttle, 32));
    }

    [Fact]
    public void AnotherUsbPortGivesTheSameIdentity() =>
        Assert.Equal(One(Throttle, 32), One(Throttle.Replace("6&8f76149&0&0000", "7&1a2b3c&0&0001"), 32));

    [Fact]
    public void CollectionsOfOneDeviceAreDistinct() =>
        Assert.NotEqual(One(Throttle, 32), One(Throttle.Replace("Col01", "Col02"), 32));

    [Fact]
    public void ADifferentButtonCountIsADifferentIdentity() =>
        Assert.NotEqual(One(Throttle, 32), One(Throttle, 128));

    [Fact]
    public void IdenticalDevicesInOneListAreToldApartByTheirThirdSegment()
    {
        var other = Throttle.Replace("6&8f76149&0&0000", "6&1111111&0&0000");
        var ids = HidIdentity.Build([new HidInterface(Throttle, 32), new HidInterface(other, 32)]);

        Assert.NotEqual(ids[0], ids[1]);
        Assert.Equal("hid:VID_4098&PID_BD65&COL01:b32:6&8F76149&0&0000", ids[0]);
    }

    [Fact]
    public void ADeviceThatDoesNotCollideCarriesNoThirdSegment()
    {
        var ids = HidIdentity.Build([new HidInterface(Throttle, 32), new HidInterface(Throttle, 128)]);

        Assert.Equal("hid:VID_4098&PID_BD65&COL01:b32", ids[0]);
    }

    [Fact]
    public void TheIdentityHasNoHash()
    {
        var other = Throttle.Replace("6&8f76149&0&0000", "6&1111111&0&0000");

        Assert.All(HidIdentity.Build([new HidInterface(Throttle, 32), new HidInterface(other, 32)]),
            id => Assert.DoesNotContain('#', id));
    }
}
