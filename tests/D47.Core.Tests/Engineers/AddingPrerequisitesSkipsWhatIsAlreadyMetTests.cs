using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// <see cref="EngineerAccess.UnmetPrerequisites"/> is what "Add to checklist" adds — one item for every
/// prerequisite whose <see cref="UnlockCriterion.Met"/> is false or null, and nothing for a line already
/// met (#257).
/// </summary>
public class AddingPrerequisitesSkipsWhatIsAlreadyMetTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json.ReplaceLineEndings(" "), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static Engineer Named(string name) =>
        EngineerDirectory.ByName(name) ?? throw new InvalidOperationException($"no {name}");

    /// <summary>
    /// Marco Qwent: referred by Elvira Martuuk at grade 3 (met, once she is unlocked at that grade), an
    /// untested invitation (unmet, no reading yet) and a contribution tribute (unmet, no reading yet) —
    /// three lines, one met.
    /// </summary>
    [Fact]
    public void OneMetLineOfThreeLeavesTwoToAdd()
    {
        var marco = Named("Marco Qwent");

        var progress = EngineerProgressState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Elvira Martuuk","EngineerID":300160,"Progress":"Unlocked","Rank":3}]}"""));

        var criteria = EngineerAccess.CriteriaFor(marco, new D47.Core.Engineers.UnlockEvidence(progress, null, null, null, null, null));

        Assert.Equal(3, criteria.Count);
        Assert.Single(criteria, criterion => criterion.Met == true);

        var items = EngineerAccess.UnmetPrerequisites(
            marco, new D47.Core.Engineers.UnlockEvidence(progress, null, null, null, null, null));

        Assert.Equal(2, items.Count);
        Assert.All(items, item => Assert.Equal(ChecklistScope.Universal, item.Scope));
        Assert.All(items, item => Assert.Equal(ChecklistItemKind.Derived, item.Kind));
        Assert.All(items, item => Assert.Equal(ChecklistSource.EngineerPrerequisite, item.Source));

        Assert.Contains(items, item => item.Intent!.Kind == ChecklistIntentKind.EngineerPrerequisite
            && item.Intent.Detail == EngineerAccess.InvitationRole);
        Assert.Contains(items, item => item.Intent!.Kind == ChecklistIntentKind.EngineerPrerequisite
            && item.Intent.Detail == EngineerAccess.TributeRole);

        // Not the referral, which is already met.
        Assert.DoesNotContain(items, item => item.Intent!.Kind == ChecklistIntentKind.EngineerAccess);
    }

    /// <summary>With every line already met, there is nothing to add.</summary>
    [Fact]
    public void NothingIsAddedWhenEverythingIsAlreadyMet()
    {
        var marco = Named("Marco Qwent");

        var progress = EngineerProgressState.Empty.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"EngineerProgress","Engineers":[{"Engineer":"Marco Qwent","EngineerID":300200,"Progress":"Unlocked","Rank":1}]}"""));

        var items = EngineerAccess.UnmetPrerequisites(
            marco, new D47.Core.Engineers.UnlockEvidence(progress, null, null, null, null, null));

        Assert.Empty(items);
    }

    /// <summary>
    /// A referral is added as the existing <see cref="ChecklistIntentKind.EngineerAccess"/> intent, at
    /// the referral's grade, so it can be tracked and ticked the same way a plan's chain items already
    /// are.
    /// </summary>
    [Fact]
    public void AnUnmetReferralReusesTheEngineerAccessIntent()
    {
        var marco = Named("Marco Qwent");

        var items = EngineerAccess.UnmetPrerequisites(
            marco, new D47.Core.Engineers.UnlockEvidence(null, null, null, null, null, null));

        var referral = Assert.Single(items, item => item.Intent!.Kind == ChecklistIntentKind.EngineerAccess);

        Assert.Equal("Elvira Martuuk", referral.Intent!.Subject);
        Assert.Equal(3, referral.Intent.Grade);
    }
}
