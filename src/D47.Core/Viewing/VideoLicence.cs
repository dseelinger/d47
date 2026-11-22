using System.Text.RegularExpressions;

namespace D47.Core.Viewing;

/// <summary>Reads a licence URL as the catalogue names it, and refuses any that is not public domain or Creative Commons.</summary>
public static partial class VideoLicence
{
    public const string PublicDomain = "public domain";

    public const string UsGovernmentWork = "US government work";

    /// <summary>
    /// "public domain" for a creativecommons.org public domain mark, CC0 or the retired public domain dedication,
    /// "CC BY-NC-SA 3.0" and the like for a creativecommons.org licence, and null for anything else.
    /// </summary>
    public static string? Label(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !string.Equals(uri.Host, "creativecommons.org", StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort)
        {
            return null;
        }

        var path = uri.AbsolutePath.ToLowerInvariant();

        if (path.StartsWith("/publicdomain/", StringComparison.Ordinal) || path.StartsWith("/licenses/publicdomain", StringComparison.Ordinal))
        {
            return PublicDomain;
        }

        return Licence().Match(path) is { Success: true } match
            ? $"CC {match.Groups["terms"].Value.ToUpperInvariant()}{(match.Groups["version"].Success ? " " + match.Groups["version"].Value : string.Empty)}"
            : null;
    }

    [GeneratedRegex(@"^/licenses/(?<terms>by(?:-nc)?(?:-sa|-nd)?)(?:/(?<version>\d+(?:\.\d+)?))?(?:/[a-z-]*)?/?$")]
    private static partial Regex Licence();
}
