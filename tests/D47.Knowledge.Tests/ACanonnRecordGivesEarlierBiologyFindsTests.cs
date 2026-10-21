using System.Net;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class ACanonnRecordGivesEarlierBiologyFindsTests
{
    private const string Sample =
        """
        {"codex":[
          {"body":"C 3 a","entryid":2100201,"english_name":"Bacterium Aurasus - Green","hud_category":"Biology","latitude":"-61.5","longitude":"125.25"},
          {"body":"Sol Earth","entryid":2100202,"english_name":"Stratum Tectonicas - Red","hud_category":"Biology","latitude":"1","longitude":"2"},
          {"body":null,"entryid":2100203,"english_name":"No Body","hud_category":"Biology","latitude":"1","longitude":"2"},
          {"body":"C 3 a","entryid":1400114,"english_name":"Silicate Vapour Fumarole","hud_category":"Geology","latitude":"1","longitude":"2"},
          {"body":"C 3 a","entryid":2100204,"english_name":"Bad Latitude","hud_category":"Biology","latitude":"north","longitude":"2"},
          {"body":"C 3 a","entryid":2100205,"english_name":"Null Longitude","hud_category":"Biology","latitude":"1","longitude":null}
        ]}
        """;

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static CanonnBiologyService Service(Answer answer) =>
        new(NullLogger<CanonnBiologyService>.Instance, new HttpClient(answer));

    [Fact]
    public async Task ABiologyRowIsReadWithTheBodyNameBuilt()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, Sample));

        var finds = await service.SystemFindsAsync("Sol", CancellationToken.None);

        Assert.Equal(new EarlierFind("Sol C 3 a", 2100201, "Bacterium Aurasus - Green", -61.5, 125.25), finds[0]);
    }

    [Fact]
    public async Task ABodyThatAlreadyStartsWithTheSystemIsUsedAsItIs()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, Sample));

        var finds = await service.SystemFindsAsync("Sol", CancellationToken.None);

        Assert.Equal("Sol Earth", finds[1].Body);
    }

    [Fact]
    public async Task RowsWithoutABodyOrOutsideBiologyOrWithBadCoordinatesAreDropped()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, Sample));

        var finds = await service.SystemFindsAsync("Sol", CancellationToken.None);

        Assert.Equal(2, finds.Count);
    }

    [Fact]
    public async Task AResponseWithNoCodexKeyGivesAnEmptyList()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, """{"system":"Nowhere","SAAsignals":[]}"""));

        Assert.Empty(await service.SystemFindsAsync("Nowhere", CancellationToken.None));
    }

    [Fact]
    public async Task AFailedRequestGivesAnEmptyList()
    {
        using var service = Service(new Answer(HttpStatusCode.InternalServerError, "oops"));

        Assert.Empty(await service.SystemFindsAsync("Sol", CancellationToken.None));
    }

    [Fact]
    public async Task TheRequestCarriesTheEscapedSystemAndNothingElse()
    {
        var answer = new Answer(HttpStatusCode.OK, "{}");
        using var service = Service(answer);

        await service.SystemFindsAsync("Synuefe XO-P c22-17", CancellationToken.None);

        var request = Assert.Single(answer.Requests);
        Assert.Equal(CanonnBiologyService.Host, request.Host);
        Assert.Equal("/query/getSystemPoi", request.AbsolutePath);
        Assert.Equal("?system=Synuefe%20XO-P%20c22-17", request.Query);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ARecordedResponseForSynuefeXoPC2217IsRead()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, Fixture.Text("canonn-systempoi-synuefe-xo-p-c22-17.json")));

        var finds = await service.SystemFindsAsync("Synuefe XO-P c22-17", CancellationToken.None);

        Assert.Equal(6, finds.Count);
        Assert.All(finds, find => Assert.StartsWith("Synuefe XO-P c22-17 ", find.Body, StringComparison.Ordinal));
        Assert.Contains(finds, find => find.Body == "Synuefe XO-P c22-17 D 3" && find.Name == "Roseum Brain Tree");
    }
}
