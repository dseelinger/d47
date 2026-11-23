using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Configuration;
using D47.Core.Podcasts;

namespace D47.App.Voice;

/// <summary>A shelf book being read: the sentence playing or paused at, and why it is paused.</summary>
/// <param name="Place">The paragraph the sentence is in.</param>
/// <param name="Sentence">Its index in that paragraph's <see cref="BookParagraph.Sentences"/>.</param>
public sealed record BookPlayback(Book Book, BookPlace Place, int Sentence, string Text, StoryRunPause Pause)
{
    public bool IsPaused => Pause != StoryRunPause.None;

    /// <summary>The book as the voice commands see it: paused only when the Commander paused it.</summary>
    public BookNow ToBookNow() => new(Book.Source, Book.Title, Place.Chapter, Book.Chapters[Place.Chapter].Title, Pause == StoryRunPause.Commander, Book.FrontMatter);

    /// <summary>The sentence's clip is not made yet, so nothing is playing.</summary>
    public bool Waiting { get; init; }

    /// <summary>Why the local voice could not read the sentence; set while the player is paused on it.</summary>
    public string? Problem { get; init; }

    /// <summary>Paused at the end of a downloaded book's text, with no download running to bring the rest.</summary>
    public bool NotArrived { get; init; }
}

/// <summary>
/// Reads a shelf book aloud, sentence by sentence from its place, with a local voice only. The next
/// <see cref="SentencesAhead"/> sentences are made one at a time off the calling thread, and each clip is dropped once
/// played. The place is saved to the shelf at each new paragraph. A push-to-talk pause, or a callout's, plays on from
/// the start of its sentence once D47 is not listening, answering or speaking; a Commander pause, or a failed voice,
/// waits for <see cref="Resume"/>. A book does not start or resume while a story plays, and a story starting pauses it.
/// Ambient music yields while it reads, and chatter is refused. In a downloaded book whose text is not whole, a
/// paragraph not yet arrived is waited for while its download runs; with none running, a resume is tried at most once
/// a minute, and the player pauses with <see cref="RestNotArrived"/> when none starts.
/// </summary>
public sealed class BookPlayer
{
    /// <summary>The group every sentence is queued in.</summary>
    public const string Group = "book-playback";

    /// <summary>How many sentences after the one playing are made before they are needed.</summary>
    public const int SentencesAhead = 3;

    public const string RestNotArrived = "The rest of this book has not arrived yet.";

    private readonly AudioArbiter _audio;
    private readonly BookShelf _shelf;
    private readonly Func<string, ITtsProvider?> _localVoice;
    private readonly Func<BookSettings> _settings;
    private readonly Action<Action> _background;
    private readonly StoryRunPlayer? _story;
    private readonly AmbientMusic? _music;
    private readonly GutenbergBookFetch? _downloads;
    private readonly Lock _gate = new();
    private readonly Lock _publishing = new();

    /// <summary>The sentence playing and those after it, made or being made, in order.</summary>
    private readonly List<Ahead> _ahead = [];

    private BookPlayback? _now;
    private AudioRequest? _queued;
    private BookVoice? _voice;
    private CancellationTokenSource _rendering = new();

    /// <summary>Where reading goes on from once that text arrives; set while waiting at the end of a downloaded book's text.</summary>
    private BookPlace? _awaiting;

    /// <summary>More of the book being read has arrived since it was opened.</summary>
    private bool _arrived;

    /// <summary>The shelf's download state when the book being read was last opened.</summary>
    private BookDownload? _opened;

    /// <summary>The last sentence started; the next one is made after it.</summary>
    private Task _lastRender = Task.CompletedTask;

    /// <param name="localVoice">The local provider for a provider id, or null when its model is not installed.</param>
    /// <param name="background">Runs work off the calling thread; the thread pool when null.</param>
    /// <param name="downloads">Brings the rest of a downloaded book's text.</param>
    public BookPlayer(
        AudioArbiter audio,
        BookShelf shelf,
        Func<string, ITtsProvider?> localVoice,
        Func<BookSettings> settings,
        Action<Action>? background = null,
        StoryRunPlayer? story = null,
        AmbientMusic? music = null,
        GutenbergBookFetch? downloads = null)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(shelf);
        ArgumentNullException.ThrowIfNull(localVoice);
        ArgumentNullException.ThrowIfNull(settings);

