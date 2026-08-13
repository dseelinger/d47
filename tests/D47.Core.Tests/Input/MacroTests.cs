using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>
/// Named sequences the Commander authored (list.md Phase 10, "Macros").
/// <para>
/// The validation is the interesting half. Authoring is the one input whose vocabulary cannot
/// be closed in advance, so everything a macro is <em>made of</em> has to be checked against a
/// vocabulary that is.
/// </para>
/// </summary>
public class MacroTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "d47-macro-tests", Guid.NewGuid().ToString("N"));

    private string File => Path.Combine(_root, "macros.json");

    public MacroTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    private MacroStore Store() => new(File, NullLogger<MacroStore>.Instance);

    private void Write(string json) => System.IO.File.WriteAllText(File, json);

    private static EliteBinds Binds(params (string Action, string Key)[] entries) => new()
    {
        PresetName = "Test",
        SourceFile = "Test.binds",
        Bindings = [.. entries.Select(e => new EliteBinding(e.Action, "Primary", "Keyboard", e.Key))],
    };

    private static ActionSurface Actions(EliteBinds binds, RecordingGameInput input, StatusFlags flags = StatusFlags.None) => new()
    {
        Binds = () => binds,
        Status = () => new GameStatus { Flags = StatusFlags.InMainShip | flags, ReadAt = DateTimeOffset.UnixEpoch },
        Input = input,
        Enabled = () => true,
    };

    [Fact]
    public void AGoodMacroLoads()
    {
        Write("""
        { "macros": [
          { "name": "docking prep",
            "steps": [
              { "action": "landing_gear", "state": "on", "pauseMs": 200 },
              { "action": "lights", "state": "on" }
            ] } ] }
        """);

        var store = Store();
        Assert.True(store.Poll([]));

        var macro = Assert.Single(store.Macros);
        Assert.Equal("docking prep", macro.Name);
        Assert.Equal(2, macro.Steps.Count);
        Assert.Empty(store.Problems);
    }

    [Fact]
    public void AnUnknownActionIsRefusedByName()
    {
        Write("""
        { "macros": [ { "name": "bad", "steps": [ { "action": "self_destruct" } ] } ] }
        """);

        var store = Store();
        store.Poll([]);

        Assert.Empty(store.Macros);
        Assert.Contains("self_destruct", Assert.Single(store.Problems).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AMacroCannotFireWeapons()
    {
        // The catalogue carries the fire groups for the arrival honk. Authored text must not be
        // a way around "the model never gets to open fire".
        Write("""
        { "macros": [ { "name": "shoot", "steps": [ { "action": "primary_fire" } ] } ] }
        """);

        var store = Store();
        store.Poll([]);

        Assert.Empty(store.Macros);
        Assert.Contains("weapons", Assert.Single(store.Problems).Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMacroCannotTakeAPhraseThatAlreadyMeansSomething()
    {
        // Otherwise the Commander has two things answering to "gear down" and no way to tell
        // which one ran.
        Write("""
        { "macros": [ { "name": "gear down", "steps": [ { "action": "lights" } ] } ] }
        """);

        var store = Store();
        store.Poll(["gear down"]);

        Assert.Empty(store.Macros);
        Assert.Contains("already a command", Assert.Single(store.Problems).Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OneBadMacroDoesNotCostTheCommanderTheirGoodOnes()
    {
        Write("""
        { "macros": [
          { "name": "good", "steps": [ { "action": "lights" } ] },
          { "name": "bad", "steps": [ { "action": "nope" } ] } ] }
        """);

        var store = Store();
        store.Poll([]);

        Assert.Single(store.Macros);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void AnUnreadableFileIsOneNamedProblemRatherThanAThrow()
    {
        Write("{ this is not json");

        var store = Store();
        store.Poll([]);

        Assert.Empty(store.Macros);
        Assert.Single(store.Problems);
    }

    [Fact]
    public void AMissingFileIsNormalRatherThanAProblem()
    {
        var store = Store();
        store.Poll([]);

        Assert.Empty(store.Macros);
        Assert.Empty(store.Problems);
    }

    [Fact]
    public void AnAbsurdPauseIsRefused()
    {
        // A macro is held input, and an extra zero would leave d47 driving the ship for hours.
        Write("""
        { "macros": [ { "name": "slow", "steps": [ { "action": "lights", "pauseMs": 3600000 } ] } ] }
        """);

        var store = Store();
        store.Poll([]);

        Assert.Empty(store.Macros);
    }

    [Fact]
    public void SavingAndReloadingRoundTrips()
    {
        var store = Store();
        store.Save([new Macro { Name = "combat", Steps = [new MacroStep("hardpoints", DesiredState.On)] }]);
        store.Poll([]);

        Assert.Equal("combat", Assert.Single(store.Macros).Name);
    }

    [Fact]
    public async Task RunningAMacroPressesEveryStepInOrder()
    {
        Write("""
        { "macros": [ { "name": "prep", "steps": [
            { "action": "landing_gear", "state": "on", "pauseMs": 0 },
            { "action": "lights", "state": "on", "pauseMs": 0 } ] } ] }
        """);

        var store = Store();
        store.Poll([]);

        var input = new RecordingGameInput();
        var registry = CapabilityRegistry.Build([
            MacroCapability.Create(store, Actions(
                Binds(("LandingGearToggle", "Key_L"), ("ShipSpotLightToggle", "Key_Insert")), input))]);

        var result = await registry.InvokeAsync(
            "run_macro",
            new ToolArguments(new Dictionary<string, string> { ["name"] = "prep" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        var pressed = input.Steps.Where(s => s.Kind == InputStepKind.KeyDown).Select(s => s.Code).ToArray();
        Assert.Equal([0x4Cu, 0x2Du], pressed);
    }

    [Fact]
    public async Task AnUnreachableStepStopsTheWholeMacroBeforeAnythingIsPressed()
    {
        // A macro that pressed three keys and then found the fourth unreachable would leave the
        // ship half-configured in a state the Commander did not ask for.
        Write("""
        { "macros": [ { "name": "prep", "steps": [
            { "action": "landing_gear" }, { "action": "hardpoints" } ] } ] }
        """);

        var store = Store();
        store.Poll([]);

        var input = new RecordingGameInput();
        var registry = CapabilityRegistry.Build([
            MacroCapability.Create(store, Actions(Binds(("LandingGearToggle", "Key_L")), input))]);

        var result = await registry.InvokeAsync(
            "run_macro",
            new ToolArguments(new Dictionary<string, string> { ["name"] = "prep" }),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Empty(input.Steps);
    }

    [Fact]
    public async Task AStepAlreadyInTheAskedForStateIsSkipped()
    {
        Write("""
        { "macros": [ { "name": "prep", "steps": [ { "action": "landing_gear", "state": "on" } ] } ] }
        """);

        var store = Store();
        store.Poll([]);

        var input = new RecordingGameInput();
        var registry = CapabilityRegistry.Build([
            MacroCapability.Create(store, Actions(
                Binds(("LandingGearToggle", "Key_L")), input, StatusFlags.LandingGearDown))]);

        var result = await registry.InvokeAsync(
            "run_macro",
            new ToolArguments(new Dictionary<string, string> { ["name"] = "prep" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Empty(input.Steps);
    }

    [Fact]
    public void AMacroNameIsSayableWithoutAModel()
    {
        Write("""
        { "macros": [ { "name": "docking prep", "steps": [ { "action": "lights" } ] } ] }
        """);

        var store = Store();
        store.Poll([]);

        var registry = CapabilityRegistry.Build([
            MacroCapability.Create(store, ActionSurface.Inert)]);

        var router = new KeywordRouter(registry, () => MacroCapability.Phrases(store));

        foreach (var utterance in new[] { "docking prep", "run docking prep" })
        {
            var match = router.MatchToolCommand(utterance);

            Assert.NotNull(match);
            Assert.Equal("run_macro", match.ToolName);
            Assert.Equal("docking prep", match.Arguments.Values["name"]);
        }
    }

    [Fact]
    public void AMacroAddedAfterStartupIsSayableWithoutARestart()
    {
        // The reason the router takes these from a function: descriptors are registered once
        // and never mutated, and the Commander authors these at runtime.
        var store = Store();
        store.Poll([]);

        var registry = CapabilityRegistry.Build([MacroCapability.Create(store, ActionSurface.Inert)]);
        var router = new KeywordRouter(registry, () => MacroCapability.Phrases(store));

        Assert.Null(router.MatchToolCommand("docking prep"));

        Write("""
        { "macros": [ { "name": "docking prep", "steps": [ { "action": "lights" } ] } ] }
        """);
        store.Poll([]);

        Assert.NotNull(router.MatchToolCommand("docking prep"));
    }

    [Fact]
    public void TheSchemaDoesNotMoveWhenTheCommanderAddsAMacro()
    {
        // Macro names change whenever the file is edited. A schema that changed with them
        // would invalidate the entire cached prefix.
        var store = Store();
        store.Poll([]);
        var before = CapabilityRegistry.Build([MacroCapability.Create(store, ActionSurface.Inert)]);

        Write("""
        { "macros": [ { "name": "docking prep", "steps": [ { "action": "lights" } ] } ] }
        """);
        store.Poll([]);
        var after = CapabilityRegistry.Build([MacroCapability.Create(store, ActionSurface.Inert)]);

        Assert.Equal(before.All[0].ToolSchemas, after.All[0].ToolSchemas);
    }
}
