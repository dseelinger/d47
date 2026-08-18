using System.Globalization;
using System.Text;
using D47.Core.Goals;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Goals;

/// <summary>
/// The walk that gives every arc its age (list.md Phase 34).
/// <para>
/// Journals written by hand rather than replayed, for the reason <see cref="Habits.HabitMinerTests"/>
/// records: the real corpus is on one machine and CI has none, so what these prove is that the
/// arithmetic is the arithmetic.
/// </para>
/// </summary>
public class GoalMinerTests : IDisposable
{
    private const string Cmdr = "F1234567";

    private static readonly DateTimeOffset Start = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-goal-tests", Guid.NewGuid().ToString("N"));

    private int _files;

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void JumpsAreCountedAsDistinctSystemsAndSummedLightYears()
    {
        Journal(
            Jump(Start, "Sol", 8.6)
            + Jump(Start.AddHours(1), "Alpha Centauri", 4.4)
            + Jump(Start.AddHours(2), "Sol", 4.4));

        var mine = Assert.Single(Mine());

        Assert.Equal(2, mine.For(GoalCatalogue.Systems)?.Have);
        Assert.Equal(17, mine.For(GoalCatalogue.Distance)?.Have);
    }

    /// <summary>
    /// The one figure the whole phase turns on: <em>how long it has been running</em>. It is the
    /// first evidence in the corpus, not the first journal in it.
    /// </summary>
    [Fact]
    public void AnArcStartsAtTheFirstEvidenceRatherThanAtTheFirstJournal()
    {
        Journal(Idle(Start));
        Journal(Jump(Start.AddDays(30), "Sol", 8.6));

        var mine = Assert.Single(Mine());

        Assert.Equal(Start.AddDays(30), mine.For(GoalCatalogue.Systems)?.Started);
    }

    /// <summary>
    /// A career nobody has begun has no start date, and that is a real answer — it is what stops a
    /// Commander at rank 0 in Exobiology being told they have been at it for a year.
    /// </summary>
    [Fact]
    public void ARankOfNothingIsNotAStart()
    {
        Journal(Line(Start, "Rank", "\"Combat\":2,\"Exobiologist\":0") + "\n");

        var mine = Assert.Single(Mine());

        Assert.Equal(Start, mine.For("rank.combat")?.Started);
        Assert.Equal(2, mine.For("rank.combat")?.Have);

        Assert.Null(mine.For("rank.exobiologist")?.Started);
        Assert.Equal(0, mine.For("rank.exobiologist")?.Have);
    }

    [Fact]
    public void ThePromotionMovesTheMinedRankAndTheStartStaysWhereItWas()
    {
        Journal(Line(Start, "Rank", "\"Explore\":4") + "\n");
        Journal(Line(Start.AddDays(60), "Promotion", "\"Explore\":5") + "\n");

        var mine = Assert.Single(Mine());

        Assert.Equal(5, mine.For("rank.explore")?.Have);
        Assert.Equal(Start, mine.For("rank.explore")?.Started);
        Assert.Equal(Start.AddDays(60), mine.For("rank.explore")?.AsOf);
    }

    /// <summary>
    /// <b>A rank that goes down is a save started again, and the corpus proved it.</b> One of the
    /// three accounts in the real folder reports Trade 7 in July, Trade 2 in January and Trade 0 in
    /// June — a Frontier id is an account, and a person can begin a new Commander inside one. Dating
    /// the arc from the first character's first rank would report a year of progress towards a
    /// ladder that has since been restarted from nothing.
    /// </summary>
    [Fact]
    public void ARankThatFallsRestartsTheArcRatherThanKeepingItsOldStart()
    {
        Journal(Rank(Start, 7));
        Journal(Rank(Start.AddDays(180), 2));

        var mine = Assert.Single(Mine());

        Assert.Equal(2, mine.For("rank.trade")?.Have);
        Assert.Equal(Start.AddDays(180), mine.For("rank.trade")?.Started);
    }

    [Fact]
    public void ARankBackToNothingLeavesTheArcUnstarted()
    {
        Journal(Rank(Start, 7));
        Journal(Rank(Start.AddDays(180), 0));

        var mine = Assert.Single(Mine());

        Assert.Equal(0, mine.For("rank.trade")?.Have);
        Assert.Null(mine.For("rank.trade")?.Started);
    }

    /// <summary>
    /// The same rule for the engineers, which is where a maximum would have been most misleading:
    /// the account above would have been told it had twenty-three unlocked when the current save
    /// has four.
    /// </summary>
    [Fact]
    public void EngineersFollowTheLatestSnapshotRatherThanTheLargest()
    {
        Journal(Engineers(Start, unlocked: 23));
        Journal(Engineers(Start.AddDays(180), unlocked: 4));

        var mine = Assert.Single(Mine());

        Assert.Equal(4, mine.For(GoalCatalogue.Engineers)?.Have);
        Assert.Equal(Start.AddDays(180), mine.For(GoalCatalogue.Engineers)?.Started);
    }

