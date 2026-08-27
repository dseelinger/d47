using System.Text.Json;
using D47.Core.Updates;
using Microsoft.Extensions.Logging;

namespace D47.App.Updates;

/// <summary>
/// A release newer than the one currently running.
/// </summary>
/// <param name="Version">The tag, without its leading v.</param>
/// <param name="ReleaseUrl">The page, opened when installing in place is not possible.</param>
/// <param name="DownloadUrl">
/// The release archive — the executable and the native libraries that ship beside it. Null when
/// the release does not carry one under a URL that belongs to this repository, which downgrades
/// the offer to "open the page" rather than trusting it.
/// </param>
/// <param name="ChecksumUrl">
/// The published sha256 sidecar. Null on the same terms, and a download without one is refused
/// rather than installed unverified.
/// </param>
public sealed record AvailableUpdate(
    string Version,
    string ReleaseUrl,
    string? DownloadUrl = null,
    string? ChecksumUrl = null)
{
    /// <summary>Whether this can be installed in place, or only opened in a browser.</summary>
    public bool CanInstall => DownloadUrl is not null && ChecksumUrl is not null;
}

/// <summary>
/// Checks GitHub Releases for a build newer than the one currently running (list.md Phase 19,
/// "Check for Updates on start"). Any failure - offline, rate-limited, malformed response -
/// resolves to "no update" rather than an error: an optional check should never be why the
/// app looks broken to a Commander with no internet.
/// </summary>
public sealed class UpdateChecker
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/dseelinger/d47/releases/latest";

    /// <summary>
    /// Where one release is looked up by its tag. Pinned like every other URL in this file, and
    /// only ever completed with a tag d47 built from its own version.
    /// </summary>
    private const string TagReleaseUrlPrefix = "https://api.github.com/repos/dseelinger/d47/releases/tags/";

    /// <summary>
    /// The only URL shape the "an update is available" button will hand to the shell. Both the
    /// version and the link come out of a network response, and the link is opened with
    /// UseShellExecute - which resolves anything, not just http - so an API that is redirected,
    /// spoofed or simply wrong is otherwise one click away from starting an arbitrary program.
    /// Pinning the prefix bounds the worst case to "the wrong page of this repository".
    /// </summary>
    private const string ReleaseUrlPrefix = "https://github.com/dseelinger/d47/";

    /// <summary>
    /// Where a release asset legitimately lives. Held to the same standard as the page above and
    /// then some: this one names bytes that will be run as a program, so an asset offered from
    /// anywhere else is ignored rather than fetched.
    /// </summary>
    private const string DownloadUrlPrefix = "https://github.com/dseelinger/d47/releases/download/";

    /// <summary>
    /// The published archive, and the checksum published beside it. An archive rather than a
    /// bare exe since v0.5.14: Whisper's native libraries ship as loose files beside the
    /// executable — their loader probes the disk, not the single-file bundle — so a release is
    /// several files now, and updating only the exe would ship the bug class this replaced.
    /// Builds older than that see no <c>d47.exe</c> asset and fall back to the release page.
    /// </summary>
    private const string ArchiveAsset = "d47.zip";
    private const string ChecksumAsset = "d47.zip.sha256";

    private static readonly HttpClient Http = CreateClient();

    private readonly ILogger<UpdateChecker> _logger;

    public UpdateChecker(ILogger<UpdateChecker> logger)
    {
        _logger = logger;
    }

    public async Task<AvailableUpdate?> CheckAsync(string runningVersion, CancellationToken cancellationToken)
    {
        // Dev builds report "unknown" (AppHost.Start) or a non-tag Version; neither compares
        // to anything meaningfully.
        if (!ReleaseVersion.TryParse(runningVersion, out var current))
        {
            return null;
        }

        try
        {
            using var response = await Http.GetAsync(LatestReleaseUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Update check skipped: GitHub returned {Status}", response.StatusCode);
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var tag = document.RootElement.TryGetProperty("tag_name", out var tagProperty)
                ? tagProperty.GetString()
                : null;
            var url = document.RootElement.TryGetProperty("html_url", out var urlProperty)
                ? urlProperty.GetString()
                : null;

            if (!ReleaseVersion.TryParse(tag, out var latest))
            {
                return null;
            }

            if (!IsTrustedReleaseUrl(url))
            {
                // Fails closed: no prompt at all rather than a prompt that opens somewhere else.
                _logger.LogWarning("Update check ignored a release whose link was not a {Prefix} URL", ReleaseUrlPrefix);
                return null;
            }

            if (!latest.IsNewerThan(current))
            {
                return null;
            }

            return new AvailableUpdate(
                latest.ToString(),
                url!,
                AssetUrl(document.RootElement, ArchiveAsset),
                AssetUrl(document.RootElement, ChecksumAsset));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogInformation(ex, "Update check failed; continuing without one");
            return null;
        }
    }

    /// <summary>
    /// Whether GitHub calls this build's own Release a pre-release
    /// (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>).
    /// <para>
    /// <b>A second call rather than a cleverer first one.</b> <see cref="CheckAsync"/> asks
    /// <c>/releases/latest</c>, which by construction cannot answer this: that endpoint returns the
    /// newest release that is <em>not</em> a pre-release, which is precisely not the build asking.
    /// One request to <c>/releases</c> could serve both, and would be worth doing if the request
    /// budget ever mattered — GitHub allows sixty an hour unauthenticated and this makes it two per
    /// start — but it would mean rewriting the update path, which is pinned, tested and load-bearing,
    /// to add a badge.
    /// </para>
    /// <para>
    /// <b>The tag is d47's own and never the network's.</b> It is built from the running version,
    /// so nothing a response says can steer the URL — the concern the pinned prefixes above exist
    /// for, arriving here from the other direction.
    /// </para>
    /// <para>
    /// Every failure resolves to <see cref="ReleaseChannel.Unknown"/> rather than to
    /// <see cref="ReleaseChannel.Release"/>, on the same principle the update check follows: this
    /// is optional, and an optional check must never be why d47 tells a Commander something untrue.
    /// A build with no parseable version — a local one — never asks at all.
    /// </para>
    /// </summary>
    public async Task<ReleaseChannel> ChannelAsync(string runningVersion, CancellationToken cancellationToken)
    {
        if (!ReleaseVersion.TryParse(runningVersion, out var current))
        {
            return ReleaseChannel.Unknown;
        }

        try
        {
            var url = $"{TagReleaseUrlPrefix}v{current}";

            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // A 404 is the ordinary answer for a build that was never released - a local
                // build, or one from a branch. Not a fault, and not evidence of anything.
                _logger.LogInformation(
                    "No release channel for {Version}: GitHub returned {Status}", current, response.StatusCode);

                return ReleaseChannel.Unknown;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("prerelease", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return ReleaseChannel.Unknown;
            }

            return flag.GetBoolean() ? ReleaseChannel.PreRelease : ReleaseChannel.Release;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogInformation(ex, "Release channel check failed; showing no marker");
            return ReleaseChannel.Unknown;
        }
    }

    /// <summary>
    /// True only for a release page on this repository. The prefix carries the scheme, the host
    /// and the repository in one comparison - the authority ends at the first slash, so a
    /// lookalike host cannot satisfy it - and the parse rejects a response that is not a
    /// well-formed absolute URL at all.
    /// </summary>
    internal static bool IsTrustedReleaseUrl(string? url) =>
        url is not null
        && url.StartsWith(ReleaseUrlPrefix, StringComparison.Ordinal)
        && Uri.TryCreate(url, UriKind.Absolute, out _);

    /// <summary>
    /// The download URL of one named asset, or null if the release does not carry it under a
    /// URL on this repository. Null is a downgrade to the browser, never a reason to error.
    /// </summary>
    private static string? AssetUrl(JsonElement release, string name)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var assetName = asset.TryGetProperty("name", out var n) ? n.GetString() : null;

            if (!string.Equals(assetName, name, StringComparison.Ordinal))
            {
                continue;
            }

            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;

            return IsTrustedDownloadUrl(url) ? url : null;
        }

        return null;
    }

    /// <summary>True only for an asset published on a release of this repository.</summary>
    internal static bool IsTrustedDownloadUrl(string? url) =>
        url is not null
        && url.StartsWith(DownloadUrlPrefix, StringComparison.Ordinal)
        && Uri.TryCreate(url, UriKind.Absolute, out _);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        // Required by the GitHub API: requests with no User-Agent are rejected outright.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-update-check");
        return client;
    }
}
