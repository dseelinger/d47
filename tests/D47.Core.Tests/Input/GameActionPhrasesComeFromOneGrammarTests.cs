using D47.Core.Actions;
using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Input;
using Xunit;

namespace D47.Core.Tests.Input;

/// <summary>Game action phrases are generated from names, a shape and verbs, and still route model-free (#934).</summary>
public class GameActionPhrasesComeFromOneGrammarTests
{
    /// <summary>Every phrase the hand-written lists declared before #934, with the action and state it reached.</summary>
    public static TheoryData<string, string, string> PhrasesDeclaredBefore => new()
    {
        { "gear down", "landing_gear", "on" },
        { "lower the gear", "landing_gear", "on" },
        { "put the gear down", "landing_gear", "on" },
        { "gear up", "landing_gear", "off" },
        { "raise the gear", "landing_gear", "off" },
        { "retract the gear", "landing_gear", "off" },
        { "landing gear", "landing_gear", "toggle" },
        { "lights on", "lights", "on" },
        { "lights off", "lights", "off" },
        { "ship lights", "lights", "toggle" },
        { "night vision", "night_vision", "toggle" },
        { "night vision on", "night_vision", "on" },
        { "night vision off", "night_vision", "off" },
        { "open the cargo scoop", "cargo_scoop", "on" },
        { "close the cargo scoop", "cargo_scoop", "off" },
        { "cargo scoop", "cargo_scoop", "toggle" },
        { "deploy hardpoints", "hardpoints", "on" },
        { "hardpoints out", "hardpoints", "on" },
        { "retract hardpoints", "hardpoints", "off" },
        { "hardpoints in", "hardpoints", "off" },
        { "toggle hardpoints", "hardpoints", "toggle" },
        { "frame shift drive", "frame_shift_drive", "toggle" },
        { "engage the frame shift drive", "frame_shift_drive", "toggle" },
        { "supercruise", "supercruise", "toggle" },
        { "engage supercruise", "supercruise", "toggle" },
        { "take us to supercruise", "supercruise", "toggle" },
        { "engage", "hyperspace", "toggle" },
        { "engage hyperspace", "hyperspace", "toggle" },
        { "hyperspace", "hyperspace", "toggle" },
        { "hyperspace jump", "hyperspace", "toggle" },
        { "jump to the next system", "hyperspace", "toggle" },
        { "flight assist on", "flight_assist", "on" },
        { "flight assist off", "flight_assist", "off" },
        { "toggle flight assist", "flight_assist", "toggle" },
        { "all stop", "throttle_zero", "toggle" },
        { "throttle to zero", "throttle_zero", "toggle" },
        { "throttle to twenty-five", "throttle_25", "toggle" },
        { "twenty-five per cent", "throttle_25", "toggle" },
        { "throttle to fifty", "throttle_50", "toggle" },
        { "fifty per cent", "throttle_50", "toggle" },
        { "military thrust", "throttle_75", "toggle" },
        { "military power", "throttle_75", "toggle" },
        { "throttle to seventy-five", "throttle_75", "toggle" },
        { "seventy-five per cent", "throttle_75", "toggle" },
        { "engage boost", "boost", "toggle" },
        { "boost us", "boost", "toggle" },
        { "next system", "target_next_route_system", "toggle" },
        { "target the next system", "target_next_route_system", "toggle" },
        { "target the next system in route", "target_next_route_system", "toggle" },
        { "pips to engines", "power_to_engines", "toggle" },
        { "power to engines", "power_to_engines", "toggle" },
        { "pips to weapons", "power_to_weapons", "toggle" },
        { "power to weapons", "power_to_weapons", "toggle" },
        { "pips to systems", "power_to_systems", "toggle" },
        { "power to systems", "power_to_systems", "toggle" },
        { "balance the power", "balance_power", "toggle" },
        { "balance power", "balance_power", "toggle" },
        { "silent running on", "silent_running", "on" },
        { "silent running off", "silent_running", "off" },
        { "silent running", "silent_running", "toggle" },
        { "heat sink", "heat_sink", "toggle" },
        { "drop a heat sink", "heat_sink", "toggle" },
        { "analysis mode", "analysis_mode", "on" },
        { "combat mode", "analysis_mode", "off" },
        { "switch hud mode", "analysis_mode", "toggle" },
        { "next fire group", "next_fire_group", "toggle" },
        { "previous fire group", "previous_fire_group", "toggle" },
        { "turret on", "srv_turret", "on" },
        { "turret off", "srv_turret", "off" },
        { "the turret", "srv_turret", "toggle" },
        { "handbrake on", "srv_handbrake", "on" },
        { "handbrake off", "srv_handbrake", "off" },
        { "the handbrake", "srv_handbrake", "toggle" },
        { "drive assist on", "srv_drive_assist", "on" },
        { "drive assist off", "srv_drive_assist", "off" },
        { "drive assist", "srv_drive_assist", "toggle" },
        { "reverse the srv", "srv_reverse", "toggle" },
        { "recall my ship", "recall_ship", "toggle" },
        { "recall the ship", "recall_ship", "toggle" },
        { "dismiss my ship", "recall_ship", "toggle" },
        { "dismiss the ship", "recall_ship", "toggle" },
        { "left panel", "left_panel", "toggle" },
        { "open the left panel", "left_panel", "toggle" },
        { "right panel", "right_panel", "toggle" },
        { "open the right panel", "right_panel", "toggle" },
        { "comms panel", "comms_panel", "toggle" },
        { "open the comms panel", "comms_panel", "toggle" },
        { "role panel", "role_panel", "toggle" },
        { "open the role panel", "role_panel", "toggle" },
        { "next panel", "next_panel", "toggle" },
        { "previous panel", "previous_panel", "toggle" },
        { "galaxy map", "galaxy_map", "toggle" },
        { "open the galaxy map", "galaxy_map", "toggle" },
        { "system map", "system_map", "toggle" },
        { "open the system map", "system_map", "toggle" },
        { "up", "ui_up", "toggle" },
        { "down", "ui_down", "toggle" },
        { "left", "ui_left", "toggle" },
        { "right", "ui_right", "toggle" },
        { "select", "ui_select", "toggle" },
        { "back", "ui_back", "toggle" },
    };

