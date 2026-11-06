using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace D47.Knowledge.Tests;

/// <summary>
/// Answers a faction search page by page from the recorded response for Eurybia Blue Mafia, cut to three systems
/// and captured from the live service on 2026-11-06, recording each request's body.
/// </summary>
internal sealed class FactionAnswer : HttpMessageHandler
{
    public const string Faction = "Eurybia Blue Mafia";

    private readonly IReadOnlyList<string> _pages;

    public FactionAnswer(params string[] pages) => _pages = pages;

    public List<string> Bodies { get; } = [];

    /// <summary>The page, counted from 0, that answers with this status in place of its body.</summary>
    public (int Page, HttpStatusCode Status)? Failing { get; set; }

    /// <summary>The recording as one page, its count set to the three systems it was cut to.</summary>
    public static string Recorded() => Page(Results(), 3);

    /// <summary>The recording as two pages, its first two systems and then its third, each with the full count.</summary>
    public static string[] TwoPages()
    {
        var results = Results();

        return [Page(results.Take(2), 3), Page(results.Skip(2), 3)];
    }

    private static JsonArray Results() =>
        JsonNode.Parse(Fixture.Text("spansh-search-faction-named-eurybia-blue-mafia.json"))!["results"]!.AsArray();

    /// <summary>A response holding these results and claiming <paramref name="count"/> in all.</summary>
    public static string Page(IEnumerable<JsonNode?> results, int count) =>
        new JsonObject
        {
            ["count"] = count,
            ["results"] = new JsonArray([.. results.Select(result => result?.DeepClone())]),
        }.ToJsonString();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        Bodies.Add(body);

        var page = JsonDocument.Parse(body).RootElement.GetProperty("page").GetInt32();

        if (Failing is { } failing && failing.Page == page)
        {
            return new HttpResponseMessage(failing.Status) { Content = new StringContent("{}") };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                page < _pages.Count ? _pages[page] : """{"count":0,"results":[]}""",
                Encoding.UTF8,
                "application/json"),
        };
    }
}
