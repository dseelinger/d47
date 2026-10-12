using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>Missions, the Engineers directory, Custom voices and voice pickers order their rows from a Segmented above the list.</summary>
public class ASortControlOrdersEachSingleLabelListTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 18, 0, 0, TimeSpan.Zero);

    private static Mission Offer(long id, long? reward, TimeSpan? expiresIn) =>
        new(id, $"Mission_{id}") { Reward = reward, Expiry = expiresIn is { } span ? Now + span : null };

    [Fact]
    public void RewardListsEachBandsHighestFirstAndKeepsTheBands()
    {
        var soonLow = Offer(1, 100, TimeSpan.FromMinutes(20));
        var soonHigh = Offer(2, 900, TimeSpan.FromMinutes(40));
        var restLow = Offer(3, 200, TimeSpan.FromDays(2));
        var restNone = Offer(4, null, TimeSpan.FromDays(3));
        var restHigh = Offer(5, 800, TimeSpan.FromDays(1));
        var board = new MissionBoard { Missions = [soonLow, soonHigh, restLow, restNone, restHigh], SeenAt = Now };
        var ranked = board.Ranked(Now, JournalLocation.Unknown);

        var sorted = MissionsPage.Sorted(ranked, 2, Now, JournalLocation.Unknown);

        Assert.Equal([2L, 1, 5, 3, 4], sorted.Select(mission => mission.Id));
    }

    [Fact]
    public void ExpiryListsEachBandsSoonestFirstAndAMissionWithNoneLast()
    {
        var late = Offer(1, 100, TimeSpan.FromDays(3));
        var none = Offer(2, 100, null);
        var early = Offer(3, 100, TimeSpan.FromDays(1));
        var board = new MissionBoard { Missions = [late, none, early], SeenAt = Now };

        var sorted = MissionsPage.Sorted(board.Ranked(Now, JournalLocation.Unknown), 1, Now, JournalLocation.Unknown);

        Assert.Equal([3L, 1, 2], sorted.Select(mission => mission.Id));
    }

    [Fact]
    public void TheBoardSortLeavesTheRankedOrderAlone()
    {
        var board = new MissionBoard { Missions = [Offer(1, 100, TimeSpan.FromDays(3)), Offer(2, 900, TimeSpan.FromDays(1))], SeenAt = Now };
        var ranked = board.Ranked(Now, JournalLocation.Unknown);

        Assert.Equal(ranked, MissionsPage.Sorted(ranked, 0, Now, JournalLocation.Unknown));
    }

    private static EngineerEntry Entry(string name, EngineerReach reach, double? lightYears) =>
        new() { Engineer = EngineerDirectory.ByName(name)!, Reach = reach, LightYears = lightYears };

    [Fact]
    public void DistanceListsEachReachGroupNearestFirstAndNoDistanceLast()
    {
        var far = Entry("Felicity Farseer", EngineerReach.WithinReach, 300);
        var unknown = Entry("Elvira Martuuk", EngineerReach.WithinReach, null);
        var near = Entry("Hera Tani", EngineerReach.WithinReach, 12);
        var other = Entry("Lei Cheung", EngineerReach.Unlocked, 500);

        var sorted = EngineerDirectoryPage.Sorted([far, unknown, near, other], 2);

        Assert.Equal(["Hera Tani", "Felicity Farseer", "Elvira Martuuk", "Lei Cheung"], sorted.Select(entry => entry.Engineer.Name));
    }

    [Fact]
    public void NameListsEachReachGroupAToZ()
    {
        var sorted = EngineerDirectoryPage.Sorted(
            [Entry("Hera Tani", EngineerReach.WithinReach, 12), Entry("Elvira Martuuk", EngineerReach.WithinReach, 99)], 1);

        Assert.Equal(["Elvira Martuuk", "Hera Tani"], sorted.Select(entry => entry.Engineer.Name));
    }

    [Fact]
    public void CustomVoicesSortAToZByName()
    {
        CustomVoice[] added = [new("my-00000001", "Zed", "", "mid", "even"), new("my-00000002", "alice", "", "mid", "even")];

        Assert.Equal(["alice", "Zed"], CustomVoicesPage.Sorted(added, 1).Select(voice => voice.Name));
        Assert.Equal(["Zed", "alice"], CustomVoicesPage.Sorted(added, 0).Select(voice => voice.Name));
    }

    private static PickerPage Picker(IReadOnlyList<PickerSort>? sorts) =>
        PickerPage.For(new PickerRequest { Prompt = "Voice", Choices = ["c", "a", "b"], Sorts = sorts });

    private static string[] Listed(PickerPage page) =>
        [.. ((IEnumerable<PickerChoice>)page.GetControl<ListBox>("Choices").ItemsSource!).Select(choice => choice.Value)];

    [AvaloniaFact]
    public void ARequestWithSortKeysShowsTheSegmentedAndNameListsAToZ()
    {
        var page = Picker([PickerSort.Name]);
        var sort = page.GetControl<Segment>("SortBox");

        Assert.True(sort.IsVisible);
        Assert.Equal(["Listed", "Name"], sort.ItemsSource);
        Assert.Equal(["c", "a", "b"], Listed(page));

        page.Show();
        sort.GetVisualDescendants().OfType<RadioButton>().ElementAt(1).IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["a", "b", "c"], Listed(page));
    }

    [AvaloniaFact]
    public void ARequestWithoutSortKeysShowsNoSegmented()
    {
        Assert.False(Picker(null).GetControl<Segment>("SortBox").IsVisible);
    }
}
