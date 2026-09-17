using D47.Core.Checklists;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Checklists;

/// <summary>
/// Narrowing the checklist to one engineer's own unlock — their invitation, their tribute, and the
/// referral required before either — and nothing else on the list (#265).
/// </summary>
public class FocusingTheChecklistOnOneEngineerTests
{
    private const int ElviraMartuuk = 300160;
    private const int MarcoQwent = 300200;
    private const int LoriJameson = 300230;

    private static Engineer Named(string name) =>
        EngineerDirectory.ByName(name) ?? throw new InvalidOperationException($"no {name}");

    /// <summary>Marco Qwent (referred by Elvira Martuuk) and Hera Tani (referred by Liz Ryder), plus a
    /// line of the Commander's own that neither filter should keep.</summary>
    private static (ChecklistService Checklists, Engineer Marco, Engineer Hera) Built()
    {
        using var install = new TempInstall();
        var checklists = TestSurface.Checklists(install.Paths);

        var marco = Named("Marco Qwent");
        var hera = Named("Hera Tani");

        checklists.AddPrerequisites(marco);
        checklists.AddPrerequisites(hera);
        checklists.AddNote(ChecklistScope.Universal, "buy limpets");

        return (checklists, marco, hera);
    }

    /// <summary>One entry per engineer with a prerequisite on the list, worded with their name, under
    /// their own heading — and no entry for an engineer nothing was added for.</summary>
    [Fact]
    public void OneEntryPerEngineerWithPrerequisitesOnTheList()
    {
        var (checklists, marco, hera) = Built();

        var axes = checklists.FilterAxes();

        var marcoRow = Assert.Single(axes, filter => filter.Key == ChecklistService.EngineerFilterKey(marco.Id));
        var heraRow = Assert.Single(axes, filter => filter.Key == ChecklistService.EngineerFilterKey(hera.Id));

        Assert.Equal("Marco Qwent", marcoRow.Word);
        Assert.Equal("Unlocking an engineer", marcoRow.Heading);
        Assert.Equal("Hera Tani", heraRow.Word);
        Assert.Equal("Unlocking an engineer", heraRow.Heading);

        // Nobody else's prerequisites are on the list, so nobody else gets a row.
        Assert.DoesNotContain(axes, filter => filter.Key == ChecklistService.EngineerFilterKey(LoriJameson));
    }

    /// <summary>
    /// Choosing Marco Qwent keeps his referral (Elvira Martuuk, the existing <see
    /// cref="ChecklistIntentKind.EngineerAccess"/> intent), his own invitation and tribute lines, and
    /// nothing that is Hera Tani's or the Commander's own.
    /// </summary>
    [Fact]
    public void ItKeepsOnlyThatEngineersOwnPrerequisitesAndTheirReferral()
    {
        var (checklists, marco, hera) = Built();

        var kept = checklists.Document.Items
            .Where(item => item.IsLive && checklists.OfferedEngineer(item, marco.Id))
            .ToList();

        Assert.Equal(3, kept.Count);

        Assert.Contains(kept, item =>
            item.Intent!.Kind == ChecklistIntentKind.EngineerAccess
            && item.Intent.Subject == "Elvira Martuuk");
        Assert.Contains(kept, item =>
            item.Intent!.Kind == ChecklistIntentKind.EngineerPrerequisite
            && item.Intent.Subject == "Marco Qwent"
            && item.Intent.Detail == EngineerAccess.InvitationRole);
        Assert.Contains(kept, item =>
            item.Intent!.Kind == ChecklistIntentKind.EngineerPrerequisite
            && item.Intent.Subject == "Marco Qwent"
            && item.Intent.Detail == EngineerAccess.TributeRole);

        // Hera Tani's own lines, and the Commander's, are not his.
        Assert.DoesNotContain(kept, item => item.Intent!.Subject == "Hera Tani");
        Assert.DoesNotContain(kept, item => item.Intent!.Subject == "Liz Ryder");
        Assert.DoesNotContain(kept, item => item.Text == "buy limpets");

        // And Hera Tani's filter is the mirror image — none of Marco's lines are hers.
        var herKept = checklists.Document.Items
            .Where(item => item.IsLive && checklists.OfferedEngineer(item, hera.Id))
            .ToList();

        Assert.DoesNotContain(herKept, item => item.Intent!.Subject is "Marco Qwent" or "Elvira Martuuk");
    }

    /// <summary>Nothing is offered where no prerequisite has ever been added for that engineer.</summary>
    [Fact]
    public void AnEngineerWithNothingOnTheListOffersNothing()
    {
        var (checklists, _, _) = Built();

        var lori = Named("Lori Jameson");

        Assert.All(
            checklists.Document.Items,
            item => Assert.False(checklists.OfferedEngineer(item, lori.Id)));
    }

    /// <summary>
    /// Finishing the last of an engineer's lines drops the filter back to <see
    /// cref="ChecklistService.Everything"/> — the only way in, since a Derived line never leaves by a
    /// hand-tick and can only go through <see cref="ChecklistService.Poll"/> forgetting it once it reads
    /// Done (#255).
    /// </summary>
    [Fact]
    public void TheFilterReturnsToEverythingOnceItsLastLineIsGone()
    {
        using var install = new TempInstall();
        var game = new GameStateStore();

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(install.Root, "checklist.json"), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(install.Root, "checklist-proposals.json"),
                NullLogger<ChecklistProposalStore>.Instance),
            () => game.Active,
            removeFulfilled: () => true);

        game.Apply(Event(
            """{"timestamp":"2026-09-01T09:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        var marco = Named("Marco Qwent");
        checklists.AddPrerequisites(marco);
        checklists.Choose(ChecklistService.EngineerFilterKey(marco.Id));

        Assert.Equal(ChecklistService.EngineerFilterKey(marco.Id), checklists.Filter);

        // The referral (Elvira Martuuk, ranked) and Marco's own unlock, both reported met.
        game.Apply(Event(
            $$"""
            {"timestamp":"2026-09-01T09:00:01Z","event":"EngineerProgress","Engineers":[
              {"Engineer":"Elvira Martuuk","EngineerID":{{ElviraMartuuk}},"Progress":"Unlocked","Rank":5},
              {"Engineer":"Marco Qwent","EngineerID":{{MarcoQwent}},"Progress":"Unlocked","Rank":1}]}
            """));

        checklists.Poll();

        Assert.DoesNotContain(checklists.Document.Items, item => item.IsLive);
        Assert.Equal(ChecklistService.Everything, checklists.Filter);
    }

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
