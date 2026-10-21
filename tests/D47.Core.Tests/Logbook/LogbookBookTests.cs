using D47.Core.Storage;
using D47.Core;
using D47.Core.CaptainsLog;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Logbook;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Logbook;

/// <summary>The two acts, the money, and the voice.</summary>
public class LogbookBookTests
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly JournalCorpus _corpus = new();
    private const string _folder = @"C:\d47-test\commander-log";


    private LogbookSettings _settings = new();
    private LogbookContext _context = new();
    private IReadOnlyList<CaptainsLogEntry> _entries = [];

    /// <summary>The quote is optional: a write with none prices the window and writes it in one call.</summary>
    [Fact]
    public async Task AWriteWithNoQuotePricesTheWindowAndWritesIt()
    {
        Evening1();
        var provider = FakeLlmProvider.Answering("A quiet run [1].");
        var book = Book(provider);

        Assert.Null(book.Armed);

        var written = await book.WriteAsync(CancellationToken.None);

        Assert.True(written.Ok, written.Message);
        Assert.Equal(1, provider.CallCount);
        Assert.Null(book.Armed);
        Assert.Single(book.Folder.Entries());
    }

    [Fact]
    public async Task AnUnquotedWriteOverAnEmptyWindowSendsNothing()
    {
        _corpus.Journal(Evening, JournalCorpus.LoadGame(Evening));

        var provider = FakeLlmProvider.Answering("...");
        var book = Book(provider);

        var refused = await book.WriteAsync(
            "today", Evening.AddYears(-3), Evening.AddYears(-3).AddHours(1), CancellationToken.None);

        Assert.False(refused.Ok);
        Assert.Equal(0, provider.CallCount);
        Assert.Contains("nothing in that window", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>A second attempt is a second spend and it gets a second quote.</summary>
    [Fact]
    public async Task TheQuoteIsSpentEvenWhenTheWriteFails()
    {
        Evening1();

        var book = Book(new FakeLlmProvider(
            new LlmStreamEvent.Failed("The provider is on fire.", Transient: false)));

        book.Estimate(null, null, null, out _);
        Assert.NotNull(book.Armed);

        var failed = await book.WriteAsync(CancellationToken.None);

        Assert.False(failed.Ok);
        Assert.Null(book.Armed);
    }

    /// <summary>
    /// Spending money to be told an empty evening was empty is the one purchase nobody would authorise
    /// twice.
    /// </summary>
    [Fact]
    public void AWindowWithNothingInItNeverArms()
    {
        _corpus.Journal(Evening, JournalCorpus.LoadGame(Evening));

        var book = Book(FakeLlmProvider.Answering("..."));

        book.Estimate("today", Evening.AddYears(-3), Evening.AddYears(-3).AddHours(1), out var message);

        Assert.Null(book.Armed);
        Assert.Contains("nothing in that window", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhatItActuallyCostGoesToTheSpendLedger()
    {
        Evening1();

        var ledger = new SpendLedger(
            Path.Combine(_folder, "spend.jsonl"), new MemoryFileSystem(),
            SystemWallClock.Instance,
            NullLogger<SpendLedger>.Instance);

        _context = _context with { Ledger = ledger };

        var book = Book(FakeLlmProvider.Answering(
            "A quiet run out and back [1].",
            new LlmUsage(2000, 400, 0, 0)));

        book.Estimate(null, null, null, out _);
        await book.WriteAsync(CancellationToken.None);

        var row = Assert.Single(ledger.Entries);

        Assert.Equal(SpendKind.Model, row.Kind);
        Assert.Equal("claude-opus-5", row.Model);
        Assert.Equal(2000, row.InputTokens);
        Assert.Equal(400, row.OutputTokens);
        Assert.True(row.Priced);

        // 2,000 in at $5/M and 400 out at $25/M.
        Assert.Equal(0.02m, row.Dollars);
    }

    /// <summary>
    /// A log priced against one model and written by another would make the quote a fiction, and the
    /// quote is the only thing standing between this feature and item 4's angry Commander.
    /// </summary>
    [Fact]
    public async Task ChangingTheModelAfterTheQuoteIsRefusedRatherThanHonoured()
    {
        Evening1();

        var book = Book(FakeLlmProvider.Answering("A quiet run [1]."));
        book.Estimate(null, null, null, out _);

        _context = _context with { Model = "claude-haiku-4-5" };

        var outcome = await book.WriteAsync(CancellationToken.None);

        Assert.False(outcome.Ok);
        Assert.Contains("re-price", outcome.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Item 3: a log is d47 speaking at length, so a personality switched off writes plainly rather
    /// than writing as somebody else.
    /// </summary>
    [Fact]
    public async Task PersonalityOffWritesPlainlyRatherThanAsSomebodyElse()
    {
        Evening1();
        _settings = _settings with { Voice = "ships-ai" };
        _context = _context with { PersonalityEnabled = false, Persona = null };

        var provider = FakeLlmProvider.Answering("A quiet run [1].");
        var book = Book(provider);

        book.Estimate(null, null, null, out _);

        Assert.Equal(LogVoice.ShipsAi, book.Armed!.Asked);
        Assert.Equal(LogVoice.FirstPerson, book.Armed.Used);

        var outcome = await book.WriteAsync(CancellationToken.None);

        Assert.True(outcome.Ok, outcome.Message);
        Assert.Null(provider.LastRequest!.Prompt.Persona);

        // And it says which voice was asked for and which one wrote, in the file — a Commander who chose a
        // narrator and got a report deserves to be told rather than to wonder.
        var file = _corpus.FileSystem.ReadText(outcome.Written!.Path!);
        Assert.Contains("Personality is switched off", file, StringComparison.Ordinal);
    }

    /// <summary>
    /// With a personality, the persona reaches the prompt — and the guardrails still sit above it,
    /// which is the invariant doing what it was written for in the one place d47 speaks at length in
    /// character.
    /// </summary>
    [Fact]
    public void TheShipsAiVoiceCarriesThePersonaUnderTheGuardrails()
    {
        Evening1();
        _settings = _settings with { Voice = "ships-ai" };
        _context = _context with { PersonalityEnabled = true, Persona = "You are DIRECTIVE 47." };

        var book = Book(FakeLlmProvider.Answering("A quiet run [1]."));
        book.Estimate(null, null, null, out _);

        var block = LogPrompt
            .Build(book.Armed!.Digest, book.Armed.Used, book.Armed.Length, "You are DIRECTIVE 47.", null)
            .RenderCachedSystemBlock();

        Assert.Contains("DIRECTIVE 47", block, StringComparison.Ordinal);
        Assert.True(
            block.IndexOf(Guardrails.Text, StringComparison.Ordinal)
            < block.IndexOf("DIRECTIVE 47", StringComparison.Ordinal));
    }

    /// <summary>item 2: the generator is handed facts, not files.</summary>
    [Fact]
    public void NoJournalReachesTheModel()
    {
        Evening1();

        var provider = FakeLlmProvider.Answering("A quiet run [1].");
        var book = Book(provider);

        book.Estimate(null, null, null, out _);

        var sent = string.Concat(
            LogPrompt.Build(book.Armed!.Digest, book.Armed.Used, book.Armed.Length, null, null)
                .History.Select(message => message.Text));

        Assert.DoesNotContain("\"event\":", sent, StringComparison.Ordinal);
        Assert.DoesNotContain("\"timestamp\":", sent, StringComparison.Ordinal);
        Assert.Contains("Deciat", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASecondLogTheSameDayDoesNotOverwriteTheFirst()
    {
        Evening1();

        var book = Book(FakeLlmProvider.Answering("A quiet run [1]."));

        book.Estimate(null, null, null, out _);
        var first = await book.WriteAsync(CancellationToken.None);

        book.Estimate(null, null, null, out _);
        var second = await book.WriteAsync(CancellationToken.None);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.NotEqual(first.Written!.Path, second.Written!.Path);
        Assert.Equal(2, book.Folder.Entries().Count);
    }

    [Fact]
    public async Task TheWrittenFileKeepsItsCitationsAndListsWhatTheyPointAt()
    {
        Evening1();

        var book = Book(FakeLlmProvider.Answering("A quiet run out to Deciat and back [1]."));
        book.Estimate(null, null, null, out _);

        var outcome = await book.WriteAsync(CancellationToken.None);
        var file = _corpus.FileSystem.ReadText(outcome.Written!.Path!);

        Assert.Contains("[1]", file, StringComparison.Ordinal);
        Assert.Contains("## Sources", file, StringComparison.Ordinal);
        Assert.Contains("LoadGame at", file, StringComparison.Ordinal);
        Assert.Contains("sentences trace back to a fact", file, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALogQuotesTheEntriesInItsRangeVerbatimAfterTheModelsText()
    {
        Evening1();
        _entries =
        [
            new CaptainsLogEntry("b", "Second thought, odd  spacing.", Evening.AddMinutes(30)) { System = "Deciat" },
            new CaptainsLogEntry("a", "First thought.", Evening.AddMinutes(5)) { System = "Sol" },
            new CaptainsLogEntry("c", "Years ago.", Evening.AddYears(-1)),
        ];

        var book = Book(FakeLlmProvider.Answering("A quiet run [1]."));
        book.Estimate(null, null, null, out _);

        var outcome = await book.WriteAsync(CancellationToken.None);
        var file = _corpus.FileSystem.ReadText(outcome.Written!.Path!)!;

        var section = file.IndexOf("## In your own words", StringComparison.Ordinal);

        Assert.True(section > file.IndexOf("A quiet run", StringComparison.Ordinal));
        Assert.True(section < file.IndexOf("## Sources", StringComparison.Ordinal));
        Assert.True(file.IndexOf("First thought.", StringComparison.Ordinal) > section);
        Assert.True(
            file.IndexOf("First thought.", StringComparison.Ordinal)
            < file.IndexOf("Second thought, odd  spacing.", StringComparison.Ordinal));
        Assert.Contains("— Sol", file, StringComparison.Ordinal);
        Assert.Contains("— Deciat", file, StringComparison.Ordinal);
        Assert.DoesNotContain("Years ago.", file, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALogWithNoEntriesInRangeHasNoOwnWordsSection()
    {
        Evening1();
        _entries = [new CaptainsLogEntry("c", "Years ago.", Evening.AddYears(-1))];

        var book = Book(FakeLlmProvider.Answering("A quiet run [1]."));
        book.Estimate(null, null, null, out _);

        var outcome = await book.WriteAsync(CancellationToken.None);

        Assert.DoesNotContain("In your own words", _corpus.FileSystem.ReadText(outcome.Written!.Path!), StringComparison.Ordinal);
    }

    [Fact]
    public void EntriesNeverReachThePromptOrChangeTheQuote()
    {
        Evening1();

        var plain = Book(FakeLlmProvider.Answering("A quiet run [1]."));
        plain.Estimate(null, null, null, out _);
        var plainPrice = plain.Armed!.Price;

        _entries = [new CaptainsLogEntry("a", "Purple walrus secret.", Evening.AddMinutes(5)) { System = "Sol" }];

        var book = Book(FakeLlmProvider.Answering("A quiet run [1]."));
        book.Estimate(null, null, null, out _);

        var request = LogPrompt.Build(book.Armed!.Digest, book.Armed.Used, book.Armed.Length, null, null);
        var sent = request.RenderCachedSystemBlock() + string.Concat(request.History.Select(message => message.Text));

        Assert.DoesNotContain("walrus", sent, StringComparison.Ordinal);
        Assert.Equal(plainPrice, book.Armed.Price);
    }

    /// <summary>One short evening: enough to have something to write about.</summary>
    private void Evening1() =>
        _corpus.Journal(
            Evening,
            JournalCorpus.LoadGame(Evening),
            JournalCorpus.Jump(Evening.AddMinutes(10), "Deciat", 8.09),
            JournalCorpus.Event(
                Evening.AddMinutes(20),
                "Docked",
                "\"StarSystem\":\"Deciat\",\"StationName\":\"Garay Terminal\""));

    /// <summary>A blank model setting means the provider's default, as it does for conversation.</summary>
    [Fact]
    public async Task ABlankModelUsesTheProvidersDefault()
    {
        Evening1();

        var provider = FakeLlmProvider.Answering("A quiet run [1].");
        var book = Book(provider);

        _context = _context with { Model = null };

        book.Estimate(null, null, null, out var said);

        Assert.Equal("claude-opus-5", book.Armed?.Model);
        Assert.DoesNotContain("no model selected", said, StringComparison.Ordinal);

        var written = await book.WriteAsync(CancellationToken.None);

        Assert.True(written.Ok, written.Message);
        Assert.Equal("claude-opus-5", provider.LastRequest!.Model);
    }

    /// <summary>A provider with no model chosen and no default of its own.</summary>
    [Fact]
    public void AProviderWithNoModelIsNotToldToChooseAProvider()
    {
        Evening1();

        var book = Book(new FakeLlmProvider() { DefaultModel = "" });

        _context = _context with { Model = null };

        Assert.Null(book.Estimate(null, null, null, out var said));

        Assert.Contains("no model selected", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose a provider", said, StringComparison.Ordinal);

        // And it says the half that is already done, so the Commander is not sent to check a setting that was
        // never the problem.
        Assert.Contains("provider is set", said, StringComparison.Ordinal);
    }

    [Fact]
    public void NoProviderIsStillToldToChooseOne()
    {
        Evening1();

        var book = new LogbookBook(
            new LogFolder(_folder, _corpus.FileSystem, NullLogger<LogFolder>.Instance),
            new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance),
            new LogWriter(NullLogger<LogWriter>.Instance),
            () => _settings,
            () => _corpus.Files,
            () => Evening.AddHours(5),
            () => new LogbookContext(),
            NullLogger<LogbookBook>.Instance);

        Assert.Null(book.Estimate(null, null, null, out var said));

        Assert.Contains("no provider selected", said, StringComparison.Ordinal);
    }

    private LogbookBook Book(ILlmProvider provider)
    {
        _context = _context with { Provider = provider, Model = _context.Model ?? "claude-opus-5" };

        return new LogbookBook(
            new LogFolder(_folder, _corpus.FileSystem, NullLogger<LogFolder>.Instance),
            new LogDigestBuilder(_corpus.FileSystem, NullLogger<LogDigestBuilder>.Instance),
            new LogWriter(NullLogger<LogWriter>.Instance),
            () => _settings,
            () => _corpus.Files,
            () => Evening.AddHours(5),
            () => _context,
            NullLogger<LogbookBook>.Instance,
            captainsLog: _ => _entries);
    }
}
