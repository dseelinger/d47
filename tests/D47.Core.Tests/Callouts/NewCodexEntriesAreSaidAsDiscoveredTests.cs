using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>Every new codex entry is said as "Discovered" and its name, in one line per tick.</summary>
public class NewCodexEntriesAreSaidAsDiscoveredTests
{
    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Entry(long id, string? name, bool isNew = true, string sub = "$Codex_SubCategory_Stars;") => $$"""
        {"timestamp":"2026-08-16T10:00:00Z","event":"CodexEntry","EntryID":{{id}},"Name":"$Codex_Ent_{{id}};",
         {{(name is null ? "" : $"\"Name_Localised\":\"{name}\",")}}"SubCategory":"{{sub}}","Region":"$Codex_RegionName_18;",
         "Region_Localised":"Inner Orion Spur"{{(isNew ? ",\"IsNewEntry\":true" : "")}}}
        """;

    private static List<Announcement> Say(bool priming, params string[] lines) =>
        [
            .. new CodexDiscoveredCallout().Examine(new CalloutContext(
                DateTimeOffset.UnixEpoch,
                IsPriming: priming,
                State: null,
                Status: GameStatus.Unknown,
                Route: NavRoute.None,
                Events: [.. lines.Select(Parse)])),
        ];

    [Theory]
    [InlineData("$Codex_SubCategory_Stars;")]
    [InlineData("$Codex_SubCategory_Organic_Structures;")]
    [InlineData("$Codex_SubCategory_Geology_Structures;")]
    [InlineData("$Codex_SubCategory_Xenological;")]
    public void ANewEntryOfAnyCategoryIsSaid(string sub)
    {
        var said = Assert.Single(Say(false, Entry(1, "T Tauri Star", sub: sub)));

        Assert.Equal("Discovered T Tauri Star.", said.Text);
    }

    [Fact]
    public void AnEntryThatIsNotNewSaysNothing() =>
        Assert.Empty(Say(false, Entry(1, "T Tauri Star", isNew: false)));

    [Fact]
    public void AnEntryWithoutAnEnglishNameSaysNothing() =>
        Assert.Empty(Say(false, Entry(1, null)));

    [Fact]
    public void NothingIsSaidWhilePriming() =>
        Assert.Empty(Say(true, Entry(1, "T Tauri Star")));

    [Fact]
    public void TwoDiscoveriesInOneTickMakeOneLine()
    {
        var said = Assert.Single(Say(false, Entry(1, "T Tauri Star"), Entry(2, "Q09-Type Anomaly")));

        Assert.Equal("Discovered T Tauri Star and Q09-Type Anomaly.", said.Text);
    }

    [Fact]
    public void DiscoveryReadsTheEntryIdRegionAndName()
    {
        var found = CodexDiscovery.Of(Parse(Entry(7, "T Tauri Star")));

        Assert.Equal(new CodexDiscovery(7, 18, "T Tauri Star"), found);
    }

    [Fact]
    public void AnEventThatIsNotACodexEntryIsNotADiscovery() =>
        Assert.Null(CodexDiscovery.Of(Parse("""{"timestamp":"2026-08-16T10:00:00Z","event":"Music","MusicTrack":"x"}""")));
}
