using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Help;

/// <summary>Every phrase in the book is described by one sentence built from what it reaches (#538).</summary>
public class EveryPhraseSaysWhatItDoesTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly TempInstall _install = new();
    private readonly CapabilityRegistry _registry;
    private readonly PhraseBook _book;

    public EveryPhraseSaysWhatItDoesTests()
    {
        _registry = TestSurface.For(_install).Registry;
        _book = PhraseBook.From(_registry, []);
    }

    public void Dispose() => _install.Dispose();

    private PhraseEntry Reaching(Func<PhraseEntry, bool> where) => _book.Entries.First(where);

    [Fact]
    public void ASettingWithAValueSaysWhatItSets()
    {
        var entry = Reaching(e => e.Row is not null && e.Value is { Length: > 0 });

        Assert.Equal($"Sets {entry.Row!.Label} to {entry.Value}.", PhraseBook.Describe(entry, _registry));
    }

    [Fact]
    public void ASettingWithNoValueSaysWhatItReports()
    {
        var row = Reaching(e => e.Row is not null).Row!;
        var entry = new PhraseEntry
        {
            Phrase = "read it out",
            Source = PhraseSource.SettingCommand,
            CapabilityId = HelpCapability.Id,
            Row = row,
            Guarded = false,
        };

        Assert.Equal($"Reports {row.Label}.", PhraseBook.Describe(entry, _registry));
    }

    [Fact]
    public void AGameActionSaysWhatItReaches()
    {
        var entry = Reaching(e =>
            e.Arguments.TryGetValue("action", out var id) && D47.Core.Input.GameActions.All.Any(a => a.Id == id));
        var action = D47.Core.Input.GameActions.All.First(a => a.Id == entry.Arguments["action"]);

        Assert.Equal($"Reaches {action.Label}.", PhraseBook.Describe(entry, _registry));
    }

    [Fact]
    public void AToolIsDescribedByTheFirstSentenceOfItsDescriptionOnly()
    {
        var entry = new PhraseEntry
        {
            Phrase = "what can you do",
            Source = PhraseSource.Keyword,
            CapabilityId = HelpCapability.Id,
            ToolName = "get_capabilities",
            Guarded = false,
        };

        var description = PhraseBook.Describe(entry, _registry);

        Assert.Equal("List what D47 can do, from its own capability registry.", description);
    }

    [Fact]
    public void AnEntryWithNoToolFallsBackToTheCapabilitySummary()
    {
        var entry = new PhraseEntry
        {
            Phrase = "help",
            Source = PhraseSource.Keyword,
            CapabilityId = HelpCapability.Id,
            Guarded = false,
        };

        Assert.Equal(
            _registry.Find(HelpCapability.Id)!.Descriptor.Summary, PhraseBook.Describe(entry, _registry));
    }

    [Fact]
    public void EveryPhraseInTheBookIsDescribedWithoutNamingATool()
    {
        var toolNames = _registry.All.SelectMany(c => c.Descriptor.Tools.Select(t => t.Name)).ToHashSet();

        foreach (var entry in _book.Entries)
        {
            var description = PhraseBook.Describe(entry, _registry);

            Assert.False(string.IsNullOrWhiteSpace(description), entry.Phrase);

            foreach (var name in toolNames)
            {
                Assert.False(
                    description.Contains(name, StringComparison.Ordinal),
                    $"\"{entry.Phrase}\" is described as \"{description}\", which names {name}");
            }
        }
    }

    [Fact]
    public void ACommandersOwnPhraseIsFoundByItsWordingAndSaysWhatItDoes()
    {
        var learned = new LearnedPhrase("drop the wheels", "gear down", At);
        var gearDown = Reaching(e => e.Phrase == "gear down");

        var answer = HelpCapability.FindPhrase(_book, _registry, "drop the wheels", [learned]);

        Assert.Contains("'drop the wheels'", answer, StringComparison.Ordinal);
        Assert.Contains("'gear down'", answer, StringComparison.Ordinal);
        Assert.Contains(PhraseBook.Describe(gearDown, _registry), answer, StringComparison.Ordinal);
    }

    [Fact]
    public void ACommandersOwnPhraseIsNotFoundForAnotherGoal()
    {
        var learned = new LearnedPhrase("drop the wheels", "gear down", At);

        var answer = HelpCapability.FindPhrase(_book, _registry, "juggle flaming Thargoid eggs", [learned]);

        Assert.DoesNotContain("drop the wheels", answer, StringComparison.Ordinal);
    }
}
