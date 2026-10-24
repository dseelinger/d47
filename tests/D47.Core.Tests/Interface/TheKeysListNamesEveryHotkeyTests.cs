using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>The keys list shows every hotkey row in the registry under its settings group, then the fixed keys (#1531).</summary>
public sealed class TheKeysListNamesEveryHotkeyTests
{
    private static IReadOnlyList<KeyGroup> Build(TestSurface surface, Func<SettingRow, IReadOnlyList<string?>>? boundAs = null) =>
        KeyList.Build(surface.Settings, boundAs ?? (row => [.. row.BoundKeys.Select(key => surface.Settings.Read(key))]));

    private static TestSurface Surface() => TestSurface.For(new MemoryInstall(), everyOptionalSurface: true);

    [Fact]
    public void EveryHotkeyRowInTheRegistryIsListedByItsLabel()
    {
        var surface = Surface();
        var listed = Build(surface).Where(g => g.Changeable).SelectMany(g => g.Entries).Select(e => e.Action).ToHashSet();

        var missing = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => row.Kind == SettingKind.Hotkey && !listed.Contains(row.Label))
            .Select(row => row.Key)
            .ToList();

        Assert.True(missing.Count == 0, $"Hotkey rows missing from the keys list: {string.Join(", ", missing)}");
    }

    [Fact]
    public void TheGroupsComeInTheOrderTheDesignDraws()
    {
        var titles = Build(Surface()).Select(g => g.Title).ToArray();

        Assert.Equal(
            ["Getting around", "Look and keys", "On the page", "In the ask box", "Microphone", "Comms channel keys", "Overlay", "Headset overlay", "Galaxy map", "Hull viewer"],
            titles);
    }

    [Fact]
    public void AChangeableGroupNamesItsSettingsPlace()
    {
        var wheres = Build(Surface()).Where(g => g.Changeable).Select(g => g.Where!).ToArray();

        Assert.Equal(["Settings › Window", "Settings › Voice Input", "Settings › What D47 may do", "Settings › Overlay", "Settings › Headset"], wheres);
    }

    [Fact]
    public void OnlyTheGroupsThatWorkInTheGameSayTheyDo()
    {
        var notes = Build(Surface()).Where(g => g.Changeable).ToDictionary(g => g.Title, g => g.Note);

        Assert.Null(notes["Look and keys"]);
        Assert.Equal("Work while Elite has the foreground", notes["Microphone"]);
        Assert.Equal("Work while Elite has the foreground", notes["Overlay"]);
        Assert.Equal("Work while Elite has the foreground", notes["Headset overlay"]);
        Assert.Equal("Work while Elite has the foreground", notes["Comms channel keys"]);
    }

    [Fact]
    public void ABoundKeyIsShownAndAClearedOneIsUnbound()
    {
        var surface = Surface();
        var groups = Build(surface, row => row.Key == "hotkeys.find" ? ["F23"] : [null]);
        var look = groups.Single(g => g.Title == "Look and keys").Entries;

        Assert.Equal(["F23"], look.Single(e => e.Action == "Find").Keys);
        Assert.True(look.Single(e => e.Action == "Open settings").Unbound);
    }

    [Fact]
    public void AKeyAndAButtonBoundTogetherAreBothShown()
    {
        var groups = Build(Surface(), row => row.Key == "listening.pushToTalkKey" ? ["Scroll", "button 12"] : [null]);
        var talk = groups.Single(g => g.Title == "Microphone").Entries.Single(e => e.Action == "Push-to-talk");

        Assert.Equal(["Scroll", "button 12"], talk.Keys);
        Assert.Equal("or", talk.Joiner);
    }

    [Fact]
    public void TheFixedGettingAroundKeysAreInTheOrderGiven()
    {
        var around = Build(Surface()).First().Entries.Select(e => e.Action).ToArray();

        Assert.Equal(
            [
                "Show the first to seventh tab", "Next tab", "Previous tab", "Go back",
                "Move between the rows of a page", "Help for the page you are on", "This list, when no text box has the cursor",
            ],
            around);
    }
}
