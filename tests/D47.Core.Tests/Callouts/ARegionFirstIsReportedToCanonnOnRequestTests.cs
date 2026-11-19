using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A recorded region first goes to Canonn when the Commander says so, once, with their name.</summary>
public class ARegionFirstIsReportedToCanonnOnRequestTests
{
    private const long Organic = 2311001;

    private const long Other = 2311002;

    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly CodexTable Table = CodexTable.Parse(
        "# Table date: 2026-10-19\n"
        + Organic + "\tFungoida Bullarum - Green\tBiology\tOrganic\t4:12\n"
        + Other + "\tFungoida Bullarum - Blue\tBiology\tOrganic\t4:12\n");

    private readonly MemoryFileSystem _files = new();

    private readonly List<Action> _offTick = [];

    private sealed class Canonn(bool answers) : IRegionFirstReport
    {
        public List<RegionFirstReport> Sent { get; } = [];

        public bool Answers { get; set; } = answers;

        public Task<bool> SendAsync(RegionFirstReport report, CancellationToken cancellationToken)
        {
            Sent.Add(report);
            return Task.FromResult(Answers);
        }
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Jump(string time) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"Sol\",\"SystemAddress\":10477373803"
        + ",\"StarPos\":[1.5,-2.25,30.0]}";

    private static string Codex(string time, long id) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id + ",\"Name\":\"$Codex_Ent_" + id
        + ";\",\"Name_Localised\":\"Entry " + id + "\",\"Category\":\"$Codex_Category_Biology;\",\"SubCategory\":"
        + "\"$Codex_SubCategory_Organic_Structures;\",\"Region\":\"$Codex_RegionName_18;\",\"Region_Localised\":"
        + "\"Inner Orion Spur\",\"System\":\"Sol\",\"SystemAddress\":10477373803,\"BodyID\":5,\"Latitude\":-12.5"
        + ",\"Longitude\":101.25,\"IsNewEntry\":true}";

    private RegionFirstBook Book(params string[] lines)
    {
        var book = new RegionFirstBook(@"C:\d47-data\region-firsts.json", _files, NullLogger.Instance, _offTick.Add);
        book.Observe("F1", CodexLedger.None.With(lines.Aggregate(CodexLedger.None, (ledger, line) => ledger.Apply(Event(line)))), Table);
        return book;
    }

    private RegionFirstBook TwoFinds() => Book(
        Jump("2026-10-10T09:00:00Z"),
        Codex("2026-10-10T10:00:00Z", Organic),
        Codex("2026-10-11T10:00:00Z", Other));

    private static CommanderGameState Commander(string name = "Ada Vance", bool odyssey = true)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", name));
        state.Apply(Event(
            "{\"timestamp\":\"2026-11-19T11:00:00Z\",\"event\":\"LoadGame\",\"FID\":\"F1\",\"Commander\":\"" + name
            + "\",\"Horizons\":true,\"Odyssey\":" + (odyssey ? "true" : "false") + "}"));
        return state;
    }

    private static RegionFirstReporting Reporting(RegionFirstBook book, Canonn canonn, CommanderGameState? commander = null) =>
        new(() => commander ?? Commander(), book, canonn, "Directive 47 1.81.0", () => Now);

    private static Task<ToolResult> Report(RegionFirstReporting reporting, ToolArguments? arguments = null) =>
        reporting.ReportAsync(arguments ?? ToolArguments.Empty, TestContext.Current.CancellationToken);

