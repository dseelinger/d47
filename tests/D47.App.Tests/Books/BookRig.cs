using D47.App.Tests.Stories;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Tests.Books;

/// <summary>A voice that records what it was asked to say, and makes each clip at once, or when released.</summary>
internal sealed class FakeBookVoice(string id) : ITtsProvider
{
    private readonly Dictionary<string, TaskCompletionSource<AudioClip>> _held = [];

    public List<string> Said { get; } = [];

    /// <summary>The sentence that throws instead of being made.</summary>
    public string? Fails { get; set; }

    /// <summary>When set, every clip waits for <see cref="Release"/>.</summary>
    public bool Holds { get; set; }

    public string Id => id;

    public string Name => id;

    public Task<VoiceCatalogue> ListVoicesAsync(CancellationToken cancellationToken = default) => Task.FromResult(VoiceCatalogue.Silent);

    public Task<AudioClip> SynthesizeAsync(string text, VoiceSelection voice, CancellationToken cancellationToken = default)
    {
        Said.Add(text);

        if (text == Fails)
        {
            throw new TtsException("model file not found");
        }

        if (!Holds)
        {
            return Task.FromResult(Clip(text));
        }

        var held = new TaskCompletionSource<AudioClip>();
        _held[text] = held;
        return held.Task;
    }

    public void Release(string text) => _held[text].SetResult(Clip(text));

    private static AudioClip Clip(string text) => new(text, new byte[960], AudioFormat.Standard);
}

/// <summary>A shelf with one four-sentence book over two chapters, and a player reading it with fake voices.</summary>
internal sealed class BookRig
{
    public const string First = "The lamp was lit.";
    public const string Second = "The door was shut.";
    public const string Third = "Night fell.";
    public const string Fourth = "The tide was out.";

    public static readonly BookSource Source = BookSource.FromFile("The Night Watch.txt");

    private BookRig(IFileSystem files, string folder, string provider)
    {
        Sink = new SteppedSink();
        Audio = new AudioArbiter(Sink, NullLogger<AudioArbiter>.Instance).Start();
        Shelf = new BookShelf(files, folder, NullLogger.Instance);
        Settings = new BookSettings { Provider = provider };
        Voices = new Dictionary<string, FakeBookVoice>(StringComparer.OrdinalIgnoreCase)
        {
            ["kokoro"] = new("kokoro"),
            ["chatterbox"] = new("chatterbox"),
            ["elevenlabs"] = new("elevenlabs"),
        };
        Player = new BookPlayer(
            Audio,
            Shelf,
            id =>
            {
                Asked.Add(id);
                return Installed ? Voices.GetValueOrDefault(id) : null;
            },
            () => Settings,
            Background.Enqueue);
    }

    public SteppedSink Sink { get; }

    public AudioArbiter Audio { get; }

    public BookShelf Shelf { get; }

    public BookPlayer Player { get; }

    public BookSettings Settings { get; set; }

    public Dictionary<string, FakeBookVoice> Voices { get; }

    /// <summary>Every provider id the player looked up.</summary>
    public List<string> Asked { get; } = [];

    /// <summary>Whether the looked-up voice's model is on this PC.</summary>
    public bool Installed { get; set; } = true;

    public Queue<Action> Background { get; } = new();

    public static Book Book => new(
        "The Night Watch",
        "H. G. Wellstone",
        Source,
        [
            new BookChapter("One", [new BookParagraph(First + " " + Second), new BookParagraph(Third)]),
            new BookChapter("Two", [new BookParagraph(Fourth)]),
        ]);

    /// <summary>A rig with the book on a shelf held in memory.</summary>
    public static BookRig InMemory(string provider = "kokoro") => OnShelf(new MemoryFileSystem(), @"C:\d47\data\books", provider, add: true);

    /// <summary>A rig over the shelf in <paramref name="folder"/>, adding the book first when <paramref name="add"/>.</summary>
    public static BookRig OnShelf(IFileSystem files, string folder, string provider = "kokoro", bool add = false)
    {
        var rig = new BookRig(files, folder, provider);

        if (add)
        {
            rig.Shelf.Add(Book);
        }

        return rig;
    }

    /// <summary>Runs what the player sent off its thread, and anything that sends.</summary>
    public void Settle()
    {
        while (Background.TryDequeue(out var work))
        {
            work();
        }
    }

    /// <summary>Plays the book and runs what that starts.</summary>
    public void Play()
    {
        Player.Play(Source);
        Settle();
    }

    /// <summary>Lets the clip named <paramref name="name"/> play to its end, and the player follow.</summary>
    public void Finish(string name)
    {
        Sink.FinishUntil(name);
        Sink.FinishCurrent();
        Settle();
    }

    /// <summary>Queues a reply from D47 in a turn's group.</summary>
    public void Reply(string name)
    {
        Audio.Enqueue(new AudioRequest
        {
            Channel = AudioChannel.Speech,
            Clip = new AudioClip(name, new byte[960], AudioFormat.Standard),
            Group = "turn-1",
        });
        Settle();
    }
}
