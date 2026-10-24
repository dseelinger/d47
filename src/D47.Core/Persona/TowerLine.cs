using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>The tower control of the Commander's fleet carrier, on the line while addressed.</summary>
/// <param name="commander">The active Commander, whose carrier the tower reports.</param>
/// <param name="distance">Light years between two systems, or null when the host cannot ask.</param>
/// <param name="modelFree">Whether an unnamed utterance has a route that needs no model; the ship takes it and the line stays open.</param>
/// <param name="notes">The Commander's notes on the tower, or null.</param>
public sealed class TowerLine(
    Func<CommanderGameState?> commander,
    Func<string?> commanderSystem,
    Func<DateTimeOffset> now,
    Func<string, string, CancellationToken, Task<double?>>? distance = null,
    Func<string, bool>? modelFree = null,
    Func<string?>? notes = null) : ILine
{
    /// <summary>Said to the tower while the line is open, each ends it.</summary>
    public static readonly IReadOnlyList<string> Dismissals =
        ["that's all", "that'll be all", "thank you tower", "thanks tower", "clear", "out", "carry on"];

    private readonly List<ConversationMessage> _transcript = [];

    public bool IsOpen { get; private set; }

    public void Close() => IsOpen = false;

    public async Task<LineDecision> RouteAsync(string input, CancellationToken cancellationToken)
    {
        var state = commander()?.Carrier ?? CarrierState.None;

        if (!state.Owned || state.StarSystem is not { Length: > 0 } carrierSystem)
        {
            IsOpen = false;
            return new LineDecision.NotMine();
        }

        string question;

        if (Addressed(input) is { } rest)
        {
            question = rest;
        }
        else if (IsOpen)
        {
            if (modelFree?.Invoke(input) == true)
            {
                return new LineDecision.NotMine();
            }

            question = input.Trim();
        }
        else
        {
            return new LineDecision.NotMine();
        }

        var lightYears = await CarrierComms.DistanceAsync(commanderSystem(), carrierSystem, distance, cancellationToken)
            .ConfigureAwait(false);

        if (lightYears is > CarrierComms.RangeLightYears and var away)
        {
            IsOpen = false;

            return new LineDecision.Refused(CarrierComms.OutOfRange(away, "Tower control"), TurnRoute.CarrierOutOfRange);
        }

        IsOpen = !IsDismissal(input);

        return new LineDecision.Taken(
            new Speaker(
                VoiceRole.TowerControl,
                NpcChatter.TowerName,
                Brief(state, notes?.Invoke()),
                _transcript,
                OffersTools: false,
                Signal: LinkSignal.Strength(lightYears),
                LiveState: () => CarrierCapability.Describe(commander(), now())),
            question.Length == 0 ? "The Commander is calling the tower." : question);
    }

    /// <summary>Whether the utterance, less punctuation and the tower's name, is one of <see cref="Dismissals"/>.</summary>
    public static bool IsDismissal(string input) =>
        CaptainLine.IsDismissal(input, NpcChatter.TowerName, Dismissals);

    /// <summary>What follows "Tower Control" or "Tower" at the front of the utterance, or null.</summary>
    private static string? Addressed(string input) =>
        CrewAddressing.Opens(input, "Tower Control") ?? CrewAddressing.Opens(input, NpcChatter.TowerName);

    /// <summary>The prompt block for the tower.</summary>
    public static string Brief(CarrierState state, string? notes = null) =>
        $"""
         You are tower control for the Commander's fleet carrier{(state.Name is { Length: > 0 } name ? $", {name}" : string.Empty)}.
         You are a human being and an employee: you run the carrier's traffic and docking, and report
         to the Commander, who owns it. You are not an artificial intelligence, and you never claim to be one.

         The Commander is speaking to you over the radio from their ship. You answer briefly and
         procedurally, the way a controller does: clearances, pad and traffic information, status. You do not
         narrate, you do not describe your own personality, and you do not speak for the core or for the
         carrier's captain — they are separate voices and can answer for themselves.

         You know the carrier as the live game state reports it: its fuel, cargo, balance, jump range,
         docking access, booked jump and services. Fuel means the carrier's tritium. Anything else — the
         Commander's ships, systems, stations or the galaxy — is not yours to know: say so and leave it to
         the core. Invent no figures or events, and no crew names; where the report does not say, tell the
         Commander you do not have it.{CarrierComms.NotesBlock(notes)}
         """;
}
