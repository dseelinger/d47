using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The generated specification table, and what is said about a hull it has no figures for.
/// <para>
/// These run against the real embedded resource rather than a fixture. A table is only worth
/// having if it is right, and a test that asserted against its own copy of the numbers would
/// prove the parser works and nothing about the data.
/// </para>
/// </summary>
public class SpecificationTests
{
    [Fact]
    public void TheShippedTableCarriesTheHullsAndTheModules()
    {
        Assert.True(EliteSpecifications.Ships.Count > 40, $"{EliteSpecifications.Ships.Count} ships");
        Assert.True(EliteSpecifications.Modules.Count > 800, $"{EliteSpecifications.Modules.Count} modules");
    }

    [Fact]
    public void AHullIsFoundByTheJournalsSymbolAndByWhatAPersonSays()
    {
        // Both arrive: get_ship reports the symbol off the Loadout, and a Commander says the name.
        var bySymbol = EliteSpecifications.Ship("krait_mkii");
        var byName = EliteSpecifications.Ship("Krait MkII");

        Assert.NotNull(bySymbol);
        Assert.Equal(bySymbol, byName);
        Assert.Equal("medium", bySymbol.Pad);
    }

    [Fact]
    public void ThePadSizeIsRightForTheShipsEverybodyAsksAbout()
    {
        // The one fact that decides whether a station is even an option, checked against three
        // hulls where a wrong answer would strand somebody.
        Assert.Equal("large", EliteSpecifications.Ship("type9")?.Pad);
        Assert.Equal("large", EliteSpecifications.Ship("anaconda")?.Pad);
        Assert.Equal("small", EliteSpecifications.Ship("sidewinder")?.Pad);
    }

    [Fact]
    public void HardpointsAreSizesLargestFirstRatherThanACount()
    {
        var anaconda = EliteSpecifications.Ship("anaconda");

        Assert.NotNull(anaconda);
        Assert.Equal(8, anaconda.Hardpoints.Count);
        Assert.Equal(4, anaconda.Hardpoints[0]);

        // "8 hardpoints" is not the answer to "what can it carry".
        Assert.Equal(anaconda.Hardpoints, [.. anaconda.Hardpoints.OrderDescending()]);
    }

    [Fact]
    public void ADriveCarriesTheNumbersAJumpRangeIsComputedFrom()
    {
        var drive = Assert.Single(EliteSpecifications.ModulesNamed("Frame Shift Drive", 5, "A"));

        Assert.True(drive.IsDrive);
        Assert.NotNull(drive.OptimalMass);
        Assert.NotNull(drive.MaxFuelPerJump);
        Assert.NotNull(drive.FuelPower);
        Assert.Equal("5A", drive.Size);
    }

    [Fact]
    public void ANonDriveModuleDoesNotClaimToBeOne()
    {
        var rack = EliteSpecifications.ModulesNamed("Cargo Rack", 4, "E");

        Assert.NotEmpty(rack);
        Assert.All(rack, module => Assert.False(module.IsDrive));
    }

    [Fact]
    public void AModuleIsFoundByTheSymbolTheJournalWrites()
    {
        // The same key a fitted module arrives under, so "what have I got" and "what is that"
        // are one lookup rather than two vocabularies.
        var drive = EliteSpecifications.Module("int_hyperdrive_size5_class5");

        Assert.NotNull(drive);
        Assert.Equal(5, drive.Class);
        Assert.Equal("A", drive.Rating);
    }

    // ---- Bulkheads -------------------------------------------------------------------------

