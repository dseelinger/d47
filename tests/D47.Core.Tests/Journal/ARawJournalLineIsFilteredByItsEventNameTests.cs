using System.Reflection;
using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

public sealed class ARawJournalLineIsFilteredByItsEventNameTests
{
    private static string Line(string kind) => $$"""{"timestamp":"2026-10-01T00:00:00Z","event":"{{kind}}"}""";

    [Fact]
    public void AnExactNameMatchesAndAnotherNameDoesNot()
    {
        var filter = JournalLineFilter.Events("Docked", "Undocked");

        Assert.True(filter.Matches(Line("Docked")));
        Assert.False(filter.Matches(Line("DockingGranted")));
        Assert.False(filter.Matches(Line("Dock")));
        Assert.False(filter.Matches("""{"timestamp":"2026-10-01T00:00:00Z"}"""));
    }

    [Theory]
    [InlineData(typeof(CarrierBackfill), "CarrierStats")]
    [InlineData(typeof(PowerplayCycleBackfill), "PowerplayMerits")]
    [InlineData(typeof(MissionBackfill), "MissionAccepted")]
    public void APrefixFilterPassesALineWhoseNameStartsWithItAndFailsOthers(Type reader, string kind)
    {
        var filter = (JournalLineFilter)reader.GetField("Reads", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

        Assert.True(filter.Matches(Line(kind)));
        Assert.False(filter.Matches(Line("FSDJump")));
    }

    [Fact]
    public void EveryNameAFilterMatchesIsActedOn()
    {
        var unhandled = new SortedSet<string>(StringComparer.Ordinal);
        var filters = 0;

        foreach (var type in typeof(JournalLineFilter).Assembly.GetTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType == typeof(JournalLineFilter) && field.GetValue(null) is JournalLineFilter filter)
                {
                    filters++;

                    foreach (var kind in filter.Kinds.Where(kind => !HandledEvents.ActedOn.Contains(kind)))
                    {
                        _ = unhandled.Add($"{type.Name}.{field.Name}: {kind}");
                    }
                }
            }
        }

        Assert.True(filters >= 20, $"Found only {filters} filters.");
        Assert.Empty(unhandled);
    }
}
