using System.Net;
using System.Text;
using D47.Core.Conversation;
using D47.Stt;
using Xunit;

namespace D47.App.Tests;

/// <summary>What the Deepgram balance reader makes of each answer the projects and balances endpoints can give.</summary>
public sealed class DeepgramBalanceReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DeepgramBalanceReader Reader(HttpMessageHandler handler, string? key = "dg_secret") =>
        new(() => key, new HttpClient(handler));

    private static HttpResponseMessage Reply(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Ok(string body) => Reply(HttpStatusCode.OK, body);

    private static string Projects(params (string Id, string Name)[] projects) =>
        "{\"projects\":[" + string.Join(",", projects.Select(p => $"{{\"project_id\":\"{p.Id}\",\"name\":\"{p.Name}\"}}")) + "]}";

    private static Scripted Account(params (string Id, string Name, string Balances)[] projects) =>
        new(request =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path == "/v1/projects")
            {
                return Ok(Projects(projects.Select(p => (p.Id, p.Name)).ToArray()));
            }

            var found = projects.First(p => path == $"/v1/projects/{p.Id}/balances");
            return Ok("{\"balances\":[" + found.Balances + "]}");
        });

    [Fact]
    public async Task NoKeyIsSaidAndNothingIsSent()
    {
        var handler = Account(("p1", "Main", ""));

        var reading = await Reader(handler, key: null).ReadAsync(Token);

        Assert.IsType<AccountReading.NoKey>(reading);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OneProjectWithOneBalanceIsOneEntry()
    {
        var handler = Account(("p1", "Main", """{"balance_id":"b","amount":187.5,"units":"usd"}"""));

        var credit = Assert.IsType<DeepgramCredit>(await Reader(handler).ReadAsync(Token));

        Assert.Equal([new DeepgramBalance("Main", 187.5m, "usd")], credit.Projects);
        Assert.All(handler.Asked, r => Assert.Equal("dg_secret", r.Headers.Authorization!.Parameter));
        Assert.All(handler.Asked, r => Assert.Equal("Token", r.Headers.Authorization!.Scheme));
    }

    [Fact]
    public async Task TwoProjectsGiveOneEntryEach()
    {
        var handler = Account(
            ("p1", "Main", """{"amount":10,"units":"usd"}"""),
            ("p2", "Side", """{"amount":2.25,"units":"usd"}, {"amount":3.75,"units":"usd"}, {"amount":600,"units":"hour"}"""));

        var credit = Assert.IsType<DeepgramCredit>(await Reader(handler).ReadAsync(Token));

        Assert.Equal(
            [
                new DeepgramBalance("Main", 10m, "usd"),
                new DeepgramBalance("Side", 6m, "usd"),
                new DeepgramBalance("Side", 600m, "hour"),
            ],
            credit.Projects);
    }

    [Fact]
    public async Task AProjectWithNoBalancesGivesNoEntry()
    {
        var handler = Account(("p1", "Main", ""), ("p2", "Side", """{"amount":4,"units":"usd"}"""));

        var credit = Assert.IsType<DeepgramCredit>(await Reader(handler).ReadAsync(Token));

        Assert.Equal([new DeepgramBalance("Side", 4m, "usd")], credit.Projects);
    }

    [Fact]
    public async Task AKeyThatReachesNoProjectIsCreditWithAnEmptyList()
    {
        var handler = Account();

        var credit = Assert.IsType<DeepgramCredit>(await Reader(handler).ReadAsync(Token));

        Assert.Empty(credit.Projects);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ARefusalOnTheBalancesCallCarriesTheStatusAndErrMsg()
    {
        var handler = new Scripted(request => request.RequestUri!.AbsolutePath == "/v1/projects"
            ? Ok(Projects(("p1", "Main")))
            : Reply(HttpStatusCode.Forbidden, """{"err_code":"INSUFFICIENT_PERMISSIONS","err_msg":"Project does not have access"}"""));

        var refused = Assert.IsType<AccountReading.Refused>(await Reader(handler).ReadAsync(Token));

        Assert.Equal(403, refused.Status);
        Assert.Equal("Project does not have access", refused.Message);
    }

    [Fact]
    public async Task ARefusalOnTheProjectsCallIsRefusedToo()
    {
        var handler = new Scripted(_ => Reply(HttpStatusCode.Unauthorized, """{"err_msg":"Invalid credentials"}"""));

        var refused = Assert.IsType<AccountReading.Refused>(await Reader(handler).ReadAsync(Token));

        Assert.Equal(401, refused.Status);
        Assert.Equal("Invalid credentials", refused.Message);
    }

    [Fact]
    public async Task AServerErrorIsUnreachable()
    {
        var handler = new Scripted(_ => Reply(HttpStatusCode.InternalServerError, "oops"));

        var reading = Assert.IsType<AccountReading.Unreachable>(await Reader(handler).ReadAsync(Token));

        Assert.Contains("500", reading.Message);
    }

    [Fact]
    public async Task AThrownRequestErrorIsUnreachableWithItsMessage()
    {
        var handler = new Scripted(_ => throw new HttpRequestException("no route to host"));

        var reading = Assert.IsType<AccountReading.Unreachable>(await Reader(handler).ReadAsync(Token));

        Assert.Equal("no route to host", reading.Message);
    }

    [Fact]
    public async Task ATimeoutIsUnreachable()
    {
        var handler = new Scripted(_ => throw new TaskCanceledException("timed out"));

        Assert.IsType<AccountReading.Unreachable>(await Reader(handler).ReadAsync(Token));
    }

    [Fact]
    public async Task ACallersCancellationStillCancels()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var handler = Account();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(handler).ReadAsync(cancelled.Token));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"projects":"none"}""")]
    [InlineData("""{"projects":[{"name":"no id"}]}""")]
    public async Task AProjectsBodyThatDoesNotParseIsUnreachable(string body)
    {
        var handler = new Scripted(_ => Ok(body));

        var reading = Assert.IsType<AccountReading.Unreachable>(await Reader(handler).ReadAsync(Token));

        Assert.Equal("the reply could not be read", reading.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"balances":[{"amount":"many","units":"usd"}]}""")]
    public async Task ABalancesBodyThatDoesNotParseIsUnreachable(string body)
    {
        var handler = new Scripted(request => request.RequestUri!.AbsolutePath == "/v1/projects"
            ? Ok(Projects(("p1", "Main")))
            : Ok(body));

        var reading = Assert.IsType<AccountReading.Unreachable>(await Reader(handler).ReadAsync(Token));

        Assert.Equal("the reply could not be read", reading.Message);
    }

    private sealed class Scripted(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls => Asked.Count;

        public List<HttpRequestMessage> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(answer(request));
        }
    }
}
