using D47.Core.Listening;

namespace D47.Core.Conversation;

public static class ConversationForms
{
    extension(TurnLoop loop)
    {
        public IAsyncEnumerable<TurnEvent> RunAsync(
            string input,
            InputSource source = InputSource.Typed,
            CancellationToken cancellationToken = default) =>
            loop.RunAsync(input, source, PttTarget.Core, cancellationToken);
    }
}
