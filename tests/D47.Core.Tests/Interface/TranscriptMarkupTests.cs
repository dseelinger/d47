using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>
/// Reading the markdown a model writes, so the transcript stops drawing the markers
/// (Phase 19). The reply in the bug report is the first case here.
/// </summary>
public class TranscriptMarkupTests
{
    /// <summary>
    /// The line from the screenshot that started this: a bullet with a bold head. Both marks
    /// are consumed, and what they meant is on the span.
    /// </summary>
    [Fact]
    public void ABulletWithABoldHeadIsDrawnAsOne()
    {
        var spans = TranscriptMarkup.Parse("- **A-rate FSD**; D-rate life support to save mass.");

        Assert.Equal(
            [
                ("• ", MarkupStyle.None),
                ("A-rate FSD", MarkupStyle.Strong),
                ("; D-rate life support to save mass.", MarkupStyle.None),
            ],
            spans.Select(span => (span.Text, span.Style)));
    }

    [Theory]
    [InlineData("**bold**", "bold", MarkupStyle.Strong)]
    [InlineData("__bold__", "bold", MarkupStyle.Strong)]
    [InlineData("*italic*", "italic", MarkupStyle.Emphasis)]
    [InlineData("_italic_", "italic", MarkupStyle.Emphasis)]
    [InlineData("`code`", "code", MarkupStyle.Code)]
    [InlineData("# Heading", "Heading", MarkupStyle.Strong)]
    public void OneMarkedStretchIsOneSpan(string written, string drawn, MarkupStyle style)
    {
        var span = Assert.Single(TranscriptMarkup.Parse(written));

        Assert.Equal(drawn, span.Text);
        Assert.Equal(style, span.Style);
    }

    /// <summary>
    /// Flags rather than one value, so nesting composes rather than the inner mark replacing
    /// the outer one. Bold inside a heading is still a heading.
    /// </summary>
    [Fact]
    public void NestedMarksCompose()
    {
        var spans = TranscriptMarkup.Parse("*an **A-rated** thruster*");

        Assert.Equal(
            [
                ("an ", MarkupStyle.Emphasis),
                ("A-rated", MarkupStyle.Emphasis | MarkupStyle.Strong),
                (" thruster", MarkupStyle.Emphasis),
            ],
            spans.Select(span => (span.Text, span.Style)));
    }

    /// <summary>
    /// The streaming case, and the reason an opener is not allowed to swallow the rest of the
    /// line: a reply arrives a delta at a time, so every bold stretch in it is unterminated for
    /// a moment. It stays literal until its other half lands.
    /// </summary>
    [Fact]
    public void AnUnterminatedMarkerStaysLiteral()
    {
        Assert.Equal("Fit **A-rate thrust", TranscriptMarkup.Plain("Fit **A-rate thrust"));
    }

    /// <summary>
    /// Inside backticks an asterisk is an asterisk, and so is a fence's whole content.
    /// </summary>
    [Fact]
    public void CodeIsNotParsedFurther()
    {
        Assert.Equal("**kept**", Assert.Single(TranscriptMarkup.Parse("`**kept**`")).Text);
    }

    [Fact]
    public void AFenceGoesAndItsContentIsCode()
    {
        var spans = TranscriptMarkup.Parse("Try:\n```json\n{\"ship\": \"sidewinder\"}\n```\nDone.");

        Assert.Equal("Try:\n{\"ship\": \"sidewinder\"}\nDone.", TranscriptMarkup.Plain(
            "Try:\n```json\n{\"ship\": \"sidewinder\"}\n```\nDone."));

        Assert.Contains(
            spans,
            span => span.Style == MarkupStyle.Code && span.Text == "{\"ship\": \"sidewinder\"}");
    }

    /// <summary>
    /// Arithmetic, identifiers and the panel's own marks are not emphasis. The last of these is
    /// the one that would be a real defect: <c>&gt; </c> is how the transcript writes what the
    /// Commander said, and a blockquote rule would have it rewriting its own convention.
    /// </summary>
    [Theory]
    [InlineData("Thrusters draw 2 * 3 * 4 MW")]
    [InlineData("read Loadout_Module_Slot for it")]
    [InlineData("> What's a good build for my Sidewinder?")]
    [InlineData("See https://example.test/a_b_c for it")]
    [InlineData("| Ship | Jump |")]
    [InlineData("---")]
    public void WhatIsNotMarkupIsLeftExactlyAsItArrived(string line)
    {
        Assert.Equal(line, TranscriptMarkup.Plain(line));
    }

    /// <summary>
    /// Links and tables are out of the subset on purpose: a URL a reader cannot see is worse
    /// than one they can, and a table drawn as runs in a wrapping block is not a table.
    /// </summary>
    [Fact]
    public void ALinkKeepsItsAddress()
    {
        Assert.Equal(
            "[the wiki](https://example.test/ships)",
            TranscriptMarkup.Plain("[the wiki](https://example.test/ships)"));
    }

    /// <summary>
    /// The panel's own note. It carries no markup and must survive the pass untouched, because
    /// the view draws it accented and reads the characters back to say so.
    /// </summary>
    [Fact]
    public void TheSwitchMarkerIsUnchanged()
    {
        Assert.Equal("\n[Switched to Sentinel]\n", TranscriptMarkup.Plain("\n[Switched to Sentinel]\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NothingToReadIsNoSpansRatherThanAFailure(string? text)
    {
        Assert.Empty(TranscriptMarkup.Parse(text));
        Assert.Equal(string.Empty, TranscriptMarkup.Plain(text));
    }

    /// <summary>
    /// Every character that is not a marker survives, which is the property that matters most:
    /// this runs over every reply the Commander will ever read, and a pass that can lose a
    /// figure is worse than one that draws asterisks.
    /// </summary>
    [Fact]
    public void NothingButMarkersIsRemoved()
    {
        const string reply =
            "A **small-pad combat trainer** build. Not a tank—an escape artist with teeth.\n"
            + "\n"
            + "- **2× small gimballed pulse lasers** — reliable, no ammunition burden.\n"
            + "- **A-rate thrusters**, and *hull reinforcement* in the other size-2 slot.\n"
            + "\n"
            + "The Sidewinder has only **60 armour and 40 base shields**.";

        var drawn = TranscriptMarkup.Plain(reply);

        Assert.DoesNotContain('*', drawn);
        Assert.Equal(
            reply.Replace("*", string.Empty).Replace("- ", "• "),
            drawn);
    }
}