        _audio = audio;
        _shelf = shelf;
        _localVoice = localVoice;
        _settings = settings;
        _background = background ?? (work => _ = Task.Run(work));
        _story = story;
        _music = music;
        _downloads = downloads;
        _audio.Played += OnPlayed;
        _audio.PlaybackPaused += () => Pause(StoryRunPause.Interrupted);
        _audio.ActivityChanged += activity =>
        {
            if (activity.Channel is null)
            {
                Settled();
            }
        };

        if (story is not null)
        {
            story.Starting += () => Pause(StoryRunPause.Commander);
        }

        if (downloads is not null)
        {
            downloads.Arrived += OnArrived;
        }
    }

    /// <summary>Raised when the sentence, the pause, waiting or the problem changed; on any thread.</summary>
    public event Action? Changed;

    /// <summary>Raised on the calling thread as reading starts or resumes, before it takes the speech channels.</summary>
    public event Action? Starting;

    /// <summary>A story is playing, or paused by anything but the Commander, so a book does not start or resume.</summary>
    public bool StoryPlaying => _story?.Now is { Pause: not StoryRunPause.Commander };

    /// <summary>What is being read or paused, or null.</summary>
    public BookPlayback? Now
    {
        get
        {
            lock (_gate)
            {
                return _now;
            }
        }
    }

    /// <summary>
    /// Starting this book pauses <paramref name="podcast"/>, and starting the podcast pauses this book, each as the
    /// Commander's pause.
    /// </summary>
    public void TakesTurnsWith(PodcastPlayer podcast)
    {
        ArgumentNullException.ThrowIfNull(podcast);

        Starting += () =>
        {
            if (podcast.State is { Playing: true })
            {
                podcast.Pause();
            }
        };
        podcast.Starting += () => Pause(StoryRunPause.Commander);
    }

    /// <summary>
    /// Reads <paramref name="source"/> from its place on the shelf, or from its start once finished. Reads its text from
    /// the disk on the calling thread. False when a story is playing, or the book is not on the shelf or has no sentence
    /// to read. With a callout queued or playing, it waits paused at its first sentence until the callout ends.
    /// </summary>
    public bool Play(BookSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (StoryPlaying)
        {
            return false;
        }

        if (_shelf.Find(source) is not { } entry || _shelf.Open(source) is not { } book)
        {
            return false;
        }

        var place = entry.Finished ? new BookPlace(0, 0) : entry.Place;
        var arriving = entry.Download is { Complete: false };
        var first = FirstFrom(book, place);

        // A place past the text that has arrived is kept and waited for; the last sentence that has arrived is shown.
        BookPlace? awaiting = first is null && arriving ? place : null;

        if ((first ?? (arriving ? LastIn(book) : FirstFrom(book, new BookPlace(0, 0)))) is not { } shown)
        {
            return false;
        }

        Starting?.Invoke();

        lock (_gate)
        {
            Discard();
            _queued = null;
            _awaiting = awaiting;
            _arrived = false;
            _opened = entry.Download;
            _now = new BookPlayback(book, shown.Place, shown.Sentence, shown.Text, StoryRunPause.None) { Waiting = awaiting is not null };
            _audio.DropGroup(Group);

            if (!_audio.HoldSpeechForMedia(Group))
            {
                _now = _now with { Pause = StoryRunPause.Interrupted };
            }
        }

        if (arriving)
        {
            _downloads?.TryResume(source);
        }

        Raise();
        _background(Fill);
        return true;
    }

    /// <summary>Pauses at the sentence playing; it plays again from its start. A Commander pause replaces any other reason.</summary>
    public void Pause(StoryRunPause reason)
    {
        lock (_gate)
        {
            if (_now is { IsPaused: true } paused && reason == StoryRunPause.Commander && (paused.Pause != StoryRunPause.Commander || paused.NotArrived))
            {
                _now = paused with { Pause = reason, NotArrived = false };
            }
            else if (_now is not { IsPaused: false } now || reason == StoryRunPause.None)
            {
                return;
            }
            else
            {
                _now = now with { Pause = reason, Waiting = false };
                _audio.PausePlayback();
            }
        }

        Raise();
    }

    /// <summary>
    /// Plays on from the start of the sentence paused at; after a failed voice, that sentence is made again. Nothing
    /// happens while a story is playing.
    /// </summary>
    public void Resume() => ResumeWhen(static _ => true);

    /// <summary>
    /// Off the calling thread, resumes a push-to-talk or alert pause when D47 is not listening, not in a turn and not
    /// speaking.
    /// </summary>
    public void Settled() => _background(ResumeIfQuiet);

    /// <summary>Ends reading. The place stays where it was last saved, the start of the paragraph read.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_now is null)
            {
                return;
            }

            End();
        }

        Raise();
    }

    private void ResumeIfQuiet()
    {
        if (Now is not { Pause: StoryRunPause.PushToTalk or StoryRunPause.Interrupted }
            || _audio.Listening
            || _audio.TurnInFlight
            || _audio.IsSpeaking)
        {
            return;
        }

        ResumeWhen(static reason => reason is StoryRunPause.PushToTalk or StoryRunPause.Interrupted);
    }

    private void ResumeWhen(Func<StoryRunPause, bool> reason)
    {
        if (Now is not { IsPaused: true } paused || !reason(paused.Pause) || StoryPlaying)
        {
            return;
        }

        Starting?.Invoke();

        lock (_gate)
        {
            if (_now is not { IsPaused: true } now || !reason(now.Pause))
            {
                return;
            }

            if (now.Problem is not null)
            {
                Discard();
            }

            // The paused sentence's clip was dropped from the queue; it is queued again from what was made.
            _queued = null;
            _now = now with { Pause = StoryRunPause.None, Problem = null, NotArrived = false };

            if (!_audio.HoldSpeechForMedia(Group))
            {
                _now = _now with { Pause = StoryRunPause.Interrupted };
            }
        }

        Raise();
        _background(Fill);
    }

    /// <summary>
    /// Tells the arbiter and the ambient music, off the calling thread, whether the book is reading, then raises
    /// <see cref="Changed"/>.
    /// </summary>
    private void Raise()
    {
        _background(Publish);
        Changed?.Invoke();
    }

    /// <summary>Reads the book's state when it runs, so calls may land in any order.</summary>
    private void Publish()
    {
        lock (_publishing)
        {
            var reading = Now is { Pause: not StoryRunPause.Commander };
            _audio.MediaPlays(reading);
            _music?.Yield(MusicYield.Book, reading);
        }
    }

    /// <summary>Clears the book. Called holding <see cref="_gate"/>.</summary>
    private void End()
    {
        var holding = _now is { IsPaused: false };

        _now = null;
        _queued = null;
        _awaiting = null;
        _arrived = false;
        Discard();

        // A paused book may no longer hold the speech channels: a story can have taken them.
        if (holding)
        {
            _audio.EndPlayback();
        }
        else
        {
            _audio.DropGroup(Group);
        }
    }

    /// <summary>Drops every clip made or being made. Called holding <see cref="_gate"/>.</summary>
    private void Discard()
    {
        _rendering.Cancel();
        _rendering = new CancellationTokenSource();
        _ahead.Clear();
        _voice = null;
    }

    private void OnPlayed(AudioRequest request)
    {
        // Raised on the audio thread, possibly under the arbiter's lock: nothing here takes this player's lock.
        if (request.Group == Group)
        {
            _background(() => Advance(request));
        }
    }

    /// <summary>The queued sentence played to its end: moves to the next, saving the place at a new paragraph.</summary>
    private void Advance(AudioRequest request)
    {
        BookSource source;
        BookPlace? save = null;
        var finished = false;

        lock (_gate)
        {
            if (!ReferenceEquals(request, _queued) || _now is not { } now)
            {
                return;
            }

            _queued = null;
            source = now.Book.Source;
            var at = SpotOf(now);

            if (_ahead.Count > 0 && _ahead[0].Spot == at)
            {
                _ahead.RemoveAt(0);
            }

            if (After(now.Book, at) is { } next)
            {
                _now = now with { Place = next.Place, Sentence = next.Sentence, Text = next.Text, Waiting = false };
                save = next.Place == now.Place ? null : next.Place;
            }
            else if (_shelf.Find(source)?.Download is { Complete: false })
            {
                _awaiting = now.Place with { Paragraph = now.Place.Paragraph + 1 };
                _now = now with { Waiting = true };
            }
            else
            {
                save = now.Place;
                finished = true;
                End();
            }
        }

        if (save is { } place)
        {
            _shelf.MoveTo(source, place, finished);
        }

        Raise();
        Fill();
    }

    /// <summary>Starts making the sentences ahead that are not made or being made, and queues the current one once made.</summary>
    private void Fill()
    {
        if (AwaitingText())
        {
            return;
        }

        var voice = VoiceNow(out var problem);
        var started = new List<Ahead>();
        bool changed;

        lock (_gate)
        {
            if (_now is not { IsPaused: false } now)
            {
                return;
            }

            if (voice is null)
            {
                changed = Fail(now, problem!);
            }
            else
            {
                var at = SpotOf(now);

                if (_voice != voice || (_ahead.Count > 0 && _ahead[0].Spot != at))
                {
                    Discard();
                }

                _voice = voice;
                // With nothing made, a sentence already queued is not made again.
                var last = _ahead.Count > 0 ? _ahead[^1].Spot : _queued is not null ? at : null;

                while (_ahead.Count <= SentencesAhead && (last is null ? at : After(now.Book, last)) is { } spot)
                {
                    var entry = new Ahead(spot, _lastRender, _rendering.Token);
                    _lastRender = entry.Made.Task;
                    _ahead.Add(entry);
                    started.Add(entry);
                    last = spot;
                }

                changed = QueueOrWait(now);
            }
        }

        foreach (var entry in started)
        {
            _background(() => _ = RenderAsync(entry, voice!));
        }

        if (changed)
        {
            Raise();
        }
    }

    /// <summary>
    /// Queues the current sentence when its clip is made, marks it waiting when not, and pauses on it when its voice
    /// failed. Whether <see cref="_now"/> changed. Called holding <see cref="_gate"/>.
    /// </summary>
    private bool QueueOrWait(BookPlayback now)
    {
        if (_queued is not null || _ahead.Count == 0)
        {
            return false;
        }

        var made = _ahead[0].Made.Task;

        if (!made.IsCompleted)
        {
            if (now.Waiting)
            {
                return false;
            }

            _now = now with { Waiting = true };
            return true;
        }

        if (!made.IsCompletedSuccessfully)
        {
            return Fail(now, made.Exception?.GetBaseException().Message ?? "The voice did not make the sentence.");
        }

        _now = now with { Waiting = false };
        _queued = new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = made.Result,
            Group = Group,
            Caption = now.Text,
        };
        _audio.Enqueue(_queued);
        return now.Waiting;
    }

    /// <summary>Pauses on the current sentence with <paramref name="problem"/>. Called holding <see cref="_gate"/>.</summary>
    private bool Fail(BookPlayback now, string problem)
    {
        _now = now with { Pause = StoryRunPause.Commander, Waiting = false, Problem = problem };
        _queued = null;
        Discard();
        _audio.PausePlayback();
        return true;
    }

    /// <summary>The configured local voice, or null with why no sentence can be made with it.</summary>
    private BookVoice? VoiceNow(out string? problem)
    {
        var settings = _settings() ?? new BookSettings();
        var id = settings.Provider;

        if (!TtsProviderCatalog.IsLocal(id))
        {
            problem = $"Books are read only with Kokoro or Chatterbox, which run on this computer, and the book voice is set to {id}.";
            return null;
        }

        ITtsProvider? provider;

        try
        {
            provider = _localVoice(id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            problem = ex.Message;
            return null;
        }

        if (provider is null)
        {
            problem = $"{TtsProviderCatalog.Selected(id).Name} is not installed.";
            return null;
        }

        problem = null;
        return new BookVoice(provider, new VoiceSelection(string.IsNullOrWhiteSpace(settings.Voice) ? null : settings.Voice));
    }

    /// <summary>Makes one sentence once the sentence before it is made, then looks again at what to queue.</summary>
    private async Task RenderAsync(Ahead entry, BookVoice voice)
    {
        try
        {
            await entry.After.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            entry.Token.ThrowIfCancellationRequested();

            var clip = await voice.Provider.SynthesizeAsync(entry.Spot.Text, voice.Selection, entry.Token).ConfigureAwait(false);
            entry.Made.TrySetResult(SpeechLeveller.Apply(clip));
        }
        catch (OperationCanceledException) when (entry.Token.IsCancellationRequested)
        {
            entry.Made.TrySetCanceled(entry.Token);
        }
        catch (Exception ex)
        {
            entry.Made.TrySetException(ex);
        }

        _background(Fill);
    }

    /// <summary>
    /// More of the book being read is on the shelf: reading waiting at the end of the text goes on, and a pause at the
    /// end of the text with no download plays on.
    /// </summary>
    private void OnArrived(BookSource source)
    {
        bool resume;

        lock (_gate)
        {
            if (_now?.Book.Source != source)
            {
                return;
            }

            _arrived = true;
            resume = _now.NotArrived;
        }

        if (RefreshText())
        {
            Raise();
        }

        if (resume)
        {
            ResumeWhen(static _ => true);
        }
        else
        {
            _background(Fill);
        }
    }

    /// <summary>Opens the book being read again when its download has written more since it was last opened; true when it changed.</summary>
    public bool RefreshText()
    {
        BookSource source;
        BookDownload? seen;

        lock (_gate)
        {
            if (_now is not { } now)
            {
                return false;
            }

            source = now.Book.Source;
            seen = _opened;
        }

        var download = _shelf.Find(source)?.Download;

        if (download == seen)
        {
            return false;
        }

        var opened = _shelf.Open(source);

        lock (_gate)
        {
            if (_now is not { } now || now.Book.Source != source || opened is null || !Holds(opened, now.Place))
            {
                return false;
            }

            _opened = download;
            _now = now with { Book = opened };
            return true;
        }
    }

    /// <summary>
    /// Opens the book again when more of it has arrived and reading is near or at the end of what it had. At the end,
    /// moves on to the next paragraph once it is there; otherwise waits while a download runs, finishes a whole book,
    /// or pauses with <see cref="RestNotArrived"/>. True while there is nothing to read yet, so nothing is made.
    /// </summary>
    private bool AwaitingText()
    {
        for (var pass = 0; pass < 2; pass++)
        {
            BookSource source;

            lock (_gate)
            {
                if (_now is not { IsPaused: false } now)
                {
                    return false;
                }

                if (_awaiting is null && !(_arrived && NearEnd(now)))
                {
                    return false;
                }

                source = now.Book.Source;
                _arrived = false;
            }

            var download = _shelf.Find(source)?.Download;
            var opened = _shelf.Open(source);
            BookPlace? save = null;
            BookPlace? last = null;

            lock (_gate)
            {
                if (_now is not { IsPaused: false } now || now.Book.Source != source)
                {
                    return true;
                }

                if (opened is not null && Holds(opened, now.Place))
                {
                    _now = now = now with { Book = opened };
                    _opened = download;
                }

                if (_awaiting is not { } awaiting)
                {
                    return false;
                }

                if (FirstFrom(now.Book, awaiting) is { } next)
                {
                    _now = now with { Place = next.Place, Sentence = next.Sentence, Text = next.Text, Waiting = true };
                    _awaiting = null;
                    save = next.Place;
                }
                else
                {
                    last = now.Place;
                }
            }

            if (save is { } place)
            {
                _shelf.MoveTo(source, place);
                Raise();
                return false;
            }

            if (_downloads?.Downloading(source) == true || _downloads?.TryResume(source) == true)
            {
                return true;
            }

            if (_shelf.Find(source)?.Download is { Complete: false })
            {
                PauseNotArrived(source);
                return true;
            }

            // The text became whole since it was opened: open it once more before finishing.
            if (pass == 1)
            {
                FinishAt(source, last!.Value);
                return true;
            }

            lock (_gate)
            {
                _arrived = true;
            }
        }

        return true;
    }

    /// <summary>Pauses at the end of the text that has arrived, with nothing bringing the rest.</summary>
    private void PauseNotArrived(BookSource source)
    {
        lock (_gate)
        {
            if (_now is not { IsPaused: false } now || now.Book.Source != source || _awaiting is null)
            {
                return;
            }

            _now = now with { Pause = StoryRunPause.Commander, Waiting = false, NotArrived = true };
            _audio.PausePlayback();
        }

        Raise();
    }

    /// <summary>Ends reading a book read to the end of its whole text.</summary>
    private void FinishAt(BookSource source, BookPlace place)
    {
        lock (_gate)
        {
            if (_now?.Book.Source != source)
            {
                return;
            }

            End();
        }

        _shelf.MoveTo(source, place, finished: true);
        Raise();
    }

    /// <summary>Fewer than <see cref="SentencesAhead"/> sentences follow the one being read in the text opened.</summary>
    private static bool NearEnd(BookPlayback now)
    {
        var spot = SpotOf(now);

        for (var ahead = 0; ahead <= SentencesAhead; ahead++)
        {
            if (After(now.Book, spot) is not { } next)
            {
                return true;
            }

            spot = next;
        }

        return false;
    }

    /// <summary>Whether <paramref name="book"/> has a paragraph at <paramref name="place"/>.</summary>
    private static bool Holds(Book book, BookPlace place) =>
        place.Chapter >= 0 && place.Chapter < book.Chapters.Count
        && place.Paragraph >= 0 && place.Paragraph < book.Chapters[place.Chapter].Paragraphs.Count;

    /// <summary>The last sentence of the book, or null when it has none.</summary>
    private static Spot? LastIn(Book book)
    {
        for (var chapter = book.Chapters.Count - 1; chapter >= 0; chapter--)
        {
            var paragraphs = book.Chapters[chapter].Paragraphs;

            for (var paragraph = paragraphs.Count - 1; paragraph >= 0; paragraph--)
            {
                if (paragraphs[paragraph].Sentences is { Count: > 0 } sentences)
                {
                    return new Spot(new BookPlace(chapter, paragraph), sentences.Count - 1, sentences[^1]);
                }
            }
        }

        return null;
    }

    private static Spot SpotOf(BookPlayback now) => new(now.Place, now.Sentence, now.Text);

    /// <summary>The first sentence from the start of <paramref name="place"/>'s paragraph on, or null past the end.</summary>
    private static Spot? FirstFrom(Book book, BookPlace place)
    {
        for (var chapter = Math.Max(0, place.Chapter); chapter < book.Chapters.Count; chapter++)
        {
            var paragraphs = book.Chapters[chapter].Paragraphs;

            for (var paragraph = chapter == place.Chapter ? Math.Max(0, place.Paragraph) : 0; paragraph < paragraphs.Count; paragraph++)
            {
                if (paragraphs[paragraph].Sentences is { Count: > 0 } sentences)
                {
                    return new Spot(new BookPlace(chapter, paragraph), 0, sentences[0]);
                }
            }
        }

        return null;
    }

    /// <summary>The sentence after <paramref name="spot"/>, or null at the end of the book.</summary>
    private static Spot? After(Book book, Spot spot)
    {
        if (!Holds(book, spot.Place))
        {
            return null;
        }

        var paragraphs = book.Chapters[spot.Place.Chapter].Paragraphs;
        var sentences = paragraphs[spot.Place.Paragraph].Sentences;

        if (spot.Sentence + 1 < sentences.Count)
        {
            return spot with { Sentence = spot.Sentence + 1, Text = sentences[spot.Sentence + 1] };
        }

        return FirstFrom(
            book,
            spot.Place.Paragraph + 1 < paragraphs.Count
                ? spot.Place with { Paragraph = spot.Place.Paragraph + 1 }
                : new BookPlace(spot.Place.Chapter + 1, 0));
    }

    private sealed record Spot(BookPlace Place, int Sentence, string Text);

    private sealed record BookVoice(ITtsProvider Provider, VoiceSelection Selection);

    /// <summary>One sentence being made after <see cref="After"/>, the sentence before it.</summary>
    private sealed class Ahead(Spot spot, Task after, CancellationToken token)
    {
        public Spot Spot => spot;

        public Task After => after;

        public CancellationToken Token => token;

        public TaskCompletionSource<AudioClip> Made { get; } = new();
    }
}
