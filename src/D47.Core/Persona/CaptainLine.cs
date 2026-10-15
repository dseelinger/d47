using System.Text;
using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>The captain of the Commander's fleet carrier, on the line while addressed.</summary>
/// <param name="commander">The active Commander, whose carrier and ships the captain knows.</param>
/// <param name="distance">Light years between two systems, or null when the host cannot ask.</param>
/// <param name="modelFree">Whether an unnamed utterance has a route that needs no model; the ship takes it and the line stays open.</param>
public sealed class CaptainLine(
    Func<CommanderGameState?> commander,
    Func<string?> commanderSystem,
    Func<DateTimeOffset> now,
    Func<string, string, CancellationToken, Task<double?>>? distance = null,
    Func<string, bool>? modelFree = null,
    Func<FightSnapshot>? fight = null,
    Func<string?>? captainName = null) : ILine
{
    /// <summary>How far the captain can be heard.</summary>
    public const double RangeLightYears = LinkSignal.LostLightYears;

    /// <summary>Said to the captain while the line is open, each ends it.</summary>
    public static readonly IReadOnlyList<string> Dismissals =
        ["that's all", "that'll be all", "thank you captain", "dismissed", "carry on"];

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

        if (CrewAddressing.Opens(input, NpcChatter.CaptainName) is { } rest)
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

        var lightYears = await DistanceAsync(carrierSystem, cancellationToken).ConfigureAwait(false);

        if (lightYears is > RangeLightYears and var away)
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
                Brief(state, captainName?.Invoke()),
                _transcript,
                OffersTools: false,
                Signal: LinkSignal.Strength(lightYears),
                LiveState: () => Knows(commander(), now(), fight?.Invoke())),
            question.Length == 0 ? "The Commander is trying to get your attention." : question);
    }

    /// <summary>Whether the utterance, less punctuation and <paramref name="name"/>, is one of <see cref="Dismissals"/>.</summary>
    public static bool IsDismissal(string input, string name = NpcChatter.CaptainName)
    {
        string[] words = new string([.. input.Replace('’', '\'').Where(c => char.IsLetter(c) || c is ' ' or '\'')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool IsNamed(string word) => string.Equals(word, name, StringComparison.OrdinalIgnoreCase);

        return Is(words)
               || (words.Length > 1 && IsNamed(words[^1]) && Is(words[..^1]))
               || (words.Length > 1 && IsNamed(words[0]) && Is(words[1..]));

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

    /// <summary>The captain's live game state: the carrier report, the roster, then any fight in the carrier's system.</summary>
    public static string Knows(CommanderGameState? state, DateTimeOffset now, FightSnapshot? fight = null)
    {
        var known = $"{CarrierCapability.Describe(state, now)}\n\n{Roster(state)}";

        return fight is not null && state is not null
               && CarrierFight.Describe(fight, state.Location.StarSystem, state.Carrier, now) is { } fact
            ? $"{known}\n\n{fact}"
            : known;
    }

    /// <summary>The ship flown and every stored ship, each by name and hull, with the count.</summary>
    public static string Roster(CommanderGameState? state)
    {
        var flown = state?.FlownShip;
        var flownSaid = flown?.TypeSaid is { } hull ? flown.Name is { } name ? $"{name} ({hull})" : hull : null;
        var fleet = state?.Fleet ?? FleetRegistry.Empty;

        if (!fleet.IsKnown)
        {
            return flownSaid is null
                ? "The Commander's ships are not known."
                : $"The Commander is flying {flownSaid}. Their other ships are not known.";
        }

        var flownId = flown?.ShipId ?? state?.Ship.ShipId;

        List<string> ships = [.. fleet.Ships
            .Where(ship => flownId is not { } id || ship.ShipId != id)
            .Select(ship => ship.Describe())];

        var count = ships.Count + (flownSaid is null ? 0 : 1);
        var roster = new StringBuilder(flownSaid is null
            ? $"The Commander has {count} stored {(count == 1 ? "ship" : "ships")}:"
            : $"The Commander owns {count} {(count == 1 ? "ship" : "ships")}:\n- {flownSaid}, flying now");

        foreach (var ship in ships)
        {
            roster.Append($"\n- {ship}");
        }

        if (flownSaid is null)
        {
            roster.Append("\nThe ship they are flying is not known.");
        }

        return roster.ToString();
    }

    /// <summary>The prompt block for the captain.</summary>
    public static string Brief(CarrierState state, string? captainName = null) =>
        $"""
         You are the captain of the Commander's fleet carrier{(state.Name is { Length: > 0 } name ? $", {name}" : string.Empty)}.
         You are a human being and an employee: you run the carrier day to day and report to the
         Commander, who owns it. You are not an artificial intelligence, and you never claim to be one.

         The Commander is speaking to you over comms from their ship. You answer briefly and
         professionally, the way an officer reporting to the owner does. You do not narrate, you do
         not describe your own personality, and you do not speak for the ship's AI — it is a separate
         voice aboard the Commander's ship and it can answer for itself.

         You know the carrier as the live game state reports it: its fuel, cargo, balance, jump range,
         docking access, booked jump and services. Fuel means the carrier's tritium. If the state
         reports a fight in the Commander's system, you know of that too, and nothing more about it. Of the Commander's
         ships you know only how many there are, and each one's name and hull. Anything else — a ship's
         fuel, cargo or fittings, systems, stations or the galaxy — is not yours to know: say so and
         leave it to the ship's AI. Invent no figures or events, and no crew names beyond your own; where the report does not
         say, tell the Commander you do not have it.{OwnName(state, captainName)}
         """;

    /// <summary>What Elite calls the captain and how the Commander's name for them relates to it; empty without a captain on file.</summary>
    private static string OwnName(CarrierState state, string? captainName)
    {
        if (CarrierPayrollCallout.EliteCaptainName(state) is not { } elite)
        {
            return string.Empty;
        }

        const string Payroll = "Each week the carrier's payroll goes out made out to that name.";

        return CarrierPayrollCallout.Classify(elite, captainName) switch
        {
            CaptainNameCase.Unnamed =>
                $"\n\nElite's crew list gives your name as {elite}, and the Commander has given you no other, so it is your own name. {Payroll}",
            CaptainNameCase.Same =>
                $"\n\nElite's crew list gives your name as {elite}, which is what the Commander calls you. {Payroll} It is made out to you, as it should be.",
            _ =>
                $"\n\nElite's crew list gives your name as {elite}, but the Commander calls you {captainName?.Trim()}. {Payroll} {elite} is your nom de plume; play that lightly if asked.",
        };
    }
}
