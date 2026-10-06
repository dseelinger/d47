using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests;

public sealed class SwitchingCommandersByVoiceTests
{
    private static readonly IReadOnlyDictionary<string, CommanderSighting> Found =
        new Dictionary<string, CommanderSighting>(StringComparer.Ordinal)
        {
            ["F1"] = new("F1", "Ada Quill", DateTimeOffset.UnixEpoch, null, null),
            ["F2"] = new("F2", "Kestrel Vane", DateTimeOffset.UnixEpoch, null, null),
        };

    private static (CapabilityRegistry Registry, List<CommanderIdentity> Picked) Build()
    {
        var picked = new List<CommanderIdentity>();
        var registry = CapabilityRegistry.Build(
            [CommandersCapability.Create(() => Found, () => "F1", picked.Add)]);

        return (registry, picked);
    }

    private static IReadOnlyList<DynamicCommand> Phrases() =>
        [.. CommandersCapability.Phrases(() => Found, () => "F1")];

    [Fact]
    public void EveryCommanderButTheActiveOneGetsTwoPhrases()
    {
        Assert.Equal(
            ["switch to commander Kestrel Vane", "switch to Kestrel Vane"],
            Phrases().Select(command => command.Phrase));
        Assert.All(Phrases(), command => Assert.Equal("F2", command.Arguments["frontier_id"]));
    }

    [Fact]
    public async Task ASaidPhraseSwitchesAndAnswersAsTheCommanderWouldHopeTo()
    {
        var (registry, picked) = Build();
        var router = new KeywordRouter(registry, Phrases);

        var match = router.MatchToolCommand("switch to Kestrel Vane");

        Assert.NotNull(match);

        var result = await registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Switched to CMDR Kestrel Vane. I'll stay with this commander until you switch again.", result.Content);
        Assert.Equal("F2", Assert.Single(picked).FrontierId);
    }

    [Fact]
    public async Task TheModelIsRefusedAndNothingSwitches()
    {
        var (registry, picked) = Build();

        var result = await registry.InvokeAsync(
            CommandersCapability.SwitchTool,
            new ToolArguments(new Dictionary<string, string> { ["frontier_id"] = "F2" }),
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.Empty(picked);
    }
}
