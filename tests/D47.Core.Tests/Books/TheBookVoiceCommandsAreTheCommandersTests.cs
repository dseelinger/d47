using D47.Core.Books;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Books;

/// <summary>The book tools, their phrases, and who may reach them (#2663).</summary>
public sealed class TheBookVoiceCommandsAreTheCommandersTests
{
    private static readonly BookSource War = BookSource.FromGutenberg(36);

    private static readonly BookSource Time = BookSource.FromGutenberg(35);

    private static readonly ShelfBook[] Books =
    [
        new(War, "The War of the Worlds", "H. G. Wells", 3, new BookPlace(0, 0), Finished: false),
        new(Time, "The Time Machine", "H. G. Wells", 2, new BookPlace(1, 4), Finished: false),
    ];

    private static readonly string[] Chapters = ["The Eve of the War", "The Falling Star", "The Cylinder"];

    private readonly List<string> _calls = [];
    private BookSource? _reading;
    private int _chapter;
    private bool _paused;
    private bool _voice = true;

    private BookDesk Desk() => new(
        () => Books,
        () => _voice,
        source =>
        {
            _calls.Add("read " + source.Id);
            _reading = source;
            _chapter = 0;
            _paused = false;
            return true;
        },
        () => _reading is null
            ? null
            : new BookNow(_reading, Books.First(book => book.Source == _reading).Title, _chapter, Chapters[_chapter], _paused),
        () => _paused = true,
        () => _paused = false,
        () =>
        {
            if (_chapter + 1 >= Chapters.Length)
            {
                return false;
            }

            _chapter++;
            return true;
        },
        () => true);

    private static CapabilityRegistry Registry(
        BookDesk desk,
        Func<bool>? story = null,
        Func<bool>? bookTakes = null,
        Func<bool>? podcastTakes = null) =>
        CapabilityRegistry.Build(
            [
                BookCapability.Create(() => desk, story, bookTakes),
                PodcastCapability.Create(() => null, story, podcastTakes),
                AudioCapability.Create(
                    storyPlaybackOpen: story,
                    podcastActive: () => bookTakes?.Invoke() == true || podcastTakes?.Invoke() == true),
                AdventureCapability.Create(desk: new AdventureCapability.AdventureDesk { PlaybackOpen = story ?? (() => false) }),
            ]);

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(pair => pair.Name, pair => pair.Value));

    [Theory]
    [InlineData("read the book", BookCapability.ReadBookTool)]
    [InlineData("pause the book", BookCapability.PauseBookTool)]
    [InlineData("resume the book", BookCapability.ResumeBookTool)]
    [InlineData("next chapter", BookCapability.NextChapterTool)]
    [InlineData("back a paragraph", BookCapability.BackAParagraphTool)]
    public void EachPhraseReachesItsToolWithoutTheModel(string said, string tool)
    {
        var router = new KeywordRouter(Registry(Desk()));

        Assert.Equal(tool, router.MatchToolCommand(said)?.ToolName);
    }

    [Fact]
    public void ReadAndATitleIsOnePhrasePerShelfBook()
    {
        var phrases = BookCapability.ReadPhrases(Desk()).ToList();

        Assert.Equal(["read The War of the Worlds", "read The Time Machine"], phrases.Select(phrase => phrase.Phrase));
        Assert.All(phrases, phrase => Assert.Equal(BookCapability.ReadBookTool, phrase.ToolName));
        Assert.Equal("The Time Machine", phrases[1].Arguments["title"]);
    }

    [Fact]
    public async Task TheModelIsRefusedEveryBookTool()
    {
        var desk = Desk();
        var registry = Registry(desk);
        var token = TestContext.Current.CancellationToken;

        foreach (var tool in new[]
                 {
                     BookCapability.ReadBookTool,
                     BookCapability.PauseBookTool,
                     BookCapability.ResumeBookTool,
                     BookCapability.NextChapterTool,
                     BookCapability.BackAParagraphTool,
                 })
        {
            Assert.True((await registry.InvokeAsync(tool, Args(), token, ToolCaller.Model)).IsError, tool);
        }

        Assert.Empty(_calls);
    }

    [Fact]
    public async Task EachToolAnswersInOneShortSentence()
    {
        var registry = Registry(Desk());
        var token = TestContext.Current.CancellationToken;

        async Task<string> Say(string tool, params (string, string)[] arguments) =>
            (await registry.InvokeAsync(tool, Args(arguments), token, ToolCaller.Commander)).Content;

        Assert.Equal("No book is being read.", await Say(BookCapability.PauseBookTool));
        Assert.Equal("Reading The War of the Worlds, chapter 1.", await Say(BookCapability.ReadBookTool, ("title", "war of the worlds")));
        Assert.Equal("Paused.", await Say(BookCapability.PauseBookTool));
        Assert.Equal("Resumed.", await Say(BookCapability.ResumeBookTool));
        Assert.Equal("Chapter 2: The Falling Star.", await Say(BookCapability.NextChapterTool));
        Assert.Equal("Back a paragraph.", await Say(BookCapability.BackAParagraphTool));
        Assert.Equal("Chapter 3: The Cylinder.", await Say(BookCapability.NextChapterTool));
        Assert.Equal("This is the last chapter.", await Say(BookCapability.NextChapterTool));
    }

    [Fact]
    public async Task ReadTheBookCarriesOnTheBookLastRead()
    {
        var registry = Registry(Desk());
        var token = TestContext.Current.CancellationToken;

        await registry.InvokeAsync(BookCapability.ReadBookTool, Args(("title", "time machine")), token, ToolCaller.Commander);
        _reading = null;
        _calls.Clear();

        var reply = await registry.InvokeAsync(BookCapability.ReadBookTool, Args(), token, ToolCaller.Commander);

        Assert.Equal(["read " + Time.Id], _calls);
        Assert.Equal("Reading The Time Machine, chapter 1.", reply.Content);
    }

    [Fact]
    public async Task WithNoLocalVoiceReadSaysBooksNeedKokoroOrChatterbox()
    {
        _voice = false;
        var registry = Registry(Desk());

        var reply = await registry.InvokeAsync(BookCapability.ReadBookTool, Args(), TestContext.Current.CancellationToken, ToolCaller.Commander);

        Assert.Equal("Books are read only by Kokoro or Chatterbox, and neither is installed.", reply.Content);
        Assert.Empty(_calls);
    }

    [Fact]
    public void BareWordsGoToTheStoryThenTheOneBookThenTheMusic()
    {
        var story = false;
        var book = false;
        var router = new KeywordRouter(Registry(Desk(), () => story, () => book));

        Assert.Equal(AudioCapability.ManageMusicTool, router.MatchToolCommand("pause")?.ToolName);

        book = true;
        Assert.Equal(BookCapability.PauseBookTool, router.MatchToolCommand("pause")?.ToolName);
        Assert.Equal(BookCapability.ResumeBookTool, router.MatchToolCommand("play")?.ToolName);

        story = true;
        Assert.Equal(AdventureCapability.StoryPlaybackTool, router.MatchToolCommand("pause")?.ToolName);
    }

    [Fact]
    public void BareWordsGoToThePodcastWhenTheBookIsNotTheOne()
    {
        var book = false;
        var podcast = true;
        var router = new KeywordRouter(Registry(Desk(), null, () => book, () => podcast && !book));

        Assert.Equal(PodcastCapability.ManagePodcastTool, router.MatchToolCommand("pause")?.ToolName);

        book = true;
        Assert.Equal(BookCapability.PauseBookTool, router.MatchToolCommand("pause")?.ToolName);
    }
}
