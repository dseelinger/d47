using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Every tool, setting and dynamic phrase reaches what it was declared for when it is said, so no
/// two phrases in the registry resolve to different targets.
/// </summary>
[Trait("Category", "Integration")]
public class NoTwoPhrasesReachDifferentTargetsGateTests
{
    [Fact]
    public void EveryDeclaredPhraseReachesItsOwnTarget()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var book = PhraseBook.From(registry, []);

        var dynamic = book.Entries
            .Where(entry => entry.Source == PhraseSource.Dynamic && entry.ToolName is not null)
            .Select(entry => new DynamicCommand(entry.Phrase, entry.CapabilityId, entry.ToolName!, entry.Arguments))
            .ToList();

        var router = new KeywordRouter(registry, () => dynamic);

        var wrong = new List<string>();

        foreach (var entry in book.Entries)
        {
            if (Shared.Contains(entry.Phrase) || entry.When is { } when && !when())
            {
                continue;
            }

            if (entry.Source == PhraseSource.SettingCommand)
            {
                var setting = router.MatchSetting(entry.Phrase);

                if (setting is null || setting.Row.Key != entry.Row!.Key || setting.Value != entry.Value)
                {
                    wrong.Add($"'{entry.Phrase}' (setting {entry.Row!.Key}={entry.Value}) reached {Describe(setting)}");
                }
            }
            else if (entry.Source is PhraseSource.ToolCommand or PhraseSource.Dynamic)
            {
                if (router.MatchSetting(entry.Phrase) is { } setting)
                {
                    wrong.Add($"'{entry.Phrase}' ({entry.ToolName}) reached {Describe(setting)}");
                    continue;
                }

                var tool = router.MatchToolCommand(entry.Phrase);

                if (tool is null
                    || tool.CapabilityId != entry.CapabilityId
                    || tool.ToolName != entry.ToolName
                    || entry.Arguments.Any(argument =>
                        !tool.Arguments.TryGetString(argument.Key, out var value) || value != argument.Value))
                {
                    wrong.Add($"'{entry.Phrase}' ({entry.ToolName} {Arguments(entry.Arguments)}) reached {Describe(tool)}");
                }
            }
        }

        Assert.True(wrong.Count == 0, "These phrases reach something else when said:\n" + string.Join('\n', wrong));
    }

    /// <summary>Phrases declared by two capabilities for two targets, each waiting on its own decision.</summary>
    private static readonly HashSet<string> Shared = new(StringComparer.OrdinalIgnoreCase)
    {
        "put that on my checklist",
        "how have i done this session",
    };

    private static string Describe(SettingCommandMatch? match) =>
        match is null ? "nothing" : $"setting {match.Row.Key}={match.Value} via '{match.Phrase}'";

    private static string Describe(ToolCommandMatch? match) =>
        match is null ? "nothing" : $"{match.ToolName} via '{match.Phrase}'";

    private static string Arguments(IReadOnlyDictionary<string, string> arguments) =>
        string.Join('&', arguments.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => $"{a.Key}={a.Value}"));
}
