using System.Text.RegularExpressions;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The join that builds the specification table, asserted against its shipped output.
/// <para>
/// Remediation 15 item 2a. `gen-elite-specs.py` joins coriolis-data to FDevIDs on Frontier's
/// `edID` and used to trust the result. It should not have: on 2026-08-19 five of those ids
/// resolved to a different module than the one that carried them, and the wrongest of them
/// shipped — `hpt_mining_subsurfdispmisle_turret_small`, a turreted Sub-surface Displacement
/// Missile, reached the Loadout tab as a **fixed 1B Pulse Laser** at 38,750 credits. That is
/// worse than a mislabelled row, because `AskModule` groups the modules it offers by name: a
/// mining missile was filed inside the pulse laser list, where nobody looking for it would
/// look and everybody choosing a laser would find it.
/// </para>
/// <para>
/// These run here rather than only in the generator because the generator is not part of the
/// build and nothing in CI runs it — the same argument `CoreDependencyTests` and the docs gate
/// are made of. A regenerated table that reintroduces any of this fails a test instead of
/// reaching a Commander.
/// </para>
/// </summary>
public class SpecificationJoinTests
{
    /// <summary>
    /// The mount Frontier's own symbol states, or null for a module that is not mounted.
    /// <para>
    /// Spelled as <see cref="ModuleSpecification.Mount"/> spells it, which is lower case: the
    /// table carries "Turreted" and Core says "turreted", because that string is spoken.
    /// </para>
    /// </summary>
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
        // Frontier's infix is unambiguous, and it caught nine rows: the two Sub-surface
        // Displacement Missile pairs reading each other's mount, and five carrying none at all
        // where the symbol states one — AX Missile Rack twice, the Heat Sink and Caustic Sink
        // Launchers, and Point Defence.
        //
        // Two-sided on purpose. A symbol with no mount infix must carry no mount, which is what
        // catches junk rather than a transposition: outfitting.csv files
        // `Int_MkIIAgileBoost_Engine_Size5_Class5` — a thruster — under the literal mount
        // "mount", its own column heading pasted into the cell.
        var disagreeing = EliteSpecifications.Modules
            .Where(module => module.Mount != MountOf(module.Symbol))
            .Select(module => $"{module.Symbol} says {module.Mount ?? "no mount"}")
            .ToList();

        Assert.Empty(disagreeing);
    }

    [Fact]
    public void TheReportedMiningMissileIsAMiningMissile()
    {
        // The reported row, named. It shipped in v0.37.0 and v0.38.0 as a fixed Pulse Laser.
        var missile = EliteSpecifications.Module("hpt_mining_subsurfdispmisle_turret_small");

        Assert.NotNull(missile);
        Assert.Equal("Sub-Surface Displacement Missile", missile.Name);
        Assert.Equal("turreted", missile.Mount);

        // And its whole family, which is the half of the report a single row would not cover:
        // the two `_fixed_` ids read Turreted and the two `_turret_` ids read Fixed, so a test
        // pinning one of the four could pass on a table that still had them swapped.
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

        // The one the mount check alone would have missed: its mount was right all along and
        // only its name was wrong, which is why the id itself has to be checked.
        Assert.Equal(
            "Seismic Charge Launcher",
            EliteSpecifications.Module("hpt_mining_seismchrgwarhd_fixed_medium")?.Name);
    }

    [Fact]
    public void NoModuleIsNamedFromARawSymbolFragment()
    {
        // `disambiguate` gives colliding modules a qualifier derived from their symbol, which is
        // right when the qualifier is a word — "Frame Shift Drive (SCO)" is what Frontier calls
        // it — and is thread B when it is not. Four names carried a raw fragment:
        // `Pulse Laser (fixed seismchrgwarhd)`, `Pulse Laser (subsurfdispmisle turret)`,
        // `Frame Shift Drive (mkii overchargebooster)` and `Thrusters (mkiiagileboost)`, the last
        // two in core sockets where every Commander with that hull meets them.
        //
        // None of the four needed inventing. Two were mis-keyed ids, and the other two are named
        // by outfitting.csv under their own symbol — "Mk II Supercharge Optimised Frame Shift
        // Drive (SCO)" and "Mk II Agile Boost Thrusters" — which the generator now reaches by
        // looking the symbol up when the id misses.
        //
        // So a lower-case parenthetical that echoes the symbol means the naming authority was not
        // consulted, or has no name to give. Either is worth looking at rather than shipping.
        var fragments = EliteSpecifications.Modules
            .Where(module => Regex.IsMatch(module.Name, @"\(([a-z0-9]+ )*[a-z0-9]+\)$"))
            .Select(module => $"{module.Symbol} -> {module.Name}")
            .ToList();

        Assert.Empty(fragments);
    }

    [Fact]
    public void FrontiersPlaceholderModulesAreNotOffered()
    {
        // "Missing Hardpoint" and "Missing Module" are Frontier's own empty-slot markers, not
        // things anybody fits. The generator dropped the seven `int_missing_` ones and kept
        // `hpt_missing_hardpoint` and `hpt_missing_utility`, which shared a name and so drew a
        // two-row group in a chooser that groups by name.
        var placeholders = EliteSpecifications.Modules
            .Where(module => module.Symbol.Contains("_missing_", StringComparison.Ordinal))
            .Select(module => module.Symbol)
            .ToList();

        Assert.Empty(placeholders);
    }

    [Fact]
    public void ADuplicatedSymbolKeepsTheEntryWithTheFigures()
    {
        // coriolis-data declares 35 symbols twice, one of the pair a husk carrying no cost and
        // sometimes a stale damage figure. Which one won used to be whichever `sorted()` put
        // last, so the table shipped an AX Missile Rack costing nothing — in a chooser where the
        // reported complaint was that price is the only thing telling two modules apart.
        Assert.Equal(1352250, EliteSpecifications.Module("hpt_atdumbfiremissile_fixed_large")?.Cost);
        Assert.Equal(412800, EliteSpecifications.Module("hpt_railgun_fixed_medium")?.Cost);
        Assert.Equal(250000, EliteSpecifications.Module("int_detailedsurfacescanner_tiny")?.Cost);

        // Every remaining priceless module, named. This is deliberately a list and not a rule:
        // coriolis-data writes `cost: 0` for these three outright rather than leaving it out, and
        // they are the Corrosion Resistant racks that are unlock rewards rather than shop stock,
        // so zero may well be the truth about them. What matters is that the set is *small and
        // named* — a husk winning a duplicate contest lands here, and the test says so.
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
