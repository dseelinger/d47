using System.Text.Json;
using D47.Core.Updates;
using Microsoft.Extensions.Logging;

namespace D47.App.Updates;

/// <summary>A release newer than the one currently running.</summary>
public sealed record AvailableUpdate(string Version, string ReleaseUrl);

/// <summary>
/// Checks GitHub Releases for a build newer than the one currently running (list.md Phase 17,
/// "Check for Updates on start"). Any failure - offline, rate-limited, malformed response -
/// resolves to "no update" rather than an error: an optional check should never be why the
/// app looks broken to a Commander with no internet.
/// </summary>
public sealed class UpdateChecker
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/dseelinger/d47/releases/latest";

    /// <summary>
    /// The only URL shape the "an update is available" button will hand to the shell. Both the
    /// version and the link come out of a network response, and the link is opened with
    /// UseShellExecute - which resolves anything, not just http - so an API that is redirected,
    /// spoofed or simply wrong is otherwise one click away from starting an arbitrary program.
    /// Pinning the prefix bounds the worst case to "the wrong page of this repository".
    /// </summary>
    private const string ReleaseUrlPrefix = "https://github.com/dseelinger/d47/";

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

            return latest.IsNewerThan(current) ? new AvailableUpdate(latest.ToString(), url!) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogInformation(ex, "Update check failed; continuing without one");
            return null;
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

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        // Required by the GitHub API: requests with no User-Agent are rejected outright.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("d47-update-check");
        return client;
    }
}
