namespace D47.Core.Configuration;

/// <summary>
/// What happened when a stored key was tried for real (Phase 16, "Ask for the keys on the
/// first run that needs them").
/// <para>
/// The item asks for a key that is <b>verified, not merely stored</b>, and the reason is that the
/// three failures a Commander actually hits — a wrong key, a revoked key, and a key pasted with a
/// trailing newline — are indistinguishable from a working one until the first turn fails. By then
/// they are somewhere else, and what they see is d47 not answering rather than a key not working.
/// </para>
/// </summary>
public enum SecretVerdict
{
    /// <summary>Nothing has been tried yet. The state a row sits in until somebody presses.</summary>
    Untested,

    /// <summary>The real call succeeded.</summary>
    Works,

    /// <summary>
    /// The service answered, and said no. A different key is needed — retrying this one is
    /// pointless, which is what separates this from <see cref="Unreachable"/>.
    /// </summary>
    Rejected,

    /// <summary>
    /// The call could not be made or could not be completed — offline, blocked, timed out. This
    /// says nothing about the key, and saying it does is worse than saying nothing: a Commander
    /// told their good key is bad will go and issue another one.
    /// </summary>
    Unreachable,
}

/// <summary>
/// The verdict plus the sentence to show for it. Both, because a verdict with no words is a
/// coloured pill nobody can act on, and words with no verdict cannot be styled or tested.
/// </summary>
/// <param name="Detail">
/// One line, in the Commander's terms. Never the raw exception, and <b>never the key</b> — this
/// string reaches a panel and a log, and the whole point of the store is that the value does not.
/// </param>
public readonly record struct SecretCheck(SecretVerdict Verdict, string Detail)
{
    public static readonly SecretCheck Untested = new(SecretVerdict.Untested, "Not checked yet.");

    public static SecretCheck Works(string detail) => new(SecretVerdict.Works, detail);

    public static SecretCheck Rejected(string detail) => new(SecretVerdict.Rejected, detail);

    public static SecretCheck Unreachable(string detail) => new(SecretVerdict.Unreachable, detail);

    /// <summary>
    /// Whether this verdict should stop a guided run from moving on. Only an outright rejection
    /// does: being offline is not a reason to hold somebody on a form, and the item is explicit
    /// that none of this is a wall.
    /// </summary>
    public bool Blocks => Verdict == SecretVerdict.Rejected;
}
