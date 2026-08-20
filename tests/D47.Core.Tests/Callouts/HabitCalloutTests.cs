using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Habits;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The personal callout (list.md Phase 32, "Callouts that are yours rather than everyone's").
/// <para>
/// The item sets a bar rather than a behaviour — "it must be right more often" — so what is
/// asserted here is the four things that make the bar keepable: it is off by default, it fires only
/// on the circumstance the claim is about, a refusal silences it, and it stands down from the
/// journal backlog like everything else in this folder.
/// </para>
/// </summary>
public class HabitCalloutTests : IDisposable
{
    private const string Cmdr = "F1234567";

    private static readonly DateTimeOffset Now = new(3311, 4, 8, 19, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-habit-callout-tests", Guid.NewGuid().ToString("N"));

    public HabitCalloutTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// The item's own sentence: "a companion that starts commenting on your flying without being
    /// asked has changed the deal". It is the only callout in the record that ships off.
    /// </summary>
    [Fact]
    public void ItIsOffInADefaultConfiguration()
    {
        var settings = new D47Settings();

        Assert.False(settings.Callouts.Habits);
        Assert.True(settings.Callouts.Continuity);
        Assert.True(settings.Callouts.Danger);
    }

    [Fact]
    public void TheClaimIsSaidWhenItsOwnCircumstanceArrives()
    {
        var callout = new HabitCallout(Book(Claim("interdiction-submit", HabitOccasion.BeingInterdicted)));

        var announcement = Assert.Single(callout.Examine(Context(Event("Interdicted", "\"Submitted\":true"))));

        Assert.Equal("habit.interdiction-submit", announcement.Key);
        Assert.Contains("19 of 40 interdictions", announcement.Text, StringComparison.Ordinal);

        // Routine, always. It is an observation about the Commander rather than about the world.
        Assert.Equal(CalloutUrgency.Routine, announcement.Urgency);
    }

    [Fact]
    public void ADifferentCircumstanceSaysNothing()
    {
        var callout = new HabitCallout(Book(Claim("interdiction-submit", HabitOccasion.BeingInterdicted)));

        Assert.Empty(callout.Examine(Context(Event("ApproachBody", "\"Body\":\"Bodge 1\""))));
    }

    [Fact]
    public void ADismissedClaimIsNeverSaid()
    {
        var store = Store();
        var book = Book(store, Claim("interdiction-submit", HabitOccasion.BeingInterdicted));

        store.Dismiss(Cmdr, "interdiction-submit");

        Assert.Empty(new HabitCallout(book).Examine(Context(Event("Interdicted", "\"Submitted\":true"))));
    }

    /// <summary>
    /// The rule every event-driven callout in this folder follows. Priming replays hours of
    /// journal, and a habit line per interdiction in the backlog would be the worst possible
    /// introduction to the feature.
    /// </summary>
    [Fact]
    public void NothingIsSaidFromTheBacklog()
    {
        var callout = new HabitCallout(Book(Claim("interdiction-submit", HabitOccasion.BeingInterdicted)));

        var priming = Context(Event("Interdicted", "\"Submitted\":true")) with { IsPriming = true };

        Assert.Empty(callout.Examine(priming));
    }

    /// <summary>
    /// Two of these in a row is a lecture. The engine owns the per-key cooldown; this is the
    /// global spacing the callout owns for itself.
    /// </summary>
    [Fact]
    public void ASecondClaimWaitsEvenWhenItsCircumstanceArrivesImmediately()
    {
        var callout = new HabitCallout(Book(
            Claim("interdiction-submit", HabitOccasion.BeingInterdicted),
            Claim("overshoot", HabitOccasion.ApproachingAPlanet)));

        Assert.Single(callout.Examine(Context(Event("Interdicted", "\"Submitted\":true"))));

        Assert.Empty(callout.Examine(
            Context(Event("ApproachBody", "\"Body\":\"Bodge 1\""), Now.AddMinutes(1))));

        Assert.Single(callout.Examine(
            Context(Event("ApproachBody", "\"Body\":\"Bodge 1\""), Now.Add(HabitCallout.BetweenAny))));
    }

    /// <summary>
    /// Item 3: "it says why it fired if asked". The claim has to be recorded as it goes out, or
    /// the argument-free phrases have nothing to refer to.
    /// </summary>
    [Fact]
    public void SayingSomethingIsWhatMakesWhyDidYouSayThatAnswerable()
    {
        var book = Book(Claim("interdiction-submit", HabitOccasion.BeingInterdicted));

        Assert.Null(book.LastSpoken);

        new HabitCallout(book).Examine(Context(Event("Interdicted", "\"Submitted\":true"))).ToList();

        Assert.Equal("interdiction-submit", book.LastSpoken?.Key);
        Assert.Contains("40 of your journals", book.LastSpoken!.Explained(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A drop at a star is a fuel stop, not an arrival at somewhere with a landing pad — so the
    /// arrival claim does not fire on one. The narrowness is what keeps the line credible.
    /// </summary>
    [Fact]
    public void OnlyAStationDropIsAnArrival()
    {
        Assert.Equal(
            HabitOccasion.ArrivingAtAStation,
            HabitCallout.OccasionOf(Event("SupercruiseExit", "\"BodyType\":\"Station\"")));

        Assert.Null(HabitCallout.OccasionOf(Event("SupercruiseExit", "\"BodyType\":\"Star\"")));
    }

    private static CalloutContext Context(JournalEvent journalEvent, DateTimeOffset? now = null) =>
        new(
            now ?? Now,
            IsPriming: false,
            State: null,
            Status: GameStatus.Unknown,
            Route: NavRoute.None,
            Events: [journalEvent]);

    private static JournalEvent Event(string kind, string fields)
    {
        var line = $"{{\"timestamp\":\"3311-04-08T19:00:00Z\",\"event\":\"{kind}\",{fields}}}";

        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private HabitStore Store() => new(Path.Combine(_folder, "habits.json"), NullLogger<HabitStore>.Instance);

    private HabitBook Book(params HabitClaim[] claims) => Book(Store(), claims);

    private static HabitBook Book(HabitStore store, params HabitClaim[] claims)
    {
        store.Record(
        [
            new HabitReport
            {
                FrontierId = Cmdr,
                Journals = 40,
                From = Now.AddYears(-1),
                To = Now,
                MinedAt = Now,
                Claims = claims,
            },
        ]);

        return new HabitBook(store, () => Cmdr);
    }

    /// <summary>
    /// A claim that is sound but uncommon is never volunteered, and is still readable
    /// (remediation.md 17, item 12).
    /// <para>
    /// Reported as *"You have dropped short of where you were going and had to come back — 14 of
    /// 490 approaches"*, said on a perfect approach. It cleared every count floor and was a thing
    /// the Commander did once in thirty-five approaches; at that rate the moment the line fires is
    /// almost always a moment when nothing is going wrong.
    /// </para>
    /// <para>
    /// The second half of the assertion is the point: the claim is still in the book. What the
    /// floor buys is silence, not deletion — asked about it, d47 still answers.
    /// </para>
    /// </summary>
    [Fact]
    public void AClaimBelowTheRateIsNeverVolunteeredAndIsStillThere()
    {
        var uncommon = Claim("overshoot", HabitOccasion.ApproachingAPlanet) with
        {
            Evidence = new HabitEvidence
            {
                Occurrences = 14,
                Opportunities = 490,
                From = Now.AddYears(-1),
                To = Now,
                Journals = 236,
            },
        };

        var book = Book(uncommon);

        Assert.Empty(new HabitCallout(book).Examine(Context(Event("ApproachBody", "\"Body\":\"Nervi 2\""))));

        var kept = Assert.Single(book.Claims);

        Assert.Equal("overshoot", kept.Key);
        Assert.True(kept.Evidence.ClearsTheFloor, "the counts are sound; it is the rate that is not");
        Assert.False(kept.Evidence.WorthSaying);
    }

    private static HabitClaim Claim(string key, HabitOccasion occasion) => new(key, key)
    {
        Observation = "You submit rather than run",
        Denominator = "interdictions",
        Occasion = occasion,
        Evidence = new HabitEvidence
        {
            Occurrences = 19,
            Opportunities = 40,
            From = Now.AddYears(-1),
            To = Now,
            Journals = 40,
        },
    };
}
