using System.Net;
using System.Text;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

[Collection(nameof(EndpointDemotionCollection))]
public class LmStudioReportsAndReloadsTheLoadedContextTests
{
    private const string Model = "google/gemma-4-e4b";
    private const string BaseUrl = "http://127.0.0.1:1234/v1";

    private const string List = "GET http://127.0.0.1:1234/api/v1/models";
    private const string Unload = "POST http://127.0.0.1:1234/api/v1/models/unload {\"instance_id\":\"google/gemma-4-e4b\"}";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public LmStudioReportsAndReloadsTheLoadedContextTests() => EndpointDemotions.Clear();

    private static string LoadAt(int tokens) =>
        $"POST http://127.0.0.1:1234/api/v1/models/load {{\"model\":\"google/gemma-4-e4b\",\"context_length\":{tokens}}}";

    [Fact]
    public async Task OneLoadedInstanceReportsItsContextAndCapsLaterCalls()
    {
        var server = new LmStudioStub((HttpStatusCode.OK, LmStudioRecordings.Loaded));
        using var provider = server.Provider();

        var context = await provider.LoadedContextAsync(Model, Token);

        Assert.Equal(new LoadedContext(8192, 131072, 1), context);
        Assert.Equal([List], server.Requests);
        Assert.Equal(8192, provider.CapabilitiesFor(Model).ContextTokens);
    }

    [Fact]
    public async Task AModelThatIsNotLoadedReportsNoTokens()
    {
        var server = new LmStudioStub((HttpStatusCode.OK, LmStudioRecordings.NotLoaded));
        using var provider = server.Provider();

        Assert.Equal(new LoadedContext(null, 131072, 0), await provider.LoadedContextAsync(Model, Token));
        Assert.Null(provider.CapabilitiesFor(Model).ContextTokens);
    }

    [Fact]
    public async Task TwoInstancesReportTheKeyedOnesContextWithoutRecordingIt()
    {
        var server = new LmStudioStub((HttpStatusCode.OK, LmStudioRecordings.TwoInstances));
        using var provider = server.Provider();

        Assert.Equal(new LoadedContext(8192, 131072, 2), await provider.LoadedContextAsync(Model, Token));
        Assert.Null(provider.CapabilitiesFor(Model).ContextTokens);
    }

    [Fact]
    public async Task AListThatIsNotLmStudiosReportsNothing()
    {
        var server = new LmStudioStub((HttpStatusCode.OK, LmStudioRecordings.ChatModels));
        using var provider = server.Provider();

        Assert.Null(await provider.LoadedContextAsync(Model, Token));
    }

    [Fact]
    public async Task ANotFoundReportsNothing()
    {
        var server = new LmStudioStub((HttpStatusCode.NotFound, LmStudioRecordings.UnknownRoute));
        using var provider = server.Provider();

        Assert.Null(await provider.LoadedContextAsync(Model, Token));
    }

    [Fact]
    public async Task AReloadUnloadsTheInstanceThenLoadsAtTheNewContext()
    {
        var server = new LmStudioStub(
            (HttpStatusCode.OK, LmStudioRecordings.Loaded),
            (HttpStatusCode.OK, LmStudioRecordings.Unloaded),
            (HttpStatusCode.OK, LmStudioRecordings.LoadedAt16384),
            (HttpStatusCode.OK, LmStudioRecordings.Reloaded));
        using var provider = server.Provider();

        var reload = await provider.ReloadAsync(Model, 16384, Token);

        Assert.Equal(new ContextReload(true, null), reload);
        Assert.Equal([List, Unload, LoadAt(16384), List], server.Requests);
        Assert.Equal(16384, provider.CapabilitiesFor(Model).ContextTokens);
    }

    [Fact]
    public async Task AModelThatIsNotLoadedIsLoadedWithoutAnUnload()
    {
        var server = new LmStudioStub(
            (HttpStatusCode.OK, LmStudioRecordings.NotLoaded),
            (HttpStatusCode.OK, LmStudioRecordings.LoadedAt16384),
            (HttpStatusCode.OK, LmStudioRecordings.Reloaded));
        using var provider = server.Provider();

        Assert.True((await provider.ReloadAsync(Model, 16384, Token)).Loaded);
        Assert.Equal([List, LoadAt(16384), List], server.Requests);
    }

    [Fact]
    public async Task AFailedLoadLoadsTheModelBackAtItsOldContext()
    {
        var server = new LmStudioStub(
            (HttpStatusCode.OK, LmStudioRecordings.Loaded),
            (HttpStatusCode.OK, LmStudioRecordings.Unloaded),
            (HttpStatusCode.NotFound, LmStudioRecordings.ModelNotFound),
            (HttpStatusCode.OK, LmStudioRecordings.LoadedAt16384));
        using var provider = server.Provider();

        var reload = await provider.ReloadAsync(Model, 16384, Token);

        Assert.Equal(new ContextReload(false, "Model google/no-such-model not found in downloaded models"), reload);
        Assert.Equal([List, Unload, LoadAt(16384), LoadAt(8192)], server.Requests);
    }

