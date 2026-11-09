using D47.Core.Campaigns;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// The Hawk and the Dove appear when two fronts are open, are the same for a squadron, urge different fronts, and the
/// councillor passed over remembers each time.
/// </summary>
public sealed class TheHawkAndTheDoveArgueOverTheFrontsTests
{
    private const string Squadron = "4401";

    private static Front Of(FrontKind kind, string system) =>
        new(kind, system, $"{kind} in {system}.", SystemIntent.Hold, true, kind is FrontKind.Conflict or FrontKind.Coup ? "Lave Purple Gang" : null, "", 0.1, Now, false);

    private static CouncilView View(IReadOnlyList<Front> fronts, ChosenFront? chosen = null, DateTimeOffset? now = null) =>
        new(Us, Squadron, fronts, chosen, now ?? Now);

    private static ChosenFront Chose(FrontKind kind, string system, int day) => new(kind, system, Now.AddDays(day), []);

    private static readonly Front[] Two = [Of(FrontKind.Coup, "Lave"), Of(FrontKind.Retreat, "Leesti")];

    [Fact]
    public void TwoOpenFrontsBringTheHawkAndTheDoveEachUrgingADifferentOne()
    {
        var step = CampaignInternalPolitics.Step(null, View(Two));

        Assert.Equal(2, step.Lines.Count);
        Assert.All(step.Lines, line => Assert.Equal(ArcAct.Argument, line.Act));
        Assert.Equal(step.Council!.Hawk, step.Lines[0].Leader);
        Assert.Equal("Lave", step.Lines[0].System);
        Assert.Equal(step.Council.Dove, step.Lines[1].Leader);
        Assert.Equal("Leesti", step.Lines[1].System);
        Assert.NotEqual(step.Council.Hawk, step.Council.Dove);
    }

    [Fact]
    public void OneOpenFrontOrStaleOnesBringNoCouncil()
    {
        Assert.Null(CampaignInternalPolitics.Step(null, View([Of(FrontKind.Coup, "Lave")])).Council);
        Assert.Null(CampaignInternalPolitics.Step(null, View([Of(FrontKind.Coup, "Lave"), Of(FrontKind.Stale, "Leesti")])).Council);
        Assert.Null(CampaignInternalPolitics.Step(null, View([Of(FrontKind.Coup, "Lave"), Of(FrontKind.Retreat, "Lave")])).Council);
        Assert.Null(CampaignInternalPolitics.Step(null, View([])).Council);
    }

    [Fact]
    public void TheyAreSeededSoASquadronAndEveryRestartMeetTheSameTwo()
    {
        Assert.Equal(CampaignInternalPolitics.Names(Squadron), CampaignInternalPolitics.Names(Squadron));

        var first = CampaignInternalPolitics.Step(null, View(Two)).Council;
        var later = CampaignInternalPolitics.Step(first, View([Of(FrontKind.Conflict, "Diso"), Of(FrontKind.BadState, "Lave")], now: Now.AddDays(3)));

        Assert.Empty(later.Lines);
        Assert.Equal(first!.Hawk, later.Council!.Hawk);
        Assert.Equal(first.Dove, later.Council.Dove);
        Assert.Equal(Now, later.Council.Since);
        Assert.NotEqual(CampaignInternalPolitics.Names(Squadron), CampaignInternalPolitics.Names("9902"));
    }

    [Fact]
    public void WithNoAggressiveFrontTheHawkStillUrgesTheFirstAndTheDoveAnother()
    {
        var picks = CampaignInternalPolitics.Picks([Of(FrontKind.Retreat, "Lave"), Of(FrontKind.BadState, "Leesti")]);

        Assert.Equal("Lave", picks!.Value.Hawk.System);
        Assert.Equal("Leesti", picks.Value.Dove.System);
    }

    [Fact]
    public void ChoosingAnAggressiveFrontSidesWithTheHawkAndTheDoveRemembersIt()
    {
        var council = CampaignInternalPolitics.Step(null, View(Two)).Council!;

        var first = CampaignInternalPolitics.Step(council, View(Two, Chose(FrontKind.Coup, "Lave", 1)));
        var line = Assert.Single(first.Lines);
        Assert.Equal(ArcAct.Sided, line.Act);
        Assert.Equal(council.Dove, line.Leader);
        Assert.Contains("siding with " + council.Hawk, line.Text);
        Assert.Contains(council.Dove + " had urged the", line.Text);
        Assert.DoesNotContain("time " + council.Dove + " has been passed over", line.Text);

        var second = CampaignInternalPolitics.Step(first.Council, View(Two, Chose(FrontKind.Conflict, "Diso", 2)));
        var again = Assert.Single(second.Lines);
        Assert.Equal(council.Dove, again.Leader);
        Assert.Contains("second time " + council.Dove + " has been passed over; the last time was over Lave", again.Text);
        Assert.Equal(2, second.Council!.Sidings.Count);
    }

    [Fact]
    public void ChoosingAnythingElseSidesWithTheDoveAndTheHawkIsPassedOver()
    {
        var council = CampaignInternalPolitics.Step(null, View(Two)).Council!;

        var line = Assert.Single(CampaignInternalPolitics.Step(council, View(Two, Chose(FrontKind.Retreat, "Leesti", 1))).Lines);

        Assert.Equal(council.Hawk, line.Leader);
        Assert.Contains("siding with " + council.Dove, line.Text);
    }

    [Fact]
    public void AChoiceAlreadyAnsweredOrMadeBeforeTheCouncilOpenedIsNotSided()
    {
        var chosen = Chose(FrontKind.Coup, "Lave", 1);
        var opened = CampaignInternalPolitics.Step(null, View(Two, chosen));

        Assert.Equal(2, opened.Lines.Count);
        Assert.Empty(CampaignInternalPolitics.Step(opened.Council, View(Two, chosen)).Lines);

        var answered = CampaignInternalPolitics.Step(opened.Council, View(Two, Chose(FrontKind.Coup, "Lave", 2)));
        Assert.Single(answered.Lines);
        Assert.Empty(CampaignInternalPolitics.Step(answered.Council, View(Two, Chose(FrontKind.Coup, "Lave", 2))).Lines);
        Assert.Empty(CampaignInternalPolitics.Step(answered.Council, View(Two, null)).Lines);
    }

    [Fact]
    public void TheBriefSpeaksAsACouncillorAndForbidsInventedFacts()
    {
        var line = CampaignInternalPolitics.Step(null, View(Two)).Lines[0];

        var brief = CampaignArcs.Brief(line);

        Assert.Contains(line.Leader, brief.Speaker);
        Assert.Contains("councillor", brief.Speaker);
        Assert.Contains(CampaignArcs.FictionRule, brief.Instruction);
        Assert.Contains(line.Text, brief.Instruction);
    }
}
