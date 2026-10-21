using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using D47.Core.Interface;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>What a publish or unpublish ended with; <see cref="Failure"/> is null when it landed.</summary>
public sealed record CodexPublishResult(CodexPictureKey Key, string? Failure)
{
    public bool Landed => Failure is null;
}

/// <summary>
/// Publishes the Commander's codex picture to the stock pictures, or takes a stock picture off them, through
/// GitHub's REST API with the maintainer's token. One runs at a time, on the pool.
/// </summary>
public sealed class CodexPicturePublisher
{
    public const int MostAssetsPerRelease = 1000;

    public const string PublishingText = "Publishing…";

    public const string UnpublishingText = "Unpublishing…";

    public const string CannotWriteText = "The token cannot write to " + CodexPictureIndex.Repository + ".";

    private const string Api = "https://api.github.com/repos/" + CodexPictureIndex.Repository;

    private const string Uploads = "https://uploads.github.com/repos/" + CodexPictureIndex.Repository;

    private const string NextIndexName = "index.next.json";

    private const int PageSize = 100;

    private readonly CodexPictureStore _store;
    private readonly Func<string?> _token;
    private readonly Func<bool> _tokenPresent;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private int _running;

    /// <param name="token">Read once at each publish and never kept.</param>
    public CodexPicturePublisher(CodexPictureStore store, Func<string?> token, Func<bool> tokenPresent, ILogger logger, HttpMessageHandler? handler = null)
    {
        _store = store;
        _token = token;
        _tokenPresent = tokenPresent;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    /// <summary>Raised on any thread when a publish or unpublish starts or ends.</summary>
    public event Action? Changed;

    public bool HasToken => _tokenPresent();

    /// <summary>What the page says while one runs; null when none is running.</summary>
    public string? Doing { get; private set; }

    /// <summary>The last publish or unpublish that ended; null before the first.</summary>
    public CodexPublishResult? Last { get; private set; }

    /// <summary>Uploads the Commander's picture of <paramref name="key"/> and lists it in the index; null when one is already running.</summary>
    public Task<CodexPublishResult>? Publish(CodexPictureKey key) => Start(key, PublishingText, token => PublishAsync(key, token));

    /// <summary>Takes the stock picture of <paramref name="key"/> off the index and the release; null when one is already running.</summary>
    public Task<CodexPublishResult>? Unpublish(CodexPictureKey key) => Start(key, UnpublishingText, token => UnpublishAsync(key, token));

    private Task<CodexPublishResult>? Start(CodexPictureKey key, string doing, Func<string, Task> work)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return null;
        }

        Doing = doing;
        Raise();

