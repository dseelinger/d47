using System.Text.RegularExpressions;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The join that builds the specification table, asserted against its shipped output.</summary>
public class SpecificationJoinTests
{
    /// <summary>The mount Frontier's own symbol states, or null for a module that is not mounted.</summary>
    private static string? MountOf(string symbol)
    {
        var tokens = symbol.Split('_');

        string? found = null;

        foreach (var (infix, word) in new[]
                 {
                     ("fixed", "fixed"), ("turret", "turreted"), ("gimbal", "gimballed"),
                 })
        {
            if (!tokens.Contains(infix))
            {
                continue;
            }

            // Two mounts in one symbol would make the answer a guess, so it stops claiming one.
            if (found is not null)
            {
                return null;
            }

            found = word;
        }

        return found;
    }

    [Fact]
    public void EveryMountAgreesWithTheSymbolItSitsBeside()
    {
        // The check that catches a transposed id without knowing anything about names.
        var disagreeing = EliteSpecifications.Modules
            .Where(module => module.Mount != MountOf(module.Symbol))
            .Select(module => $"{module.Symbol} says {module.Mount ?? "no mount"}")
            .ToList();

        Assert.Empty(disagreeing);
    }

    [Fact]
    public void TheReportedMiningMissileIsAMiningMissile()
    {
        // The reported row, named.
        var missile = EliteSpecifications.Module("hpt_mining_subsurfdispmisle_turret_small");

        Assert.NotNull(missile);
        Assert.Equal("Sub-Surface Displacement Missile", missile.Name);
        Assert.Equal("turreted", missile.Mount);

        // And its whole family, which is the half of the report a single row would not cover: the two
        // `_fixed_` ids read Turreted and the two `_turret_` ids read Fixed, so a test pinning one of the
        // four could pass on a table that still had them swapped.
        foreach (var symbol in new[]
                 {
                     "hpt_mining_subsurfdispmisle_fixed_small",
                     "hpt_mining_subsurfdispmisle_fixed_medium",
                     "hpt_mining_subsurfdispmisle_turret_medium",
                 })
        {
            var module = EliteSpecifications.Module(symbol);

            Assert.NotNull(module);
            Assert.Equal("Sub-Surface Displacement Missile", module.Name);
        }

        // The one the mount check alone would have missed: its mount was right all along and only its name
        // was wrong, which is why the id itself has to be checked.
        Assert.Equal(
            "Seismic Charge Launcher",
            EliteSpecifications.Module("hpt_mining_seismchrgwarhd_fixed_medium")?.Name);
    }

    [Fact]
    public void NoModuleIsNamedFromARawSymbolFragment()
    {
        // `disambiguate` gives colliding modules a qualifier derived from their symbol, which is right when
        // the qualifier is a word — "Frame Shift Drive (SCO)" is what Frontier calls it — and is thread B
        // when it is not.
        var expected = new[] { "free", "size5", "size6" };

        var fragments = EliteSpecifications.Modules
            .Where(module => Regex.IsMatch(module.Name, @"\(([a-z0-9]+ )*[a-z0-9]+\)$"))
            .Where(module => !expected.Any(token =>
                module.Name.EndsWith($"({token})", StringComparison.Ordinal)))
            .Select(module => $"{module.Symbol} -> {module.Name}")
            .ToList();

        Assert.Empty(fragments);
    }

    [Fact]
    public void FrontiersPlaceholderModulesAreNotOffered()
    {
        // "Missing Hardpoint" and "Missing Module" are Frontier's own empty-slot markers, not things anybody
        // fits.
        var placeholders = EliteSpecifications.Modules
            .Where(module => module.Symbol.Contains("_missing_", StringComparison.Ordinal))
            .Select(module => module.Symbol)
            .ToList();

        Assert.Empty(placeholders);
    }

    [Fact]
    public void ADuplicatedSymbolKeepsTheEntryWithTheFigures()
    {
        // coriolis-data declares 35 symbols twice, one of the pair a husk carrying no cost and sometimes a
        // stale damage figure.
        Assert.Equal(1352250, EliteSpecifications.Module("hpt_atdumbfiremissile_fixed_large")?.Cost);
        Assert.Equal(412800, EliteSpecifications.Module("hpt_railgun_fixed_medium")?.Cost);
        Assert.Equal(250000, EliteSpecifications.Module("int_detailedsurfacescanner_tiny")?.Cost);

        // Every remaining priceless module, named.
        var free = EliteSpecifications.Modules
            .Where(module => module is { Cost: 0, IsBulkhead: false })
            .Select(module => module.Symbol)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "int_corrosionproofcargorack_size1_class2",
                "int_corrosionproofcargorack_size5_class1",
                "int_corrosionproofcargorack_size6_class1",
            ],
            free);
    }
}
