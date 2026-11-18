using System.Net;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class ACanonnSystemRecordKeepsEverySectionTests
{
    private static CommunitySystemRecord Synuefe()
    {
        using var document = Fixture.Json("canonn-systempoi-synuefe-full.json");
        return CanonnBiologyService.ReadRecord(document);
    }

    [Fact]
    public void AGeologyCodexRowIsKept()
    {
        var row = Synuefe().Codex.First(report => report.Category == "Geology");

        Assert.Equal(new CodexReport("C 3 a", "Silicate Vapour Fumarole", 1400114, "Geology", null, -7.14649, 86.98011, "false"), row);
    }

    [Fact]
    public void AnOrganicScanWithoutAPositionIsKept()
    {
        var row = Synuefe().OrganicScans.First(report => report.Latitude is null);

        Assert.Equal(new CodexReport("AB 4", "Stratum Tectonicas - Lime", 2420702, "Biology", null, null, null, "false"), row);
    }

    [Fact]
    public void ASurfaceSignalRowIsKept()
    {
        var row = Assert.Single(Synuefe().Signals);

        Assert.Equal(new SignalReport("C 1", "Guardian", "Guardian", 1), row);
    }

    [Fact]
    public void EveryRowOfEachSectionIsKept()
    {
        var record = Synuefe();

        Assert.Equal(4, record.Codex.Count);
        Assert.Equal(2, record.OrganicScans.Count);
    }

    [Fact]
    public async Task TheServiceAnswersWithTheWholeRecord()
    {
        using var service = Service(HttpStatusCode.OK, Fixture.Text("canonn-systempoi-synuefe-full.json"));

        var record = await service.SystemRecordAsync("Synuefe XO-P c22-17", CancellationToken.None);

        Assert.Equal(4, record?.Codex.Count);
    }

    [Fact]
    public async Task AFailedRequestGivesNoRecord()
    {
        using var service = Service(HttpStatusCode.InternalServerError, "oops");

        Assert.Null(await service.SystemRecordAsync("Synuefe XO-P c22-17", CancellationToken.None));
    }

    private static CanonnBiologyService Service(HttpStatusCode status, string body) =>
        new(NullLogger<CanonnBiologyService>.Instance, new HttpClient(new Answer(status, body)));

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
