using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingHaveIMetThisCommanderTests
{
    private static string Target(string at, string name, string ship) =>
        $$"""{"timestamp":"{{at}}","event":"ShipTargeted","TargetLocked":true,"PilotName":"$cmdr_decorate:#name={{name}};","Ship_Localised":"{{ship}}"}""";

    private static readonly string[] Earlier =
    [
        """{"timestamp":"2026-10-01T10:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
        """{"timestamp":"2026-10-01T10:01:00Z","event":"Location","StarSystem":"Sol"}""",
        Target("2026-10-01T10:05:00Z", "Arden Vale", "Python"),
        """{"timestamp":"2026-10-01T10:06:00Z","event":"Interdicted","Interdictor":"Cmdr Arden Vale","IsPlayer":true,"Submitted":false}""",
        """{"timestamp":"2026-10-03T10:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
        """{"timestamp":"2026-10-03T10:01:00Z","event":"FSDJump","StarSystem":"Shinrarta Dezhra"}""",
        Target("2026-10-03T10:05:00Z", "Arden Vale", "Python"),
        Target("2026-10-03T10:06:00Z", "Bo Reeve", "Cobra Mk V"),
    ];

    private static readonly string[] ThisSession =
    [
        """{"timestamp":"2026-10-05T10:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
        """{"timestamp":"2026-10-05T10:01:00Z","event":"Location","StarSystem":"Colonia"}""",
        Target("2026-10-05T10:05:00Z", "Cass Ode", "Anaconda"),
    ];

    private static readonly string[] NothingTargetedYet =
    [
        """{"timestamp":"2026-10-05T10:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
    ];

    private static CommanderLog Log(bool folded, params string[][] sessions)
    {
        var log = new CommanderLog(new MemoryInstall().Files, NullLogger<CommanderLog>.Instance);
        log.Fold(sessions.SelectMany(s => s));

        if (folded)
        {
            log.FoldHistory([]);
        }

        return log;
    }

    private static CapabilityRegistry Registry(CommanderLog log) =>
        CapabilityRegistry.Build([CommanderLogCapability.Create(log, () => "F1")]);

    private static async Task<string> Ask(CommanderLog log, string json = "{}") =>
        (await Registry(log).InvokeAsync(
            "get_commander_log", ToolArguments.FromJson(json), TestContext.Current.CancellationToken)).Content;

    [Fact]
    public async Task ANamedCommanderListsEncountersNewestFirstWithSystemShipAndKinds()
    {
        var answer = await Ask(Log(true, Earlier), """{"name":"CMDR arden vale"}""");

        Assert.Equal(
            "Arden Vale: 2 encounters:\n"
            + "2026-10-03 10:05 UTC, Shinrarta Dezhra, Python: targeted\n"
            + "2026-10-01 10:06 UTC, Sol, Python: targeted, interdicted you",
            answer);
    }

    [Fact]
    public async Task WithoutANameItAnswersForTheCommanderTargetedThisSession()
    {
        var answer = await Ask(Log(true, Earlier, ThisSession));

        Assert.StartsWith("Cass Ode: 1 encounter:", answer);
        Assert.DoesNotContain("Arden", answer);
    }

    [Fact]
    public async Task WithoutANameOrATargetItListsTheCommandersMetMostRecently()
    {
        var answer = await Ask(Log(true, Earlier, NothingTargetedYet));

        Assert.StartsWith("No Commander targeted this session. Most recently met, of 2:", answer);
        Assert.Contains("Bo Reeve, 1 encounter, last: 2026-10-03 10:06 UTC, Shinrarta Dezhra, Cobra Mk V: targeted", answer);
        Assert.Contains("Arden Vale, 2 encounters", answer);
    }

    [Fact]
    public async Task WhileOlderJournalsAreStillBeingReadTheAnswerSaysSo()
    {
        var answer = await Ask(Log(false, Earlier), """{"name":"Arden Vale"}""");

        Assert.StartsWith("Older journals are still being read, so this is what is known so far. Arden Vale: 2 encounters", answer);
    }

    [Fact]
    public async Task AnUnknownNameSaysNoneWereFound()
    {
        Assert.Equal(
            "No encounters with Nobody in the journals read.",
            await Ask(Log(true, Earlier), """{"name":"Nobody"}"""));
    }

    [Fact]
    public async Task TheModelMayCallTheTool()
    {
        var result = await Registry(Log(true, Earlier)).InvokeAsync(
            "get_commander_log", ToolArguments.Empty, TestContext.Current.CancellationToken, ToolCaller.Model);

        Assert.False(result.IsError);
    }

    [Fact]
    public void HaveIMetThisCommanderIsAnsweredByTheKeywordRouter()
    {
        var router = new KeywordRouter(Registry(Log(true, Earlier)));

        Assert.Equal("get_commander_log", router.MatchToolCommand("have I met this commander")?.ToolName);
    }

    [Fact]
    public void TheLanguageModelDisclosureSaysAskingByNameSendsTheNameAndEncounters()
    {
        var what = EgressDisclosure.Entry(
            EgressDisclosure.LanguageModel,
            D47Settings.Defaults with { Llm = D47Settings.Defaults.Llm with { Provider = LlmProviderCatalog.AnthropicId } },
            llmKeyPresent: true).What;

        Assert.Contains("sends that Commander's name and your encounters with them", what);
    }
}
