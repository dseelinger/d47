using System.Net;
using System.Text;
using D47.Core.Conversation;
using D47.Tts;
using Xunit;

namespace D47.Tts.Tests;

/// <summary>What the ElevenLabs account reader makes of each answer the subscription endpoint can give.</summary>
public sealed class ElevenLabsAccountReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Body(string extension = "0", string reset = "1790000000", string period = "\"monthly_period\"") => $$"""
        {
          "character_count": 1200,
          "character_limit": 100000,
          "next_character_count_reset_unix": {{reset}},
          "character_refresh_period": {{period}},
          "max_credit_limit_extension": {{extension}},
          "current_overage": { "amount": "1.25", "currency": "usd" }
        }
        """;

    private static ElevenLabsAccountReader Reader(HttpMessageHandler handler, string? key = "sk_secret") =>
        new(() => key, new HttpClient(handler));

    [Fact]
    public async Task NoKeyIsSaidAndNothingIsSent()
    {
        var handler = new Scripted(_ => Reply(HttpStatusCode.OK, Body()));

        var reading = await Reader(handler, key: null).ReadAsync(Token);

        Assert.IsType<AccountReading.NoKey>(reading);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("0", true, false, 0)]
    [InlineData("5000", false, false, 5000)]
    [InlineData("\"unlimited\"", false, true, null)]
    public async Task TheFiguresAreReadWithEachFormOfExtension(string extension, bool stops, bool unlimited, object? characters)
    {
        var handler = new Scripted(_ => Reply(HttpStatusCode.OK, Body(extension)));

        var figures = Assert.IsType<ElevenLabsFigures>(await Reader(handler).ReadAsync(Token));

        Assert.Equal("https://api.elevenlabs.io/v1/user/subscription", handler.Asked!.RequestUri!.ToString());
        Assert.Equal("sk_secret", handler.Asked.Headers.GetValues("xi-api-key").Single());
        Assert.Equal(1200, figures.CharactersUsed);
        Assert.Equal(100000, figures.CharacterLimit);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790000000), figures.NextReset);
        Assert.Equal(RefreshPeriod.Monthly, figures.Refresh);
        Assert.Equal(stops, figures.Extension.IsStop);
        Assert.Equal(unlimited, figures.Extension.IsUnlimited);
        Assert.Equal(characters is null ? null : Convert.ToInt64(characters), figures.Extension.Characters);
        Assert.Equal(new Money(1.25m, "usd"), figures.Overage);
    }

    [Fact]
    public async Task ANullResetAndANullPeriodAreNone()
    {
        var handler = new Scripted(_ => Reply(HttpStatusCode.OK, Body(reset: "null", period: "null")));

        var figures = Assert.IsType<ElevenLabsFigures>(await Reader(handler).ReadAsync(Token));

        Assert.Null(figures.NextReset);
        Assert.Null(figures.Refresh);
    }

    [Theory]
    [InlineData("\"3_month_period\"", RefreshPeriod.Quarterly)]
    [InlineData("\"6_month_period\"", RefreshPeriod.HalfYearly)]
    [InlineData("\"annual_period\"", RefreshPeriod.Annual)]
    public async Task EachRefreshPeriodMapsToItsMonths(string period, RefreshPeriod expected)
    {
        var handler = new Scripted(_ => Reply(HttpStatusCode.OK, Body(period: period)));

        var figures = Assert.IsType<ElevenLabsFigures>(await Reader(handler).ReadAsync(Token));

        Assert.Equal(expected, figures.Refresh);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ARefusedKeyCarriesTheStatusAndTheProvidersMessage(HttpStatusCode status)
    {
        var handler = new Scripted(_ => Reply(status, """{ "detail": { "status": "missing_permissions", "message": "The API key is missing the permission user_read." } }"""));

        var refused = Assert.IsType<AccountReading.Refused>(await Reader(handler).ReadAsync(Token));

        Assert.Equal((int)status, refused.Status);
        Assert.Equal("The API key is missing the permission user_read", refused.Message);
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
        var handler = new Scripted(_ => Reply(HttpStatusCode.OK, Body()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader(handler).ReadAsync(cancelled.Token));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "character_count": "many" }""")]
    public async Task ABodyThatDoesNotParseIsUnreachable(string body)
    {
        var handler = new Scripted(_ => Reply(HttpStatusCode.OK, body));

        var reading = Assert.IsType<AccountReading.Unreachable>(await Reader(handler).ReadAsync(Token));

        Assert.Equal("the reply could not be read", reading.Message);
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Scripted(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public HttpRequestMessage? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Asked = request;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(answer(request));
        }
    }
}
