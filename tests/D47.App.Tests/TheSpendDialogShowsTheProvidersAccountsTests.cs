using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Your accounts section at the top of SPEND, in every state the AccountBlock card draws.</summary>
public class TheSpendDialogShowsTheProvidersAccountsTests
{
    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow { get; } = at;
    }

    private sealed class FakeReader(AccountProvider provider, Func<CancellationToken, Task<AccountReading>> read) : IAccountReader
    {
        public AccountProvider Provider { get; } = provider;

        public int Reads { get; private set; }

        public bool ReadOnTheUiThread { get; private set; }

        public CancellationToken Token { get; private set; }

        public Task<AccountReading> ReadAsync(CancellationToken cancellationToken)
        {
            Reads++;
            ReadOnTheUiThread = Dispatcher.UIThread.CheckAccess();
            Token = cancellationToken;
            return read(cancellationToken);
        }
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 18, 19, 42, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Reset = new(2026, 12, 2, 0, 0, 0, TimeSpan.Zero);

    private static FakeReader Reader(AccountProvider provider, AccountReading reading) =>
        new(provider, _ => Task.FromResult(reading));

    private static FakeReader Unfinished(AccountProvider provider, TaskCompletionSource<AccountReading> source) =>
        new(provider, token =>
        {
            token.Register(() => source.TrySetCanceled(token));
            return source.Task;
        });

    private static ElevenLabsFigures Figures(
        long used,
        long limit = 100_000,
        DateTimeOffset? reset = null,
        LimitExtension? extension = null,
        decimal overage = 0m) =>
        new(used, limit, reset ?? Reset, RefreshPeriod.Monthly, extension ?? LimitExtension.Stops, new Money(overage, "usd"));

    private static readonly DateTimeOffset Midnight = new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);

    private static SpendDialog Dialog(params IAccountReader[] readers) => At(Now, readers);

    private static SpendDialog At(DateTimeOffset now, params IAccountReader[] readers)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var root = Path.Combine(TestSurface.MemoryFolder("d47-spend-accounts"), "spend.jsonl");
        var ledger = new SpendLedger(root, new MemoryFileSystem(), new StoppedClock(Now), NullLogger.Instance);

        return new SpendDialog(
            null,
            new SpendTracker(ledger),
            new SpeechSpend(),
            ledger,
            TestSurface.Settings().Current,
            TimeZoneInfo.Utc,
            launchedAt: null,
            accounts: readers,
            now: () => now);
    }

    private static string Words(Control control) =>
        string.Join("\n", control.GetVisualDescendants().OfType<TextBlock>().Select(Said));

    private static string Said(TextBlock block) =>
        block.Text ?? string.Concat(block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? []);

    private static async Task<Window> Open(SpendDialog dialog, Func<bool> ready)
    {
        var window = dialog.Show();

        for (var attempt = 0; attempt < 300 && !(ready()); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static Task<Window> Open(SpendDialog dialog, string until) =>
        Open(dialog, () => Words(dialog).Contains(until, StringComparison.OrdinalIgnoreCase));

    [AvaloniaFact]
    public void WithNeitherProviderInUseTheSectionIsLeftOut()
    {
        var dialog = Dialog();
        var window = dialog.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("YOUR ACCOUNTS", Words(dialog).ToUpperInvariant(), StringComparison.Ordinal);
        Assert.Contains("Everything below is D47's estimate from published rates", Words(dialog), StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheEstimatesLineSitsDirectlyUnderTheSection()
    {
        var dialog = Dialog(Reader(AccountProvider.Deepgram, new AccountReading.NoKey()));
        var window = await Open(dialog, "No Deepgram key");

        var blocks = dialog.GetVisualDescendants().OfType<TextBlock>().Select(block => Said(block).ToUpperInvariant()).ToList();
        var accounts = blocks.FindIndex(text => text == "YOUR ACCOUNTS");
        var estimates = blocks.FindIndex(text => text.StartsWith("EVERYTHING BELOW IS D47'S ESTIMATE", StringComparison.Ordinal));
        var now = blocks.FindIndex(text => text == "NOW");

        Assert.True(accounts >= 0 && accounts < estimates && estimates < now, $"accounts {accounts}, estimates {estimates}, now {now}");

        Assert.Contains(
            "Everything below is D47's estimate from published rates, not what your account is billed. A subscription with bundled credits can make the real cost anything from higher to nothing at all.",
            Words(dialog),
            StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ABlockShowsTheWaitMarkAndItsSentenceUntilItsResultArrives()
    {
        var source = new TaskCompletionSource<AccountReading>();
        var reader = Unfinished(AccountProvider.ElevenLabs, source);
        var dialog = Dialog(reader);

        var window = await Open(dialog, () => reader.Reads > 0);

        Assert.Contains("Reading your ElevenLabs account…", Words(dialog), StringComparison.Ordinal);
        Assert.Single(dialog.GetVisualDescendants().OfType<WaitingState>());

        source.SetResult(Figures(27_600));

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheReadStartsOffTheUiThreadAndTheAsideGivesItsLocalTime()
    {
        var reader = Reader(AccountProvider.Deepgram, new AccountReading.NoKey());
        var dialog = Dialog(reader);

        var window = await Open(dialog, "No Deepgram key");

        Assert.Equal(1, reader.Reads);
        Assert.False(reader.ReadOnTheUiThread);
        Assert.Contains("As each provider reports it · read 19:42", Words(dialog), StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task TheTwoProvidersAreReadAtTheSameTime()
    {
        var elevenLabs = new TaskCompletionSource<AccountReading>();
        var deepgram = new TaskCompletionSource<AccountReading>();
        var first = Unfinished(AccountProvider.ElevenLabs, elevenLabs);
        var second = Unfinished(AccountProvider.Deepgram, deepgram);
        var dialog = Dialog(first, second);

        var window = await Open(dialog, () => first.Reads > 0 && second.Reads > 0);

        Assert.Equal(1, first.Reads);
        Assert.Equal(1, second.Reads);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ClosingTheDialogCancelsAReadStillRunning()
    {
        var source = new TaskCompletionSource<AccountReading>();
        var reader = Unfinished(AccountProvider.ElevenLabs, source);
        var dialog = Dialog(reader);

        var window = await Open(dialog, () => reader.Reads > 0);
        Assert.False(reader.Token.IsCancellationRequested);

        dialog.Close();

        Assert.True(reader.Token.IsCancellationRequested);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ElevenLabsThatLastsToTheResetSaysSoWithWhatWillBeLeft()
    {
        var dialog = At(Midnight, Reader(AccountProvider.ElevenLabs, Figures(27_600, extension: LimitExtension.UpTo(50_000))));
        var window = await Open(dialog, "At this rate");
        var words = Words(dialog);

        Assert.Contains("72,400", words, StringComparison.Ordinal);
        Assert.Contains("characters left of 100,000", words, StringComparison.Ordinal);
        Assert.Contains("whole account", words, StringComparison.Ordinal);
        Assert.Contains("27,600 this period, by everything on the account", words, StringComparison.Ordinal);
        Assert.Contains("2 Dec 00:00 · in 14 days", words, StringComparison.Ordinal);
        Assert.Contains("Lasts to the reset, with about 48,250 left", words, StringComparison.Ordinal);
        Assert.Contains("Goes up to 50,000 past it, billed as overage", words, StringComparison.Ordinal);
        Assert.NotNull(dialog.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PaceMark"));

        window.SaveFrame("spend-accounts-lasts.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ElevenLabsThatRunsOutSaysWhenAndHowManyDaysBeforeTheReset()
    {
        var dialog = At(Midnight, Reader(AccountProvider.ElevenLabs, Figures(60_770)));
        var window = await Open(dialog, "At this rate");
        var words = Words(dialog);

        Assert.Contains("39,230", words, StringComparison.Ordinal);
        Assert.Contains("Runs out about 28 Nov, 4 days before the reset", words, StringComparison.Ordinal);
        Assert.Contains("Stops. This account can't go past its limit.", words, StringComparison.Ordinal);

        window.SaveFrame("spend-accounts-runs-out.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ElevenLabsThatCanGoPastItsLimitSaysItReachesTheLimitThenOverage()
    {
        var dialog = At(Midnight, Reader(AccountProvider.ElevenLabs, Figures(60_770, extension: LimitExtension.Unlimited)));
        var window = await Open(dialog, "At this rate");
        var words = Words(dialog);

        Assert.Contains("Reaches the limit about 28 Nov, then overage until the reset", words, StringComparison.Ordinal);
        Assert.Contains("Goes past it, with no cap on overage", words, StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ElevenLabsInItsFirstDayOfAPeriodGivesNoEstimate()
    {
        var dialog = Dialog(Reader(AccountProvider.ElevenLabs, Figures(2_140, reset: new DateTimeOffset(2026, 12, 18, 8, 0, 0, TimeSpan.Zero))));
        var window = await Open(dialog, "At this rate");

        Assert.Contains("Estimated after the first day, from 19 Nov 08:00", Words(dialog), StringComparison.Ordinal);

        window.SaveFrame("spend-accounts-first-day.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ElevenLabsWithNothingUsedGivesNoEstimate()
    {
        var dialog = Dialog(Reader(AccountProvider.ElevenLabs, Figures(0)));
        var window = await Open(dialog, "At this rate");

        Assert.Contains("Nothing used this period, so no estimate", Words(dialog), StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ElevenLabsWithNoResetTimeSaysItReportsNone()
    {
        var figures = Figures(60_770) with { NextReset = null };
        var dialog = Dialog(Reader(AccountProvider.ElevenLabs, figures));
        var window = await Open(dialog, "At this rate");
        var words = Words(dialog);

        Assert.Contains("ElevenLabs reports no reset time", words, StringComparison.Ordinal);
        Assert.Contains("No estimate without a reset time", words, StringComparison.Ordinal);
        Assert.Null(dialog.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "PaceMark"));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ElevenLabsAtALimitThatStopsShowsZeroAndTheLimitReachedNotice()
    {
        var dialog = Dialog(Reader(AccountProvider.ElevenLabs, Figures(100_000)));
        var window = await Open(dialog, "Limit reached");
        var words = Words(dialog);

        Assert.Contains("characters left of 100,000", words, StringComparison.Ordinal);
        Assert.Contains("ElevenLabs refuses anything more on this account until the reset at 2 Dec 00:00.", words, StringComparison.Ordinal);
        Assert.DoesNotContain("AT THIS RATE", words.ToUpperInvariant(), StringComparison.Ordinal);

        window.SaveFrame("spend-accounts-at-limit.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ElevenLabsPastItsLimitShowsTheCharactersPastItAndTheOverage()
    {
        var figures = Figures(112_400, extension: LimitExtension.Unlimited, overage: 3.72m);
        var dialog = Dialog(Reader(AccountProvider.ElevenLabs, figures));
        var window = await Open(dialog, "Overage");
        var words = Words(dialog);

        Assert.Contains("12,400", words, StringComparison.Ordinal);
        Assert.Contains("characters past the limit of 100,000", words, StringComparison.Ordinal);
        Assert.Contains("$3.72 this period, billed on top of the plan", words, StringComparison.Ordinal);
        Assert.DoesNotContain("AT THIS RATE", words.ToUpperInvariant(), StringComparison.Ordinal);

        window.SaveFrame("spend-accounts-overage.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task DeepgramWithOneProjectShowsItsCreditAsDollars()
    {
        var credit = new DeepgramCredit([new DeepgramBalance("Cockpit transcription", 187.42m, "usd")]);
        var dialog = Dialog(Reader(AccountProvider.Deepgram, credit));
        var window = await Open(dialog, "credit left");
        var words = Words(dialog);

        Assert.Contains("$187.42", words, StringComparison.Ordinal);
        Assert.Contains("prepaid · no reset", words, StringComparison.Ordinal);
        Assert.DoesNotContain("Cockpit transcription", words, StringComparison.Ordinal);

        window.SaveFrame("spend-accounts-deepgram.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task DeepgramWithSeveralProjectsListsEachWithItsCredit()
    {
        var credit = new DeepgramCredit(
        [
            new DeepgramBalance("Cockpit transcription", 187.42m, "usd"),
            new DeepgramBalance("Second project", 12m, "usd"),
        ]);

        var dialog = Dialog(Reader(AccountProvider.Deepgram, credit));
        var window = await Open(dialog, "Second project");
        var words = Words(dialog);

        Assert.Contains("Cockpit transcription", words, StringComparison.Ordinal);
        Assert.Contains("$187.42", words, StringComparison.Ordinal);
        Assert.Contains("$12.00", words, StringComparison.Ordinal);

        window.SaveFrame("spend-accounts-deepgram-projects.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task DeepgramUnitsOtherThanDollarsAreShownAsGiven()
    {
        var credit = new DeepgramCredit([new DeepgramBalance("Cockpit transcription", 12.5m, "credits")]);
        var dialog = Dialog(Reader(AccountProvider.Deepgram, credit));
        var window = await Open(dialog, "credit left");

        Assert.Contains("12.5 credits", Words(dialog), StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task DeepgramWithAKeyThatReachesNoProjectSaysSo()
    {
        var dialog = Dialog(Reader(AccountProvider.Deepgram, new DeepgramCredit([])));
        var window = await Open(dialog, "This key reaches no Deepgram project.");

        Assert.Contains("This key reaches no Deepgram project.", Words(dialog), StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AMissingKeyIsAnAmberNoticeNamingTheProvider()
    {
        var dialog = Dialog(Reader(AccountProvider.ElevenLabs, new AccountReading.NoKey()));
        var window = await Open(dialog, "No ElevenLabs key");

        var notice = Assert.Single(dialog.GetVisualDescendants().OfType<Notice>());
        Assert.Equal(NoticeLevel.Warning, notice.Level);
        Assert.Contains("no key is stored for it", Words(dialog), StringComparison.Ordinal);
        Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<Button>(), button => button.Name?.StartsWith("Retry", StringComparison.Ordinal) == true);

        window.SaveFrame("spend-accounts-no-key.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task ARefusedKeyIsARedNoticeWithTheProvidersOwnMessageAndNoRetry()
    {
        var refused = new AccountReading.Refused(401, "missing_permissions");
        var dialog = Dialog(Reader(AccountProvider.ElevenLabs, refused));
        var window = await Open(dialog, "refused the read");
        var words = Words(dialog);

        var notice = Assert.Single(dialog.GetVisualDescendants().OfType<Notice>());
        Assert.Equal(NoticeLevel.Error, notice.Level);
        Assert.Contains("Speech is unaffected.", words, StringComparison.Ordinal);
        Assert.Contains("HTTP 401 · MISSING_PERMISSIONS", words.ToUpperInvariant(), StringComparison.Ordinal);
        Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<Button>(), button => button.Name?.StartsWith("Retry", StringComparison.Ordinal) == true);

        window.SaveFrame("spend-accounts-refused.png");
        window.Close();
    }

    [AvaloniaFact]
    public async Task AnUnreachableProviderOffersRetryWhichReadsItAgain()
    {
        var reader = Reader(AccountProvider.Deepgram, new AccountReading.Unreachable("The connection was reset"));
        var dialog = Dialog(reader);
        var window = await Open(dialog, "Deepgram not reached");

        Assert.Contains("D47 couldn't reach api.deepgram.com. Check the connection, then try again.", Words(dialog), StringComparison.Ordinal);

        var retry = Assert.Single(dialog.GetVisualDescendants().OfType<Button>(), button => button.Name == "RetryDeepgram");
        window.SaveFrame("spend-accounts-unreachable.png");

        retry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        for (var attempt = 0; attempt < 300 && reader.Reads < 2; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.Equal(2, reader.Reads);

        window.Close();
    }

    [AvaloniaFact]
    public async Task AFailedProviderDoesNotHideTheOthersFigures()
    {
        var dialog = Dialog(
            Reader(AccountProvider.ElevenLabs, new AccountReading.Refused(401, "no")),
            Reader(AccountProvider.Deepgram, new DeepgramCredit([new DeepgramBalance("Cockpit transcription", 187.42m, "usd")])));

        var window = await Open(dialog, () => Words(dialog).Contains("$187.42", StringComparison.Ordinal) && Words(dialog).Contains("refused", StringComparison.OrdinalIgnoreCase));

        Assert.Single(dialog.GetVisualDescendants().OfType<Notice>());
        Assert.Contains("$187.42", Words(dialog), StringComparison.Ordinal);

        window.SaveFrame("spend-accounts-both.png");
        window.Close();
    }

    [AvaloniaFact]
    public void MoneyIsDollarsToTwoPlacesForUsdAndAsGivenOtherwise()
    {
        Assert.Equal("$187.42", AccountsSection.MoneyText(187.42m, "USD"));
        Assert.Equal("$3.00", AccountsSection.MoneyText(3m, "usd"));
        Assert.Equal("12.5 credits", AccountsSection.MoneyText(12.5m, "credits"));
    }
}
