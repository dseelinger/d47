using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The hand-authored map of settings rows into areas and places (#217), checked against a live surface.</summary>
public class SettingsLayoutTests
{
    [Fact]
    public void EveryBoundRowIsPlacedExactlyOnceTests()
    {
        var surface = Surface();
        var placedKeys = AllPlacedKeys(surface.Settings);

        var duplicates = placedKeys
            .GroupBy(key => key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        Assert.True(duplicates.Length == 0, $"Placed more than once: {string.Join(", ", duplicates)}");

        var placed = placedKeys.ToHashSet(StringComparer.Ordinal);

        var boundKeys = surface.Settings.Sections
            .SelectMany(section => section.Rows)
            .Where(row => !row.PageTop && !row.DrawnElsewhere)
            .Select(row => row.Key)
            .ToArray();

        var unplaced = boundKeys.Where(key => !placed.Contains(key)).ToArray();
        Assert.True(
            unplaced.Length == 0,
            $"Bound but not placed in SettingsLayout.cs: {string.Join(", ", unplaced)}");

        var boundSet = boundKeys.ToHashSet(StringComparer.Ordinal);
        var placedButNotBound = placed.Where(key => !boundSet.Contains(key)).ToArray();
        Assert.True(
            placedButNotBound.Length == 0,
            $"Placed in SettingsLayout.cs but not a bound row: {string.Join(", ", placedButNotBound)}");
    }

    [Fact]
    public void EveryEntryResolvesToAtLeastOneRowTests()
    {
        var surface = Surface();
        var empty = new List<string>();

        foreach (var place in AllPlaceIds())
        {
            foreach (var entry in EntriesFor(place))
            {
                if (RowsForEntry(surface.Settings, place, entry).Count == 0)
                {
                    empty.Add($"{entry.Key ?? "(family)"} in {place}");
                }
            }
        }

        Assert.True(empty.Count == 0, $"Entries that resolve to nothing: {string.Join(", ", empty)}");
    }

    [Fact]
    public void AreaPlaceAndTabIdsAreEachUniqueTests()
    {
        AssertUnique("area", SettingsLayout.Areas.Select(a => a.Id));
        AssertUnique(
            "place",
            SettingsLayout.Areas.SelectMany(a => a.Places).Select(p => p.Id));
        AssertUnique("tab place", SettingsLayout.Tabs.Select(t => t.Id));

        static void AssertUnique(string what, IEnumerable<string> ids)
        {
            var duplicates = ids
                .GroupBy(id => id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.True(duplicates.Length == 0, $"Duplicate {what} id(s): {string.Join(", ", duplicates)}");
        }
    }

    [Fact]
    public void EveryDocsCapabilityIdIsARegisteredCapabilityTests()
    {
        var registered = Surface().Registry.All
            .Select(c => c.Descriptor.Id)
            .ToHashSet(StringComparer.Ordinal);

        var missing = SettingsLayout.Areas
            .SelectMany(a => a.Places)
            .Select(p => p.DocsCapabilityId)
            .Where(id => !registered.Contains(id))
            .ToArray();

        Assert.True(missing.Length == 0, $"DocsCapabilityId names no registered capability: {string.Join(", ", missing)}");
    }

    [Fact]
    public void NoAreaHoldsMoreThanSixPlacesTests()
    {
        var offending = SettingsLayout.Areas
            .Where(a => a.Places.Count > SettingsLayout.MostPlacesPerArea)
            .Select(a => $"{a.Id} ({a.Places.Count})")
            .ToArray();

        Assert.True(offending.Length == 0, $"Areas over the cap: {string.Join(", ", offending)}");
    }

    [Fact]
    public void NoPlaceShowsMoreThanEightEntriesExceptTheDocumentedExceptionTests()
    {
        var surface = Surface();

        var offending = SettingsLayout.Areas
            .SelectMany(a => a.Places)
            .Where(p => !SettingsLayout.ShownLimitExceptions.Contains(p.Id))
            .Select(p => (p.Id, Shown: ShownCount(surface.Settings, p.Id)))
            .Where(p => p.Shown > SettingsLayout.MostShownPerPlace)
            .Select(p => $"{p.Id} ({p.Shown})")
            .ToArray();

        Assert.True(offending.Length == 0, $"Places over the shown cap: {string.Join(", ", offending)}");
    }

    [Fact]
    public void NoPlaceHoldsMoreThanFourteenEntriesExceptTheDocumentedExceptionTests()
    {
        var offending = SettingsLayout.Areas
            .SelectMany(a => a.Places)
            .Where(p => !SettingsLayout.TotalLimitExceptions.Contains(p.Id))
            .Select(p => (p.Id, Total: EntryCount(p.Id)))
            .Where(p => p.Total > SettingsLayout.MostEntriesPerPlace)
            .Select(p => $"{p.Id} ({p.Total})")
            .ToArray();

        Assert.True(offending.Length == 0, $"Places over the total cap: {string.Join(", ", offending)}");
    }

    /// <summary>
    /// The issue's own arrangement table names 8 of these; the acceptance rule requires every row
    /// <see cref="AboutCapability"/> declares, which is 11 (#217, "Notes for the build").
    /// </summary>
    [Fact]
    public void UpdatesHoldsExactlyTheElevenNamedRowsTests()
    {
        var surface = Surface();
        var rows = surface.Settings.RowsForPlace("updates");

        Assert.Equal(11, EntryCount("updates"));
        Assert.Equal(11, ShownCount(surface.Settings, "updates"));

        Assert.Equal(
            [
                PrivacyCapability.UpdateCheckKey,
                AboutCapability.InstallUpdateKey,
                AboutCapability.ChangelogKey,
                AboutCapability.ChangelogOnlineKey,
                AboutCapability.SetUpKeysKey,
                AboutCapability.StartMenuKey,
                AboutCapability.DataFolderKey,
                AboutCapability.CommunityKey,
                AboutCapability.VersionKey,
                AboutCapability.BuildKey,
                AboutCapability.AttributionKey,
            ],
            rows.Select(r => r.Key));
    }

    /// <summary>Every channel's Level and Mute, and Duck for the three that duck (#217, "Notes for the build").</summary>
    [Fact]
    public void SoundsHoldsExactlySeventeenAllAdvancedEntriesTests()
    {
        var surface = Surface();

        Assert.Equal(17, EntryCount("sounds"));
        Assert.Equal(0, ShownCount(surface.Settings, "sounds"));
        Assert.Equal(17, surface.Settings.RowsForPlace("sounds").Count);
    }

    [Fact]
    public void InstallIsTheLastAreaTests()
    {
        Assert.Equal("install", SettingsLayout.Areas[^1].Id);
    }

    [Theory]
    [InlineData("voice-input", ListeningCapability.PushToTalkKeyKey)]
    [InlineData("voice", SpeechCapability.ProviderKey)]
    [InlineData("turn-fails", SpeechCapability.RetryAttemptsKey)]
    [InlineData("may-do", "actions.keyboard")]
    [InlineData("may-do", "actions.takeUsOut")]
    [InlineData("headset", VrCapability.EnabledKey)]
    [InlineData("privacy", "egress.llm")]
    [InlineData("updates", AboutCapability.VersionKey)]
    [InlineData("diagnostics", DiagnosticsCapability.PausedKey)]
    [InlineData("exploring", "callouts.surveyedBiology")]
    public void NamedRowsLandInTheirStatedPlaceTests(string placeId, string key)
    {
        var surface = Surface();

        var keys = surface.Settings.RowsForPlace(placeId).Select(r => r.Key).ToArray();

        Assert.Contains(key, keys);
    }

    [Fact]
    public void AFamilyEntryResolvesEveryMatchingBoundRowTests()
    {
        var surface = Surface();

        var placed = surface.Settings.RowsForPlace("privacy")
            .Select(r => r.Key)
            .Where(SettingsLayout.IsEgressFamily)
            .ToHashSet(StringComparer.Ordinal);

        var bound = surface.Settings.Sections
            .SelectMany(s => s.Rows)
            .Select(r => r.Key)
            .Where(SettingsLayout.IsEgressFamily)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(bound, placed);
        Assert.True(bound.Count > 0);
    }

    [Fact]
    public void ATabPlaceResolvesToItsFlatListOfRowsTests()
    {
        var surface = Surface();

        var keys = surface.Settings.RowsForPlace("fleet-ships").Select(r => r.Key).ToArray();

        Assert.Equal(["ships.remembered", "ships.art"], keys);
    }

    [Fact]
    public void ResetPlaceResetsAChangedAttemptsRowAndLeavesTheProviderRowAloneTests()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply(SpeechCapability.RetryAttemptsKey, "5", SettingsCaller.Panel);
        surface.Settings.Apply(
            ConversationCapability.ProviderKey, D47.Core.Conversation.LlmProviderCatalog.OpenAiId, SettingsCaller.Panel);

        var moved = surface.Settings.ResetPlace("turn-fails", SettingsCaller.Panel);

        Assert.Equal(1, moved);
        Assert.False(surface.Settings.IsChanged(SpeechCapability.RetryAttemptsKey));
        Assert.True(surface.Settings.IsChanged(ConversationCapability.ProviderKey));
    }

    [Fact]
    public void ResetPlaceWithNothingChangedResetsNothingTests()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        Assert.Equal(0, surface.Settings.ResetPlace("turn-fails", SettingsCaller.Panel));
    }

    [Fact]
    public void ResetPlaceWorksOnATabPlaceTooTests()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);

        surface.Settings.Apply("knowledge.notablePlaces", "true", SettingsCaller.Panel);

        var moved = surface.Settings.ResetPlace("adventures", SettingsCaller.Panel);

        Assert.Equal(1, moved);
        Assert.False(surface.Settings.IsChanged("knowledge.notablePlaces"));
    }

    private static TestSurface Surface() => TestSurface.For(new TempInstall(), everyOptionalSurface: true);

    private static IEnumerable<string> AllPlaceIds() =>
        SettingsLayout.Areas.SelectMany(a => a.Places).Select(p => p.Id)
            .Concat(SettingsLayout.Tabs.Select(t => t.Id));

    private static IReadOnlyList<SettingsEntry> EntriesFor(string placeId)
    {
        var place = SettingsLayout.Areas.SelectMany(a => a.Places).FirstOrDefault(p => p.Id == placeId);

        if (place is not null)
        {
            return [.. place.Groups.SelectMany(g => g.Entries)];
        }

        return SettingsLayout.Tabs.First(t => t.Id == placeId).Entries;
    }

    /// <summary>Declared layout entries, which is what the shown and total caps count — not resolved rows.</summary>
    private static int EntryCount(string placeId) => EntriesFor(placeId).Count;

    /// <summary>
    /// An entry counts as shown when any row it resolves to has <c>Advanced == false</c> or
    /// <c>Kind == Secret</c> (#217, "Accepted when").
    /// </summary>
    private static int ShownCount(SettingsService settings, string placeId) =>
        EntriesFor(placeId).Count(entry =>
            RowsForEntry(settings, placeId, entry).Any(row => !row.Advanced || row.Kind == SettingKind.Secret));

    private static IReadOnlyList<SettingRow> RowsForEntry(SettingsService settings, string placeId, SettingsEntry entry)
    {
        var all = settings.RowsForPlace(placeId);

        if (entry.Key is { } key)
        {
            return [.. all.Where(r => string.Equals(r.Key, key, StringComparison.Ordinal))];
        }

        return [.. all.Where(r => entry.Family!(r.Key))];
    }

    private static IReadOnlyList<string> AllPlacedKeys(SettingsService settings) =>
        [.. AllPlaceIds().SelectMany(settings.RowsForPlace).Select(r => r.Key)];
}
