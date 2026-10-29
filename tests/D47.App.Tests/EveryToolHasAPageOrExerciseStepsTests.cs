using D47.App.Coverage;
using D47.Core.Capabilities;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every registered tool has a page on the panel or steps for exercising it by hand.</summary>
[Trait("Category", "Gate")]
public sealed class EveryToolHasAPageOrExerciseStepsTests
{
    private static readonly HashSet<string> WithNeither = new(StringComparer.Ordinal)
    {
        "get_capabilities",
        "find_phrase",
        "drill_capabilities",
        "get_app_status",
        "set_log_verbosity",
        "describe_crew",
        "find_mission_stations",
        "pause_story",
        "resume_story",
        "answer_story_ending",
        "refuse_story_beat",
        "change_story_objective",
        "recap_story",
        "mark_objective_done",
        "set_course_for_objective",
        "copy_objective_system",
        "choose_story_option",
        "change_adventure",
        "accept_adventure",
        "reject_adventure",
        "ask_for_adventure",
        "get_earlier_finds",
        "cancel_turn",
        "get_model_status",
        "state_identity",
        "describe_persona",
        "stop_speaking",
        "manage_music",
        "get_listening_status",
        "get_spelling_alphabet",
        "teach_phrase",
        "add_phrase",
        "forget_learned_phrase",
        "say_the_time",
        "set_timer",
        "set_alarm",
        "cancel_reminder",
        "get_memories",
        "remember_about_me",
        "forget_memory",
        "get_data_egress",
        "list_settings",
        "get_setting",
        "set_setting",
    };

    [Fact]
    public void EveryToolHasAPageOrExerciseStepsOrIsOnTheList()
    {
        var (_, _, _, registry, _) = TestSurface.CreateFull();
        var tools = registry.All.SelectMany(capability => capability.Descriptor.Tools).ToList();
        var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        bool Covered(ToolDefinition tool) => ToolPlaces.For(tool.Name) is not null || !string.IsNullOrWhiteSpace(tool.Exercise);

        var missing = tools.Where(tool => !Covered(tool) && !WithNeither.Contains(tool.Name)).Select(tool => tool.Name).ToList();
        var stale = tools.Where(tool => Covered(tool) && WithNeither.Contains(tool.Name)).Select(tool => tool.Name).ToList();
        var unknown = WithNeither.Where(name => !names.Contains(name)).ToList();

        Assert.True(missing.Count == 0, "No page and no Exercise, and not on the list: " + string.Join(", ", missing));
        Assert.True(stale.Count == 0, "Now covered, remove from the list: " + string.Join(", ", stale));
        Assert.True(unknown.Count == 0, "On the list but not registered: " + string.Join(", ", unknown));
    }
}
