using D47.Core.Books;
using D47.Core.Conversation;

namespace D47.Core.Capabilities.Builtin;

/// <summary>Reading the books on the shelf aloud, by voice. Every tool is the Commander's.</summary>
public static class BookCapability
{
    public const string Id = "books";

    public const string ReadBookTool = "read_book";

    public const string PauseBookTool = "pause_book";

    public const string ResumeBookTool = "resume_book";

    public const string NextChapterTool = "next_chapter";

    public const string BackAParagraphTool = "back_a_paragraph";

    public const string ReadPhrase = "read ";

    /// <summary>"Read" and a title, one per book on the shelf.</summary>
    public static IEnumerable<DynamicCommand> ReadPhrases(BookDesk? desk)
    {
        if (desk is null)
        {
            yield break;
        }

        foreach (var book in desk.Shelf())
        {
            yield return new DynamicCommand(
                ReadPhrase + book.Title,
                Id,
                ReadBookTool,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["title"] = book.Title });
        }
    }

    /// <param name="desk">The desk, or null where nothing reads books.</param>
    /// <param name="storyPlaybackOpen">Whether a story playback is open, which takes bare "play" and "pause" first.</param>
    /// <param name="takesBare">Whether a book takes bare "play" and "pause" over a podcast and the music.</param>
    public static CapabilityDescriptor Create(Func<BookDesk?> desk, Func<bool>? storyPlaybackOpen = null, Func<bool>? takesBare = null)
    {
        bool Bare() => storyPlaybackOpen?.Invoke() != true && takesBare?.Invoke() == true;

        return new CapabilityDescriptor
        {
            Id = Id,
            Group = "Voice",
            Name = "Books",
            Summary = "Read the books on your shelf aloud with a local voice, and move by chapter and paragraph.",
            Examples = ["read the book", "pause the book", "next chapter", "back a paragraph"],
            Display = new CapabilityDisplay { PanelTitle = "Books", Order = 94, ShowOnPanel = false },
            Tools =
            [
                Tool(
                    ReadBookTool,
                    "Reads a book on the shelf aloud, from where it was left or from the start once finished. With no title it reads the book last read.",
                    "With a book on the shelf and Kokoro or Chatterbox installed, say \"read the book\": the book last read carries on and the reply names it and its chapter. Say read followed by a shelf title to read that book. With no local voice installed the reply says books are read only by Kokoro or Chatterbox.",
                    "read the book",
                    desk,
                    (books, arguments) => books.Read(arguments.Text("title")),
                    [Phrase("read the book"), Phrase("read book")],
                    [ToolParameter.Text("title", "The shelf title, or part of it. Optional.")]),
                Tool(
                    PauseBookTool,
                    "Pauses the book being read.",
                    "While a book is being read, say \"pause the book\" or \"pause\": the reply is \"Paused.\" and the reading stops at the sentence it was on.",
                    "pause the book",
                    desk,
                    (books, _) => books.Pause(),
                    [Phrase("pause the book"), Phrase("pause book"), Phrase("pause") with { When = Bare }]),
                Tool(
                    ResumeBookTool,
                    "Resumes the book that was paused.",
                    "With a book paused, say \"resume the book\" or \"play\": the sentence it stopped on is read again and the reply is \"Resumed.\"",
                    "resume the book",
                    desk,
                    (books, _) => books.Resume(),
                    [Phrase("resume the book"), Phrase("resume book"), Phrase("play") with { When = Bare }]),
                Tool(
                    NextChapterTool,
                    "Reads the book being read from the start of its next chapter.",
                    "While a book is being read, say \"next chapter\": the next chapter starts and the reply gives its number and title. In the last chapter the reply says so.",
                    "next chapter",
                    desk,
                    (books, _) => books.NextChapter(),
                    [Phrase("next chapter")]),
                Tool(
                    BackAParagraphTool,
                    "Reads the book being read again from the start of the paragraph before the one it is in.",
                    "While a book is being read, say \"back a paragraph\": the reading starts again from the paragraph before and the reply is \"Back a paragraph.\"",
                    "back a paragraph",
                    desk,
                    (books, _) => books.BackAParagraph(),
                    [Phrase("back a paragraph")]),
            ],
        };
    }

    private static ToolCommandPhrase Phrase(string phrase) =>
        new(phrase, new Dictionary<string, string>(StringComparer.Ordinal));

    private static ToolDefinition Tool(
        string name,
        string description,
        string exercise,
        string refusalExample,
        Func<BookDesk?> desk,
        Func<BookDesk, ToolArguments, string> act,
        IReadOnlyList<ToolCommandPhrase> commands,
        IReadOnlyList<ToolParameter>? parameters = null) => new()
    {
        Name = name,
        Description = description,
        Exercise = exercise,
        Parameters = parameters ?? [],
        Protected = true,
        RefusalExample = refusalExample,
        Commands = commands,
        Handler = (arguments, _) =>
            Task.FromResult(desk() is { } books
                ? ToolResult.Ok(act(books, arguments))
                : ToolResult.Error("Books are not available.")),
    };
}
