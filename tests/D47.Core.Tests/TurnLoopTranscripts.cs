using System.Runtime.CompilerServices;
using D47.Core.Conversation;

namespace D47.Core.Tests;

/// <summary>Keeps the list a loop writes its transcript to, so a test can read what the loop recorded.</summary>
internal static class TurnLoopTranscripts
{
    private static readonly ConditionalWeakTable<TurnLoop, List<ConversationMessage>> Kept = new();

    public static TurnLoop Recording(this TurnLoop loop)
    {
        var transcript = new List<ConversationMessage>();
        loop.UseTranscript(transcript);
        Kept.Add(loop, transcript);

        return loop;
    }

    public static IReadOnlyList<ConversationMessage> History(this TurnLoop loop) =>
        Kept.TryGetValue(loop, out var transcript)
            ? transcript
            : throw new InvalidOperationException("The loop was not built with Recording().");
}
