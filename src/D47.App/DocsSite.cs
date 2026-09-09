namespace D47.App;

/// <summary>Where the long-form documentation lives.</summary>
public static class DocsSite
{
    /// <summary>The site itself — what the panel's help button opens.</summary>
    public const string Root = "https://dseelinger.github.io/d47/";

    /// <summary>A capability's page, optionally at one row's anchor.</summary>
    public static string Capability(string capabilityId, string? anchor = null) =>
        $"{Root}capabilities/{capabilityId}.html{(anchor is null ? string.Empty : $"#{anchor}")}";

    /// <summary>
    /// Any help page's address, capability or general — the two live in different folders, and which
    /// one a page is lives in its id.
    /// </summary>
    public static string Page(string pageId) =>
        pageId.StartsWith(D47.Core.Help.HelpLibrary.GeneralPrefix, StringComparison.Ordinal)
            ? Root + pageId[D47.Core.Help.HelpLibrary.GeneralPrefix.Length..] + ".html"
            : Capability(pageId);
}
