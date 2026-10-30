using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>"Set course for home": plots the flying Commander's home system.</summary>
public static class HomeCourse
{
    /// <summary>The word <c>plot_course</c> reads as the home system.</summary>
    public const string Word = "home";

    public const string NotSet =
        "You have not set a home system. Set one in Settings, Exploring, under Home system.";

    public static readonly string[] CourseSpellings =
    [
        "set course for home",
        "set course home",
        "set a course for home",
        "plot a course home",
        "plot a course to home",
        "take me home",
        "take us home",
        "navigate home",
        "route home",
        "plot a route home",
    ];

    public static bool IsHome(string? system) =>
        string.Equals(system?.Trim(), Word, StringComparison.OrdinalIgnoreCase);

    /// <summary>The commands, given the home system now; with none set they carry the word and <c>plot_course</c> says so.</summary>
    public static IEnumerable<DynamicCommand> Phrases(Func<string?> homeSystem)
    {
        var home = homeSystem();
        var system = string.IsNullOrWhiteSpace(home) ? Word : home.Trim();

        foreach (var phrase in CourseSpellings)
        {
            yield return new DynamicCommand(
                phrase,
                NavigationCapability.Id,
                "plot_course",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system });
        }
    }
}