    [Theory]
    [MemberData(nameof(PhrasesDeclaredBefore))]
    public void APhraseThatRoutedBeforeStillReachesTheSameActionAndState(string phrase, string action, string state) =>
        AssertRoutes(phrase, action, state);

    [Theory]
    [InlineData("raise the landing gear", "landing_gear", "off")]
    [InlineData("retract the landing gear", "landing_gear", "off")]
    [InlineData("toggle landing gear", "landing_gear", "toggle")]
    [InlineData("turn the lights off", "lights", "off")]
    [InlineData("deploy the cargo scoop", "cargo_scoop", "on")]
    [InlineData("scoop away", "cargo_scoop", "off")]
    [InlineData("boost", "boost", "toggle")]
    [InlineData("jump", "hyperspace", "toggle")]
    [InlineData("warp", "supercruise", "toggle")]
    [InlineData("flight assist", "flight_assist", "toggle")]
    [InlineData("analysis mode", "analysis_mode", "on")]
    [InlineData("switch to combat mode", "analysis_mode", "off")]
    [InlineData("turret view", "srv_turret", "toggle")]
    public void AWordingTheListsMissedReachesTheActionWithNoModel(string phrase, string action, string state) =>
        AssertRoutes(phrase, action, state);

    [Fact]
    public void EverySwitchAnswersEveryForm()
    {
        var switches = GameActions.All.Where(action => action.Shape == PhraseShape.Switch).ToList();

        Assert.NotEmpty(switches);

        foreach (var action in switches)
        {
            Assert.True(action.Reports is not null, $"{action.Id} is a switch with no state Elite reports.");

            var phrases = action.Phrases.ToHashSet();

            foreach (var name in action.Names)
            {
                List<(string, DesiredState)> expected =
                [
                    (name, DesiredState.Toggle),
                    ($"toggle {name}", DesiredState.Toggle),
                ];

                foreach (var (state, word, verbs, particle) in new[]
                {
                    (DesiredState.On, "on", action.OnVerbs, action.OnParticle),
                    (DesiredState.Off, "off", action.OffVerbs, action.OffParticle),
                })
                {
                    expected.Add(($"{name} {word}", state));
                    expected.Add(($"turn {word} {name}", state));
                    expected.Add(($"turn {name} {word}", state));
                    expected.Add(($"switch {word} {name}", state));
                    expected.Add(($"switch {name} {word}", state));
                    expected.AddRange(verbs.Select(verb => ($"{verb} {name}", state)));

                    if (particle is not null)
                    {
                        expected.Add(($"{name} {particle}", state));
                    }
                }

                foreach (var form in expected)
                {
                    Assert.True(phrases.Contains(form), $"{action.Id} is missing '{form.Item1}' ({form.Item2}).");
                }
            }
        }
    }

    [Theory]
    [InlineData("landing_gear", DesiredState.On, "Aye, gear down.")]
    [InlineData("landing_gear", DesiredState.Off, "Aye, gear up.")]
    [InlineData("cargo_scoop", DesiredState.Off, "Aye, cargo scoop away.")]
    [InlineData("hardpoints", DesiredState.On, "Aye, hardpoints out.")]
    [InlineData("lights", DesiredState.Off, "Aye, lights off.")]
    [InlineData("flight_assist", DesiredState.On, "Aye, flight assist on.")]
    [InlineData("analysis_mode", DesiredState.On, "Aye, analysis mode.")]
    [InlineData("analysis_mode", DesiredState.Off, "Aye, combat mode.")]
    [InlineData("heat_sink", DesiredState.Toggle, "Aye, heat sink.")]
    public void TheAcknowledgementSaysTheShortForm(string id, DesiredState state, string said) =>
        Assert.Contains(said, Acknowledgements.Forms(GameActions.Find(id)!, state));

    [Fact]
    public void NoAcknowledgementOpensWithAVerb()
    {
        var verbs = GameActions.All
            .SelectMany(action => action.OnVerbs.Concat(action.OffVerbs).Concat(action.Verbs))
            .Append("turn").Append("toggle").Append("put")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var action in GameActions.All.Where(action => action.Shape != PhraseShape.Key))
        {
            foreach (var state in Enum.GetValues<DesiredState>())
            {
                foreach (var form in Acknowledgements.Forms(action, state).Where(form => form.StartsWith("Aye, ", StringComparison.Ordinal)))
                {
                    var first = form["Aye, ".Length..].Split(' ')[0].TrimEnd('.');

                    Assert.False(verbs.Contains(first), $"{action.Id} {state} is acknowledged as '{form}'.");
                }
            }
        }
    }

    private static void AssertRoutes(string phrase, string action, string state)
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        // TurnLoop's order: a setting command is tried before a tool command.
        Assert.Null(router.MatchSetting(phrase));

        var match = router.MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.True(match.Arguments.TryGetString("action", out var reached), $"'{phrase}' reached {match.ToolName} with no action.");
        Assert.Equal(action, reached);
        Assert.True(match.Arguments.TryGetString("state", out var asked));
        Assert.Equal(state, asked);
    }
}
