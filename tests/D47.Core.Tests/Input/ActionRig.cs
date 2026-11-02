using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>The action capabilities over a recording input, with the given bindings and GUI focus.</summary>
public static class ActionRig
{
    public static (CapabilityRegistry Registry, KeywordRouter Router, RecordingGameInput Input) Build(
        EliteBinds binds, GuiFocus focus)
    {
        var input = new RecordingGameInput();
        var surface = new ActionSurface
        {
            Binds = () => binds,
            Status = () => new GameStatus
            {
                Flags = StatusFlags.InMainShip,
                GuiFocus = focus,
                ReadAt = DateTimeOffset.UnixEpoch,
            },
            Input = input,
            Enabled = () => true,
        };

        var registry = CapabilityRegistry.Build(ActionCapabilities.All(surface));

        return (registry, new KeywordRouter(registry), input);
    }

    public static async Task<ToolResult> Say(
        (CapabilityRegistry Registry, KeywordRouter Router, RecordingGameInput Input) rig, string phrase)
    {
        var match = rig.Router.MatchToolCommand(phrase);
        Assert.NotNull(match);

        return await rig.Registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
    }
}
