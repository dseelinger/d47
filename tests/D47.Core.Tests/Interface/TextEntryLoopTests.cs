using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// The correction loop behind "say it, or type it" (Phase 25).
/// <para>
/// <b>The keyboard returns by itself on the three failures d47 can detect</b> — nothing heard, a
/// transcription below confidence, and a value that fails to resolve — and that list is exactly
/// three long on purpose. <em>Confident, valid and still wrong</em> is not detectable from here
/// at all, which is what the readback is for.
/// </para>
/// </summary>
public class TextEntryLoopTests
{
    [Fact]
    public void NothingHeardBringsTheKeyboardBack()
    {
        Assert.Equal(
            EntryFallback.NothingHeard,
            TextEntryLoop.Judge(null, null, out _));

        Assert.Equal(
            EntryFallback.NothingHeard,
            TextEntryLoop.Judge(new Heard("   ", 1, Final: true), null, out _));
    }

    [Fact]
    public void AnUnsureTranscriptionBringsTheKeyboardBack()
    {
        Assert.Equal(
            EntryFallback.LowConfidence,
            TextEntryLoop.Judge(new Heard("Shinrarta Dezhra", 0.2, Final: true), null, out _));
    }

    /// <summary>
    /// A value that does not resolve is the third, and it is real rather than theoretical: the
    /// two-resolver check already catches a renamed system and an invented one.
    /// </summary>
    [Fact]
    public void AValueThatDoesNotResolveBringsTheKeyboardBackWithTheReason()
    {
        var fallback = TextEntryLoop.Judge(
            new Heard("Shinrarta Dezhraa", 0.95, Final: true),
            _ => EntryVerdict.No("There is no system by that name."),
            out var verdict);

        Assert.NotNull(fallback);
        Assert.Equal(EntryFallback.DidNotResolve, fallback.Value);
        Assert.Equal("There is no system by that name.", verdict?.Complaint);

        // And the complaint is what the keyboard opens under, so its reappearance reads as an
        // answer rather than as the microphone having failed silently.
        Assert.Equal(
            "There is no system by that name.",
            TextEntryLoop.Explain(fallback.Value, verdict?.Complaint));
    }

    /// <summary>Confident, valid, and therefore taken. Nothing else is detectable from here.</summary>
    [Fact]
    public void AConfidentValidValueIsAccepted()
    {
        Assert.Null(TextEntryLoop.Judge(
            new Heard("Shinrarta Dezhra", 0.95, Final: true),
            _ => EntryVerdict.Ok,
            out var verdict));

        Assert.True(verdict?.Accepted);
    }

    /// <summary>
    /// A partial is neither an answer nor a failure. It is shown and nothing else — which is the
    /// visible listening state doing its job, because a Commander talking at a blank page has no
    /// way to tell a microphone that is off from one that is not hearing them.
    /// </summary>
    [Fact]
    public void APartialIsShownAndNeverCommitted()
    {
        Assert.Null(TextEntryLoop.Judge(
            new Heard("shinrar", 0.4, Final: false),
            _ => throw new InvalidOperationException("a partial must never be validated"),
            out var verdict));

        Assert.Null(verdict);
    }

    /// <summary>
    /// A caller with nothing to check accepts anything confident. Validation is optional, and
    /// asking for it where there is no vocabulary to check against would be inventing a refusal.
    /// </summary>
    [Fact]
    public void WithNothingToValidateAgainstAConfidentValueIsTaken()
    {
        Assert.Null(TextEntryLoop.Judge(new Heard("buy limpets", 0.9, Final: true), null, out _));
    }

    /// <summary>The two failures d47 can describe for itself say so in the Commander's terms.</summary>
    [Fact]
    public void TheExplanationsAreSentencesRatherThanCodes()
    {
        Assert.Contains("did not catch", TextEntryLoop.Explain(EntryFallback.NothingHeard, null), StringComparison.Ordinal);
        Assert.Contains("not sure", TextEntryLoop.Explain(EntryFallback.LowConfidence, null), StringComparison.Ordinal);

        // And the third falls back to a sentence rather than to nothing when a caller refused
        // without saying why.
        Assert.False(string.IsNullOrWhiteSpace(
            TextEntryLoop.Explain(EntryFallback.DidNotResolve, null)));
    }

    /// <summary>
    /// The readback is the only check left for the failure that cannot be detected, so it has to
    /// carry both what was heard and what it is for.
    /// </summary>
    [Fact]
    public void TheReadbackNamesTheFieldAndTheValue()
    {
        var said = TextEntryLoop.ReadBack("System name", "Shinrarta Dezhra");

        Assert.Contains("System name", said, StringComparison.Ordinal);
        Assert.Contains("Shinrarta Dezhra", said, StringComparison.Ordinal);
    }
}
