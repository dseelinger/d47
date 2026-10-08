using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Input;

public class TargetingDefenceAndFighterOrdersAreSpokenTests
{
    private static string? Route(string phrase) =>
        GameActions.All
            .FirstOrDefault(action => action.Phrases.Any(p => string.Equals(p.Phrase, phrase, StringComparison.OrdinalIgnoreCase)))
            ?.Id;

    [Theory]
    [InlineData("target ahead", "select_target")]
    [InlineData("select target", "select_target")]
    [InlineData("next target", "next_target")]
    [InlineData("previous target", "previous_target")]
    [InlineData("next hostile", "next_hostile")]
    [InlineData("previous hostile", "previous_hostile")]
    [InlineData("highest threat", "highest_threat")]
    [InlineData("target the highest threat", "highest_threat")]
    [InlineData("next subsystem", "next_subsystem")]
    [InlineData("previous subsystem", "previous_subsystem")]
    [InlineData("target wingman one", "target_wingman_1")]
    [InlineData("target wingman two", "target_wingman_2")]
    [InlineData("target wingman three", "target_wingman_3")]
    [InlineData("target wingman's target", "wingman_target")]
    [InlineData("wingman nav lock", "wingman_nav_lock")]
    [InlineData("nav lock", "wingman_nav_lock")]
    [InlineData("fighter orders", "fighter_orders")]
    [InlineData("recall the fighter", "fighter_dock")]
    [InlineData("fighter dock", "fighter_dock")]
    [InlineData("fighter defend", "fighter_defend")]
    [InlineData("fighter defensive", "fighter_defend")]
    [InlineData("fighter engage at will", "fighter_engage")]
    [InlineData("fighter attack", "fighter_engage")]
    [InlineData("fighter attack my target", "fighter_focus")]
    [InlineData("fighter focus my target", "fighter_focus")]
    [InlineData("fighter hold fire", "fighter_hold_fire")]
    [InlineData("fighter hold position", "fighter_hold_position")]
    [InlineData("fighter follow me", "fighter_follow")]
    [InlineData("fighter form up", "fighter_follow")]
    [InlineData("chaff", "chaff")]
    [InlineData("deploy chaff", "chaff")]
    [InlineData("fire chaff", "chaff")]
    [InlineData("shield cell", "shield_cell")]
    [InlineData("use a shield cell", "shield_cell")]
    [InlineData("fire a shield cell", "shield_cell")]
    [InlineData("ecm", "ecm")]
    [InlineData("fire the ecm", "ecm")]
    [InlineData("charge the ecm", "ecm")]
    public void EachPhraseReachesItsActionWithoutTheModel(string phrase, string id) =>
        Assert.Equal(id, Route(phrase));

    [Fact]
    public void TheCombatToolListsTargetingAndFighterOrders()
    {
        var surface = ActionSurface.Inert;
        var tool = ActionCapabilities.All(surface).SelectMany(c => c.Tools).Single(t => t.Name == "control_combat");
        var allowed = tool.Parameters.Single(p => p.Name == "action").AllowedValues!;

        Assert.Contains("select_target", allowed);
        Assert.Contains("fighter_follow", allowed);
        Assert.DoesNotContain("chaff", allowed);
    }

    [Fact]
    public void TheSystemsToolListsTheCountermeasures()
    {
        var tool = ActionCapabilities.All(ActionSurface.Inert).SelectMany(c => c.Tools).Single(t => t.Name == "control_systems");
        var allowed = tool.Parameters.Single(p => p.Name == "action").AllowedValues!;

        Assert.Contains("chaff", allowed);
        Assert.Contains("shield_cell", allowed);
        Assert.Contains("ecm", allowed);
    }

    [Fact]
    public void OnlyTheEcmIsHeld() =>
        Assert.Equal(["ecm"], GameActions.All.Where(a => a.HoldFor is not null).Select(a => a.Id));

    [Theory]
    [InlineData("EjectAllCargo")]
    [InlineData("EjectAllCargo_Buggy")]
    public void CargoEjectionIsNotOffered(string binding) =>
        Assert.DoesNotContain(GameActions.All, a => a.Variants.Any(v => v.EliteAction == binding));

    [Fact]
    public async Task TheEcmHoldsItsKeyForFourSecondsAndAChaffTaps()
    {
        var input = new RecordingGameInput();
        var binds = new EliteBinds
        {
            PresetName = "Test",
            SourceFile = "Test.binds",
            Bindings =
            [
                new EliteBinding("ChargeECM", "Primary", "Keyboard", "Key_E"),
                new EliteBinding("FireChaffLauncher", "Primary", "Keyboard", "Key_C"),
            ],
        };

        var surface = new ActionSurface
        {
            Binds = () => binds,
            Status = () => new GameStatus { Flags = StatusFlags.InMainShip, ReadAt = DateTimeOffset.UnixEpoch },
            Input = input,
            Enabled = () => true,
        };

        var registry = CapabilityRegistry.Build(ActionCapabilities.All(surface));
        var router = new KeywordRouter(registry);

        async Task<TimeSpan> Held(string phrase)
        {
            input.Clear();
            var match = router.MatchToolCommand(phrase);
            Assert.NotNull(match);
            var result = await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);
            Assert.False(result.IsError);

            return input.Steps.Where(s => s.Kind == InputStepKind.Delay).Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Delay);
        }

        Assert.Equal(TimeSpan.FromSeconds(4), await Held("fire the ecm"));
        Assert.Equal(InputSequence.TapHold, await Held("fire chaff"));
    }
}
