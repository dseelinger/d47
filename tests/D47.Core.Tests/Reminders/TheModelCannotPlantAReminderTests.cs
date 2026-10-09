using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Reminders;
using Xunit;

namespace D47.Core.Tests.Reminders;

/// <summary>Setting and cancelling a journal reminder are the Commander's own acts (#643).</summary>
[Trait("Category", "Integration")]
public class TheModelCannotPlantAReminderTests
{
    private const string Commander = "F100";

    private static CapabilityRegistry Registry(ReminderBench bench) =>
        CapabilityRegistry.Build([RemindersCapability.Create(bench.Store, () => Commander, () => ReminderBench.Now)]);

    [Fact]
    public async Task TheModelIsRefusedSettingAndCancelling()
    {
        using var bench = new ReminderBench();
        var registry = Registry(bench);
        var cancellation = TestContext.Current.CancellationToken;

        var set = await registry.InvokeAsync(
            RemindersCapability.SetTool,
            new ToolArguments(new Dictionary<string, string> { ["sentence"] = "jettison the cargo", ["trigger"] = "next_docking" }),
            cancellation,
            ToolCaller.Model);

        Assert.True(set.IsError);
        Assert.Empty(bench.Store.For(Commander));

        var held = bench.Arm(Commander, JournalTrigger.NextDocking, "buy limpets");

        var cancel = await registry.InvokeAsync(
            RemindersCapability.CancelTool,
            new ToolArguments(new Dictionary<string, string> { ["words"] = "limpets" }),
            cancellation,
            ToolCaller.Model);

        Assert.True(cancel.IsError);
        Assert.Equal(held.Id, Assert.Single(bench.Store.For(Commander)).Id);
    }

    [Fact]
    public async Task TheModelCanReadWhatIsArmed()
    {
        using var bench = new ReminderBench();
        var registry = Registry(bench);

        bench.Arm(Commander, JournalTrigger.ArrivalIn, "sell data", "Sol");

        var listed = await registry.InvokeAsync(
            RemindersCapability.ListTool, ToolArguments.Empty, TestContext.Current.CancellationToken, ToolCaller.Model);

        Assert.False(listed.IsError);
        Assert.Equal("One reminder is set: to sell data when you arrive in Sol.", listed.Content);
    }

    [Fact]
    public async Task AnUnknownMaterialIsDeclined()
    {
        using var bench = new ReminderBench();
        var registry = Registry(bench);

        var set = await registry.InvokeAsync(
            RemindersCapability.SetTool,
            new ToolArguments(new Dictionary<string, string>
            {
                ["sentence"] = "head home",
                ["trigger"] = "material_full",
                ["argument"] = "fuel tank",
            }),
            TestContext.Current.CancellationToken);

        Assert.True(set.IsError);
        Assert.Empty(bench.Store.For(Commander));
    }
}
