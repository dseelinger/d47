using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// Where every engineer is, and what stands between the Commander and each of them (list.md
/// Phase 28, "Where every engineer is" and "Who can roll this").
/// <para>
/// The claim under all of it is that <b>distance is local arithmetic</b>: the coordinates shipped
/// in the table, the Commander's own position comes out of the journal, and nothing here reaches
/// a network or a clock.
/// </para>
/// </summary>
public class EngineerAccessTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static EngineerProgressState Progress(params string[] lines)
    {
        var state = EngineerProgressState.Empty;

        foreach (var line in lines)
        {
            state = state.Apply(Event(line));
        }

        return state;
    }

    private static Engineer Named(string name) =>
        EngineerDirectory.ByName(name) ?? throw new InvalidOperationException($"no {name}");

    /// <summary>
    /// The enabling item: every engineer carries a coordinate, so nothing has to be looked up to
    /// rank them. A gap here is not a missing figure — it is a person who silently cannot be
    /// ranked at all.
    /// </summary>
    [Fact]
    public void EveryEngineerIsPlaced()
    {
        Assert.Equal(38, EngineerDirectory.All.Count);
        Assert.All(EngineerDirectory.All, engineer => Assert.NotNull(engineer.Position));
    }

    /// <summary>
    /// The figure itself, against one nobody has to take on trust: Deciat is about 131 light
    /// years from Sol, and Sol is the origin of Frontier's own axes.
    /// </summary>
    [Fact]
    public void DistanceIsMeasuredInLightYears()
    {
        var sol = new StarPosition(0, 0, 0);

        Assert.Equal(131.4, Named("Felicity Farseer").DistanceFrom(sol)!.Value, 1);

        // Colonia, which is the case the ranking never needs a rule for.
        Assert.True(Named("Baltanos").DistanceFrom(sol) > 21_000);
    }

    /// <summary>
    /// An unknown position answers null rather than zero. Zero would sort an engineer nobody can
    /// place to the top of a list whose entire subject is who is nearest.
    /// </summary>
    [Fact]
    public void AnUnknownPositionIsNotZero()
    {
        Assert.Null(Named("Felicity Farseer").DistanceFrom(null));
        Assert.Null(new Engineer { Id = 1, Name = "Nobody" }.DistanceFrom(new StarPosition(0, 0, 0)));
    }

    /// <summary>
    /// Where the Commander is, from the three events that state it. All three carry a
    /// <c>StarPos</c> on every occurrence in a 912-journal corpus, and nothing else carries one.
    /// </summary>
    [Theory]
    [InlineData("""{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Deciat","StarPos":[122.625,-0.8125,-47.28125],"Docked":false}""")]
    [InlineData("""{"timestamp":"2026-08-18T09:00:00Z","event":"FSDJump","StarSystem":"Deciat","StarPos":[122.625,-0.8125,-47.28125]}""")]
    [InlineData("""{"timestamp":"2026-08-18T09:00:00Z","event":"CarrierJump","StarSystem":"Deciat","StarPos":[122.625,-0.8125,-47.28125],"Docked":true}""")]
    public void TheCommanderHasAPosition(string json)
    {
        var location = JournalLocation.Unknown.Apply(Event(json));

        Assert.Equal(new StarPosition(122.625, -0.8125, -47.28125), location.StarPos);
    }

    /// <summary>
    /// Docking does not move the Commander, and it states no position — so it must leave the one
    /// they have alone. Folding it on every event that names a system would make the position go
    /// unknown the moment they landed.
    /// </summary>
    [Fact]
    public void DockingDoesNotForgetWhereYouAre()
    {
        var landed = JournalLocation.Unknown
            .Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"FSDJump","StarSystem":"Deciat","StarPos":[122.625,-0.8125,-47.28125]}"""))
            .Apply(Event("""{"timestamp":"2026-08-18T09:05:00Z","event":"Docked","StarSystem":"Deciat","StationName":"Farseer Inc"}"""));

        Assert.Equal(new StarPosition(122.625, -0.8125, -47.28125), landed.StarPos);
    }

    /// <summary>
    /// A jump is indivisible, so a distance rounds up. 84 light years at 30 is three jumps, and
    /// reporting 2.8 would be reporting a flight nobody can make.
    /// </summary>
    [Fact]
    public void DistanceBecomesJumps()
    {
        Assert.Equal(3, EngineerAccess.Jumps(84, 30));
        Assert.Equal(1, EngineerAccess.Jumps(0.5, 30));

        // Never guessed. No range reported means no jump count, rather than a plausible one.
        Assert.Null(EngineerAccess.Jumps(84, null));
        Assert.Null(EngineerAccess.Jumps(null, 30));
    }

    /// <summary>
    /// The three states the directory sorts by, and the order they are declared in is the order
    /// the page shows them: what can be acted on today comes first.
    /// </summary>
    [Fact]
    public void ReachSaysWhatCanBeActedOnToday()
    {
        var progress = Progress(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5},{"Engineer":"Broo Tarquin","EngineerID":300030,"Progress":"Invited"}]}""");

        // Unlocked outright.
        Assert.Equal(EngineerReach.Unlocked, EngineerAccess.ReachOf(Named("Liz Ryder"), progress));

        // Invited: a referral that has already happened, and the journal beats the table on that.
        Assert.Equal(EngineerReach.WithinReach, EngineerAccess.ReachOf(Named("Broo Tarquin"), progress));

        // Nobody has to recommend Farseer.
        Assert.Equal(EngineerReach.WithinReach, EngineerAccess.ReachOf(Named("Felicity Farseer"), progress));

        // Reached through Liz Ryder, who is unlocked past the referral grade.
        Assert.Equal(EngineerReach.WithinReach, EngineerAccess.ReachOf(Named("Hera Tani"), progress));

        // Reached through Marco Qwent, who is reached through Elvira Martuuk. Neither is unlocked.
        Assert.Equal(EngineerReach.Locked, EngineerAccess.ReachOf(Named("Lori Jameson"), progress));
    }

    /// <summary>
    /// A referral grade that is not met is not a referral. Grade 1 with Liz Ryder does not
    /// introduce anybody to Hera Tani, and reading "unlocked" as "recommended" would put an
    /// engineer in the within-reach list that the game will not let the Commander approach.
    /// </summary>
    [Fact]
    public void AReferralNeedsItsGrade()
    {
        var progress = Progress(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":1}]}""");

        Assert.Equal(EngineerReach.Locked, EngineerAccess.ReachOf(Named("Hera Tani"), progress));
    }

    /// <summary>
    /// The chain, which is the whole of "the fastest way in" for one engineer: Broo Tarquin comes
    /// through Hera Tani, who comes through Liz Ryder, and each stop carries the grade the next
    /// one needs rather than the grade the Commander eventually wants.
    /// </summary>
    [Fact]
    public void AChainIsWalkedBackToSomebodyReachable()
    {
        var chain = EngineerAccess.ChainTo(
            Named("Broo Tarquin"), 5, EngineerProgressState.Empty, new StarPosition(0, 0, 0), 30);

        Assert.Equal(
            ["Liz Ryder", "Hera Tani", "Broo Tarquin"],
            chain.Steps.Select(step => step.Engineer.Name));

        Assert.Equal([3, 3, 5], chain.Steps.Select(step => step.Grade));

        // Every leg measured from the stop before it, so the total is the flight actually flown.
        Assert.All(chain.Steps, step => Assert.NotNull(step.Jumps));
        Assert.NotNull(chain.Jumps);
    }

    /// <summary>
    /// A stop already behind the Commander is not a stop. Being grade 5 with Liz Ryder means the
    /// chain to Broo Tarquin starts at Hera Tani.
    /// </summary>
    [Fact]
    public void AChainSkipsWhatIsAlreadyDone()
    {
        var progress = Progress(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Liz Ryder","EngineerID":300080,"Progress":"Unlocked","Rank":5}]}""");

        var chain = EngineerAccess.ChainTo(Named("Broo Tarquin"), 1, progress, null, null);

        Assert.Equal(["Hera Tani", "Broo Tarquin"], chain.Steps.Select(step => step.Engineer.Name));
    }

    /// <summary>
    /// An engineer already unlocked at the grade wanted has no chain at all — and one unlocked
    /// below it has exactly one stop, which is a rank climb rather than an unlock.
    /// </summary>
    [Fact]
    public void AnUnlockedEngineerIsOnlyAStopWhileTheGradeIsShort()
    {
        var progress = Progress(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Unlocked","Rank":3}]}""");

        Assert.True(EngineerAccess.ChainTo(Named("Felicity Farseer"), 3, progress, null, null).IsDone);

        var climb = EngineerAccess.ChainTo(Named("Felicity Farseer"), 5, progress, null, null);

        Assert.Single(climb.Steps);
        Assert.Equal(3, climb.Steps[0].Held);
        Assert.Equal(5, climb.Steps[0].Grade);
        Assert.Equal(16_000_000, climb.Steps[0].ReputationCost);

        // Nothing open-ended is invented for somebody already unlocked: the invitation happened.
        Assert.False(climb.Steps[0].IsOpenEnded);
    }

    /// <summary>
    /// An invitation already extended is not an open-ended requirement. The whole point of the
    /// distinction is that a combat rank cannot be priced and a delivery to a known system can.
    /// </summary>
    [Fact]
    public void AnInvitationAlreadyOfferedCostsNoRank()
    {
        var invited = Progress(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Felicity Farseer","EngineerID":300100,"Progress":"Invited"}]}""");

        var offered = EngineerAccess.ChainTo(Named("Felicity Farseer"), 1, invited, null, null);

        Assert.False(offered.Steps[0].IsOpenEnded);
        Assert.True(offered.Steps[0].IsDelivery);
        Assert.Equal(0, offered.OpenEnded);

        var unmet = EngineerAccess.ChainTo(Named("Felicity Farseer"), 1, EngineerProgressState.Empty, null, null);

        Assert.True(unmet.Steps[0].IsOpenEnded);
        Assert.Contains("exploration rank", unmet.Steps[0].Meeting);
    }

    /// <summary>
    /// A total nobody can compute is not reported as a small one. One leg with no distance makes
    /// the whole flight unknown rather than the sum of the legs that happened to have one.
    /// </summary>
    [Fact]
    public void APartialSumIsNotATotal()
    {
        var chain = EngineerAccess.ChainTo(
            Named("Broo Tarquin"), 1, EngineerProgressState.Empty, null, 30);

        Assert.Equal(3, chain.Steps.Count);
        Assert.Null(chain.LightYears);
        Assert.Null(chain.Jumps);
    }
}
