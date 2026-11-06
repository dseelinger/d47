using D47.Core.Adventures;
using Xunit;
using static D47.Core.Tests.Stories.PlannerWorld;

namespace D47.Core.Tests.Adventures;

public sealed class AnArriveBeatWhereTheCommanderAlreadyIsGoesBackThroughTheTurnTests
{
    [Fact]
    public void NoArriveIsPlannedInTheSystemTheCommanderOrTheLastStopIsIn()
    {
        var inputs = Inputs(Bounty, Codex, Salvage);

        for (uint seed = 1; seed <= 40; seed++)
        {
            var at = Start;

            foreach (var step in Placed(Planned(inputs, Places(), seed)))
            {
                if (step.Kind == TriggerKind.Arrive)
                {
                    Assert.NotEqual(at, step.System);
                }

                at = step.System ?? at;
            }
        }
    }
}
