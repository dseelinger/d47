namespace D47.App.Panel;

/// <summary>
/// A page that answers the panel's search by filtering itself, and then marking what it kept (Phase 12,
/// "Search whichever tab you are looking at").
/// </summary>
public interface IFilterablePage
{
    /// <summary>Shows only what matches.</summary>
    void Filter(string? query);

    /// <summary>
    /// Whether a query would do anything to this page as it is showing right now (remediation.md 11,
    /// item 6).
    /// </summary>
    bool Filters { get; }
}
