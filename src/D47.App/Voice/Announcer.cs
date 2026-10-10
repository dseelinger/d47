using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Lore;
using D47.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>Speaks what d47 says unasked: callouts, story lines, reminders and asides.</summary>
public sealed class Announcer : IDisposable
{
    private readonly AudioArbiter _audio;
    private readonly Func<CueLibrary> _cues;
    private readonly Func<NavRoute> _route;
    private readonly Func<Announcement, VoiceCast> _castFor;
    private readonly Func<VoiceRole, VoiceGender> _genderOf;
    private readonly Action<string> _said;
    private readonly Action<string> _heardFromOutside;
    private readonly Func<bool> _canSearch;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly SpeakingTurn _speaking = new();
    private readonly SpokenReferent _referent = new();

    internal Announcer(
        VoicePipeline voice,
        AudioArbiter audio,
        Func<CueLibrary> cues,
        TurnLoop turns,
        D47.Core.Persona.PersonaHost personas,
        GameStateStore gameState,
        Func<NavRoute> route,
        ILoggerFactory loggers,
        Func<Announcement, VoiceCast> castFor,
        Func<VoiceRole, VoiceGender> genderOf,
        Action<string> said,
        SettingsService settings,
        SpendTracker spend,
        CalloutEngine callouts,
        LineWriter writer,
        StoryRecords records,
        Action<string> heardFromOutside,
        Func<bool> canSearch)
    {
        Voice = voice;
        _audio = audio;
        _cues = cues;
        Turns = turns;
        Personas = personas;
        GameState = gameState;
        _route = route;
        _loggerFactory = loggers;
        _logger = loggers.CreateLogger<Announcer>();
        _castFor = castFor;
        _genderOf = genderOf;
        _said = said;
        Settings = settings;
        Spend = spend;
        Callouts = callouts;
        Writer = writer;
        Records = records;
        _heardFromOutside = heardFromOutside;
        _canSearch = canSearch;
    }

    /// <summary>
    /// Raised with a spoken callout that joins the Conversation page (#276): its text as spoken, who
    /// said it, and the callout key, once the wording and the contradiction check have settled what was
    /// actually said.
    /// </summary>
    public event Action<string, string, string, string?>? CalloutSaid;

    private VoicePipeline Voice { get; }

    private TurnLoop Turns { get; }

    private D47.Core.Persona.PersonaHost Personas { get; }

    private GameStateStore GameState { get; }

    private SettingsService Settings { get; }

    private SpendTracker Spend { get; }

    private CalloutEngine Callouts { get; }

    private LineWriter Writer { get; }

    private StoryRecords Records { get; }

    /// <summary>Where in-game comms are written down (#264).</summary>
    private ILogger Comms => _comms ??= _loggerFactory.CreateLogger("D47.App.Voice.Comms");

    private ILogger? _comms;

    /// <summary>Runs <paramref name="speak"/> once no other unprompted speaker is speaking. Never awaited by code holding the turn.</summary>
    internal Task InTurnAsync(Func<Task> speak) => _speaking.TakeAsync(speak);

