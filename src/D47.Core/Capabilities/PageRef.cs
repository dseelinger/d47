namespace D47.Core.Capabilities;

public enum PageKind
{
    Engineer,
    Ship,
    System,
}

/// <summary>What a tool's answer is about, for the panel to open; the App decides how it is drawn.</summary>
public sealed record PageRef(PageKind Kind, long Id)
{
    /// <summary>One engineer, by the id the journal writes.</summary>
    public static PageRef Engineer(int id) => new(PageKind.Engineer, id);

    /// <summary>One owned ship, by the journal's ship id.</summary>
    public static PageRef Ship(int shipId) => new(PageKind.Ship, shipId);

    /// <summary>One star system, by the journal's system address.</summary>
    public static PageRef System(long systemAddress) => new(PageKind.System, systemAddress);
}
