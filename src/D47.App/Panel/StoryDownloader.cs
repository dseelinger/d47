using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.App.Panel;

/// <summary>
/// Fetches stock stories from the <c>stories-1</c> release into <c>data\stories\</c>: the list once per session with
/// the cast pictures it names, a story's hidden layer and cast pictures when it is picked, and a running story's
/// missing files at startup. Every fetch runs on the pool.
/// </summary>
public sealed partial class StoryDownloader
{
    /// <summary>Where stories are published. A breaking change to the story format moves the pin to the next tag.</summary>
    public const string Source = "https://github.com/dseelinger/d47/releases/download/stories-1/";

    private readonly string _folder;
    private readonly Func<bool> _allowed;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, Task<bool>> _inFlight = new(StringComparer.OrdinalIgnoreCase);
    private int _listAsked;

    /// <param name="folder"><c>AppPaths.Stories</c>.</param>
    /// <param name="allowed">Read at each fetch, so turning the setting off stops the next one.</param>
    public StoryDownloader(string folder, Func<bool> allowed, ILogger logger, HttpMessageHandler? handler = null)
    {
        _folder = folder;
        _allowed = allowed;
        _logger = logger;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(90);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47-stories");
    }

    /// <summary>Whether the Download stock stories setting is on.</summary>
    public bool Enabled => _allowed();

    /// <summary>Raised on a background thread after a file has landed, once the story files it belongs to are all in place.</summary>
    public event Action? Landed;

    /// <summary>Whether the hidden layer of <paramref name="id"/> is on disk.</summary>
    public bool IsOnDisk(string id) => SafeName(id) && File.Exists(Path.Combine(_folder, id + StoryCatalog.SealedExtension));

    /// <summary>Asks for the story list, the first time it is called in a session while downloads are on.</summary>
    public Task AskForList()
    {
        if (!_allowed() || Interlocked.Exchange(ref _listAsked, 1) == 1)
        {
            return Task.CompletedTask;
        }

        return Task.Run(FetchListAsync);
    }

    /// <summary>
    /// Fetches the hidden layer of <paramref name="id"/>, then every cast picture it names. The hidden layer is moved
    /// into place last, so it is on disk only when every file is. True when the story is complete on disk.
    /// </summary>
    public Task<bool> FetchStory(string id) =>
        _allowed() && SafeName(id) ? _inFlight.GetOrAdd(id, key => Task.Run(() => FetchStoryAsync(key))) : Task.FromResult(false);

    /// <summary>
    /// Fetches each story in <paramref name="ids"/> whose hidden layer is not on disk, and each cast picture missing
    /// from a story that is.
    /// </summary>
    public Task FetchMissing(IEnumerable<string> ids)
    {
        var stories = ids.Where(SafeName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return stories.Count == 0 || !_allowed()
            ? Task.CompletedTask
            : Task.Run(async () =>
            {
                foreach (var id in stories)
                {
                    await (IsOnDisk(id) ? FetchMissingPicturesAsync(id) : FetchStory(id)).ConfigureAwait(false);
                }
            });
    }

    private async Task FetchListAsync()
    {
        var partial = await GetAsync(StoryCatalog.IndexFile).ConfigureAwait(false);

        if (partial is null)
        {
            return;
        }

        List<string> listed;

        try
        {
            using (var stream = File.OpenRead(partial))
            using (var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException("The story list is not an array.");
                }

                listed = CastPictureNames(document.RootElement);
            }

            File.Move(partial, Path.Combine(_folder, StoryCatalog.IndexFile), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogInformation("The story list could not be used ({Error}); the copy on disk stays.", ex.GetType().Name);
            Discard(partial);
            return;
        }

        var safe = listed.Where(SafeName).Distinct(StringComparer.Ordinal).ToList();

        if (safe.Count < listed.Distinct(StringComparer.Ordinal).Count())
        {
            _logger.LogInformation("The story list names a cast picture that is not a file name.");
        }

        try
        {
            await FetchNamedPicturesAsync(safe).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation("The list's cast pictures could not be stored ({Error}).", ex.GetType().Name);
        }

        Raise();
    }

    /// <summary>Every name in each card's <c>castPictures</c>; a card or entry of another shape contributes none.</summary>
    private static List<string> CastPictureNames(JsonElement list)
    {
        var names = new List<string>();

        foreach (var card in list.EnumerateArray())
        {
            if (card.ValueKind != JsonValueKind.Object
                || !card.TryGetProperty("castPictures", out var pictures)
                || pictures.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            names.AddRange(pictures.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!));
        }

        return names;
    }

    private async Task<bool> FetchStoryAsync(string id)
    {
        try
        {
            return await FetchFilesAsync(id).ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(id, out _);
        }
    }

    private async Task<bool> FetchFilesAsync(string id)
    {
        var sealedFile = id + StoryCatalog.SealedExtension;
        var hiddenPartial = await GetAsync(sealedFile).ConfigureAwait(false);

        if (hiddenPartial is null)
        {
            return false;
        }

        try
        {
            var entry = StoryCatalog.Unseal(await File.ReadAllTextAsync(hiddenPartial, Encoding.ASCII).ConfigureAwait(false))
                .FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                _logger.LogInformation("Story {StoryId}: the downloaded file has no entry for it.", id);
                Discard(hiddenPartial);
                return false;
            }

            if (await FetchPicturesAsync(entry).ConfigureAwait(false) is null)
            {
                Discard(hiddenPartial);
                return false;
            }

            File.Move(hiddenPartial, Path.Combine(_folder, sealedFile), overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException or InvalidDataException)
        {
            _logger.LogInformation("Story {StoryId} could not be downloaded ({Error}).", id, ex.GetType().Name);
            Discard(hiddenPartial);
            return false;
        }

        _logger.LogInformation("Story {StoryId} downloaded.", id);
        Raise();
        return true;
    }

    private async Task FetchMissingPicturesAsync(string id)
    {
        try
        {
            var entry = StoryCatalog.Unseal(await File.ReadAllTextAsync(Path.Combine(_folder, id + StoryCatalog.SealedExtension), Encoding.ASCII).ConfigureAwait(false))
                .FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));

            if (entry is not null && await FetchPicturesAsync(entry).ConfigureAwait(false) > 0)
            {
                Raise();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException or InvalidDataException)
        {
            _logger.LogInformation("Story {StoryId}: its missing cast pictures could not be fetched ({Error}).", id, ex.GetType().Name);
        }
    }

