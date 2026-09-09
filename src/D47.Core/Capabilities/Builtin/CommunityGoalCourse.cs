using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// "Set a course" (#325): pointing <c>plot_course</c> at the system the last nearest-first commodity
/// search actually found, the same shape <see cref="CarrierCourse"/> already uses for "set course for
/// my carrier".
/// </summary>
public static class CommunityGoalCourse
{
    public const string SetCourse = "set a course";

    /// <summary>The command, given what was last found.</summary>
    public static IEnumerable<DynamicCommand> Phrases(LastFoundSystem lastFound)
    {
        if (lastFound.System is not { Length: > 0 } system)
        {
            yield break;
        }

        yield return new DynamicCommand(
            SetCourse,
            NavigationCapability.Id,
            "plot_course",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system });
    }
}
