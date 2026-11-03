using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Modules;
using Xunit;
using static D47.Core.Tests.Modules.FakeModuleTools;

namespace D47.Core.Tests.Modules;

public class APressOnAModulePageCallsItsOwnToolAsTheCommanderTests
{
    private readonly List<(string Name, ToolArguments Arguments, ToolCaller Caller)> _calls = [];

    private Task<ToolResult> Invoke(string name, ToolArguments arguments, ToolCaller caller, CancellationToken cancellation)
    {
        _calls.Add((name, arguments, caller));
        return Task.FromResult(ToolResult.Ok("done"));
    }

    [Fact]
    public async Task APressedTileCallsTheModulesToolWithTheCommanderAsCaller()
    {
        await ModulePagePress.CallAsync(Invoke, "ledger", new ModuleToolCall("remind", """{"minutes":30}"""), CancellationToken.None);

        var (name, arguments, caller) = Assert.Single(_calls);
        Assert.Equal("mod_ledger_remind", name);
        Assert.Equal(ToolCaller.Commander, caller);
        Assert.Equal("""{"minutes":30}""", arguments.Json);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("100000")]
    [InlineData("\"today\"")]
    public async Task AControlSendsItsValueUnderItsArgumentInItsOwnJsonType(string value)
    {
        await ModulePagePress.SendAsync(Invoke, "ledger", new ModuleSends("count", "period"), value, CancellationToken.None);

        var (name, arguments, caller) = Assert.Single(_calls);
        Assert.Equal("mod_ledger_count", name);
        Assert.Equal(ToolCaller.Commander, caller);
        Assert.Equal($$"""{"period":{{value}}}""", arguments.Json);
    }

    [Fact]
    public async Task ThroughTheRegistryTheModuleIsSentTheTypedValue()
    {
        var registry = CapabilityRegistry.Build([]);
        var modules = new FakeModuleTools().Offering("ledger", "Bounty ledger", Tool("ledger", "say_each"));
        modules.AttachTo(registry);

        await ModulePagePress.SendAsync(
            (name, arguments, caller, cancellation) => registry.InvokeAsync(name, arguments, cancellation, caller),
            "ledger",
            new ModuleSends("say_each", "on"),
            "true",
            CancellationToken.None);

        var (moduleId, tool, sent) = Assert.Single(modules.Calls);
        Assert.Equal(("ledger", "say_each"), (moduleId, tool));
        Assert.Equal(JsonValueKind.True, Assert.IsType<JsonElement>(sent["on"]).ValueKind);
    }
}
