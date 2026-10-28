using D47.Core.Knowledge;

namespace D47.Core.Capabilities;

public enum PageKind
{
    Engineer,
    Ship,
    System,
    Search,
    CodexEntry,
    Plan,
}

/// <summary>What a tool's answer is about, for the panel to open; the App decides how it is drawn.</summary>
public sealed record PageRef(PageKind Kind, long Id, int? Region = null, string? Name = null)
{
    /// <summary>One engineer, by the id the journal writes.</summary>
    public static PageRef Engineer(int id) => new(PageKind.Engineer, id);

    /// <summary>One owned ship, by the journal's ship id.</summary>
    public static PageRef Ship(int shipId) => new(PageKind.Ship, shipId);

    /// <summary>One star system, by the journal's system address.</summary>
    public static PageRef System(long systemAddress) => new(PageKind.System, systemAddress);

    /// <summary>The search page of one kind, which draws that kind's last answer.</summary>
    public static PageRef Search(GalaxySearchKind kind) => new(PageKind.Search, (long)kind);

    /// <summary>One codex entry, by its id and region number; <paramref name="name"/> words its crumb.</summary>
    public static PageRef CodexEntry(long entryId, int? regionId, string name) => new(PageKind.CodexEntry, entryId, regionId, name);

    /// <summary>The stored plan of one kind; <paramref name="headline"/> words its crumb.</summary>
    public static PageRef Plan(RoutePlanKind kind, string headline) => new(PageKind.Plan, (long)kind, null, headline);
}