    [Fact]
    public async Task ACancelAfterTheUnloadStillSendsTheLoad()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var server = new LmStudioStub(
            (HttpStatusCode.OK, LmStudioRecordings.Loaded),
            (HttpStatusCode.OK, LmStudioRecordings.Unloaded),
            (HttpStatusCode.OK, LmStudioRecordings.LoadedAt16384),
            (HttpStatusCode.OK, LmStudioRecordings.Reloaded))
        {
            OnRequest = request =>
            {
                if (request.StartsWith("POST http://127.0.0.1:1234/api/v1/models/unload", StringComparison.Ordinal))
                {
                    cancel.Cancel();
                }
            },
        };

        using var provider = server.Provider();

        var reload = await provider.ReloadAsync(Model, 16384, cancel.Token);

        Assert.True(cancel.IsCancellationRequested);
        Assert.True(reload.Loaded);
        Assert.Equal([List, Unload, LoadAt(16384), List], server.Requests);
    }

    [Fact]
    public async Task AModelWithTwoInstancesIsNotReloaded()
    {
        var server = new LmStudioStub((HttpStatusCode.OK, LmStudioRecordings.TwoInstances));
        using var provider = server.Provider();

        var reload = await provider.ReloadAsync(Model, 16384, Token);

        Assert.False(reload.Loaded);
        Assert.Equal([List], server.Requests);
    }

    [Theory]
    [InlineData(262144)]
    [InlineData(0)]
    public async Task AContextTheModelCannotTakeUnloadsNothing(int tokens)
    {
        var server = new LmStudioStub((HttpStatusCode.OK, LmStudioRecordings.Loaded));
        using var provider = server.Provider();

        var reload = await provider.ReloadAsync(Model, tokens, Token);

        Assert.False(reload.Loaded);
        Assert.Equal([List], server.Requests);
    }

    [Fact]
    public async Task AnInstanceWithNoReportedContextIsNotUnloaded()
    {
        var server = new LmStudioStub(
            (HttpStatusCode.OK, LmStudioRecordings.Loaded.Replace("\"context_length\": 8192,", string.Empty, StringComparison.Ordinal)));
        using var provider = server.Provider();

        var reload = await provider.ReloadAsync(Model, 16384, Token);

        Assert.False(reload.Loaded);
        Assert.Equal([List], server.Requests);
    }

    [Fact]
    public async Task ASecondReloadWaitsForTheFirst()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var server = new LmStudioStub(
            (HttpStatusCode.OK, LmStudioRecordings.Loaded),
            (HttpStatusCode.OK, LmStudioRecordings.Unloaded),
            (HttpStatusCode.OK, LmStudioRecordings.LoadedAt16384),
            (HttpStatusCode.OK, LmStudioRecordings.Reloaded),
            (HttpStatusCode.OK, LmStudioRecordings.Reloaded),
            (HttpStatusCode.OK, LmStudioRecordings.Unloaded),
            (HttpStatusCode.OK, LmStudioRecordings.LoadedAt16384),
            (HttpStatusCode.OK, LmStudioRecordings.Reloaded))
        {
            Hold = request => request.StartsWith("POST http://127.0.0.1:1234/api/v1/models/load", StringComparison.Ordinal)
                ? release.Task
                : Task.CompletedTask,
        };

        using var provider = server.Provider();

        var first = provider.ReloadAsync(Model, 16384, Token);
        var second = provider.ReloadAsync(Model, 16384, Token);

        Assert.Equal([List, Unload, LoadAt(16384)], server.Requests);
        Assert.False(second.IsCompleted);

        release.SetResult();

        Assert.True((await first).Loaded);
        Assert.True((await second).Loaded);
        Assert.Equal([List, Unload, LoadAt(16384), List, List, Unload, LoadAt(16384), List], server.Requests);
    }

    [Fact]
    public async Task AnEndpointOffThisMachineIsSentNothing()
    {
        var server = new LmStudioStub();
        using var provider = server.Provider("https://openrouter.ai/api/v1");

        Assert.Null(await provider.LoadedContextAsync(Model, Token));
        Assert.False((await provider.ReloadAsync(Model, 16384, Token)).Loaded);
        Assert.Empty(server.Requests);
    }

    /// <summary>Answers each request with the next scripted response and records it as "METHOD url body".</summary>
    private sealed class LmStudioStub(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new(responses);

        public List<string> Requests { get; } = [];

        public Action<string>? OnRequest { get; init; }

        public Func<string, Task>? Hold { get; init; }

        public ChatCompletionsLlmProvider Provider(string endpoint = BaseUrl) =>
            new(apiKey: null, endpoint, new HttpClient(this));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : " " + await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var line = $"{request.Method} {request.RequestUri}{body}";
            Requests.Add(line);
            OnRequest?.Invoke(line);

            if (Hold is not null)
            {
                await Hold(line).ConfigureAwait(false);
            }

            var (status, text) = _responses.Dequeue();

            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }
}