    /// <summary>
    /// Fetches each cast picture of <paramref name="entry"/> not on disk, skipping one the release does not have.
    /// Returns how many landed, or null when a name is unsafe or a fetch failed.
    /// </summary>
    private async Task<int?> FetchPicturesAsync(StorySecret entry)
    {
        var names = Pictures(entry).ToList();

        if (!names.All(SafeName))
        {
            _logger.LogInformation("Story {StoryId}: a cast picture has a name that is not a file name.", entry.Id);
            return null;
        }

        return await FetchNamedPicturesAsync(names).ConfigureAwait(false);
    }

    /// <summary>Fetches each of <paramref name="names"/> not on disk. Returns how many landed, or null when a fetch failed.</summary>
    private async Task<int?> FetchNamedPicturesAsync(IEnumerable<string> names)
    {
        var landed = 0;

        foreach (var picture in names)
        {
            var file = picture + ".jpg";
            var notFound = false;

            if (File.Exists(Path.Combine(_folder, file)))
            {
                continue;
            }

            var picturePartial = await GetAsync(file, noFile => notFound = noFile).ConfigureAwait(false);

            if (picturePartial is null)
            {
                if (notFound)
                {
                    continue;
                }

                return null;
            }

            File.Move(picturePartial, Path.Combine(_folder, file), overwrite: true);
            landed++;
        }

        return landed;
    }

    /// <summary>The picture names of every cast member, both versions of one that has versions.</summary>
    private static IEnumerable<string> Pictures(StorySecret entry) => entry.Cast.SelectMany(speaker =>
        speaker.Versions is { } versions
            ? versions.All().Select(version => $"{entry.Id}.{speaker.Id}.{version.Key}")
            : [$"{entry.Id}.{speaker.Id}"]);

    /// <summary>Fetches <paramref name="file"/> to a temporary file in the stories folder, and returns its path, or null. <paramref name="missing"/> is told when the release answered 404.</summary>
    private async Task<string?> GetAsync(string file, Action<bool>? missing = null)
    {
        var partial = Path.Combine(_folder, file + ".part");

        try
        {
            Directory.CreateDirectory(_folder);

            using var response = await _http.GetAsync(
                Source + Uri.EscapeDataString(file), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("No stock story file {File}: the stories release returned {Status}.", file, response.StatusCode);
                missing?.Invoke(response.StatusCode == HttpStatusCode.NotFound);
                return null;
            }

            await using (var target = File.Create(partial))
            {
                await response.Content.CopyToAsync(target, CancellationToken.None).ConfigureAwait(false);
            }

            return partial;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            _logger.LogInformation("Stock story file {File} could not be fetched ({Error}).", file, ex.GetType().Name);
            Discard(partial);
            return null;
        }
    }

    private void Raise()
    {
        try
        {
            Landed?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redrawing after a story download failed");
        }
    }

    private static void Discard(string partial)
    {
        try
        {
            File.Delete(partial);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover .part is untidy, not broken: nothing reads that name.
        }
    }

    /// <summary>A name made only of the characters of a story id, so a downloaded id cannot leave the stories folder.</summary>
    private static bool SafeName(string name) => name.Length > 0 && !name.Contains("..", StringComparison.Ordinal) && FileName().IsMatch(name);

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex FileName();
}
