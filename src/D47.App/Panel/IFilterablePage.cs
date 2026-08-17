namespace D47.App.Panel;

/// <summary>
/// A page that answers the panel's search by filtering itself, and then marking what it kept
/// (list.md Phase 12, "Search whichever tab you are looking at").
/// <para>
/// Settings is 92 rows across 14 sections, so highlighting <em>instead of</em> filtering is a
/// scroll hunt with extra colour — the query has to take rows away. It highlights as well,
/// which is a different claim: the filter cuts the haystack down and the highlight says why each
/// survivor is in it. Neither half answers on its own. A page of rows with nothing marked leaves
/// a Commander comparing the query against every word to work out what it caught.
/// </para>
/// <para>
/// Both are properties of the page, so the page implements them, and the panel stays a panel:
/// it holds one query and one gesture and hands the string to whatever is showing, rather than
/// knowing what a settings row is.
/// </para>
/// </summary>
public interface IFilterablePage
{
    /// <summary>Shows only what matches. An empty or null query restores everything.</summary>
    void Filter(string? query);
}
