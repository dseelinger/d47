using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class EveryModuleElitePutsAMassOnHasOneTests
{
    [Theory]
    [InlineData("int_fuelscoop_size7_class5", 0)]
    [InlineData("int_repairer_size5_class1", 0)]
    [InlineData("int_refinery_size4_class1", 0)]
    [InlineData("int_dockingcomputer_advanced", 0)]
    [InlineData("int_largebuggybay_size2_class3", 12)]
    [InlineData("int_mkii_passengercabin_size4_class1", 10)]
    public void AModuleCarriesTheMassEdsyOrCoriolisGives(string symbol, double mass) =>
        Assert.Equal(mass, EliteSpecifications.Module(symbol)!.Mass);
}
