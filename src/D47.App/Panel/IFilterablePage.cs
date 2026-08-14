namespace D47.App.Panel;

/// <summary>
/// A page that answers the panel's search by filtering itself rather than by highlighting
/// (list.md Phase 12, "Search whichever tab you are looking at").
/// <para>
/// Settings is 92 rows across 14 sections, so highlighting in place is a scroll hunt with extra
/// colour — the query has to take rows away. That is a property of the page, so the page
/// implements it, and the panel stays a panel: it holds one query and one gesture and hands the
/// string to whatever is showing, rather than knowing what a settings row is.
/// </para>
/// </summary>
public interface IFilterablePage
{
    /// <summary>Shows only what matches. An empty or null query restores everything.</summary>
    void Filter(string? query);
}
