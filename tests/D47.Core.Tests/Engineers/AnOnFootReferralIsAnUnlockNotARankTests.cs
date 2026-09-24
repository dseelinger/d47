using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// On-foot engineers have no rank: Elite writes <c>"Rank":0</c> for every unlocked one, and a referral
/// into an on-foot engineer is met once the referrer is unlocked (#461).
/// </summary>
public class AnOnFootReferralIsAnUnlockNotARankTests
{
    private const string JudeUnlocked =
        """{ "Engineer":"Jude Navarro", "EngineerID":400001, "Progress":"Unlocked", "RankProgress":0, "Rank":0 }""";

    private const string JudeKnown = """{ "Engineer":"Jude Navarro", "EngineerID":400001, "Progress":"Known" }""";

    private const string TerraKnown = """{ "Engineer":"Terra Velasquez", "EngineerID":400006, "Progress":"Known" }""";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Progress(params string[] standings) =>
        """{"timestamp":"2026-09-20T09:00:00Z","event":"EngineerProgress","Engineers":["""
        + string.Join(",", standings) + "]}";

    private static EngineerProgressState Fold(params string[] standings) =>
        EngineerProgressState.Empty.Apply(Event(Progress(standings)));

    private static D47.Core.Engineers.UnlockEvidence Evidence(EngineerProgressState progress) =>
        new(progress, null, null, null, null, null);

    private static Engineer Named(string name) =>
        EngineerDirectory.ByName(name) ?? throw new InvalidOperationException($"no {name}");

    [Fact]
    public void AnUnlockedReferrerAtRankZeroMeetsTheReferral()
    {
        var terra = Named("Terra Velasquez");
        var progress = Fold(JudeUnlocked, TerraKnown);

        var referral = EngineerAccess.CriteriaFor(terra, Evidence(progress))[0];

        Assert.Equal("Unlock Jude Navarro.", referral.Text);
        Assert.True(referral.Met);
        Assert.DoesNotContain(
            EngineerAccess.UnmetPrerequisites(terra, Evidence(progress)),
            item => item.Intent!.Kind == ChecklistIntentKind.EngineerAccess);
        Assert.DoesNotContain(
            EngineerAccess.ChainTo(terra, 1, progress, null, null).Steps,
            step => step.Engineer.Name == "Jude Navarro");
    }

    [Fact]
    public void AKnownReferrerLeavesAnUnlockItemThatIsDoneOnceUnlocked()
    {
        var terra = Named("Terra Velasquez");
        var known = Evidence(Fold(JudeKnown, TerraKnown));

        var referral = EngineerAccess.CriteriaFor(terra, known)[0];

        Assert.Equal("Unlock Jude Navarro.", referral.Text);
        Assert.NotEqual(true, referral.Met);

        var item = Assert.Single(
            EngineerAccess.UnmetPrerequisites(terra, known),
            candidate => candidate.Intent!.Kind == ChecklistIntentKind.EngineerAccess);

        Assert.DoesNotContain("Rank", item.Text, StringComparison.Ordinal);

        var state = new GameStateStore();
        state.Apply(Event("""{"timestamp":"2026-09-20T08:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));
        state.Apply(Event(Progress(JudeUnlocked, TerraKnown)));

        Assert.Equal(ChecklistState.Done, ChecklistEvaluator.Evaluate(item, state.Active)!.Value.State);
    }

    [Fact]
    public void AnyOneOfYiShensThreeReferrersUnlockedMeetsTheReferral()
    {
        var yiShen = Named("Yi Shen");
        var progress = Fold(
            """{ "Engineer":"Baltanos", "EngineerID":400010, "Progress":"Known" }""",
            """{ "Engineer":"Eleanor Bresa", "EngineerID":400011, "Progress":"Unlocked", "RankProgress":0, "Rank":0 }""",
            """{ "Engineer":"Rosa Dayette", "EngineerID":400012, "Progress":"Known" }""");

        var criteria = EngineerAccess.CriteriaFor(yiShen, Evidence(progress));

        Assert.All(criteria.Take(yiShen.ReferredBy.Count), criterion => Assert.True(criterion.Met));
        Assert.DoesNotContain(
            EngineerAccess.UnmetPrerequisites(yiShen, Evidence(progress)),
            item => item.Intent!.Kind == ChecklistIntentKind.EngineerAccess);
    }

    [Fact]
    public void TheReferrersGateLineReadsAsAnUnlock()
    {
        var jude = Named("Jude Navarro");
        var terra = Named("Terra Velasquez");
        PlannedWork[] planned = [new("Suit", "Terra's work", 1, [terra.Name])];

        var gate = EngineerAccess.Gate(jude, Fold(JudeKnown, TerraKnown), planned);
        var entry = new EngineerEntry { Engineer = jude, Reach = EngineerReach.WithinReach, Gate = gate };

        Assert.Equal("unlocking them opens Terra Velasquez", entry.GateLine);
        Assert.Empty(EngineerAccess.Gate(jude, Fold(JudeUnlocked, TerraKnown), planned));
    }

    [Fact]
    public void AShipReferralIsStillJudgedOnRank()
    {
        var juri = Named("Juri Ishmaak");
        var farseerAt = (int rank) => Evidence(Fold(
            $$"""{ "Engineer":"Felicity Farseer", "EngineerID":300100, "Progress":"Unlocked", "RankProgress":45, "Rank":{{rank}} }"""));

        var short_ = EngineerAccess.CriteriaFor(juri, farseerAt(2))[0];

        Assert.Equal("Grade 3 with Felicity Farseer.", short_.Text);
        Assert.NotEqual(true, short_.Met);
        Assert.True(EngineerAccess.CriteriaFor(juri, farseerAt(3))[0].Met);
    }
}
