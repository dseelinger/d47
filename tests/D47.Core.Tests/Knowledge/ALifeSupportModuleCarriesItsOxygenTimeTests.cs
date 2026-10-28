using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ALifeSupportModuleCarriesItsOxygenTimeTests
{
    [Fact]
    public void ARatingEModuleHoldsFiveMinutes() =>
        Assert.Equal(300, EliteSpecifications.OxygenSeconds("int_lifesupport_size1_class1"));

    [Fact]
    public void ARatingAModuleHoldsTwentyFiveMinutes() =>
        Assert.Equal(1500, EliteSpecifications.OxygenSeconds("int_lifesupport_size8_class5"));

    [Fact]
    public void AnyOtherModuleHasNone()
    {
        Assert.Null(EliteSpecifications.OxygenSeconds("int_dronecontrol_collection_size1_class1"));
        Assert.Null(EliteSpecifications.OxygenSeconds("hpt_pulselaser_fixed_small"));
        Assert.Null(EliteSpecifications.OxygenSeconds(null));
    }

    [Fact]
    public void OnlyLifeSupportRowsCarryTheFigure() =>
        Assert.All(
            EliteSpecifications.Modules.Where(module => module.Figure("oxygen") is not null),
            module => Assert.StartsWith("int_lifesupport_", module.Symbol, StringComparison.OrdinalIgnoreCase));
}
