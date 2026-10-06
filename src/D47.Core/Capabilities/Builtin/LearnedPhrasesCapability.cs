using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// What the flying Commander has taught D47 stands for a declared phrase, listed and forgotten on its own
/// panel page rather than a settings row (#171).
/// </summary>
public static class LearnedPhrasesCapability
{
    public const string Id = "learned-phrases";

    public const string ForgetTool = "forget_learned_phrase";

    public const string AddTool = "add_phrase";

    public static CapabilityDescriptor Create(
        LearnedPhrasesStore? store, Func<string> frontierId, Func<PhraseBook>? phraseBook = null) => new()
    {
        Id = Id,
        Group = "Voice",
        Name = "Learned phrases",
        Summary = "What the Commander has taught D47 another way of saying a command, and forgetting one.",
        Examples = ["forget 'set focus on elite'"],
        Display = new CapabilityDisplay { PanelTitle = "Learned phrases", Order = 4 },
        Tools =
        [
            new ToolDefinition
            {
                Name = AddTool,
                Description =
                    "Teach D47 a pattern of words that runs an existing phrase. A pattern is plain words and "
                    + "[a|b|c] groups of alternatives; an empty alternative makes a group optional, as in "
                    + "\"[please|] drop the wheels\". Groups do not nest and a pattern makes at most 100 "
                    + "wordings. Never callable by the model.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "pattern",
                        Type = ToolParameterType.String,
                        Description = "The words to teach, with optional [a|b] groups of alternatives.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "phrase",
                        Type = ToolParameterType.String,
                        Description = "The phrase in the phrase book the pattern should run.",
                        Required = true,
                    },
                ],
                Protected = true,
                Handler = (arguments, _) =>
                    Task.FromResult(Add(store, frontierId(), phraseBook, arguments)),
            },
            new ToolDefinition
            {
                Name = ForgetTool,
                Description =
                    "Forget one pattern the Commander taught D47, as written, with every wording it "
                    + "produced. Never callable by the model — only the panel, a hotkey or the Commander's "
                    + "own \"forget\" phrase reach it.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "said",
                        Type = ToolParameterType.String,
                        Description = "The pattern to forget, exactly as it reads on the learned-phrases page.",
                        Required = true,
                    },
                ],

                // The Commander's own taught wording is undone the same way it was made: never through the
                // model, only through the page, a phrase the router already knows, or a hotkey.
                Protected = true,
                Handler = (arguments, _) => Task.FromResult(Forget(store, frontierId(), arguments)),
            },
        ],
    };

    /// <summary>"Forget 'set focus on elite'", one per entry the flying Commander has taught D47.</summary>
    public static IEnumerable<DynamicCommand> Phrases(LearnedPhrasesStore? store, Func<string> frontierId)
    {
        var fid = frontierId();

        if (store is null || fid.Length == 0)
        {
            yield break;
        }

        foreach (var learned in store.For(fid))
        {
            var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["said"] = learned.Said };

            yield return new DynamicCommand($"forget '{learned.Said}'", Id, ForgetTool, arguments);
        }
    }

    private static ToolResult Add(
        LearnedPhrasesStore? store, string frontierId, Func<PhraseBook>? phraseBook, ToolArguments arguments)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error("Nobody is flying, so there is nobody to teach a phrase to.");
        }

        if (!arguments.TryGetString("pattern", out var pattern) || string.IsNullOrWhiteSpace(pattern)
            || !arguments.TryGetString("phrase", out var phrase) || string.IsNullOrWhiteSpace(phrase))
        {
            return ToolResult.Error("Teaching needs both the pattern and the phrase it runs.");
        }

        pattern = pattern.Trim();

        if (!PhrasePattern.TryExpand(pattern, out var wordings, out var problem))
        {
            return ToolResult.Error($"\"{pattern}\" is refused: {problem}");
        }

        var book = phraseBook?.Invoke();
        var wanted = KeywordRouter.Utterance(phrase);
        var target = book?.Entries.FirstOrDefault(entry =>
            string.Equals(KeywordRouter.Utterance(entry.Phrase), wanted, StringComparison.OrdinalIgnoreCase));

        if (book is null || target is null)
        {
            return ToolResult.Error($"\"{phrase.Trim()}\" is not a phrase D47 knows.");
        }

        if (store.Taught(frontierId, pattern, target.Phrase) is not null)
        {
            return ToolResult.Ok($"\"{pattern}\" is already taught for \"{target.Phrase}\".");
        }

        if (store.FindClash(frontierId, wordings, target.Phrase, book) is { } clash)
        {
            return ToolResult.Error(clash.Kind == PhraseClashKind.BookPhrase
                ? $"\"{clash.Wording}\" is already the phrase \"{clash.StandsFor}\" in {clash.CapabilityId}."
                : $"\"{clash.Wording}\" is already taught by \"{clash.Pattern}\" for \"{clash.StandsFor}\".");
        }

        return store.Learn(frontierId, pattern, target.Phrase, DateTimeOffset.Now)
            ? ToolResult.Ok($"Taught: \"{pattern}\" runs \"{target.Phrase}\", {wordings.Count} wording(s).")
            : ToolResult.Error($"\"{pattern}\" could not be stored.");
    }

    private static ToolResult Forget(LearnedPhrasesStore? store, string frontierId, ToolArguments arguments)
    {
        if (store is null || frontierId.Length == 0)
        {
            return ToolResult.Error("Nobody is flying, so there is nothing learned to forget.");
        }

        if (!arguments.TryGetString("said", out var said) || string.IsNullOrWhiteSpace(said))
        {
            return ToolResult.Error("Which one? Forgetting needs the wording it was said as.");
        }

        return store.Forget(frontierId, said.Trim())
            ? ToolResult.Ok($"Forgotten: \"{said.Trim()}\".")
            : ToolResult.Error($"Nothing learned matches \"{said.Trim()}\".");
    }
}
