using D47.Core.Adventures;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class AnOutlineHasNoLengthLimitTests
{
    [Fact]
    public void ALongPremiseIsNotAProblemAndIsSaved()
    {
        var long2000 = new string('a', 2000);
        var adventure = LanternRoute() with
        {
            Spine = new AdventureSpine { Premise = long2000, Want = long2000, Stake = long2000, Turn = long2000, Ending = long2000 },
        };

        Assert.Empty(AdventureValidation.Problems(adventure));

        var store = new AdventureStore("adventures.json", new MemoryFileSystem(), NullLogger<AdventureStore>.Instance);
        Assert.Null(store.Save("F1", adventure));
    }
}
