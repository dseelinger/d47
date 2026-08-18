using System.Globalization;
using System.Text;
using D47.Core.Habits;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Habits;

/// <summary>
/// The batch job over journals that already exist (list.md Phase 32, "Mine the Commander's own
/// journals").
/// <para>
/// The journals here are written by hand rather than replayed from a corpus, and that is
/// deliberate: the real corpus is on one machine and CI has none, so what these prove is that the
/// arithmetic is the arithmetic — a detector that counts nine of two thousand three hundred and
/// seventy on real data has to count two of four on four lines of JSON, or the number on real data
/// means nothing.
/// </para>
/// </summary>
public class HabitMinerTests : IDisposable
{
    private const string Cmdr = "F1234567";

    private static readonly DateTimeOffset Start = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-habit-tests", Guid.NewGuid().ToString("N"));

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
    public void AHandfulOfJournalsProducesNoClaimAndSaysWhy()
    {
        Journal(Submitted(Start, times: 40));

        var report = Assert.Single(Mine());

        Assert.Empty(report.Claims);
        Assert.NotNull(report.Refusal);
        Assert.Contains("20", report.Refusal, StringComparison.Ordinal);

        // Item 2's whole point: forty submissions is a pattern by any arithmetic, and one journal
        // is not enough of a person to claim one from. The floor beats the count.
        Assert.Empty(report.Quiet);
    }

