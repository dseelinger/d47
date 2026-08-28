using D47.Core.Habits;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Habits;

/// <summary>
/// The store, and the one structural decision in it (Phase 32).
/// <para>
/// Most of what is asserted here is about <b>dismissals surviving things</b>, because that is the
/// property the item calls "the failure mode this whole phase risks" — the same wrong observation
/// arriving monthly. Mining rebuilds every claim from scratch, so a dismissal held on a claim would
/// be erased by the next run, and nothing would report that it had been.
/// </para>
/// </summary>
public class HabitStoreTests : IDisposable
{
    private const string Cmdr = "F1234567";

    private static readonly DateTimeOffset Now = new(3311, 4, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-habit-store-tests", Guid.NewGuid().ToString("N"));

    public HabitStoreTests() => Directory.CreateDirectory(_folder);

    private string StorePath => Path.Combine(_folder, "habits.json");

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void AMinedReportSurvivesARestart()
    {
        var store = Store();
        store.Record([Report(Claim("overshoot"))]);

        var reread = Store();
        reread.Poll();

        var claim = Assert.Single(Assert.Single(reread.Everything()).Claims);

        Assert.Equal("overshoot", claim.Key);
        Assert.Equal(14, claim.Evidence.Occurrences);
        Assert.Equal(490, claim.Evidence.Opportunities);
        Assert.Equal("approaches", claim.Denominator);
    }

    /// <summary>
    /// The assertion the split exists for. A re-mine is a recomputation, and the refusal has to
    /// outlive it — otherwise the Commander is told the same thing again a month later.
    /// </summary>
    [Fact]
    public void ADismissalOutlivesTheClaimItDismissed()
    {
        var store = Store();
        store.Record([Report(Claim("overshoot"))]);
        store.Dismiss(Cmdr, "overshoot");

        // Mining again, exactly as pressing the button a month later would.
        store.Record([Report(Claim("overshoot"))]);

        Assert.Contains("overshoot", store.DismissedBy(Cmdr));

        var book = new HabitBook(store, () => Cmdr);

        Assert.Empty(book.Claims);
        Assert.Single(book.Mine!.Claims);
    }

    /// <summary>
    /// Emptying is "forget what you noticed", not "forget what I told you". The dismissals are the
    /// only thing in the file a person wrote.
    /// </summary>
    [Fact]
    public void EmptyingTheStoreLeavesTheDismissalsStanding()
    {
        var store = Store();
        store.Record([Report(Claim("overshoot"), Claim("interdiction-submit"))]);
        store.Dismiss(Cmdr, "overshoot");

        Assert.Equal(2, store.Empty());

        Assert.Null(store.For(Cmdr));
        Assert.Contains("overshoot", store.DismissedBy(Cmdr));

        var reread = Store();
        reread.Poll();

        Assert.Contains("overshoot", reread.DismissedBy(Cmdr));
    }

    [Fact]
    public void AHandEditIsPickedUpWithoutARestart()
    {
        var store = Store();
        store.Record([Report(Claim("overshoot"))]);

        var changed = 0;
        store.Changed += () => changed++;

        File.WriteAllText(StorePath, File.ReadAllText(StorePath).Replace("\"occurrences\": 14", "\"occurrences\": 40"));

        Assert.True(store.Poll());
        Assert.Equal(1, changed);
        Assert.Equal(40, Assert.Single(store.For(Cmdr)!.Claims).Evidence.Occurrences);

        // Content comparison, not a timestamp: a second poll with the same bytes is not a change.
        Assert.False(store.Poll());
    }

    [Fact]
    public void AClaimThatCannotBeReadBackIsReportedRatherThanDropped()
    {
        File.WriteAllText(
            StorePath,
            """
            {
              "commanders": [
                {
                  "frontierId": "F1234567",
                  "minedAt": "3311-04-02T09:00:00+00:00",
                  "journals": 40,
                  "claims": [
                    { "key": "overshoot", "observation": "You overshoot", "occurrences": 9, "opportunities": 40 },
                    { "key": "", "observation": "" }
                  ]
                }
              ]
            }
            """);

        var store = Store();
        store.Poll();

        Assert.Single(store.For(Cmdr)!.Claims);
        Assert.Single(store.Problems);
    }

    /// <summary>
    /// With no key, "that" is the one just said — which is what makes the phrase reachable through
    /// the router at all, since its grammar carries no arguments.
    /// </summary>
    [Fact]
    public void DismissingWithNoKeyDropsTheOneJustSpoken()
    {
        var store = Store();
        store.Record([Report(Claim("overshoot"), Claim("interdiction-submit"))]);

        var book = new HabitBook(store, () => Cmdr);
        book.Spoke(book.Claims.Single(claim => claim.Key == "interdiction-submit"));

        var dropped = book.Dismiss(null);

        Assert.NotNull(dropped);
        Assert.Equal("interdiction-submit", dropped.Key);
        Assert.Equal("overshoot", Assert.Single(book.Claims).Key);

        // And nothing is "that" any more, so a second phrase does not drop a second claim.
        Assert.Null(book.Dismiss(null));
    }

    [Fact]
    public void TheReadbackNamesTheClaimsTheSilencesAndTheDismissals()
    {
        var store = Store();
        store.Record([Report(Claim("overshoot")) with
        {
            Quiet = [new HabitSilence("high-gravity", "landing on heavy worlds", "nothing over one g")],
        }]);

        store.Dismiss(Cmdr, "interdiction-submit");

        var described = new HabitBook(store, () => Cmdr).Describe();

        Assert.Contains("14 of 490 approaches", described, StringComparison.Ordinal);
        Assert.Contains("nothing over one g", described, StringComparison.Ordinal);
        Assert.Contains("told me to drop 1", described, StringComparison.Ordinal);
    }

    private HabitStore Store() => new(StorePath, NullLogger<HabitStore>.Instance);

    private static HabitReport Report(params HabitClaim[] claims) => new()
    {
        FrontierId = Cmdr,
        Journals = 490,
        From = Now.AddYears(-1),
        To = Now,
        MinedAt = Now,
        Claims = claims,
    };

    private static HabitClaim Claim(string key) => new(key, "overshooting and going round again")
    {
        Observation = "You have dropped short of where you were going and had to come back",
        Denominator = "approaches",
        Occasion = HabitOccasion.ApproachingAPlanet,
        Evidence = new HabitEvidence
        {
            Occurrences = 14,
            Opportunities = 490,
            From = Now.AddYears(-1),
            To = Now,
            Journals = 490,
        },
    };
}
