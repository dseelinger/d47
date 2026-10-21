using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace D47.App.Tests;

/// <summary>Publish and Unpublish against a fake GitHub (#1371).</summary>
public sealed class ACodexPictureIsPublishedToTheReleaseTests
{
    private const string Folder = @"C:\d47\data\codex-pictures";

    private const string Token = "github_pat_SECRET_TOKEN_1371";

    private const string ApiRepo = "/repos/" + CodexPictureIndex.Repository;

    private static readonly CodexPictureKey Key = new(101, 18);

    private static readonly byte[] Mine = Encoding.ASCII.GetBytes("the commander's picture");

    private static readonly byte[] OldPicture = Encoding.ASCII.GetBytes("the old stock picture");

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static CodexPictureRow RowOf(CodexPictureKey key, byte[] bytes, string tag = CodexPictureIndex.FirstTag) =>
        new(key.EntryId, key.RegionId, tag, CodexPictureIndex.AssetName(key.EntryId, key.RegionId, Sha(bytes)), Sha(bytes), bytes.Length);

    /// <summary>The release endpoints the publisher calls, kept in memory.</summary>
    private sealed class FakeGitHub : HttpMessageHandler
    {
        private readonly Lock _gate = new();
        private readonly List<FakeRelease> _releases = [];
        private long _nextId = 1000;

        public List<string> Calls { get; } = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public string? CreatedBody { get; private set; }

        /// <summary>Answers the first request it matches with the status and body, instead of acting on it.</summary>
        public (Func<HttpRequestMessage, bool> When, HttpStatusCode Status, string Body)? Failure { get; set; }

        public FakeRelease Release(int number)
        {
            var release = new FakeRelease(CodexPictureIndex.TagPrefix + number.ToString(CultureInfo.InvariantCulture), _nextId++);
            _releases.Add(release);

            return release;
        }

        public FakeRelease? Find(string tag) => _releases.FirstOrDefault(release => release.Tag == tag);

        public long Asset(FakeRelease release, string name, byte[] bytes)
        {
            var id = _nextId++;
            release.Assets.Add(new FakeAsset(id, name, bytes));

            return id;
        }

        public IReadOnlyList<CodexPictureRow> Index() =>
            CodexPictureIndex.Parse(Encoding.UTF8.GetString(Find(CodexPictureIndex.FirstTag)!.Assets.Single(asset => asset.Name == CodexPictureIndex.IndexName).Bytes)).Rows;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);

