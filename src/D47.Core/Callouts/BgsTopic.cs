using D47.Core.Campaigns;
using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>What a BGS callout line is about, which the topic rows in Settings switch.</summary>
public enum BgsTopic
{
    War,
    Election,
    Other,
}

public static class BgsTopics
{
    private static readonly string War = FactionStates.Display("War");

    private static readonly string CivilWar = FactionStates.Display("CivilWar");

    private static readonly string Election = FactionStates.Display("Election");

    /// <summary>The topic of a conflict of this type: war and civil war are wars, an election is an election, anything else is other.</summary>
    public static BgsTopic OfConflict(string? type)
    {
        if (type is null)
        {
            return BgsTopic.Other;
        }

        var display = FactionStates.Display(type);

        return display == War || display == CivilWar ? BgsTopic.War : display == Election ? BgsTopic.Election : BgsTopic.Other;
    }

    /// <summary>The topic of a front: that of its conflict, or other when it has none.</summary>
    public static BgsTopic Of(Front? front) => OfConflict(front?.Conflict?.Type);

    /// <summary>The topic of an arc line: war when its system has a war or civil war front, else election when the line is an election act or the system has an election front, else other.</summary>
    public static BgsTopic Of(CampaignArcLine line, IReadOnlyList<Front> fronts)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(fronts);

        var topics = fronts
            .Where(front => front.Kind == FrontKind.Conflict && string.Equals(front.System, line.System, StringComparison.OrdinalIgnoreCase))
            .Select(Of)
            .ToList();

        if (topics.Contains(BgsTopic.War))
        {
            return BgsTopic.War;
        }

        return line.Act == ArcAct.Election || topics.Contains(BgsTopic.Election) ? BgsTopic.Election : BgsTopic.Other;
    }
}