    private static ToolArguments Pick(long entryId, int regionId) =>
        new(new Dictionary<string, string>
        {
            ["entry_id"] = entryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["region_id"] = regionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

    [Fact]
    public async Task TheNewestUnreportedFindIsSentWithTheCommandersName()
    {
        var canonn = new Canonn(answers: true);
        var book = TwoFinds();

        var result = await Report(Reporting(book, canonn));

        Assert.Equal(RegionFirstReporting.Sent, result.Content);
        var sent = Assert.Single(canonn.Sent);
        Assert.Equal((Other, 18, "Ada Vance", true, "Directive 47 1.81.0"), (sent.Record.EntryId, sent.Record.RegionId, sent.CommanderName, sent.Odyssey, sent.ClientVersion));
    }

    [Fact]
    public async Task ASentFindIsMarkedReportedAndNotSentAgain()
    {
        var canonn = new Canonn(answers: true);
        var book = TwoFinds();
        var reporting = Reporting(book, canonn);

        await Report(reporting);
        await Report(reporting);
        var third = await Report(reporting);

        Assert.Equal([Other, Organic], canonn.Sent.Select(report => report.Record.EntryId));
        Assert.Equal(Now, book.Find("F1", Other, 18)?.Reported);
        Assert.Equal(RegionFirstReporting.NothingWaiting, third.Content);

        var again = await Report(reporting, Pick(Organic, 18));

        Assert.Equal(RegionFirstReporting.AlreadySent, again.Content);
        Assert.Equal(2, canonn.Sent.Count);
    }

    [Fact]
    public async Task TheMarkIsWrittenOffTheCallersThread()
    {
        var book = TwoFinds();
        foreach (var work in _offTick)
        {
            work();
        }

        _offTick.Clear();

        await Report(Reporting(book, new Canonn(answers: true)));

        Assert.Single(_offTick);
        _offTick[0]();

        var reopened = new RegionFirstBook(@"C:\d47-data\region-firsts.json", _files, NullLogger.Instance, _ => { });
        Assert.Equal(Now, reopened.Find("F1", Other, 18)?.Reported);
    }

    [Fact]
    public async Task AFailedSendLeavesTheFindUnreported()
    {
        var canonn = new Canonn(answers: false);
        var book = TwoFinds();
        var reporting = Reporting(book, canonn);

        var result = await Report(reporting);

        Assert.Equal(RegionFirstReporting.Unreachable, result.Content);
        Assert.Null(book.Find("F1", Other, 18)?.Reported);

        canonn.Answers = true;
        await Report(reporting);

        Assert.Equal([Other, Other], canonn.Sent.Select(report => report.Record.EntryId));
    }

    [Fact]
    public async Task ANamedFindIsTheOneSent()
    {
        var canonn = new Canonn(answers: true);

        var result = await Report(Reporting(TwoFinds(), canonn), Pick(Organic, 18));

        Assert.Equal(RegionFirstReporting.Sent, result.Content);
        Assert.Equal(Organic, Assert.Single(canonn.Sent).Record.EntryId);
    }

    [Fact]
    public async Task AFindThatIsNotRecordedIsNotSent()
    {
        var canonn = new Canonn(answers: true);

        var result = await Report(Reporting(TwoFinds(), canonn), Pick(Organic, 17));

        Assert.Equal(RegionFirstReporting.NotFound, result.Content);
        Assert.Empty(canonn.Sent);
    }

    [Fact]
    public async Task NothingIsSentWithoutACommanderName()
    {
        var canonn = new Canonn(answers: true);

        var result = await Report(Reporting(TwoFinds(), canonn, Commander(name: " ")));

        Assert.Equal(RegionFirstReporting.NoName, result.Content);
        Assert.Empty(canonn.Sent);
    }

    [Fact]
    public async Task WithNothingRecordedThereIsNothingWaiting()
    {
        var canonn = new Canonn(answers: true);

        var result = await Report(Reporting(Book(), canonn));

        Assert.Equal(RegionFirstReporting.NothingWaiting, result.Content);
        Assert.Empty(canonn.Sent);
    }

    [Fact]
    public async Task AFindWithNoPositionIsNotSent()
    {
        var canonn = new Canonn(answers: true);
        var book = Book(Codex("2026-10-10T10:00:00Z", Organic));

        var newest = await Report(Reporting(book, canonn));
        var named = await Report(Reporting(book, canonn), Pick(Organic, 18));

        Assert.Equal(RegionFirstReporting.NothingWaiting, newest.Content);
        Assert.Equal(RegionFirstReporting.NothingToSend, named.Content);
        Assert.Empty(canonn.Sent);
    }

    [Fact]
    public async Task TheModelIsRefusedAndTheCommanderIsNot()
    {
        var canonn = new Canonn(answers: true);
        var registry = CapabilityRegistry.Build([CalloutCapability.Create(() => string.Empty, Reporting(TwoFinds(), canonn))]);

        var refused = await registry.InvokeAsync(RegionFirstReporting.Tool, ToolArguments.Empty, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Empty(canonn.Sent);

        var result = await registry.InvokeAsync(RegionFirstReporting.Tool, ToolArguments.Empty, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(RegionFirstReporting.Sent, result.Content);
        Assert.Single(canonn.Sent);
    }

    [Theory]
    [InlineData("report it to canonn")]
    [InlineData("Report it to Cannon.")]
    [InlineData("report that to cannon")]
    [InlineData("report that to canonn")]
    [InlineData("report my region first")]
    [InlineData("report my region first to cannon")]
    public void ThePhrasesReachTheTool(string said)
    {
        var router = new KeywordRouter(CapabilityRegistry.Build([CalloutCapability.Create(() => string.Empty)]));

        Assert.Equal(RegionFirstReporting.Tool, router.MatchToolCommand(said)?.ToolName);
    }
}