    /// <summary>
    /// The snapshot counts and the delta does not. A single-engineer <c>EngineerProgress</c> is a
    /// rank-up, and counting it as a total would report one unlocked engineer the moment anybody
    /// ranked up with one.
    /// </summary>
    [Fact]
    public void OnlyTheEngineerSnapshotIsCountedAsATotal()
    {
        Journal(Engineers(Start, unlocked: 4));
        Journal(Line(Start.AddDays(1), "EngineerProgress", "\"Engineer\":\"Felicity Farseer\",\"EngineerID\":300100,\"Progress\":\"Unlocked\",\"Rank\":5") + "\n");

        Assert.Equal(4, Assert.Single(Mine()).For(GoalCatalogue.Engineers)?.Have);
    }

    [Fact]
    public void HullsAreCountedFromEveryPlaceElitePutsOne()
    {
        Journal(Line(Start, "Loadout", "\"Ship\":\"krait_mkii\"") + "\n");
        Journal(Line(Start.AddDays(1), "ShipyardBuy", "\"ShipType\":\"adder\"") + "\n");
        Journal(Line(Start.AddDays(2), "Loadout", "\"Ship\":\"krait_mkii\"") + "\n");

        Assert.Equal(2, Assert.Single(Mine()).For(GoalCatalogue.Ships)?.Have);
    }

    /// <summary>
    /// Three accounts in one folder is what a long corpus really looks like, and pooling them
    /// would report one person's progress to another. The same assertion Phase 32 made, because
    /// this is the same folder.
    /// </summary>
    [Fact]
    public void TwoCommandersAreMinedApart()
    {
        Journal(Jump(Start, "Sol", 8.6), "F1111111");
        Journal(Jump(Start, "Sol", 8.6) + Jump(Start.AddHours(1), "Lave", 40.2), "F2222222");

        var mines = Mine();

        Assert.Equal(2, mines.Count);
        Assert.Equal(1, mines.Single(mine => mine.FrontierId == "F1111111").For(GoalCatalogue.Systems)?.Have);
        Assert.Equal(2, mines.Single(mine => mine.FrontierId == "F2222222").For(GoalCatalogue.Systems)?.Have);
    }

    [Fact]
    public void AFolderWithNothingInItMinesToNothingRatherThanThrowing()
    {
        Assert.Empty(new GoalMiner(NullLogger<GoalMiner>.Instance)
            .Mine(Path.Combine(_folder, "nowhere"), Start));
    }

    private IReadOnlyList<GoalMine> Mine() =>
        new GoalMiner(NullLogger<GoalMiner>.Instance).Mine(Files(), Start.AddYears(1));

    private IReadOnlyList<string> Files() =>
        [.. Directory.EnumerateFiles(_folder, "Journal.*.log").OrderBy(Path.GetFileName, StringComparer.Ordinal)];

    private void Journal(string body, string commander = Cmdr)
    {
        Directory.CreateDirectory(_folder);

        var text = new StringBuilder()
            .AppendLine(Line(Start, "Fileheader", string.Empty))
            .AppendLine(Line(Start, "Commander", $"\"FID\":\"{commander}\""))
            .Append(body)
            .ToString();

        File.WriteAllText(Path.Combine(_folder, $"Journal.{_files++:D5}.01.log"), text);
    }

    private static string Idle(DateTimeOffset at) => Line(at, "Music", "\"MusicTrack\":\"NoTrack\"") + "\n";

    private static string Rank(DateTimeOffset at, int trade) =>
        Line(at, "Rank", $"\"Trade\":{trade.ToString(CultureInfo.InvariantCulture)}") + "\n";

    private static string Jump(DateTimeOffset at, string system, double distance) =>
        Line(
            at,
            "FSDJump",
            $"\"StarSystem\":\"{system}\",\"JumpDist\":{distance.ToString("0.00", CultureInfo.InvariantCulture)}")
        + "\n";

    private static string Engineers(DateTimeOffset at, int unlocked)
    {
        var rows = Enumerable.Range(0, unlocked)
            .Select(index => $"{{ \"Engineer\":\"E{index}\", \"EngineerID\":{300100 + index}, \"Progress\":\"Unlocked\" }}");

        return Line(at, "EngineerProgress", $"\"Engineers\":[{string.Join(",", rows)}]") + "\n";
    }

    private static string Line(DateTimeOffset at, string kind, string fields) =>
        $"{{ \"timestamp\":\"{at.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"{kind}\""
        + (fields.Length == 0 ? string.Empty : ", " + fields)
        + " }";
}
