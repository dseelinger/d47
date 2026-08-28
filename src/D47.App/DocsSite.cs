namespace D47.App;

/// <summary>
/// Where the long-form documentation lives. One constant, because the panel's help button and
/// every settings row's "?" point into the same site and a second copy of the address is a
/// second thing to be wrong after a rename.
/// </summary>
public static class DocsSite
{
    /// <summary>The site itself — what the panel's help button opens.</summary>
    public const string Root = "https://dseelinger.github.io/d47/";

    /// <summary>
    /// A capability's page, optionally at one row's anchor. In-app help is the short form and
    /// this is the long form (Phase 4, "Link each settings row to its documentation").
    /// </summary>
    public static string Capability(string capabilityId, string? anchor = null) =>
        $"{Root}capabilities/{capabilityId}.html{(anchor is null ? string.Empty : $"#{anchor}")}";

    /// <summary>
    /// Any help page's address, capability or general — the two live in different folders, and
    /// which one a page is lives in its id.
    /// <para>
    /// <b>The general pages were being addressed as capabilities</b>, so the long-form card at
    /// the foot of Overview, Installing and Talking to Directive 47 pointed at
    /// <c>capabilities/general-overview.html</c> and so on — a folder that does not hold them,
    /// under a name that is an embedding key rather than a filename. Three 404s, and the card
    /// that exists so the panel does not quietly hide the documentation.
    /// </para>
    /// </summary>
    public static string Page(string pageId) =>
        pageId.StartsWith(D47.Core.Help.HelpLibrary.GeneralPrefix, StringComparison.Ordinal)
            ? Root + pageId[D47.Core.Help.HelpLibrary.GeneralPrefix.Length..] + ".html"
            : Capability(pageId);
}
