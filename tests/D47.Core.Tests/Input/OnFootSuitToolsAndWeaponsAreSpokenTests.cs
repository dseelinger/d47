using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

public class OnFootSuitToolsAndWeaponsAreSpokenTests
{
    private static GameAction? Route(string phrase) =>
        GameActions.All.FirstOrDefault(action => action.Phrases.Any(p => string.Equals(p.Phrase, phrase, StringComparison.OrdinalIgnoreCase)));

    [Theory]
    [InlineData("shields", "suit_shields")]
    [InlineData("toggle shields", "suit_shields")]
    [InlineData("shields on", "suit_shields")]
    [InlineData("shields off", "suit_shields")]
    [InlineData("medkit", "medkit")]
    [InlineData("use a medkit", "medkit")]
    [InlineData("heal", "medkit")]
    [InlineData("energy cell", "energy_cell")]
    [InlineData("use an energy cell", "energy_cell")]
    [InlineData("energylink", "energylink")]
    [InlineData("energy link", "energylink")]
    [InlineData("profile analyser", "profile_analyser")]
    [InlineData("profile analyzer", "profile_analyser")]
    [InlineData("suit tool", "genetic_sampler")]
    [InlineData("genetic sampler", "genetic_sampler")]
    [InlineData("arc cutter", "genetic_sampler")]
    [InlineData("primary weapon", "primary_weapon")]
    [InlineData("secondary weapon", "secondary_weapon")]
    [InlineData("sidearm", "utility_weapon")]
    [InlineData("utility weapon", "utility_weapon")]
    [InlineData("holster", "holster")]
    [InlineData("holster weapon", "holster")]
    [InlineData("frag grenade", "frag_grenade")]
    [InlineData("emp grenade", "emp_grenade")]
    [InlineData("shield projector", "shield_grenade")]
    [InlineData("mission help", "mission_help")]
    public void EachPhraseReachesItsActionWithoutTheModel(string phrase, string id) =>
        Assert.Equal(id, Route(phrase)?.Id);

    [Fact]
    public void EveryOnFootActionWorksOnFootAndNowhereElse() =>
        Assert.All(
            GameActions.All.Where(action => action.Group == GameActions.OnFoot),
            action => Assert.Equal(ControlContext.OnFoot, action.Contexts));

    [Fact]
    public void TheOnFootToolListsTheSuitActions()
    {
        var tool = ActionCapabilities.All(ActionSurface.Inert).SelectMany(c => c.Tools).Single(t => t.Name == "control_on_foot");
        var allowed = tool.Parameters.Single(p => p.Name == "action").AllowedValues!;

        Assert.Equal(14, allowed.Count);
        Assert.Contains("suit_shields", allowed);
        Assert.Contains("mission_help", allowed);
    }

    [Fact]
    public async Task ShieldsOnAndOffBothPressTheToggleAndShipsRefuseThem()
    {
        var input = new RecordingGameInput();
        var binds = new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings = [new EliteBinding("HumanoidToggleShieldsButton", "Primary", "Keyboard", "Key_Z")],
        };

        var status = new GameStatus { Flags2 = (uint)StatusFlags2.OnFoot, ReadAt = DateTimeOffset.UnixEpoch };
        var surface = new ActionSurface { Binds = () => binds, Status = () => status, Input = input, Enabled = () => true };
        var registry = CapabilityRegistry.Build(ActionCapabilities.All(surface));
        var router = new KeywordRouter(registry);

        foreach (var phrase in new[] { "shields on", "shields off" })
        {
            input.Clear();
            var match = router.MatchToolCommand(phrase);
            Assert.NotNull(match);
            var result = await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
            Assert.False(result.IsError);
            Assert.NotEmpty(input.Steps);
        }

        status = new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch };
        input.Clear();
        var refused = router.MatchToolCommand("use a medkit");
        Assert.NotNull(refused);
        var refusal = await registry.InvokeAsync(refused.ToolName, refused.Arguments, TestContext.Current.CancellationToken);
        Assert.True(refusal.IsError);
        Assert.Empty(input.Steps);
    }
}
