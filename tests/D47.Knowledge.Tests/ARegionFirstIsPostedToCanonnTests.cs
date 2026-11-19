using System.Net;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A region first is posted to Canonn's postEvent as Canonn's own plugin posts a codex entry.</summary>
public class ARegionFirstIsPostedToCanonnTests
{
    private const string Line =
        "{\"timestamp\":\"2026-10-10T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":2311001,"
        + "\"Name\":\"$Codex_Ent_Fungoida_02_Green_Name;\",\"Name_Localised\":\"Fungoida Bullarum - Green\","
        + "\"Region\":\"$Codex_RegionName_18;\",\"Region_Localised\":\"Inner Orion Spur\",\"System\":\"Sol\","
        + "\"SystemAddress\":10477373803,\"BodyID\":5,\"Latitude\":-12.5,\"Longitude\":101.25,\"IsNewEntry\":true}";

    private static readonly RegionFirstRecord Record = new(
        2311001,
        "Fungoida Bullarum - Green",
        18,
        "Inner Orion Spur",
        CodexFirst.Region,
        "Sol",
        10477373803,
        new StarPosition(1.5, -2.25, 30.0),
        5,
        "Sol 3 a",
        -12.5,
        101.25,
        new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero),
        Line,
        "2026-10-19");

    private static readonly RegionFirstReport Report = new(Record, "Ada Vance", Odyssey: true, "Directive 47 1.81.0");

    private sealed class Answer(HttpStatusCode status) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string? Type, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method,
                request.RequestUri!,
                request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status) { Content = new StringContent("ok") };
        }
    }

    private static CanonnBiologyService Service(Answer answer) =>
        new(NullLogger<CanonnBiologyService>.Instance, new HttpClient(answer));

    [Fact]
    public async Task TheBodyCarriesTheEventThePlaceAndTheCommander()
    {
        var answer = new Answer(HttpStatusCode.OK);
        using var service = Service(answer);

        Assert.True(await service.SendAsync(Report, TestContext.Current.CancellationToken));

        var (method, uri, type, body) = Assert.Single(answer.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal($"https://{CanonnBiologyService.Host}/postEvent", uri.ToString());
        Assert.Equal("application/json", type);

        using var expected = JsonDocument.Parse(
            "{\"gameState\":{\"systemName\":\"Sol\",\"systemCoordinates\":[1.5,-2.25,30],\"bodyName\":\"Sol 3 a\","
            + "\"latitude\":-12.5,\"longitude\":101.25,\"clientVersion\":\"Directive 47 1.81.0\",\"isBeta\":false,"
            + "\"platform\":\"PC\",\"odyssey\":true},\"rawEvent\":" + Line + ",\"eventType\":\"CodexEntry\","
            + "\"cmdrName\":\"Ada Vance\"}");
        using var sent = JsonDocument.Parse(body);

        Assert.True(
            JsonElement.DeepEquals(expected.RootElement, sent.RootElement),
            $"Sent {body}");
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task AnythingButA200IsAFailure(HttpStatusCode status)
    {
        using var service = Service(new Answer(status));

        Assert.False(await service.SendAsync(Report, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ARecordWithNoPositionOrLineIsNotPosted()
    {
        var answer = new Answer(HttpStatusCode.OK);
        using var service = Service(answer);

        Assert.False(await service.SendAsync(Report with { Record = Record with { StarPos = null } }, TestContext.Current.CancellationToken));
        Assert.False(await service.SendAsync(Report with { Record = Record with { Journal = "not json" } }, TestContext.Current.CancellationToken));
        Assert.False(await service.SendAsync(Report with { Record = Record with { Journal = null } }, TestContext.Current.CancellationToken));
        Assert.Empty(answer.Requests);
    }
}
