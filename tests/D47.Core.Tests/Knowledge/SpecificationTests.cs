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
}
