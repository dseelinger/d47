using System.Text.RegularExpressions;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Tests.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// The biology items of <see cref="SystemWorth"/> against <c>get_biology_left_in_system</c>, system by system, over
/// real journal lines in <c>tests/fixtures/exobiology/sampled-bodies.log</c>.
/// </summary>
[Trait("Category", "Integration")]
public partial class TheSystemWorthAgreesWithWhatIsLeftToSampleTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EverySystemHasTheSameBiologyLeftInBoth()
    {
        var store = new GameStateStore();
        var names = new Dictionary<long, string>();

        store.Apply(Parse("""{"timestamp":"2025-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}"""));

        foreach (var line in EmbeddedFixture.Lines("exobiology.sampled-bodies.log"))
        {
            var journalEvent = Parse(line);
            store.Apply(journalEvent);

            if (journalEvent.Long("SystemAddress") is { } address && journalEvent.String("StarSystem") is { } name)
            {
                names.TryAdd(address, name);
            }
        }

        var registry = CapabilityRegistry.Build(
            [ExobiologyCapability.Create(null, () => store.Active, TestSurface.For(new MemoryInstall()).Settings)]);
        var compared = 0;

        foreach (var (address, system) in names)
        {
            store.Apply(Parse(
                $$"""{"timestamp":"2026-01-01T00:00:00Z","event":"Location","StarSystem":"{{system}}","SystemAddress":{{address}},"StarPos":[0,0,0]}"""));

            var said = (await registry.InvokeAsync(
                    ExobiologyCapability.LeftInSystemTool, ToolArguments.FromJson("{}"), TestContext.Current.CancellationToken))
                .Content;
            var state = store.Active!;
            var biology = SystemWorth.For(state, address, StarPosition.Origin, null, 0, []).Items
                .Where(item => item.Kind == WorthKind.Biology)
                .ToList();
            var mapped = biology.Where(item => state.Biology.For(address, item.BodyId!.Value)?.Mapped == true).ToList();
            var unmapped = biology.Count - mapped.Count;

            output.WriteLine($"{system}: {said}");

            var named = NamedLeft(said);
            var rest = Rest().Match(said) is { Success: true } more ? int.Parse(more.Groups[1].Value) : 0;
            var notMapped = NotMapped().Match(said) is { Success: true } count ? int.Parse(count.Groups[1].Value) : 0;

            Assert.Equal(named.Count + rest, mapped.Sum(item => item.Names.Count));
            Assert.Equal(notMapped, unmapped);
            Assert.Subset(
                mapped.Select(item => BiologyCallout.Short(item.BodyName, system)).ToHashSet(),
                named.ToHashSet());

            compared++;
        }

        Assert.Equal(21, compared);
    }

    /// <summary>The body of each genus the answer names before its count of the rest.</summary>
    private static List<string> NamedLeft(string said)
    {
        const string Lead = "Left to sample: ";

        if (!said.StartsWith(Lead, StringComparison.Ordinal))
        {
            return [];
        }

        var list = said[Lead.Length..];
        var more = list.IndexOf(", and ", StringComparison.Ordinal);
        var end = more >= 0 ? more : list.IndexOf('.', StringComparison.Ordinal);

        return [.. list[..end].Split("; ").Select(entry => entry[(entry.LastIndexOf(" on ", StringComparison.Ordinal) + 4)..])];
    }

    [GeneratedRegex(@", and (\d+) more on \d+ bod")]
    private static partial Regex Rest();

    [GeneratedRegex(@"(\d+) bod(?:y is|ies are) not mapped")]
    private static partial Regex NotMapped();

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