            lock (_gate)
            {
                var uri = request.RequestUri!;
                var call = $"{request.Method} {uri.Host}{uri.AbsolutePath}{uri.Query}";
                Calls.Add(call);
                Requests.Add(request);
                Bodies.Add(Encoding.UTF8.GetString(body));

                if (Failure is { } failure && failure.When(request))
                {
                    Failure = null;
                    return Json(failure.Status, failure.Body);
                }

                return Answer(request.Method, uri, body);
            }
        }

        private HttpResponseMessage Answer(HttpMethod method, Uri uri, byte[] body)
        {
            var path = uri.AbsolutePath;

            if (method == HttpMethod.Get && path.StartsWith(ApiRepo + "/releases/tags/", StringComparison.Ordinal))
            {
                var release = Find(path[(ApiRepo + "/releases/tags/").Length..]);

                return release is null
                    ? Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}")
                    : Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { id = release.Id, tag_name = release.Tag }));
            }

            if (method == HttpMethod.Get && path.StartsWith(ApiRepo + "/releases/assets/", StringComparison.Ordinal))
            {
                var asset = AssetById(long.Parse(path[(ApiRepo + "/releases/assets/").Length..], CultureInfo.InvariantCulture));

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(asset!.Bytes) };
            }

            if (method == HttpMethod.Get && path.EndsWith("/assets", StringComparison.Ordinal))
            {
                var id = long.Parse(path.Split('/')[^2], CultureInfo.InvariantCulture);
                var page = int.Parse(Query(uri, "page"), CultureInfo.InvariantCulture);
                var size = int.Parse(Query(uri, "per_page"), CultureInfo.InvariantCulture);
                var assets = _releases.Single(release => release.Id == id).Assets.Skip((page - 1) * size).Take(size);

                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(assets.Select(asset => new { id = asset.Id, name = asset.Name })));
            }

            if (method == HttpMethod.Post && path == ApiRepo + "/releases")
            {
                CreatedBody = Encoding.UTF8.GetString(body);
                using var document = JsonDocument.Parse(body);
                var tag = document.RootElement.GetProperty("tag_name").GetString()!;
                var release = new FakeRelease(tag, _nextId++);
                _releases.Add(release);

                return Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { id = release.Id }));
            }

            if (method == HttpMethod.Post && uri.Host == "uploads.github.com")
            {
                var id = long.Parse(path.Split('/')[^2], CultureInfo.InvariantCulture);
                var release = _releases.Single(held => held.Id == id);
                var name = Query(uri, "name");

                if (release.Assets.Any(asset => asset.Name == name))
                {
                    return Json(HttpStatusCode.UnprocessableEntity, "{\"message\":\"Validation Failed\"}");
                }

                return Json(HttpStatusCode.Created, JsonSerializer.Serialize(new { id = Asset(release, name, body) }));
            }

            if (method == HttpMethod.Delete)
            {
                var id = long.Parse(path.Split('/')[^1], CultureInfo.InvariantCulture);

                foreach (var release in _releases)
                {
                    release.Assets.RemoveAll(asset => asset.Id == id);
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (method == HttpMethod.Patch)
            {
                var id = long.Parse(path.Split('/')[^1], CultureInfo.InvariantCulture);
                using var document = JsonDocument.Parse(body);
                var asset = AssetById(id)!;
                var release = _releases.Single(held => held.Assets.Contains(asset));
                release.Assets[release.Assets.IndexOf(asset)] = asset with { Name = document.RootElement.GetProperty("name").GetString()! };

                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { id }));
            }

            return Json(HttpStatusCode.NotFound, "{\"message\":\"Not Found\"}");
        }

        private FakeAsset? AssetById(long id) => _releases.SelectMany(release => release.Assets).FirstOrDefault(asset => asset.Id == id);

        private static string Query(Uri uri, string name) =>
            uri.Query.TrimStart('?').Split('&').Select(part => part.Split('=')).First(pair => pair[0] == name)[1] is var value
                ? Uri.UnescapeDataString(value)
                : string.Empty;

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed record FakeRelease(string Tag, long Id)
    {
        public List<FakeAsset> Assets { get; } = [];
    }

    private sealed record FakeAsset(long Id, string Name, byte[] Bytes);

    /// <summary>The download URL the store reads stock pictures from, answering that nothing is there.</summary>
    private sealed class NothingPublished : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_lines)
            {
                _lines.Add(formatter(state, exception) + exception);
            }
        }
    }

    private sealed class Surface
    {
        public MemoryFileSystem Files { get; } = new();

        public FakeGitHub GitHub { get; } = new();

        public RecordingLogger Log { get; } = new();

        public CodexPictureStore Store { get; }

        public CodexPicturePublisher Publisher { get; }

        public Surface(string? token = Token)
        {
            Store = new CodexPictureStore(Files, Folder, () => true, Log, new NothingPublished());
            Publisher = new CodexPicturePublisher(Store, () => token, () => token is not null, Log, GitHub);
        }

        public void OwnPicture()
        {
            Store.SaveLocal(Key, Mine, Encoding.ASCII.GetBytes("source"), new CropSquare(0, 0, 600));
        }

        public void StockOnDisk(CodexPictureRow row, byte[] bytes)
        {
            Files.WriteText(Stock(CodexPictureIndex.IndexName), CodexPictureIndex.Write([row]));
            Files.WriteBytes(Stock(row.Asset), bytes);
        }

        public FakeRelease Published(params (CodexPictureRow Row, byte[] Bytes)[] pictures)
        {
            var first = GitHub.Release(1);

            foreach (var (row, bytes) in pictures)
            {
                GitHub.Asset(first, row.Asset, bytes);
            }

            GitHub.Asset(first, CodexPictureIndex.IndexName, Encoding.UTF8.GetBytes(CodexPictureIndex.Write(pictures.Select(picture => picture.Row))));

            return first;
        }

        public string Stock(string name) => Path.Combine(Folder, CodexPictureStore.StockFolder, name);

        public string Local(string name) => Path.Combine(Folder, CodexPictureStore.LocalFolder, name);

        public Dictionary<string, byte[]> Snapshot() =>
            Files.Enumerate(Path.Combine(Folder, CodexPictureStore.StockFolder), "*")
                .Concat(Files.Enumerate(Path.Combine(Folder, CodexPictureStore.LocalFolder), "*"))
                .ToDictionary(path => path, path => Files.ReadBytes(path)!);
    }

    private static int IndexOf(IReadOnlyList<string> calls, Func<string, bool> match)
    {
        for (var i = 0; i < calls.Count; i++)
        {
            if (match(calls[i]))
            {
                return i;
            }
        }

        return -1;
    }

    [Fact]
    public async Task PublishUploadsThenReplacesTheIndexThenDeletesTheReplacedPicture()
    {
        var surface = new Surface();
        surface.OwnPicture();
        var old = RowOf(Key, OldPicture);
        var other = RowOf(new CodexPictureKey(202, 3), Encoding.ASCII.GetBytes("another"));
        var first = surface.Published((old, OldPicture), (other, Encoding.ASCII.GetBytes("another")));
        var oldId = first.Assets.Single(asset => asset.Name == old.Asset).Id;
        surface.StockOnDisk(old, OldPicture);

        var result = await surface.Publisher.Publish(Key)!;

        Assert.True(result.Landed, result.Failure);

        var mine = RowOf(Key, Mine);
        var calls = surface.GitHub.Calls;
        var upload = IndexOf(calls, call => call.StartsWith("POST uploads.github.com", StringComparison.Ordinal) && call.Contains(mine.Asset, StringComparison.Ordinal));
        var index = IndexOf(calls, call => call.StartsWith("PATCH ", StringComparison.Ordinal));
        var delete = IndexOf(calls, call => call == $"DELETE api.github.com{ApiRepo}/releases/assets/{oldId}");

        Assert.True(upload >= 0 && index > upload && delete > index, string.Join("\n", calls));

        Assert.Equal([mine, other], surface.GitHub.Index());
        Assert.DoesNotContain(first.Assets, asset => asset.Name == old.Asset);
        Assert.Equal(Mine, first.Assets.Single(asset => asset.Name == mine.Asset).Bytes);

        Assert.Equal(Mine, surface.Files.ReadBytes(surface.Stock(mine.Asset)));
        Assert.Equal([mine, other], CodexPictureIndex.Parse(surface.Files.ReadText(surface.Stock(CodexPictureIndex.IndexName))!).Rows);
        Assert.Contains(surface.Store.StockRows(), row => row == mine);
        Assert.Null(surface.Files.ReadBytes(surface.Local(Key.FileName)));
        Assert.NotNull(surface.Files.ReadBytes(surface.Local(Key.SourceFileName)));
        Assert.NotNull(surface.Files.ReadText(surface.Local(Key.CropFileName)));
        Assert.Null(surface.Publisher.Doing);
    }

    [Fact]
    public async Task AMissingReleaseIsCreatedWithMakeLatestFalse()
    {
        var surface = new Surface();
        surface.OwnPicture();

        var result = await surface.Publisher.Publish(Key)!;

        Assert.True(result.Landed, result.Failure);

        using var created = JsonDocument.Parse(surface.GitHub.CreatedBody!);
        Assert.Equal(CodexPictureIndex.FirstTag, created.RootElement.GetProperty("tag_name").GetString());
        Assert.Equal("false", created.RootElement.GetProperty("make_latest").GetString());
        Assert.Equal([RowOf(Key, Mine)], surface.GitHub.Index());
    }

    [Fact]
    public async Task AFullReleaseSendsThePictureToTheNextOneAndTheIndexStaysInTheFirst()
    {
        var surface = new Surface();
        surface.OwnPicture();
        var first = surface.Published();

        for (var i = 1; first.Assets.Count < CodexPicturePublisher.MostAssetsPerRelease; i++)
        {
            surface.GitHub.Asset(first, $"filler-{i}.jpg", [1]);
        }

        var result = await surface.Publisher.Publish(Key)!;

        Assert.True(result.Landed, result.Failure);

        var mine = RowOf(Key, Mine, CodexPictureIndex.TagPrefix + "2");
        var second = surface.GitHub.Find(CodexPictureIndex.TagPrefix + "2");

        Assert.NotNull(second);
        Assert.Contains(second.Assets, asset => asset.Name == mine.Asset);
        Assert.DoesNotContain(second.Assets, asset => asset.Name == CodexPictureIndex.IndexName);
        Assert.Equal([mine], surface.GitHub.Index());
    }

    [Fact]
    public async Task UnpublishRemovesTheRowAndThePicture()
    {
        var surface = new Surface();
        var stock = RowOf(Key, OldPicture);
        var first = surface.Published((stock, OldPicture));
        surface.StockOnDisk(stock, OldPicture);

        var result = await surface.Publisher.Unpublish(Key)!;

        Assert.True(result.Landed, result.Failure);
        Assert.Empty(surface.GitHub.Index());
        Assert.DoesNotContain(first.Assets, asset => asset.Name == stock.Asset);
        Assert.Null(surface.Files.ReadBytes(surface.Stock(stock.Asset)));
        Assert.Empty(CodexPictureIndex.Parse(surface.Files.ReadText(surface.Stock(CodexPictureIndex.IndexName))!).Rows);
    }

    [Fact]
    public async Task AForbiddenAnswerShowsGitHubsMessageAndTheTokenLineAndChangesNoFile()
    {
        var surface = new Surface();
        surface.OwnPicture();
        var old = RowOf(Key, OldPicture);
        surface.Published((old, OldPicture));
        surface.StockOnDisk(old, OldPicture);
        var before = surface.Snapshot();
        surface.GitHub.Failure = (
            request => request.RequestUri!.Host == "uploads.github.com",
            HttpStatusCode.Forbidden,
            "{\"message\":\"Resource not accessible by personal access token\",\"documentation_url\":\"https://docs.github.com/rest\",\"status\":\"403\"}");

        var result = await surface.Publisher.Publish(Key)!;

        Assert.Equal(
            "Uploading the picture failed: Resource not accessible by personal access token. " + CodexPicturePublisher.CannotWriteText,
            result.Failure);
        Assert.DoesNotContain("documentation_url", result.Failure, StringComparison.Ordinal);
        Assert.Equal(before.Keys.Order(), surface.Snapshot().Keys.Order());
        Assert.All(before, pair => Assert.Equal(pair.Value, surface.Files.ReadBytes(pair.Key)));
        Assert.Equal([old], surface.GitHub.Index());
    }

    [Fact]
    public async Task AFailedIndexUploadLeavesTheOldIndexInPlace()
    {
        var surface = new Surface();
        surface.OwnPicture();
        var old = RowOf(Key, OldPicture);
        var other = RowOf(new CodexPictureKey(202, 3), Encoding.ASCII.GetBytes("another"));
        surface.Published((old, OldPicture), (other, Encoding.ASCII.GetBytes("another")));
        surface.GitHub.Failure = (
            request => request.RequestUri!.Query.Contains("index.next.json", StringComparison.Ordinal),
            HttpStatusCode.BadGateway,
            "<html>bad gateway</html>");

        var result = await surface.Publisher.Publish(Key)!;

        Assert.Equal("Replacing the index failed: 502.", result.Failure);
        Assert.Equal([old, other], surface.GitHub.Index());
    }

    [Fact]
    public async Task ReleaseOneWithPicturesButNoIndexIsRefusedRatherThanGivenANewIndex()
    {
        var surface = new Surface();
        surface.OwnPicture();
        var first = surface.GitHub.Release(1);
        surface.GitHub.Asset(first, RowOf(new CodexPictureKey(202, 3), OldPicture).Asset, OldPicture);

        var result = await surface.Publisher.Publish(Key)!;

        Assert.Contains("holds pictures but no index.json", result.Failure, StringComparison.Ordinal);
        Assert.DoesNotContain(surface.GitHub.Calls, call => call.StartsWith("POST", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheTokenGoesOnlyInTheAuthorizationHeaderAndNeverInALogLine()
    {
        var surface = new Surface();
        surface.OwnPicture();
        surface.GitHub.Failure = (request => request.Method == HttpMethod.Patch, HttpStatusCode.Unauthorized, "{\"message\":\"Bad credentials\"}");

        var result = await surface.Publisher.Publish(Key)!;

        Assert.False(result.Landed);
        Assert.NotEmpty(surface.GitHub.Requests);
        Assert.All(surface.GitHub.Requests, request =>
        {
            Assert.Contains(request.RequestUri!.Host, new[] { "api.github.com", "uploads.github.com" });
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(Token, request.Headers.Authorization.Parameter);
            Assert.DoesNotContain(Token, request.RequestUri.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(
                request.Headers.Where(header => header.Key != "Authorization").SelectMany(header => header.Value),
                value => value.Contains(Token, StringComparison.Ordinal));
        });
        Assert.All(surface.GitHub.Bodies, body => Assert.DoesNotContain(Token, body, StringComparison.Ordinal));
        Assert.NotEmpty(surface.Log.Lines);
        Assert.All(surface.Log.Lines, line => Assert.DoesNotContain(Token, line, StringComparison.Ordinal));
        Assert.DoesNotContain(Token, result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyOneRunsAtATime()
    {
        var surface = new Surface();
        surface.OwnPicture();
        var gate = new TaskCompletionSource();
        var held = new CodexPicturePublisher(surface.Store, () => { gate.Task.Wait(TestContext.Current.CancellationToken); return Token; }, () => true, surface.Log, surface.GitHub);

        var running = held.Publish(Key);

        Assert.NotNull(running);
        Assert.Equal(CodexPicturePublisher.PublishingText, held.Doing);
        Assert.Null(held.Publish(Key));
        Assert.Null(held.Unpublish(Key));

        gate.SetResult();
        await running;

        Assert.Null(held.Doing);
    }
}