        return Task.Run(async () =>
        {
            CodexPublishResult result;

            try
            {
                if (_token() is not { } token)
                {
                    throw new PublishFailure("No codex publish token is stored.");
                }

                await work(token).ConfigureAwait(false);
                result = new CodexPublishResult(key, null);
            }
            catch (PublishFailure ex)
            {
                _logger.LogInformation("Codex picture {Entry}-{Region}: {Failure}", key.EntryId, key.RegionId, ex.Message);
                result = new CodexPublishResult(key, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Codex picture {Entry}-{Region} stopped on {Error}", key.EntryId, key.RegionId, ex.GetType().Name);
                result = new CodexPublishResult(key, $"It stopped on an error ({ex.GetType().Name}).");
            }

            Last = result;
            Doing = null;
            Volatile.Write(ref _running, 0);
            Raise();

            return result;
        });
    }

    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redrawing after a codex publish changed failed");
        }
    }

    private async Task PublishAsync(CodexPictureKey key, string token)
    {
        var picture = _store.Local(key) ?? throw new PublishFailure("Your picture could not be read.");

        if (picture.Length > CodexPictureIndex.MaxBytes)
        {
            throw new PublishFailure($"Your picture is larger than {CodexPictureIndex.MaxBytes / 1024 / 1024} MB.");
        }

        await _store.IndexFetched.ConfigureAwait(false);

        var releases = await ReleasesAsync(token).ConfigureAwait(false);
        var first = releases.FirstOrDefault(release => release.Number == 1);
        var (rows, indexAsset) = first is null ? ([], null) : await IndexAsync(first, token).ConfigureAwait(false);

        var target = await TargetAsync(releases, token).ConfigureAwait(false);
        first ??= target;

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(picture));
        var row = new CodexPictureRow(
            key.EntryId, key.RegionId, target.Tag, CodexPictureIndex.AssetName(key.EntryId, key.RegionId, sha256), sha256, picture.Length);
        var old = rows.FirstOrDefault(existing => existing.Key == key);

        if (!target.Assets.Any(asset => asset.Name == row.Asset))
        {
            await UploadAsync(target.Id, row.Asset, "image/jpeg", picture, "Uploading the picture", token).ConfigureAwait(false);
        }

        var updated = CodexPictureIndex.Upsert(rows, row);
        var index = CodexPictureIndex.Write(updated);

        await ReplaceIndexAsync(first, indexAsset, index, token).ConfigureAwait(false);

        if (old is not null && (old.Tag != row.Tag || old.Asset != row.Asset))
        {
            await DeleteAssetAsync(old, releases, token).ConfigureAwait(false);
        }

        Keep(() => _store.Published(row, picture, index, updated));
    }

    private async Task UnpublishAsync(CodexPictureKey key, string token)
    {
        await _store.IndexFetched.ConfigureAwait(false);

        var releases = await ReleasesAsync(token).ConfigureAwait(false);
        var first = releases.FirstOrDefault(release => release.Number == 1)
                    ?? throw new PublishFailure("No stock picture is published for this entry and region.");
        var (rows, indexAsset) = await IndexAsync(first, token).ConfigureAwait(false);
        var row = rows.FirstOrDefault(existing => existing.Key == key)
                  ?? throw new PublishFailure("No stock picture is published for this entry and region.");

        var updated = CodexPictureIndex.Remove(rows, key);
        var index = CodexPictureIndex.Write(updated);

        await ReplaceIndexAsync(first, indexAsset, index, token).ConfigureAwait(false);
        await DeleteAssetAsync(row, releases, token).ConfigureAwait(false);

        Keep(() => _store.Unpublished(row, index, updated));
    }

    private static void Keep(Action write)
    {
        try
        {
            write();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PublishFailure($"GitHub has the change, but this PC could not keep it ({ex.GetType().Name}).");
        }
    }

    /// <summary>Every <c>codex-pictures-N</c> release, probing N from 1 until one is missing.</summary>
    private async Task<List<Release>> ReleasesAsync(string token)
    {
        var releases = new List<Release>();

        for (var number = 1; ; number++)
        {
            var tag = Tag(number);
            var (status, body) = await SendAsync(HttpMethod.Get, $"{Api}/releases/tags/{tag}", token, "Finding the release").ConfigureAwait(false);

            if (status == HttpStatusCode.NotFound)
            {
                return releases;
            }

            var id = ReadId(body, "Finding the release");
            releases.Add(new Release(number, tag, id, await AssetsAsync(id, token).ConfigureAwait(false)));
        }
    }

    /// <summary>The highest-numbered release with room for a picture, or the next one, created.</summary>
    private async Task<Release> TargetAsync(List<Release> releases, string token)
    {
        if (releases.Where(release => release.Assets.Count < MostAssetsPerRelease).MaxBy(release => release.Number) is { } room)
        {
            return room;
        }

        var number = releases.Count == 0 ? 1 : releases.Max(release => release.Number) + 1;
        var tag = Tag(number);
        var request = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["tag_name"] = tag,
            ["name"] = string.Create(CultureInfo.InvariantCulture, $"Codex pictures {number}"),
            ["body"] = "Stock codex pictures downloaded by d47. Not an app version.",
            ["make_latest"] = "false",
        });

        var (_, body) = await SendAsync(
                HttpMethod.Post,
                $"{Api}/releases",
                token,
                "Creating the release",
                new StringContent(request, Encoding.UTF8, "application/json"))
            .ConfigureAwait(false);

        var created = new Release(number, tag, ReadId(body, "Creating the release"), []);
        releases.Add(created);

        return created;
    }

    /// <summary>The rows of <c>index.json</c> on <paramref name="first"/>, and that asset; none when it holds no picture yet.</summary>
    private async Task<(IReadOnlyList<CodexPictureRow> Rows, Asset? Index)> IndexAsync(Release first, string token)
    {
        if (first.Assets.FirstOrDefault(asset => asset.Name == CodexPictureIndex.IndexName) is not { } asset)
        {
            return first.Assets.Any(held => held.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                ? throw new PublishFailure($"{CodexPictureIndex.FirstTag} holds pictures but no {CodexPictureIndex.IndexName}, so writing a new one would drop them.")
                : ([], null);
        }

        var (_, body) = await SendAsync(
                HttpMethod.Get, $"{Api}/releases/assets/{asset.Id}", token, "Reading the index", accept: "application/octet-stream")
            .ConfigureAwait(false);
        var parsed = CodexPictureIndex.Parse(Encoding.UTF8.GetString(body));

        if (parsed.Reasons.Count > 0)
        {
            throw new PublishFailure($"The published index could not be read, so it was left as it is. {parsed.Reasons[0]}");
        }

        return (parsed.Rows, asset);
    }

    /// <summary>Uploads the new index under a second name, deletes the old one, then renames the new one.</summary>
    private async Task ReplaceIndexAsync(Release first, Asset? old, string index, string token)
    {
        const string step = "Replacing the index";

        foreach (var leftover in first.Assets.Where(asset => asset.Name == NextIndexName))
        {
            await SendAsync(HttpMethod.Delete, $"{Api}/releases/assets/{leftover.Id}", token, step).ConfigureAwait(false);
        }

        var uploaded = await UploadAsync(first.Id, NextIndexName, "application/json", Encoding.UTF8.GetBytes(index), step, token)
            .ConfigureAwait(false);

        if (old is not null)
        {
            await SendAsync(HttpMethod.Delete, $"{Api}/releases/assets/{old.Id}", token, step).ConfigureAwait(false);
        }

        var rename = JsonSerializer.Serialize(new Dictionary<string, string> { ["name"] = CodexPictureIndex.IndexName });
        await SendAsync(
                HttpMethod.Patch,
                $"{Api}/releases/assets/{uploaded}",
                token,
                step,
                new StringContent(rename, Encoding.UTF8, "application/json"))
            .ConfigureAwait(false);
    }

    /// <summary>Deletes the asset <paramref name="row"/> names from its release; nothing when it is already gone.</summary>
    private async Task DeleteAssetAsync(CodexPictureRow row, List<Release> releases, string token)
    {
        var asset = releases
            .FirstOrDefault(release => release.Tag == row.Tag)?
            .Assets.FirstOrDefault(held => held.Name == row.Asset);

        if (asset is not null)
        {
            await SendAsync(HttpMethod.Delete, $"{Api}/releases/assets/{asset.Id}", token, "Deleting the old picture").ConfigureAwait(false);
        }
    }

    private async Task<long> UploadAsync(long releaseId, string name, string contentType, byte[] bytes, string step, string token)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var (_, body) = await SendAsync(
                HttpMethod.Post, $"{Uploads}/releases/{releaseId}/assets?name={Uri.EscapeDataString(name)}", token, step, content)
            .ConfigureAwait(false);

        return ReadId(body, step);
    }

    private async Task<IReadOnlyList<Asset>> AssetsAsync(long releaseId, string token)
    {
        var assets = new List<Asset>();

        for (var page = 1; ; page++)
        {
            var (_, body) = await SendAsync(
                    HttpMethod.Get,
                    string.Create(CultureInfo.InvariantCulture, $"{Api}/releases/{releaseId}/assets?per_page={PageSize}&page={page}"),
                    token,
                    "Finding the release")
                .ConfigureAwait(false);

            var listed = ReadAssets(body);
            assets.AddRange(listed);

            if (listed.Count < PageSize)
            {
                return assets;
            }
        }
    }

    /// <summary>
    /// Sends one request with the token in the Authorization header only. A 404 on a GET is returned; any other
    /// failure throws with GitHub's message, never the raw body.
    /// </summary>
    private async Task<(HttpStatusCode Status, byte[] Body)> SendAsync(
        HttpMethod method, string url, string token, string step, HttpContent? content = null, string accept = "application/vnd.github+json")
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd(accept);
        request.Headers.UserAgent.ParseAdd("d47-codex-pictures");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        try
        {
            using var response = await _http.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            var body = await response.Content.ReadAsByteArrayAsync(CancellationToken.None).ConfigureAwait(false);

            if (response.IsSuccessStatusCode || (method == HttpMethod.Get && response.StatusCode == HttpStatusCode.NotFound))
            {
                return (response.StatusCode, body);
            }

            var message = $"{step} failed: {GitHubMessage(body) ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)}.";

            throw new PublishFailure(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? $"{message} {CannotWriteText}"
                : message);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            throw new PublishFailure($"{step} failed: GitHub could not be reached ({ex.GetType().Name}).");
        }
    }

    /// <summary>The <c>message</c> of a GitHub error body, or of its <c>error</c> object; null when there is none.</summary>
    internal static string? GitHubMessage(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString()?.TrimEnd('.');
            }

            return root.TryGetProperty("error", out var error)
                   && error.ValueKind == JsonValueKind.Object
                   && error.TryGetProperty("message", out var inner)
                   && inner.ValueKind == JsonValueKind.String
                ? inner.GetString()?.TrimEnd('.')
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Tag(int number) => string.Create(CultureInfo.InvariantCulture, $"{CodexPictureIndex.TagPrefix}{number}");

    private static long ReadId(byte[] body, string step)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("id", out var id)
                && id.TryGetInt64(out var value))
            {
                return value;
            }
        }
        catch (JsonException)
        {
        }

        throw new PublishFailure($"{step} failed: GitHub's answer had no id.");
    }

    private static List<Asset> ReadAssets(byte[] body)
    {
        var assets = new List<Asset>();

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new PublishFailure("Finding the release failed: GitHub's answer was not a list of files.");
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object
                    && element.TryGetProperty("id", out var id)
                    && id.TryGetInt64(out var value)
                    && element.TryGetProperty("name", out var name)
                    && name.GetString() is { } text)
                {
                    assets.Add(new Asset(value, text));
                }
            }
        }
        catch (JsonException)
        {
            throw new PublishFailure("Finding the release failed: GitHub's answer was not a list of files.");
        }

        return assets;
    }

    private sealed record Release(int Number, string Tag, long Id, IReadOnlyList<Asset> Assets);

    private sealed record Asset(long Id, string Name);

    private sealed class PublishFailure(string message) : Exception(message);
}
