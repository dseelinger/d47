using System.Text;
using D47.Core.Updates;
using Xunit;

namespace D47.Core.Tests.Updates;

/// <summary>
/// What a local build says it worked, read back out of the binary
/// (<a href="https://github.com/dseelinger/d47/issues/207">#207</a>).
/// <para>
/// Parsing is in Core so a malformed stamp can be handed to it without a window — and because the
/// one thing this must never do is take the app down. It is chrome on a build for testing; a badge
/// that threw because a publish wrote a value it could not read would be worse than no badge.
/// </para>
/// </summary>
public class WhatALocalBuildWorkedTests
{
    private static string Stamp(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void AStampedListComesBackWhole()
    {
        var worked = LocalBuildNotes.Parse(Stamp(
            """[{"n":205,"s":"open","t":"Make the badge clickable","l":["ready","change-request"]}]"""));

        var issue = Assert.Single(worked);

        Assert.Equal(205, issue.Number);
        Assert.Equal("open", issue.State);
        Assert.Equal("Make the badge clickable", issue.Title);
        Assert.Equal(["ready", "change-request"], issue.Labels);
    }

    /// <summary>
    /// One issue is still a list. Windows PowerShell unwraps a one-element array on the way into
    /// <c>ConvertTo-Json</c>, which is why <c>get-local.ps1</c> passes it by <c>-InputObject</c> —
    /// and a build that worked exactly one issue is the commonest local build there is.
    /// </summary>
    [Fact]
    public void OneIssueIsStillAList()
    {
        Assert.Single(LocalBuildNotes.Parse(Stamp("""[{"n":1,"s":"open","t":"A","l":[]}]""")));
    }

    /// <summary>
    /// <b>A withheld title is the ordinary shape, not a failure.</b> A title is text somebody else
    /// wrote, and only an issue the Commander wrote or vouched for gets one baked — so the reader
    /// has to cope with the number being all there is.
    /// </summary>
    [Fact]
    public void AWithheldTitleIsNullRatherThanEmpty()
    {
        var issue = Assert.Single(LocalBuildNotes.Parse(Stamp("""[{"n":9,"s":"open","t":null,"l":[]}]""")));

        Assert.Null(issue.Title);
        Assert.Equal(9, issue.Number);
    }

    /// <summary>And a title of nothing but spaces is the same thing said differently.</summary>
    [Fact]
    public void AnEmptyTitleIsWithheldToo()
    {
        Assert.Null(Assert.Single(LocalBuildNotes.Parse(Stamp("""[{"n":9,"s":"open","t":"   ","l":[]}]"""))).Title);
    }

    /// <summary>
    /// A stamp from an older <c>get-local</c>, or one written when GitHub could not be reached, is
    /// a real shape rather than a broken one — so nothing downstream has to be null-aware about a
    /// record it did not write.
    /// </summary>
    [Fact]
    public void MissingFieldsFillThemselvesIn()
    {
        var issue = Assert.Single(LocalBuildNotes.Parse(Stamp("""[{"n":42}]""")));

        Assert.Equal(42, issue.Number);
        Assert.Equal("unknown", issue.State);
        Assert.Null(issue.Title);
        Assert.Empty(issue.Labels);
    }

    /// <summary>
    /// A published release carries no attribute at all, which is the whole gate — the feature is
    /// absent from a real build by construction rather than by a run-time check.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoStampIsNoList(string? stamped)
    {
        Assert.Empty(LocalBuildNotes.Parse(stamped));
    }

    /// <summary>
    /// <b>Nothing here throws.</b> Each of these is a way a stamp could arrive wrong — a truncated
    /// command line, an older encoding, a hand-edited property — and every one of them has to come
    /// back as "no list" rather than as a crash on the way to drawing a panel.
    /// </summary>
    [Theory]
    [InlineData("not base64 at all!!")]
    [InlineData("Zm9v")]                                  // valid base64, not JSON
    [InlineData("W3sibiI6MX0=")]                          // JSON, truncated array
    [InlineData("eyJuIjoxfQ==")]                          // JSON, an object where a list was expected
    [InlineData("//79")]                                  // bytes that are not valid UTF-8
    public void AStampItCannotReadIsNoListRatherThanAThrow(string stamped)
    {
        Assert.Empty(LocalBuildNotes.Parse(stamped));
    }

    /// <summary>
    /// An entry with no number is not an issue. It cannot be linked to and cannot be looked up, so
    /// drawing a chip reading <c>#0</c> would be d47 inventing one.
    /// </summary>
    [Fact]
    public void AnEntryWithNoNumberIsDropped()
    {
        Assert.Single(LocalBuildNotes.Parse(Stamp(
            """[{"n":0,"s":"open","t":"A","l":[]},{"n":7,"s":"open","t":"B","l":[]}]""")));
    }

    /// <summary>
    /// <b>The link is built from the number and never from anything stamped.</b> A list baked at
    /// publish time carries text somebody else wrote; the one thing it must not be able to carry
    /// is a destination, because <c>UseShellExecute</c> resolves whatever it is given.
    /// </summary>
    [Fact]
    public void TheLinkIsBuiltFromTheNumberAlone()
    {
        var issue = Assert.Single(LocalBuildNotes.Parse(Stamp(
            """[{"n":205,"s":"open","t":"https://example.invalid/","l":["https://example.invalid/"]}]""")));

        Assert.Equal("https://github.com/dseelinger/d47/issues/205", issue.Url);
        Assert.Equal("dseelinger/d47 #205", issue.Reference);
    }

    /// <summary>
    /// The sentence that goes beside every list, full or empty. It is a property of how the list
    /// is gathered rather than of what is in it, so it lives with the gathering.
    /// </summary>
    [Fact]
    public void TheCaveatSaysWhatTheListCannotSee()
    {
        Assert.Contains("Fixes #N", LocalBuildNotes.Caveat, StringComparison.Ordinal);
        Assert.Contains("working tree", LocalBuildNotes.Caveat, StringComparison.Ordinal);
    }
}