    [Fact]
    public void EveryBulkheadTheJournalWritesIsInTheTable()
    {
        // Armour is per-hull, so coriolis-data files it inside each ship's own JSON rather than
        // under modules/ — and reading only modules/ dropped all 241 of them. That is not a
        // corner: 1,725 of 20,526 engineered modules across 912 real journals are bulkheads, and
        // every one of them was read out to the Commander as its raw symbol.
        string[] written =
        [
            "mandalay_armour_grade1", "panthermkii_armour_grade1", "cobramkv_armour_reactive",
            "corsair_armour_grade3", "type9_armour_grade2", "sidewinder_armour_mirrored",
        ];

        Assert.All(written, symbol => Assert.NotNull(EliteSpecifications.Module(symbol)));

        var bulkheads = EliteSpecifications.Modules.Count(module => module.IsBulkhead);

        Assert.True(bulkheads > 200, $"{bulkheads} bulkheads");
    }

    [Fact]
    public void ArmourIsTheOnlyModuleKindThatReachesTheTableWithNoClass()
    {
        // What IsBulkhead is read off, asserted against the shipped table rather than assumed. A
        // generic module arriving without a class would otherwise be described as armour and
        // quoted a hull boost and four resistances it has none of.
        var classless = EliteSpecifications.Modules.Where(module => module.Class is null).ToArray();
        var armour = EliteSpecifications.Modules
            .Where(module => module.Symbol.Contains("_armour_", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(classless);
        Assert.Equal(armour.Length, classless.Length);
        Assert.All(classless, module => Assert.True(module.IsBulkhead, module.Symbol));
        Assert.All(armour, module => Assert.True(module.IsBulkhead, module.Symbol));
    }

    [Fact]
    public void ABulkheadsNameCarriesItsHull()
    {
        // Forty-eight hulls have a "Lightweight Alloy". The outfitting screen can leave the ship
        // unsaid because the Commander is standing in it; a table with one row per object cannot,
        // and forty-eight identically named rows carrying different mass and different cost is
        // exactly the collision this table is built the way it is to avoid.
        Assert.Equal("Mandalay Lightweight Alloy", EliteSpecifications.Module("mandalay_armour_grade1")?.Name);
        Assert.Equal("Type-9 Heavy Reinforced Alloy", EliteSpecifications.Module("type9_armour_grade2")?.Name);

        var bulkheads = EliteSpecifications.Modules.Where(module => module.IsBulkhead).ToArray();

        Assert.True(
            bulkheads.Count(module => module.Name.EndsWith("Lightweight Alloy", StringComparison.Ordinal)) > 40);

        // And the hull is what makes them unique, which is what keeps a symbol lookup and a spoken
        // lookup answering about the same object.
        Assert.Equal(
            bulkheads.Length,
            bulkheads.Select(module => module.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TheHullBoostIsTheFractionAddedToTheShipsOwnArmour()
    {
        // The join between the two sections: a Sidewinder's 60 armour under Military Grade
        // Composite is the 210 the outfitting screen shows, and it is only derivable if the
        // bulkhead row and the hull row agree about what the number means.
        var sidewinder = EliteSpecifications.Ship("sidewinder");
        var military = EliteSpecifications.Module("sidewinder_armour_grade3");

        Assert.NotNull(sidewinder);
        Assert.NotNull(military);
        Assert.Equal(2.5, military.HullBoost);
        Assert.Equal(210, sidewinder.Armour * (1 + military.HullBoost));
    }

    [Fact]
    public void MirroredAndReactiveAreToldApartByTheirResistancesAndNothingElse()
    {
        // The whole reason the resistances are carried. These two weigh the same, boost the hull
        // by the same amount and differ in price, so a table without them would offer a Commander
        // a choice between two rows that read identically.
        var mirrored = EliteSpecifications.Module("mandalay_armour_mirrored");
        var reactive = EliteSpecifications.Module("mandalay_armour_reactive");

        Assert.NotNull(mirrored);
        Assert.NotNull(reactive);
        Assert.Equal(mirrored.Mass, reactive.Mass);
        Assert.Equal(mirrored.HullBoost, reactive.HullBoost);

        // Signed, and the sign is the answer: Mirrored gives half again against thermal and takes
        // three quarters more kinetic, and Reactive is the other way round.
        Assert.Equal(0.5, mirrored.ThermalResistance);
        Assert.Equal(-0.75, mirrored.KineticResistance);
        Assert.Equal(-0.4, reactive.ThermalResistance);
        Assert.Equal(0.25, reactive.KineticResistance);
    }

    [Fact]
    public void ABulkheadNamedInTheIdListAndAbsentFromTheFiguresIsStillABulkhead()
    {
        // Five of them, all the Lynx Highliner's. The symbol is what the journal writes, so a
        // named row still answers "that is your Lightweight Alloy" rather than reading out
        // mediumtransport01_armour_grade1 — which is the failure that started all this.
        var lynx = EliteSpecifications.Module("mediumtransport01_armour_grade1");

        Assert.NotNull(lynx);
        Assert.True(lynx.IsBulkhead);
        Assert.False(lynx.IsDrive);
        Assert.Null(lynx.HullBoost);
        Assert.Equal("Lynx Highliner Lightweight Alloy", lynx.Name);
    }

    [Fact]
    public void AModuleThatIsNotArmourClaimsNoArmourFigures()
    {
        var drive = EliteSpecifications.Module("int_hyperdrive_size5_class5");

        Assert.NotNull(drive);
        Assert.False(drive.IsBulkhead);
        Assert.Null(drive.HullBoost);
        Assert.Null(drive.KineticResistance);
    }

    // ---- Names that tell things apart --------------------------------------------------------

    [Fact]
    public void ARackThatResistsCorrosionSaysSoRatherThanCarryingASymbolFragment()
    {
        // These two used to collide. outfitting.csv indexes the corrosion-resistant rack at sizes
        // 1 and 4 and not at 5 or 6, so the larger two fell through to a name taken from their
        // file — "Cargo Rack", the same as the plain rack — and `disambiguate` then separated
        // them with the only thing that told them apart, giving "Cargo Rack (corrosionproof)".
        //
        // Remediation 15 item 2a: that is not a cosmetic blemish, because `AskModule` groups what
        // it offers by name. A rack whose whole point is that it resists corrosion sat inside the
        // plain rack's list, one letter of a symbol away from being findable.
        //
        // The generator now takes the name from the module's own siblings when the naming
        // authority has none for it, so all four read alike and nothing collides.
        Assert.Equal("Cargo Rack", EliteSpecifications.Module("int_cargorack_size5_class1")?.Name);
        Assert.Equal("Cargo Rack", EliteSpecifications.Module("int_cargorack_size6_class1")?.Name);

        foreach (var size in new[] { 1, 4, 5, 6 })
        {
            Assert.Equal(
                "Corrosion Resistant Cargo Rack",
                EliteSpecifications.Module($"int_corrosionproofcargorack_size{size}_class1")?.Name);
        }
    }

    [Fact]
    public void NoShippedModuleNeedsAQualifierToTellItApart()
    {
        // `disambiguate` is the generator's last resort: where two modules would otherwise share a
        // name, class, rating and mount, it separates them with whatever token their symbols do
        // not share. As of remediation 15 item 2a it alters nothing — every module in the table is
        // named by FDevIDs, by id or by symbol or through its own family — so the only
        // parentheses left are Frontier's own, in "Frame Shift Drive (SCO)".
        //
        // Asserted rather than assumed, because a qualifier is how a wrong name used to arrive:
        // all four of the raw-fragment names in the reported batch were `disambiguate` doing its
        // job honestly on top of a name that was already wrong. A new one firing means a name went
        // missing upstream, and that is worth a look rather than a shrug.
        var qualified = EliteSpecifications.Modules
            .Where(module => module.Name.Contains(" (", StringComparison.Ordinal))
            .Select(module => module.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        // **Amended 2026-08-20**, and the amendment is the gate working rather than being
        // weakened. Keeping a module `outfitting.csv` names and coriolis-data has no figures for —
        // the rule the bulkheads have always followed — added 28 modules, and ten of them collide
        // with a name already in the table. Every qualifier below is Frontier's own symbol token
        // and every one is true:
        //
        //   * `(free)` separates the starter-ship variants from the modules they share a name
        //     with. A Sidewinder flies with them, so they are not hypothetical.
        //   * `(size5)` and `(size6)` separate the Mk II cabins, which `outfitting.csv` files
        //     under one name *and* one class — it rates the size 6 pair class 5. The symbol is the
        //     only place the difference survives, which is exactly where `disambiguate` looks.
        //
        // A qualifier appearing that is not in this list still means a name went missing upstream.
        Assert.Equal(
            [
                "Basic Discovery Scanner (free)",
                "Cargo Rack (free)",
                "Frame Shift Drive (SCO)",
                "Frame Shift Drive (free)",
                "Fuel Tank (free)",
                "Life Support (free)",
                "Mk II Supercharge Optimised Frame Shift Drive (SCO)",
                "MkII Business Class Passenger Cabin (size5)",
                "MkII Business Class Passenger Cabin (size6)",
                "MkII Economy Class Passenger Cabin (size5)",
                "MkII Economy Class Passenger Cabin (size6)",
                "Power Distributor (free)",
                "Power Plant (free)",
                "Sensors (free)",
                "Shield Generator (free)",
                "Thrusters (free)",
            ],
            qualified);
    }

    [Fact]
    public void NoNameQualifiesItselfWithAWordItAlreadySays()
    {
        // The general form of the above, swept over the whole shipped table, so the next time a
        // source renames something it cannot quietly bring the doubled word back.
        foreach (var module in EliteSpecifications.Modules)
        {
            var open = module.Name.IndexOf(" (", StringComparison.Ordinal);

            if (open < 0 || !module.Name.EndsWith(')'))
            {
                continue;
            }

            var qualified = Letters(module.Name[..open]);

            foreach (var word in module.Name[(open + 2)..^1].Split(' '))
            {
                Assert.DoesNotContain(qualified, Letters(word), StringComparison.Ordinal);
            }
        }
    }

    private static string Letters(string text) =>
        new([.. text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    // ---- What the model sees ---------------------------------------------------------------

    private static CapabilityRegistry Registry(string? shipType = "python")
    {
        var gameState = new GameStateStore();

        Assert.True(JournalEvent.TryParse(
            """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
            NullLogger.Instance,
            out var identity));

        gameState.Apply(identity!);

        if (shipType is not null)
        {
            Assert.True(JournalEvent.TryParse(
                $$"""{"timestamp":"2026-01-01T00:00:01Z","event":"Loadout","Ship":"{{shipType}}","MaxJumpRange":30}""",
                NullLogger.Instance,
                out var loadout));

            gameState.Apply(loadout!);
        }

        return CapabilityRegistry.Build([SpecificationCapability.Create(() => gameState.Active)]);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    [Fact]
    public async Task AskingWithNoShipNamedAnswersAboutTheOneBeingFlown()
    {
        var result = await Registry().InvokeAsync(
            "get_ship_specification",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Python", result.Content, StringComparison.Ordinal);
        Assert.Contains("Needs a medium pad", result.Content, StringComparison.Ordinal);
        Assert.Contains("before any modules", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHullTheTableKnowsOfAndHasNoFiguresForSaysExactlyThat()
    {
        // Newer than the id list the table is keyed by, so its figures are unreachable and its
        // existence is certain. "I don't know that ship" would tell a Commander flying a brand
        // new hull that d47 is broken.
        Assert.NotEmpty(EliteSpecifications.KnownButUnmeasured);

        var result = await Registry().InvokeAsync(
            "get_ship_specification",
            Args(("ship", EliteSpecifications.KnownButUnmeasured[0])),
            TestContext.Current.CancellationToken);

        Assert.Contains("newer than the specification table", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("I don't know a ship", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AShipNobodyHasHeardOfGetsSuggestionsRatherThanInventedFigures()
    {
        var result = await Registry().InvokeAsync(
            "get_ship_specification",
            Args(("ship", "Anacondia")),
            TestContext.Current.CancellationToken);

        Assert.Contains("Anaconda", result.Content, StringComparison.Ordinal);
        Assert.Contains("Did you mean", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AModuleNameWithNoSizeReturnsTheSizesRatherThanPickingOne()
    {
        // Eleven drives differing by an order of magnitude in every figure worth quoting. Picking
        // one and reporting its numbers would answer a question nobody asked.
        var result = await Registry().InvokeAsync(
            "get_module_specification",
            Args(("module", "Frame Shift Drive")),
            TestContext.Current.CancellationToken);

        Assert.Contains("variants", result.Content, StringComparison.Ordinal);
        Assert.Contains("Ask for a size and rating", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASizeThatDoesNotExistIsAnsweredWithTheOnesThatDo()
    {
        var result = await Registry().InvokeAsync(
            "get_module_specification",
            Args(("module", "Frame Shift Drive"), ("size", "9"), ("rating", "A")),
            TestContext.Current.CancellationToken);

        Assert.Contains("There is no 9A Frame Shift Drive", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskedForOneDriveItGivesTheJumpNumbers()
    {
        var result = await Registry().InvokeAsync(
            "get_module_specification",
            Args(("module", "frame shift drive"), ("size", "5"), ("rating", "A")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Optimal mass", result.Content, StringComparison.Ordinal);
        Assert.Contains("max fuel per jump", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoLoadoutYetItNamesTheEventItIsWaitingFor()
    {
        var result = await Registry(shipType: null).InvokeAsync(
            "get_ship_specification",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken);

        Assert.Contains("Loadout event", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskedForABulkheadItGivesTheHullBoostAndTheResistancesWithTheirSigns()
    {
        var result = await Registry().InvokeAsync(
            "get_module_specification",
            Args(("module", "Mandalay Reactive Surface Composite")),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("Hull +250%", result.Content, StringComparison.Ordinal);
        Assert.Contains("kinetic +25%", result.Content, StringComparison.Ordinal);
        Assert.Contains("thermal -40%", result.Content, StringComparison.Ordinal);

        // Zero is said out loud. A resistance left unmentioned reads as one nobody knows, and
        // "no effect" is a different claim from "no figure".
        Assert.Contains("caustic 0%", result.Content, StringComparison.Ordinal);

        // Armour has no size and no rating, so the size falls back to the name — and leading with
        // both would say the whole thing twice.
        Assert.DoesNotContain(
            "Mandalay Reactive Surface Composite Mandalay",
            result.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABulkheadAskedForAtASizeIsToldItHasNoneRatherThanTheSizesItComesIn()
    {
        var result = await Registry().InvokeAsync(
            "get_module_specification",
            Args(("module", "Mandalay Reactive Surface Composite"), ("size", "5"), ("rating", "A")),
            TestContext.Current.CancellationToken);

        Assert.Contains("is armour", result.Content, StringComparison.Ordinal);
        Assert.Contains("no size or rating", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("It comes in", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABareArmourNameOffersTheHullsRatherThanPickingOne()
    {
        // Forty-eight hulls have a Lightweight Alloy and they weigh and cost different amounts, so
        // an unqualified name has no right answer. Catalogue refuses an ambiguous fragment, which
        // turns this into the suggestions rather than into one arbitrary ship's figures.
        var result = await Registry().InvokeAsync(
            "get_module_specification",
            Args(("module", "Lightweight Alloy")),
            TestContext.Current.CancellationToken);

        Assert.Contains("Did you mean", result.Content, StringComparison.Ordinal);
        Assert.Contains("Lightweight Alloy", result.Content, StringComparison.Ordinal);
    }
}
