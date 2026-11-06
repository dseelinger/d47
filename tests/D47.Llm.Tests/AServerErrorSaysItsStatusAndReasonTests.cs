using System.Net;
using Xunit;

namespace D47.Llm.Tests;

public class AServerErrorSaysItsStatusAndReasonTests
{
    private static (string Message, bool Transient) Describe(HttpStatusCode status, string? detail)
    {
        using var endpoint = new OpenAi.OpenAiEndpoint("https://api.openai.com/v1", "key");
        return endpoint.Describe(status, detail);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    [InlineData(HttpStatusCode.BadGateway, "502")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "503")]
    [InlineData(HttpStatusCode.GatewayTimeout, "504")]
    public void AServerErrorNamesItsStatusAndTheProvidersReason(HttpStatusCode status, string number)
    {
        var (message, transient) = Describe(status, "The server is overloaded.");

        Assert.Contains(number, message);
        Assert.Contains("The server is overloaded.", message);
        Assert.True(transient);
    }

    [Fact]
    public void AServerErrorWithNoReasonNamesItsStatusAlone()
    {
        var (message, transient) = Describe(HttpStatusCode.ServiceUnavailable, null);

        Assert.Contains("503", message);
        Assert.DoesNotContain(" — ", message);
        Assert.True(transient);
    }

    [Fact]
    public void TheOtherMessagesAreUnchanged()
    {
        Assert.Equal("Rate limited by api.openai.com; it should clear shortly.", Describe(HttpStatusCode.TooManyRequests, "x").Message);
        Assert.Equal("api.openai.com rejected the API key. Check it in settings. — x", Describe(HttpStatusCode.Unauthorized, "x").Message);
        Assert.Equal("This key is not permitted to use that model at api.openai.com. — x", Describe(HttpStatusCode.Forbidden, "x").Message);
    }
}
