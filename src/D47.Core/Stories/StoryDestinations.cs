using D47.Core.Checklists;
using D47.Core.Journal;

namespace D47.Core.Stories;

/// <summary>The systems a span in the black may travel toward, placed without a network call.</summary>
public static class StoryDestinations
{
    /// <summary>Within this many light years of a destination, the Commander has arrived.</summary>
    public const double Arrived = 1_000;

    private static readonly IReadOnlyDictionary<string, StarPosition> Positions = new Dictionary<string, StarPosition>(StringComparer.OrdinalIgnoreCase)
    {
        ["Sol"] = StarPosition.Origin,
        ["Colonia"] = ExpeditionKit.Colonia,
        ["Sagittarius A*"] = new(25.21875, -20.90625, 25899.96875),
        ["Beagle Point"] = new(-1111.5625, -134.21875, 65269.75),
    };

    public static IReadOnlyList<string> Names { get; } = [.. Positions.Keys];

    public static StarPosition? PositionOf(string name) =>
        name is not null && Positions.TryGetValue(name, out var at) ? at : null;
}
