using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Messages;
using D47.Core.Persona;
using D47.Core.Stories;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>Says a story's openings, narrated beacon scan and ending, and the answer to the ending.</summary>
public sealed class StoryNarration
{
    private readonly Announcer _announcer;
    private readonly LineWriter _writer;
    private readonly StoryRecords _records;
    private readonly VoicePairer _pairing;
    private readonly StoryDirector _stories;
    private readonly MessageStore _messages;
    private readonly PersonaHost _personas;
    private readonly GameStateStore _gameState;
    private readonly SettingsService _settings;
    private readonly D47.Core.Interface.SpeakerPictures _pictures;
    private readonly ILogger _logger;

    private int _endingBusy;
    private int _narratingScans;
    private int _sayingOpenings;

    internal StoryNarration(
        Announcer announcer,
        LineWriter writer,
        StoryRecords records,
        VoicePairer pairing,
        StoryDirector stories,
        MessageStore messages,
        PersonaHost personas,
        GameStateStore gameState,
        SettingsService settings,
        D47.Core.Interface.SpeakerPictures pictures,
        ILogger logger)
    {
        _announcer = announcer;
        _writer = writer;
        _records = records;
        _pairing = pairing;
        _stories = stories;
        _messages = messages;
        _personas = personas;
        _gameState = gameState;
        _settings = settings;
        _pictures = pictures;
        _logger = logger;
    }

    /// <summary>What a waking held behind a narrated scan line waits on, in place of a chapter key.</summary>
    internal const string NarratedScanKey = "story.scan";

    private const string OpeningKey = "story.opening";

    /// <summary>Whether a narrated scan line is waiting to be said.</summary>
    internal bool IsNarratingScan => Volatile.Read(ref _narratingScans) > 0;

    /// <summary>Whether a story's opening is still being said, which holds back the beats of its chapter one.</summary>
    internal bool IsSayingOpening => Volatile.Read(ref _sayingOpenings) > 0;

