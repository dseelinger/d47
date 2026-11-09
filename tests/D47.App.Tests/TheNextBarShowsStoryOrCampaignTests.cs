using D47.App.Panel;
using D47.Core;
using D47.Core.Campaigns;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The NEXT bar shows the story objective or the chosen campaign front, and the choice is kept.</summary>
public sealed class TheNextBarShowsStoryOrCampaignTests
{
    private static readonly ObjectiveStripText Story =
        new("NEXT", "Scan the beacon in ", "Sol", "Sol", null, false, false, "THE SALT ROAD");

    private static CampaignNextAction Action(string sentence, string system = "Arque", FrontKind kind = FrontKind.Conflict) =>
        new(new Front(kind, system, "reason", SystemIntent.Hold, true, "Arque Partners", "", null, null, false), sentence, null)
        {
            Faction = "Kestrel Union",
        };

    private static ObjectiveStripText Campaign() =>
        ObjectiveStrip.DescribeCampaign(Action("Redeem your combat bonds for Kestrel Union in Arque."), "Sol")!;

    private static ViewStateStore Store() =>
        new(new AppPaths("C:\\d47-next-bar-tests"), new MemoryFileSystem(), NullLogger<ViewStateStore>.Instance);

    [Fact]
    public void WithOnlyAStoryTheStoryShowsWithNoToggle()
    {
        var chosen = ObjectiveStrip.Choose(Story, null, NextBarSource.Campaign)!;

        Assert.Equal(NextBarSource.Story, chosen.Source);
        Assert.False(chosen.Toggle);
    }

    [Fact]
    public void WithOnlyAFrontTheFrontShowsWithNoToggle()
    {
        var chosen = ObjectiveStrip.Choose(null, Campaign(), NextBarSource.Story)!;

        Assert.Equal(NextBarSource.Campaign, chosen.Source);
        Assert.False(chosen.Toggle);
    }

    [Theory]
    [InlineData(NextBarSource.Story)]
    [InlineData(NextBarSource.Campaign)]
    public void WithBothTheRememberedChoiceShowsWithTheToggle(NextBarSource remembered)
    {
        var chosen = ObjectiveStrip.Choose(Story, Campaign(), remembered)!;

        Assert.Equal(remembered, chosen.Source);
        Assert.True(chosen.Toggle);
    }

    [Fact]
    public void WithNeitherTheStripIsEmpty() =>
        Assert.Null(ObjectiveStrip.Choose(null, null, NextBarSource.Campaign));

    [Fact]
    public void TheCampaignSentenceIsSplitAroundItsSystemAndTheTagNamesTheOpponent()
    {
        var text = ObjectiveStrip.DescribeCampaign(
            Action("Redeem your 5,000 credits in bounties at a station in Arque, then fight for Kestrel Union there. Trade does not count."), "Sol")!;

        Assert.Equal("Redeem your 5,000 credits in bounties at a station in ", text.Before);
        Assert.Equal("Arque", text.System);
        Assert.Equal("Arque", text.CopyValue);
        Assert.Equal(", then fight for Kestrel Union there", text.After);
        Assert.False(text.InSystem);
        Assert.Equal("CONFLICT · ARQUE PARTNERS", text.Tag);
    }

    [Fact]
    public void TheSystemIsCyanWhenTheCommanderIsInIt()
    {
        var text = ObjectiveStrip.DescribeCampaign(Action("Redeem your combat bonds for Kestrel Union in Arque."), "arque")!;

        Assert.True(text.InSystem);
    }

    [Fact]
    public void AFrontThatIsNotAConflictTagsTheCampaignFaction()
    {
        var text = ObjectiveStrip.DescribeCampaign(
            Action("Run missions and trade for Kestrel Union in Arque to widen its lead.", kind: FrontKind.ThinMargin), "Sol")!;

        Assert.Equal("THIN MARGIN · KESTREL UNION", text.Tag);
    }

    [Fact]
    public void NoActionMeansNoCampaignLine() =>
        Assert.Null(ObjectiveStrip.DescribeCampaign(null, "Sol"));

    [Fact]
    public void TheChoiceIsSharedByEveryHolderOfTheMemoryAndKeptAcrossLaunches()
    {
        var store = Store();
        var memory = new NextBarMemory(store);
        var changed = 0;
        memory.Changed += () => changed++;

        Assert.Equal(NextBarSource.Story, memory.Source);

        memory.Remember(NextBarSource.Campaign);
        memory.Remember(NextBarSource.Campaign);

        Assert.Equal(NextBarSource.Campaign, memory.Source);
        Assert.Equal(1, changed);
        Assert.Equal(NextBarSource.Campaign, new NextBarMemory(store).Source);
    }

    [Fact]
    public void NextBarSurvivesViewStateSerialisation()
    {
        var store = Store();
        store.Save(new ViewState { NextBar = NextBarSource.Campaign });

        Assert.Equal(NextBarSource.Campaign, store.Load().NextBar);
        Assert.Equal(NextBarSource.Story, new ViewState().NextBar);
    }
}
