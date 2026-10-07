namespace D47.Core.Journal;

/// <summary>A journal file and the byte offset of its next unread line.</summary>
public sealed record JournalMark(string Path, long Position);
