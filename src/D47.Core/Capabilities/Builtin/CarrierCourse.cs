using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>"Set course for my carrier" (docs/plans/change-requests.md item 31).</summary>
public static class CarrierCourse
{
    /// <summary>The ways a Commander asks to be taken there.</summary>
    public static readonly string[] CourseSpellings =
    [
        "set course for my carrier",
        "set course to my carrier",
        "set a course for my carrier",
        "set course for my fleet carrier",
        "set course to my fleet carrier",
        "plot a course to my carrier",
        "plot a course to my fleet carrier",
        "plot me to my carrier",
        "take me to my carrier",
        "take me to my fleet carrier",
        "take us to my carrier",
        "navigate to my carrier",

        // "Route" is the generic word, and to a Commander it means the galaxy map (#405).
        "route to my carrier",
        "plot a route to my carrier",
    ];

    /// <summary>
    /// The ways a Commander asks for the neutron plot — the supercharge route the galaxy map cannot
    /// work out, which is the one thing the planner has that the map has not (#405).
    /// </summary>
    public static IEnumerable<string> NeutronSpellings()
    {
        string[] askings =
        [
            "neutron jump",
            "neutron jumps",
            "neutron route",
            "plot neutron jumps",
            "plot a neutron jump",
            "plot a neutron route",
        ];

        string[] carriers = ["to my carrier", "to my fleet carrier"];

        return
            from asking in askings
            from carrier in carriers
            select $"{asking} {carrier}";
    }

    /// <summary>The commands, given where the carrier is now.</summary>
    public static IEnumerable<DynamicCommand> Phrases(Func<CarrierState?> carrier)
    {
        if (carrier() is not { StarSystem: { Length: > 0 } system })
        {
            yield break;
        }

        foreach (var phrase in CourseSpellings)
        {
            yield return new DynamicCommand(
                phrase,
                NavigationCapability.Id,
                "plot_course",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["system"] = system });
        }

        foreach (var phrase in NeutronSpellings())
        {
            yield return new DynamicCommand(
                phrase,
                RouteCapability.Id,
                "plot_route",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["to"] = system });
        }
    }
}
