using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

public class ANewQueryStartsAtTheFirstHitTests
{
    private const string Page = "fuel low. fuel ok. fuel low. scoop on.";

    [Fact]
    public void ANewQueryStartsAtTheFirstHitEvenAfterTheOldOneWasStepped()
    {
        var search = new PageSearch();
        search.Ask("fuel");
        search.Find(Page);
        search.Step(1);
        search.Step(1);

        search.Ask("low");
        search.Find(Page);

        Assert.Equal(0, search.Hit);
    }

    [Fact]
    public void TheCurrentHitStaysOnTheSameTextWhenTextArrivesAfterIt()
    {
        var search = new PageSearch();
        search.Ask("fuel");
        search.Find(Page);
        search.Step(1);
        var start = search.Matches[search.Hit].Start;

        search.Find(Page + " fuel again. fuel last.");

        Assert.Equal(start, search.Matches[search.Hit].Start);
    }

    [Fact]
    public void ForgettingThePageAndFindingItAgainComesBackToTheSameHit()
    {
        var search = new PageSearch();
        search.Ask("fuel");
        search.Find(Page);
        search.Step(1);
        var hit = search.Hit;

        search.Forget();
        Assert.Equal(-1, search.Hit);
        Assert.Empty(search.Matches);
        search.Find(Page);

        Assert.Equal(hit, search.Hit);
    }

    [Fact]
    public void SteppingWithNoMatchesReturnsFalseAndChangesNothing()
    {
        var search = new PageSearch();
        search.Ask("nothing");
        search.Find(Page);

        Assert.False(search.Step(1));
        Assert.Equal(-1, search.Hit);
        Assert.Empty(search.Matches);
        Assert.Equal("no matches", search.Describe());
    }
}
