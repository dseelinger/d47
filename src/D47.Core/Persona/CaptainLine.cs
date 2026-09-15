using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>The captain of the Commander's fleet carrier, on the line while addressed.</summary>
/// <param name="shipAiName">What the Commander calls the ship's AI; at the front of an utterance it closes the line.</param>
/// <param name="distance">Light years between two systems, or null when the host cannot ask.</param>
public sealed class CaptainLine(
    Func<CarrierState> carrier,
    Func<string?> commanderSystem,
    Func<string?> shipAiName,
    Func<string, string, CancellationToken, Task<double?>>? distance = null) : ILine
{
    /// <summary>How far the captain can be heard.</summary>
    public const double RangeLightYears = 500;

    /// <summary>Said to the captain while the line is open, each ends it.</summary>
    public static readonly IReadOnlyList<string> Dismissals =
        ["that's all", "that'll be all", "thank you captain", "dismissed", "carry on"];

    private readonly List<ConversationMessage> _transcript = [];

    public bool IsOpen { get; private set; }

    public void Close() => IsOpen = false;

    public async Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken)
    {
        var state = carrier();

        if (!state.Owned || state.StarSystem is not { Length: > 0 } carrierSystem)
        {
            IsOpen = false;
            return new LineDecision.NotMine();
        }

        if (shipAiName() is { Length: > 0 } shipAi && CrewAddressing.Opens(input, shipAi) is not null)
        {
            IsOpen = false;
            return new LineDecision.NotMine();
        }

        string question;

        if (CrewAddressing.Opens(input, NpcChatter.CaptainName) is { } rest)
        {
            question = rest;
        }
        else if (IsOpen)
        {
            question = input.Trim();
        }
        else
        {
            return new LineDecision.NotMine();
        }

        if (await DistanceAsync(carrierSystem, cancellationToken).ConfigureAwait(false) is > RangeLightYears and var away)
        {
            IsOpen = false;

            return new LineDecision.Refused(
                $"The carrier is {away:N0} light years away, beyond comms range. The captain can't hear you from here.",
                TurnRoute.CarrierOutOfRange);
        }

        IsOpen = !IsDismissal(input);

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.CarrierCaptain,
                NpcChatter.CaptainName,
                Brief(state),
                _transcript,
                OffersTools: true,
                Signal: 1),
            question.Length == 0 ? "The Commander is trying to get your attention." : question);
    }

    /// <summary>Whether the utterance, less punctuation and the captain's name, is one of <see cref="Dismissals"/>.</summary>
    public static bool IsDismissal(string input)
    {
        string[] words = new string([.. input.Replace('’', '\'').Where(c => char.IsLetter(c) || c is ' ' or '\'')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool IsCaptain(string word) => string.Equals(word, NpcChatter.CaptainName, StringComparison.OrdinalIgnoreCase);

        return Is(words)
               || (words.Length > 1 && IsCaptain(words[^1]) && Is(words[..^1]))
               || (words.Length > 1 && IsCaptain(words[0]) && Is(words[1..]));

        static bool Is(string[] said) => Dismissals.Any(dismissal =>
            string.Equals(string.Join(' ', said), dismissal, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Zero in the same system; null when there is nothing to ask or the answer fails.</summary>
    private async Task<double?> DistanceAsync(string carrierSystem, CancellationToken cancellationToken)
    {
        if (commanderSystem() is not { Length: > 0 } here)
        {
            return null;
        }

        if (string.Equals(here, carrierSystem, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (distance is null)
        {
            return null;
        }

        try
        {
            return await distance(here, carrierSystem, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The prompt block for the captain.</summary>
    public static string Brief(CarrierState state) =>
        $"""
         You are the captain of the Commander's fleet carrier{(state.Name is { Length: > 0 } name ? $", {name}" : string.Empty)}.
         You are a human being and an employee: you run the carrier day to day and report to the
         Commander, who owns it. You are not an artificial intelligence, and you never claim to be one.

         The Commander is speaking to you over comms from their ship. You answer briefly and
         professionally, the way an officer reporting to the owner does. You do not narrate, you do
         not describe your own personality, and you do not speak for the ship's AI — it is a separate
         voice aboard the Commander's ship and it can answer for itself.

         For the carrier's fuel, cargo, balance, jump range, docking access, booked jump and services,
         call describe_carrier and answer from what it returns. For anything about systems, stations
         or the galaxy, use the galaxy tools. Invent no figures, crew names or events; where a tool
         does not say, tell the Commander you do not have it.
         """;
}