    [Fact]
    public void AboveTheFloorTheSubmissionRateIsClaimedWithItsDenominator()
    {
        Spread(20, index => Submitted(Start.AddDays(index), times: 1, submitted: index != 0));

        var claim = ClaimOf(Mine(), "interdiction-submit");

        Assert.Equal(19, claim.Evidence.Occurrences);
        Assert.Equal(20, claim.Evidence.Opportunities);
        Assert.Contains("19 of 20 interdictions", claim.Spoken(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The assertion that keeps the phase honest. A claim is never allowed to be a bare
    /// <em>you always</em>, so the rendered sentence has to carry the numbers by construction
    /// rather than by whoever wrote the caller remembering to add them.
    /// </summary>
    [Fact]
    public void EveryClaimSpeaksItsCountItsDenominatorAndItsWindow()
    {
        Spread(24, index => Submitted(Start.AddDays(index), times: 1, submitted: index % 4 != 0));

        var claim = ClaimOf(Mine(), "interdiction-submit");

        Assert.DoesNotContain("always", claim.Spoken(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("18 of 24", claim.Spoken(), StringComparison.Ordinal);
        Assert.Contains("24 of your journals", claim.Explained(), StringComparison.Ordinal);
        Assert.Contains(claim.Evidence.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            claim.Explained(), StringComparison.Ordinal);
    }

    [Fact]
    public void TooFewChancesIsReportedAsTooFewRatherThanAsNothingToSay()
    {
        // Twenty-five journals, so the corpus floor is met — but only eight interdictions in them,
        // which is below the denominator floor. The distinction item 2 spends its length on.
        Spread(25, index => index < 8 ? Submitted(Start.AddDays(index), times: 1) : Idle(Start.AddDays(index)));

        var report = Assert.Single(Mine());

        Assert.DoesNotContain(report.Claims, claim => claim.Key == "interdiction-submit");

        var quiet = Assert.Single(report.Quiet, silence => silence.Key == "interdiction-submit");

        Assert.Contains("8 interdictions", quiet.Why, StringComparison.Ordinal);
        Assert.Contains("too few", quiet.Why, StringComparison.Ordinal);
    }

    /// <summary>
    /// The detector that ships having found nothing, and the reason it ships (list.md Phase 32).
    /// A Commander who lands only on light worlds is told that, not told there is nothing to say.
    /// </summary>
    [Fact]
    public void LandingOnlyOnLightWorldsIsReportedAsSuchRatherThanAsSilence()
    {
        Spread(25, index => Landing(Start.AddDays(index), "Bodge 1 a", gravity: 0.21));

        var quiet = Assert.Single(Assert.Single(Mine()).Quiet, silence => silence.Key == "high-gravity");

        Assert.Contains("not landed anywhere over one g", quiet.Why, StringComparison.Ordinal);
        Assert.Contains("0.21 g", quiet.Why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A body's gravity arrives in a Scan that may be months away from the landing on it, so the
    /// join happens after the whole corpus is read rather than as it goes.
    /// </summary>
    [Fact]
    public void GravityIsJoinedEvenWhenTheScanArrivesAfterTheLanding()
    {
        Spread(25, index => Landing(Start.AddDays(index), "Heavy 2 c", gravity: null));

        // The scan lands last, in its own journal, long after every touchdown.
        Journal(Scan(Start.AddDays(40), "Heavy 2 c", 2.4));

        var claim = ClaimOf(Mine(), "high-gravity");

        Assert.Equal(25, claim.Evidence.Occurrences);
        Assert.Equal(25, claim.Evidence.Opportunities);
    }

    /// <summary>
    /// Three accounts under nine character names is what a thirteen-month corpus really looks
    /// like. Pooling them would report one person's flying to another, which is the failure the
    /// per-Commander keying exists to prevent.
    /// </summary>
    [Fact]
    public void TwoCommandersAreCountedApart()
    {
        Spread(22, index => Submitted(Start.AddDays(index), times: 1), commander: "F1111111");
        Spread(21, index => Submitted(Start.AddDays(index), times: 1, submitted: false), commander: "F2222222");

        var reports = Mine();

        Assert.Equal(2, reports.Count);

        var first = Assert.Single(reports, report => report.FrontierId == "F1111111");
        var second = Assert.Single(reports, report => report.FrontierId == "F2222222");

        Assert.Equal(22, ClaimOf([first], "interdiction-submit").Evidence.Occurrences);
        Assert.DoesNotContain(second.Claims, claim => claim.Key == "interdiction-submit");
    }

    /// <summary>
    /// Flying into a station writes several hull-damage events in the same second. Counting each
    /// would turn one bad approach into a habit.
    /// </summary>
    [Fact]
    public void OneCollisionIsOneOccurrenceHoweverManyHullEventsItWrote()
    {
        Spread(25, index => Collision(Start.AddDays(index), hits: 4));

        var claim = ClaimOf(Mine(), "arrival-collision");

        Assert.Equal(25, claim.Evidence.Occurrences);
    }

    /// <summary>
    /// The whole of the inference behind the strongest detector in the phase: nobody was shooting.
    /// </summary>
    [Fact]
    public void HullDamageWithAnAttackerBehindItIsNotACollision()
    {
        Spread(25, index => UnderFire(Start.AddDays(index)));

        var report = Assert.Single(Mine());

        Assert.DoesNotContain(report.Claims, claim => claim.Key == "arrival-collision");
    }

    /// <summary>
    /// Item 2's example sentence needs the recent half — a habit somebody grew out of eight months
    /// ago and one they have twice this week are the same total.
    /// </summary>
    [Fact]
    public void RecentOccurrencesAreCountedSeparatelyFromTheTotal()
    {
        Spread(21, index => Submitted(Start.AddDays(index), times: 1));

        // Mined forty days after the first of them, so the window's far end is real rather than
        // sitting in the middle of the occurrences.
        var mined = new HabitMiner(NullLogger<HabitMiner>.Instance)
            .Mine(Files(), Start.AddDays(40));

        var claim = ClaimOf(mined, "interdiction-submit");

        Assert.Equal(21, claim.Evidence.Occurrences);

        // One a day from day zero to day twenty, read on day forty: eleven of them — days ten to
        // twenty — are inside the thirty-day window, and the first ten are not.
        Assert.Equal(11, claim.Evidence.Recent);
        Assert.Contains("in the last month", claim.Spoken(), StringComparison.Ordinal);
    }

    private IReadOnlyList<HabitReport> Mine() =>
        new HabitMiner(NullLogger<HabitMiner>.Instance).Mine(Files(), Start.AddYears(1));

    private IReadOnlyList<string> Files() =>
        [.. Directory.EnumerateFiles(_folder, "Journal.*.log").OrderBy(Path.GetFileName, StringComparer.Ordinal)];

    private static HabitClaim ClaimOf(IReadOnlyList<HabitReport> reports, string key) =>
        Assert.Single(Assert.Single(reports).Claims, claim => claim.Key == key);

    /// <summary>One journal per index, which is how the corpus floor is cleared in these tests.</summary>
    private void Spread(int journals, Func<int, string> body, string commander = Cmdr)
    {
        for (var index = 0; index < journals; index++)
        {
            Journal(body(index), commander);
        }
    }

    private void Journal(string body, string commander = Cmdr)
    {
        Directory.CreateDirectory(_folder);

        var text = new StringBuilder()
            .AppendLine(Line(Start, "Fileheader", string.Empty))
            .AppendLine(Line(Start, "Commander", $"\"FID\":\"{commander}\""))
            .Append(body)
            .ToString();

        // Ordinal file order is chronological for Elite's naming, which is what the miner relies
        // on — so the fixture names them the same way.
        File.WriteAllText(Path.Combine(_folder, $"Journal.{_files++:D5}.01.log"), text);
    }

    private static string Idle(DateTimeOffset at) => Line(at, "Music", "\"MusicTrack\":\"NoTrack\"") + "\n";

    private static string Submitted(DateTimeOffset at, int times, bool submitted = true)
    {
        var text = new StringBuilder();

        for (var index = 0; index < times; index++)
        {
            text.AppendLine(Line(
                at.AddMinutes(index),
                "Interdicted",
                $"\"Submitted\":{(submitted ? "true" : "false")},\"Interdictor\":\"Someone\""));
        }

        return text.ToString();
    }

    private static string Landing(DateTimeOffset at, string body, double? gravity)
    {
        var text = new StringBuilder();

        if (gravity is { } value)
        {
            text.AppendLine(Scan(at, body, value).TrimEnd());
        }

        text.AppendLine(Line(at.AddMinutes(1), "Touchdown", $"\"Body\":\"{body}\""));

        return text.ToString();
    }

    private static string Scan(DateTimeOffset at, string body, double gravity) =>
        Line(
            at,
            "Scan",
            $"\"BodyName\":\"{body}\",\"SurfaceGravity\":"
            + (gravity * HighGravityLandingDetector.Standard).ToString("0.000000", CultureInfo.InvariantCulture))
        + "\n";

    private static string Collision(DateTimeOffset at, int hits)
    {
        var text = new StringBuilder()
            .AppendLine(Line(at, "SupercruiseExit", "\"StarSystem\":\"Bodge\",\"Body\":\"Bodge Ring\",\"BodyType\":\"Station\""));

        for (var index = 0; index < hits; index++)
        {
            text.AppendLine(Line(at.AddSeconds(30 + index), "HullDamage", "\"Health\":0.6,\"PlayerPilot\":true"));
        }

        return text.ToString();
    }

    private static string UnderFire(DateTimeOffset at) =>
        new StringBuilder()
            .AppendLine(Line(at, "SupercruiseExit", "\"StarSystem\":\"Bodge\",\"Body\":\"Bodge Ring\",\"BodyType\":\"Station\""))
            .AppendLine(Line(at.AddSeconds(20), "UnderAttack", "\"Target\":\"You\""))
            .AppendLine(Line(at.AddSeconds(30), "HullDamage", "\"Health\":0.6,\"PlayerPilot\":true"))
            .ToString();

    private static string Line(DateTimeOffset at, string kind, string fields) =>
        $"{{ \"timestamp\":\"{at.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"{kind}\""
        + (fields.Length == 0 ? string.Empty : ", " + fields)
        + " }";
}
