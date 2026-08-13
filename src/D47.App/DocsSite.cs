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
    /// this is the long form (list.md Phase 4, "Link each settings row to its documentation").
    /// </summary>
    public static string Capability(string capabilityId, string? anchor = null) =>
        $"{Root}capabilities/{capabilityId}.html{(anchor is null ? string.Empty : $"#{anchor}")}";
}
