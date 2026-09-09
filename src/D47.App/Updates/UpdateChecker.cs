using System.Text.Json;
using D47.Core.Updates;
using Microsoft.Extensions.Logging;

namespace D47.App.Updates;

/// <summary>A release newer than the one currently running.</summary>
/// <param name="Version">The tag, without its leading v.</param>
/// <param name="ReleaseUrl">The page, opened when installing in place is not possible.</param>
/// <param name="DownloadUrl">
/// The release archive — the executable and the native libraries that ship beside it.
/// </param>
/// <param name="ChecksumUrl">The published sha256 sidecar.</param>
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
/// Checks GitHub Releases for a build newer than the one currently running (Phase 19, "Check for
/// Updates on start").
/// </summary>
public sealed class UpdateChecker
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/dseelinger/d47/releases/latest";

    /// <summary>Where one release is looked up by its tag.</summary>
    private const string TagReleaseUrlPrefix = "https://api.github.com/repos/dseelinger/d47/releases/tags/";

    /// <summary>The only URL shape the "an update is available" button will hand to the shell.</summary>
    private const string ReleaseUrlPrefix = "https://github.com/dseelinger/d47/";

    /// <summary>Where a release asset legitimately lives.</summary>
    private const string DownloadUrlPrefix = "https://github.com/dseelinger/d47/releases/download/";

    /// <summary>The published archive, and the checksum published beside it.</summary>
    internal const string ArchiveAsset = "d47.zip";

    /// <inheritdoc cref="ArchiveAsset"/>
    internal const string ChecksumAsset = "d47.zip.sha256";

    private static readonly HttpClient Http = CreateClient();

    private readonly ILogger<UpdateChecker> _logger;

    public UpdateChecker(ILogger<UpdateChecker> logger)
    {
        _logger = logger;
    }

    public async Task<AvailableUpdate?> CheckAsync(string runningVersion, CancellationToken cancellationToken)
    {
        // Dev builds report "unknown" (AppHost.Start) or a non-tag Version; neither compares to anything
        // meaningfully.
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

    /// <summary>Whether GitHub calls this build's own Release a pre-release (#92).</summary>
    public async Task<ReleaseChannel> ChannelAsync(string runningVersion, CancellationToken cancellationToken)
    {
        var channel = await ResolveChannelAsync(runningVersion, cancellationToken).ConfigureAwait(false);

        // One line per call, whatever the answer: a silent log must not mean two different things.
        _logger.LogInformation("Release channel for {Version}: {Channel}", runningVersion, channel);

        return channel;
    }

    /// <inheritdoc cref="ChannelAsync"/>
    private async Task<ReleaseChannel> ResolveChannelAsync(string runningVersion, CancellationToken cancellationToken)
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
                // A 404 is the ordinary answer for a build that was never released - a local build, or one
                // from a branch.
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
                _logger.LogInformation("The release for {Version} carries no prerelease flag", current);
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

    /// <summary>True only for a release page on this repository.</summary>
    internal static bool IsTrustedReleaseUrl(string? url) =>
        url is not null
        && url.StartsWith(ReleaseUrlPrefix, StringComparison.Ordinal)
        && Uri.TryCreate(url, UriKind.Absolute, out _);

    /// <summary>
    /// The download URL of one named asset, or null if the release does not carry it under a URL on
    /// this repository.
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