    /// <summary>The systems a line could be about: where the Commander is, and where they are going.</summary>
    private string[] SystemsIn(string text) =>
        [.. new[] { GameState.Active?.Location.StarSystem, _route().Hops.LastOrDefault()?.StarSystem }
            .Where(name => name is { Length: > 0 }
                && text.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(name => name!)];

    /// <summary>Says one announcement and returns what was queued to play, for a message to keep.</summary>
    internal async Task<SpokenClip?> SayAsync(Announcement announcement)
    {
        var written = announcement;

        // The voice takes the pronoun; everything written below keeps the name, so a Commander scrolling back
        // can always see which system "it" was.
        // Not a narration, which is prose and keeps its names.
        if (announcement.Voice != VoiceRole.Narrator)
        {
            announcement = announcement with
            {
                Text = _referent.Speak(announcement.Text, SystemsIn(announcement.Text), DateTimeOffset.Now),
            };
        }

        var voice = SpeakerAccent.VoiceOf(_castFor(announcement), announcement);

        // Written before it is spoken, and whether or not the speaking works: a message that could not be
        // synthesised is still a message that arrived. **Into the log, on the Commander's instruction**
        // (#264): "In-game comms should appear in the Log File - voice related stuff."
        if (announcement.Transcript is { Length: > 0 } line)
        {
            Comms.LogInformation("{Message}", line.TrimEnd());
        }
        else if (announcement.ConversationLine is { Length: > 0 } spoken)
        {
            // Onto the story's own feed, so "why did you say that" can answer about a callout too
            // (remediation.md 17, item 4).
            Turns.Said(spoken);
        }

        // Joined only now, so the conversation feed above never carries it.
        var clip = await Voice.AnnounceAsync(announcement with { Text = announcement.Heard, Verbatim = null }, voice)
            .ConfigureAwait(false);

        // The Transcript keeps the names the voice replaced with a pronoun.
        CalloutSaid?.Invoke(written.Heard, ConversationSpeaker(written, Personas.ShipName), written.Key, ConversationPicture(written));
        return clip;
    }

    /// <summary>Takes whatever the callouts queued this tick and says it. Called on the tick thread.</summary>
    internal void SpeakPending()
    {
        var pending = Callouts.Drain();

        // A line queued before a pick is about the Commander no longer shown.
        if (pending.Count == 0 || GameState.IsOffDuty)
        {
            return;
        }

        // Somebody else's words, written down in the session record and extracted from by nothing (#162).
        foreach (var message in pending.Where(announcement =>
                     announcement.Key.StartsWith("message.", StringComparison.Ordinal)))
        {
            _heardFromOutside(message.Text);
        }

        _ = Task.Run(async () =>
        {
            // Written before the turn is taken, never while holding it.
            var lines = new List<Announcement>(pending.Count);

            foreach (var announcement in pending)
            {
                lines.AddRange(await Writer.WriteAsync(announcement).ConfigureAwait(false));
            }

            // One at a time, and in order.
            await InTurnAsync(async () =>
            {
                try
                {
                    var beat = 0;

                    foreach (var announcement in lines)
                    {
                        if (announcement.Key == NpcChatter.LineKey)
                        {
                            // Air between the lines of an exchange (#259), reported as two people never once
                            // leaving a gap.
                            await HoldTheBeatAsync(NpcChatter.Beat(beat++)).ConfigureAwait(false);

                            // Checked after the beat rather than before it: this loop runs ahead of playback,
                            // so the Commander starts talking while the next line is still waiting on its beat.
                            // The arbiter refuses a line synthesised after that anyway; this only saves paying
                            // to synthesise it (#61).
                            if (Voice.Engaged)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            beat = 0;
                        }

                        var spoken = await SayAsync(announcement).ConfigureAwait(false);

                        // Only a line actually spoken can be answered.
                        if (announcement.Invented is { } chatter)
                        {
                            Turns.Lines.OfType<D47.Core.Persona.ChatterLine>().FirstOrDefault()
                                ?.Heard(chatter.Line, chatter.Answerable, chatter.ExchangeIndex);
                        }

                        if (announcement.Voice == VoiceRole.Narrator)
                        {
                            Turns.Lines.OfType<D47.Core.Persona.NarratorLine>().FirstOrDefault()?.Heard(announcement.Text);
                        }

                        // What the Commander actually heard about a story, kept (asked for 2026-08-22).
                        Records.RecordAdventure(announcement, spoken);
                        Records.RecordNudge(announcement, spoken);
                        Records.RecordClue(announcement, spoken);

                        // After the fact has been spoken, and not awaited: the search is a round trip through
                        // somebody else's index, and the rest of this batch is where a danger callout would be
                        // waiting.
                        LookUpLore(announcement);
                    }
                }
                catch (Exception ex)
                {
                    // A callout that cannot be synthesised is a callout the Commander does not hear.
                    _logger.LogError(ex, "A callout could not be spoken");
                }
            }).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// The pause in front of a line of an invented exchange (#259), taken in slices so it can be
    /// abandoned.
    /// </summary>
    private async Task HoldTheBeatAsync(TimeSpan beat)
    {
        var slice = TimeSpan.FromMilliseconds(100);

        for (var held = TimeSpan.Zero; held < beat; held += slice)
        {
            if (Callouts.AnythingUrgentWaiting)
            {
                return;
            }

            await Task.Delay(slice < beat - held ? slice : beat - held).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The second half of an arrival remark: a web search, and what it found (Phase 23, "Look it up,
    /// and say where the answer came from"). Takes the turn on the pool, so the drain holding it never waits on it.
    /// </summary>
    private void LookUpLore(Announcement announcement)
    {
        if (LoreCallout.AddressOf(announcement.Key) is not { } address
            || Settings.Current.Callouts.Lore != LoreRemarks.Lookup
            || !_canSearch())
        {
            return;
        }

        // The name as the journal spelled it, taken now rather than when the answer lands: by then the
        // Commander may be somewhere else, and this is the system being asked about.
        var name = GameState.Active?.Location.StarSystem
                   ?? D47.Core.Knowledge.LoreDirectory.ByAddress(address)?.Name;

        if (name is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            using var budget = new CancellationTokenSource(LoreLookup.Budget);

            var found = await FlavourTurn.AskAsync(
                Turns.Provider,
                Turns.BackgroundModel,

                // No persona block.
                persona: null,
                aboutMe: null,
                LoreLookup.Instruction(name),
                gameState: null,
                Spend,
                PriceTable.Default,
                _logger,
                budget.Token,
                webSearch: true,

                // The same cold sampling the notes window asks for, from the same place.
                sampling: LoreLookup.Sampling).ConfigureAwait(false);

            // Dropped rather than spoken when the Commander has moved on.
            if (LoreLookup.Spoken(found) is not { } line)
            {
                return;
            }

            if (!LoreLookup.StillHere(address, GameState.Active?.Location.SystemAddress))
            {
                _logger.LogInformation("A lore lookup for {System} landed after the Commander had left", name);
                return;
            }

            await InTurnAsync(async () =>
            {
                try
                {
                    await SayAsync(new Announcement($"{LoreCallout.KeyPrefix}search.{address}", line))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A lore lookup could not be spoken");
                }
            }).ConfigureAwait(false);
        });
    }

    /// <summary>Sounds what came due, and says which (Phase 24, "A timer says its own name").</summary>
    internal void SoundReminders(IReadOnlyList<Fired> fired)
    {
        if (fired.Count == 0)
        {
            return;
        }

        var zone = TimeZoneInfo.Local;

        foreach (var (reminder, missed) in fired)
        {
            _logger.LogInformation(
                "{Kind} \"{Name}\" {What}",
                reminder.Kind,
                reminder.Name,
                missed ? "was due while d47 was closed" : "went off");

            if (!missed && Voice.CuesEnabled)
            {
                _audio.Enqueue(new AudioRequest
                {
                    Channel = AudioChannel.Cue,
                    Clip = _cues().For(AlertCue.TimerElapsed),

                    // Captioned like the warnings are (#201).
                    Caption = AlertCues.Caption(AlertCue.TimerElapsed),
                });
            }

            var said = missed ? reminder.AnnounceMissed(zone) : reminder.Announce();

            _ = Voice.AnnounceAsync(said);

            _said(said);

            // A timer going off is d47 speaking unasked, like a callout (remediation.md 17, item 4). "Why did
            // you just say that?" has to be answerable about this too.
            Turns.Said(said);
        }
    }

    /// <summary>
    /// A line d47 says because the panel asked it to - a generator's reply, a refusal - spoken, shown,
    /// and recorded exactly as a timer going off is (Phase 47). "Why did you just say that?" has to be
    /// answerable about this too.
    /// </summary>
    public void SayAside(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _ = Voice.AnnounceAsync(line);
        _said(line);
        Turns.Said(line);
    }

    /// <summary>The chip the Conversation page names this speaker with.</summary>
    internal static string ConversationSpeaker(Announcement announcement, string shipName) =>
        announcement.Speaker is { Length: > 0 } speaker
            ? announcement.Invented is null ? speaker : NpcChatter.Invented(speaker)
            : VoiceRoles.Called(announcement.Voice) ?? shipName;

    /// <summary>The picture name the Conversation page shows a spoken line with, or null.</summary>
    private string? ConversationPicture(Announcement announcement) =>
        ConversationPicture(announcement, Personas.Current.Id, _genderOf, GameState.Active?.Crew);

    /// <summary>
    /// The picture name for a spoken line: a cast line's own, the core aboard for the ship, the Narrator's, the
    /// captain's or tower's by the gender of its voice, and a hired pilot's by name; null for anyone else.
    /// </summary>
    internal static string? ConversationPicture(
        Announcement announcement,
        string coreAboard,
        Func<VoiceRole, VoiceGender> genderOf,
        ShipCrew? crew)
    {
        if (D47.Core.Interface.SpeakerPictures.IsName(announcement.Picture))
        {
            return announcement.Picture;
        }

        return announcement.Voice switch
        {
            VoiceRole.ShipAi when announcement.Speaker is null => D47.Core.Interface.SpeakerPictures.Core(coreAboard),
            VoiceRole.Narrator => D47.Core.Interface.SpeakerPictures.Narrator,
            VoiceRole.CarrierCaptain or VoiceRole.TowerControl =>
                D47.Core.Interface.SpeakerPictures.ByVoice(announcement.Voice, genderOf(announcement.Voice)),
            VoiceRole.Crew => CrewPicture(announcement.Speaker, crew),
            _ => null,
        };
    }

    /// <summary>The picture name of the hired pilot called <paramref name="name"/>, or null when none on the roster is.</summary>
    internal static string? CrewPicture(string? name, ShipCrew? crew) =>
        name is { Length: > 0 } && crew?.Members.FirstOrDefault(member =>
            string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase)) is { } pilot
            ? D47.Core.Interface.SpeakerPictures.Crew(pilot.CrewId)
            : null;

    public void Dispose() => _speaking.Dispose();
}
