namespace D47.Core.Stories;

/// <summary>The line drawn under a finished chapter's told log.</summary>
public static class ChapterEnd
{
    public const string OfStory = "The end.";

    public const string OfChapter = "End of chapter.";

    /// <summary><see cref="OfStory"/> for the last chapter of a finished story, otherwise <see cref="OfChapter"/>; <paramref name="story"/> is null for a Short Story.</summary>
    public static string Line(Story? story, string chapterKey) =>
        story is { State: StoryState.Finished, Chapters: [.., var last] } && string.Equals(last, chapterKey, StringComparison.OrdinalIgnoreCase)
            ? OfStory
            : OfChapter;
}
