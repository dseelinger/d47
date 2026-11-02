using System.Net;
using System.Text;

namespace D47.Knowledge.Tests;

/// <summary>Answers every request with one status and a JSON body, recording the URIs and user agents.</summary>
internal sealed class RecordingAnswer(HttpStatusCode status, string body) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public List<string> UserAgents { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        UserAgents.Add(request.Headers.UserAgent.ToString());

        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