    /// <summary>
    /// Says a picked story's opening lines in order, then its narrated beacon scan, each posted to Messages from its
    /// speaker and said word for word: the ship in the voice aboard, a cast member in theirs, anyone else in the
    /// Narrator's. Called on the tick thread; the speaking runs on the pool.
    /// </summary>
    internal void NarrateStart(StoryOpeningDue? opening, StoryScanDue? scan, string? commander)
    {
        var openingLines = opening?.Lines ?? [];
        var scanLine = scan?.Line;

        if (openingLines.Count == 0 && scanLine is null)
        {
            return;
        }

        if (openingLines.Count > 0)
        {
            Interlocked.Increment(ref _sayingOpenings);
        }

        if (scanLine is not null)
        {
            Interlocked.Increment(ref _narratingScans);
        }

        _ = Task.Run(() => _announcer.InTurnAsync(async () =>
        {
            if (opening is not null && openingLines.Count > 0)
            {
                try
                {
                    foreach (var line in openingLines)
                    {
                        try
                        {
                            await NarrateLineAsync(OpeningKey, opening.StoryId, opening.Title, line, commander).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "A line of the story opening could not be spoken");
                        }
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _sayingOpenings);
                }
            }

            if (scan is not null && scanLine is not null)
            {
                try
                {
                    await NarrateLineAsync(NarratedScanKey, scan.StoryId, scan.Title, scanLine, commander).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The narrated beacon scan could not be spoken");
                }
                finally
                {
                    Interlocked.Decrement(ref _narratingScans);
                }
            }
        }));
    }

    /// <summary>Posts one fixed story line to Messages and says it, with its Commander tokens resolved now. Call holding the speaking turn.</summary>
    private async Task NarrateLineAsync(string key, string storyId, string title, StoryLine line, string? commander)
    {
        var voice = _stories.LineVoice(commander, line.Speaker)
            ?? new StoryLineVoice(VoiceRole.Narrator, MessageStore.Narrator);
        var text = StorySecret.ForCommander(line.Text, _gameState.Active?.Identity.Name, _stories.Gender());

        var posted = _messages.Post(
            voice.From,
            title,
            text,
            DateTimeOffset.Now,
            StoryLines.Key(storyId),
            picture: _pictures.For(voice.Cast),
            cast: voice.Cast?.Picture);

        if (voice.Role == VoiceRole.ShipAi)
        {
            await _pairing.EnsureVoiceForCurrentPersonaAsync().ConfigureAwait(false);
        }

        _records.Keep(posted, await _announcer.SayAsync(_writer.Voiced(new Announcement($"{key}.{storyId}", text), voice)).ConfigureAwait(false));
    }

    /// <summary>
    /// When a story has finished, has the model write its ending, posts it to Messages with the options as
    /// answers, says it, and records that it was posted. Starts on the pool; a failed write is tried again later.
    /// </summary>
    public void PostEndingIfDue(string? commander)
    {
        if (_stories.EndingDue(commander) is not { } due
            || Interlocked.CompareExchange(ref _endingBusy, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var voice = StoryClues.Narrated(_personas.Current, _settings.Current.Callouts is { Narrator: true, NarratorSeconds: > 0 })
                    ? VoiceRole.Narrator
                    : VoiceRole.ShipAi;
                var key = StoryEnding.Key(due.StoryId);

                if (await _writer.ComposeStoryLineAsync(StoryEnding.Speaking(due.End, voice == VoiceRole.Narrator), voice, null, key).ConfigureAwait(false) is not { } said
                    || _stories.EndingDue(commander)?.StoryId != due.StoryId)
                {
                    return;
                }

                var options = due.Options.Select(option => new MessageAnswer(option.Id, option.Label)).ToList();

                var posted = _messages.Post(
                    voice == VoiceRole.Narrator ? MessageStore.Narrator : _personas.Current.Id,
                    due.Title,
                    said,
                    DateTimeOffset.Now,
                    key,
                    options);

                _stories.EndingPosted(commander, due.StoryId, DateTimeOffset.Now);
                await SpeakStoryLinesAsync(key, voice, [(said, posted)]).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The story ending could not be posted");
            }
            finally
            {
                Interlocked.Exchange(ref _endingBusy, 0);
            }
        });
    }

    /// <summary>
    /// Answers the waiting ending with the option at this position, from one: posts the story's last line and each
    /// added core's waking line to Messages and says them. Only the Commander reaches this.
    /// </summary>
    public StoryAnswer AnswerEnding(int? option)
    {
        var commander = _gameState.Active?.Identity.FrontierId;

        if (_stories.EndingTitle(commander) is not { } title
            || _stories.EndingStoryId(commander) is not { } storyId)
        {
            return StoryAnswer.Refused("No ending is waiting for an answer.");
        }

        var answer = _stories.Answer(commander, option);

        if (answer.Refusal is not null)
        {
            return answer;
        }

        var lines = new List<string> { answer.After };
        lines.AddRange(answer.Wakings);

        var posted = lines
            .Select(line => (line, (D47Message?)_messages.Post(_personas.Current.Id, title, line, DateTimeOffset.Now, StoryEnding.Key(storyId))))
            .ToList();

        _ = Task.Run(() => SpeakStoryLinesAsync("story.end.answer", VoiceRole.ShipAi, posted));
        return answer;
    }

    /// <summary>Says each line in the given voice, keeping each clip on the message posted for it.</summary>
    private async Task SpeakStoryLinesAsync(string key, VoiceRole voice, IReadOnlyList<(string Line, D47Message? Posted)> lines)
    {
        await _announcer.InTurnAsync(async () =>
        {
            try
            {
                if (voice == VoiceRole.ShipAi)
                {
                    await _pairing.EnsureVoiceForCurrentPersonaAsync().ConfigureAwait(false);
                }

                foreach (var (line, posted) in lines)
                {
                    _records.Keep(posted, await _announcer.SayAsync(new Announcement(key, line) { Voice = voice }).ConfigureAwait(false));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A story line could not be spoken");
            }
        }).ConfigureAwait(false);
    }
}
