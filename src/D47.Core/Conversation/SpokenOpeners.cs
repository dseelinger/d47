namespace D47.Core.Conversation;

/// <summary>
/// The words a Commander puts in front of the thing they actually want, and which mean nothing on
/// their own — <i>show me the</i>, <i>go to</i>, <i>switch to</i>, <i>set</i>.
/// <para>
/// <b>Why this is shared rather than a list in each route</b>, reported 2026-08-23 as two bugs in
/// one evening. <c>PanelPhrases</c> had carried openers since Phase 25 and the setting-command
/// route had none, so <i>"switch to full panel"</i> missed a phrase that exists — the bare
/// <i>"full panel"</i> has always worked — and fell through to the model, which reached for
/// Elite's own ship panels and offered those instead. Every setting command in d47 had the same
/// hole, not just that one. Two lists would have drifted; one list is one answer to one question.
/// </para>
/// <para>
/// <b>This does not loosen matching, and that distinction is load-bearing.</b> Both routes stay
/// whole-utterance — <see cref="KeywordRouter.MatchSetting"/> is deliberately one notch stricter
/// than the keyword route because it acts rather than answers, and remediation 16 is what a loose
/// match costs. What changes is that a <em>closed</em> set of leading words is removed before the
/// comparison, so the grammar is still closed and still exact. "Can you switch to full panel while
/// I dock" matches nothing here, exactly as it did before.
/// </para>
/// </summary>
public static class SpokenOpeners
{
    /// <summary>
    /// Every opener, each ending in the space that separates it from what follows. Longest first,
    /// so <c>"show me the "</c> is taken before <c>"show "</c> and the remainder is the whole of
    /// what the Commander named.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        "take me to the ",
        "take me to ",
        "show me the ",
        "switch to the ",
        "show me ",
        "switch to ",
        "select the ",
        "go to the ",
        "show the ",
        "open the ",
        "set the ",
        "select ",
        "go to ",
        "show ",
        "open ",
        "set ",
    ];

    /// <summary>
    /// The utterance with one leading opener removed, or unchanged where it opens with none.
    /// <para>
    /// One, not all: stripping repeatedly would turn "show the show" into "show", which is a
    /// different request from the one that was made. The caller has already normalised — lower
    /// case, no punctuation, single spaces — because both routes share
    /// <see cref="KeywordRouter"/>'s normaliser and a second one here would be the drift this
    /// class exists to prevent.
    /// </para>
    /// <para>
    /// A bare opener with nothing after it is left alone. "show me" is not a request for a
    /// setting called the empty string.
    /// </para>
    /// </summary>
    public static string Strip(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return utterance;
        }

        foreach (var opener in All)
        {
            if (utterance.Length > opener.Length
                && utterance.StartsWith(opener, StringComparison.OrdinalIgnoreCase))
            {
                return utterance[opener.Length..];
            }
        }

        return utterance;
    }
}
