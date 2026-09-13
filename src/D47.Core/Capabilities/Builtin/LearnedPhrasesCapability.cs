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

    public static CapabilityDescriptor Create(LearnedPhrasesStore? store, Func<string> frontierId) => new()
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
                Name = ForgetTool,
                Description =
                    "Forget one utterance the Commander taught D47 to treat as a command, by the wording "
                    + "they said. Never callable by the model — only the panel, a hotkey or the Commander's "
                    + "own \"forget\" phrase reach it.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "said",
                        Type = ToolParameterType.String,
                        Description = "The utterance to forget, exactly as it reads on the learned-phrases page.",
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
