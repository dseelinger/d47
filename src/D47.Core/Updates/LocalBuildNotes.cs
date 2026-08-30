using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace D47.Core.Updates;

/// <summary>
/// One issue a local build worked, as the build itself recorded it
/// (<a href="https://github.com/dseelinger/d47/issues/207">#207</a>).
/// </summary>
/// <param name="Number">The issue number, and the only field a link is ever built from.</param>
/// <param name="State">
/// <c>open</c>, <c>closed</c>, or <c>unknown</c> when GitHub could not be reached at publish time.
/// Lowercase as stamped; nothing here re-cases it, because a state d47 does not recognise should
/// read as itself rather than as one of the two it does.
/// </param>
/// <param name="Title">
/// The issue's own words, or null when they were withheld.
/// <para>
/// <b>Withheld is the ordinary shape rather than a failure.</b> A title is text somebody else
/// wrote, and <c>get-local.ps1</c> bakes one only for an issue the Commander wrote or vouched for
/// — the same <c>Resolve-Trust</c> that keeps a stranger's prose out of an agent's context. It is
/// also null when GitHub could not be asked. The two are not distinguished here on purpose: what
/// a reader needs to know is that the number is the whole of what d47 can vouch for.
/// </para>
/// </param>
/// <param name="Labels">What GitHub had on it. Facts GitHub assigns, never prose.</param>
public sealed record LocalBuildIssue(
    [property: JsonPropertyName("n")] int Number,
    [property: JsonPropertyName("s")] string State,
    [property: JsonPropertyName("t")] string? Title,
    [property: JsonPropertyName("l")] IReadOnlyList<string> Labels)
{
    /// <summary>Where this issue lives, built from the number and never from anything stamped.</summary>
    public string Url => $"https://github.com/{Repository}/issues/{Number}";

    /// <summary>The repository, as a hovercard names one.</summary>
    public const string Repository = "dseelinger/d47";

    /// <summary>How GitHub's own reference chip reads: <c>dseelinger/d47 #205</c>.</summary>
    public string Reference => $"{Repository} #{Number}";
}

/// <summary>
/// What a local build says it worked, read back out of the binary
/// (<a href="https://github.com/dseelinger/d47/issues/207">#207</a>).
/// <para>
/// <b>Baked rather than fetched, and that is the whole design.</b> Nothing in a running d47 can
/// discover which issues a working tree closed — the answer exists only in the git log, at publish
/// time. So <c>get-local.ps1</c> reads the commits since the newest tag for what they say they
/// close, asks GitHub for each one's state and labels, and stamps the lot into an
/// <c>AssemblyMetadata</c> attribute. A published release never carries one, so the whole feature
/// is absent from a real build by construction rather than by a run-time check.
/// </para>
/// <para>
/// <b>It sees only what a commit wrote down.</b> Work still in the tree, or committed without a
/// <c>Fixes #N</c>, is invisible to this — which is why <see cref="Caveat"/> exists and why the
/// window that shows a list also shows that sentence. An empty list that read as "nothing was
/// done" would be the one wrong answer this can give.
/// </para>
/// <para>
/// In Core, and parsed here rather than in the App, so a malformed stamp is something a test can
/// hand it without a window (architecture.md §8).
/// </para>
/// </summary>
public static class LocalBuildNotes
{
    /// <summary>The attribute the build stamps. One spelling, read by one place.</summary>
    public const string MetadataKey = "LocalBuildIssues";

    /// <summary>
    /// What a reader has to be told alongside the list, whether or not it is empty. Here rather
    /// than in the window's own text because it is a property of how the list is gathered.
    /// </summary>
    public const string Caveat =
        "Only what a commit wrote down. Work still in the working tree, or committed without a "
        + "\"Fixes #N\", does not appear here.";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// The stamped list, or empty for a build that carries none.
    /// <para>
    /// <b>Never throws.</b> This is chrome on a build for testing, and a badge that took the app
    /// down because a publish wrote a value it could not read would be worse than a badge that is
    /// not there. Anything unreadable is an empty list, which the window already knows how to say
    /// something true about.
    /// </para>
    /// </summary>
    public static IReadOnlyList<LocalBuildIssue> Parse(string? stamped)
    {
        if (string.IsNullOrWhiteSpace(stamped))
        {
            return [];
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(stamped.Trim()));

            return JsonSerializer.Deserialize<LocalBuildIssue[]>(json, Options) is { } issues
                ? [.. issues.Where(issue => issue is { Number: > 0 }).Select(Sane)]
                : [];
        }
        catch (Exception ex) when (ex is FormatException or JsonException or DecoderFallbackException)
        {
            return [];
        }
    }

    /// <summary>
    /// One issue with its holes filled. A stamp written by an older <c>get-local</c>, or by one
    /// that could not reach GitHub, is a real shape rather than a broken one — so a missing state
    /// reads as unknown and a missing label list as none, and nothing downstream has to be
    /// null-aware about a record it did not write.
    /// </summary>
    private static LocalBuildIssue Sane(LocalBuildIssue issue) => issue with
    {
        State = string.IsNullOrWhiteSpace(issue.State) ? "unknown" : issue.State.Trim(),
        Title = string.IsNullOrWhiteSpace(issue.Title) ? null : issue.Title.Trim(),
        Labels = issue.Labels ?? [],
    };
}
