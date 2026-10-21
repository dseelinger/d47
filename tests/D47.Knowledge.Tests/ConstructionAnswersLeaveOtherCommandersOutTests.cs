using System.Net;
using System.Text;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>What Raven Colonial returns about other people does not reach the tools' answers.</summary>
public class ConstructionAnswersLeaveOtherCommandersOutTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private const string Sites = """[{"id":"x1","name":"Alpha","bodyNum":2,"buildType":"coriolis","status":"build","buildId":"b-1","marketId":1}]""";

    private const string Project = """
        {
          "buildId":"b-1","complete":false,
          "commodities":{"steel":120},
          "linkedFC":[{"marketId":"1","name":"AAA-111","displayName":"SECRET CARRIER","assign":[]}],
          "commanders":{"CMDR One":["steel"],"CMDR Two":[]},
          "notes":"meet in the bar","discordLink":"https://discord.example/secret",
          "architectName":"CMDR Architect"
        }
        """;

    private sealed class Fake : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("/sites", StringComparison.Ordinal) ? Sites
                : path.EndsWith("/bodies", StringComparison.Ordinal) ? """[{"name":"Sys A 1","num":2}]"""
                : path.EndsWith("/fc", StringComparison.Ordinal) ? """{"1":{"steel":30}}"""
                : Project;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task NotesDiscordLinksAndCommanderNamesAreNotInEitherAnswer()
    {
        var gameState = new GameStateStore();
        gameState.Apply(JournalEvent.TryParse("""{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""", NullLogger.Instance, out var commander) ? commander! : throw new InvalidOperationException());

        foreach (var line in new[]
        {
            """{"timestamp":"2026-09-29T09:00:00Z","event":"Location","StarSystem":"Sys A","SystemAddress":1}""",
            """{"timestamp":"2026-09-29T09:01:00Z","event":"Docked","StationName":"Site One","MarketID":1,"StarSystem":"Sys A"}""",
            $$"""{"timestamp":"{{Now.AddDays(-1):yyyy-MM-ddTHH:mm:ssZ}}","event":"ColonisationConstructionDepot","MarketID":1,"ConstructionProgress":0.2,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":400,"ProvidedAmount":0,"Payment":100}]}""",
        })
        {
            gameState.Apply(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException());
        }

        using var service = new RavenColonialPlanService(
            NullLogger<RavenColonialPlanService>.Instance, new HttpClient(new Fake()), () => Now);

        var plans = new ColonisationPlans(service, () => true, () => gameState.Active, NullLogger<ColonisationPlans>.Instance);
        var registry = CapabilityRegistry.Build([ColonisationCapability.Create(() => gameState.Active, now: () => Now, plans: () => plans)]);

        foreach (var tool in new[] { "get_construction_sites", "get_construction_needs" })
        {
            var result = await registry.InvokeAsync(tool, ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken);

            Assert.Contains(
                tool == "get_construction_sites" ? "Coriolis" : "2 Commanders and 1 linked carrier",
                result.Content,
                StringComparison.Ordinal);

            foreach (var forbidden in new[] { "meet in the bar", "discord", "CMDR", "SECRET CARRIER", "AAA-111", "b-1" })
            {
                Assert.DoesNotContain(forbidden, result.Content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
