using D47.Core;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Logbook;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Logbook;

/// <summary>
/// The two acts, the money, and the voice (Phase 33, items 3 and 4).
/// </summary>
public class LogbookBookTests : IDisposable
{
    private static readonly DateTimeOffset Evening = new(3311, 4, 2, 19, 0, 0, TimeSpan.Zero);

    private readonly JournalCorpus _corpus = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "d47-logbook-out", Guid.NewGuid().ToString("N"));

    private LogbookSettings _settings = new();
    private LogbookContext _context = new();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _corpus.Dispose();

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// Item 4 in one assertion. There is no path from anywhere that sends the largest request d47
    /// makes without a person having seen the figure first.
    /// </summary>
    [Fact]
    public async Task NothingIsWrittenUntilAQuoteHasBeenGiven()
    {
        Evening1();
        var provider = FakeLlmProvider.Answering("A quiet run [1].");
        var book = Book(provider);

        var refused = await book.WriteAsync(CancellationToken.None);

        Assert.False(refused.Ok);
        Assert.Equal(0, provider.CallCount);
        Assert.Contains("what that would cost", refused.Message, StringComparison.Ordinal);

        book.Estimate(null, null, null, out _);
        Assert.NotNull(book.Armed);

        var written = await book.WriteAsync(CancellationToken.None);

        Assert.True(written.Ok, written.Message);
        Assert.Equal(1, provider.CallCount);
    }

    /// <summary>
    /// A second attempt is a second spend and it gets a second quote. An arming that survived a
    /// failed run would let a retry loop bill without ever showing a number again.
    /// </summary>
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
    /// Spending money to be told an empty evening was empty is the one purchase nobody would
    /// authorise twice.
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
            Path.Combine(_folder, "spend.jsonl"),
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
    /// A log priced against one model and written by another would make the quote a fiction, and
    /// the quote is the only thing standing between this feature and item 4's angry Commander.
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

        // And it says which voice was asked for and which one wrote, in the file — a Commander who
        // chose a narrator and got a report deserves to be told rather than to wonder.
        var file = File.ReadAllText(outcome.Written!.Path!);
        Assert.Contains("Personality is switched off", file, StringComparison.Ordinal);
    }

    /// <summary>
    /// With a personality, the persona reaches the prompt — and the guardrails still sit above it,
    /// which is the invariant doing what it was written for in the one place d47 speaks at length
    /// in character.
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

    /// <summary>
    /// item 2: the generator is handed facts, not files. Nothing that looks like a journal line
    /// reaches the prompt, whatever the window contained.
    /// </summary>
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
        var file = File.ReadAllText(outcome.Written!.Path!);

        Assert.Contains("[1]", file, StringComparison.Ordinal);
        Assert.Contains("## Sources", file, StringComparison.Ordinal);
        Assert.Contains("LoadGame at", file, StringComparison.Ordinal);
        Assert.Contains("sentences trace back to a fact", file, StringComparison.Ordinal);
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

    /// <summary>
    /// A provider with no model beneath it, which is the state on a fresh install and the state
    /// the Commander was in (remediation.md 11, item 12).
    /// </summary>
    [Fact]
    public void AProviderWithNoModelIsNotToldToChooseAProvider()
    {
        Evening1();

        _context = _context with { Model = null };

        var book = Book(FakeLlmProvider.Answering("..."));

        _context = _context with { Model = null };

        Assert.Null(book.Estimate(null, null, null, out var said));

        Assert.Contains("no model selected", said, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose a provider", said, StringComparison.Ordinal);

        // And it says the half that is already done, so the Commander is not sent to check a
        // setting that was never the problem.
        Assert.Contains("provider is set", said, StringComparison.Ordinal);
    }

    /// <summary>And no provider at all is still told to choose one.</summary>
    [Fact]
    public void NoProviderIsStillToldToChooseOne()
    {
        Evening1();

        var book = new LogbookBook(
            new LogFolder(_folder, NullLogger<LogFolder>.Instance),
            new LogDigestBuilder(NullLogger<LogDigestBuilder>.Instance),
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
            new LogFolder(_folder, NullLogger<LogFolder>.Instance),
            new LogDigestBuilder(NullLogger<LogDigestBuilder>.Instance),
            new LogWriter(NullLogger<LogWriter>.Instance),
            () => _settings,
            () => _corpus.Files,
            () => Evening.AddHours(5),
            () => _context,
            NullLogger<LogbookBook>.Instance);
    }
}
