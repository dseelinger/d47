namespace D47.Core.Configuration;

/// <summary>
/// What happened when a stored key was tried for real (Phase 16, "Ask for the keys on the first run
/// that needs them").
/// </summary>
public enum SecretVerdict
{
    /// <summary>Nothing has been tried yet.</summary>
    Untested,

    /// <summary>The real call succeeded.</summary>
    Works,

    /// <summary>The service answered, and said no.</summary>
    Rejected,

    /// <summary>The call could not be made or could not be completed — offline, blocked, timed out.</summary>
    Unreachable,
}

/// <summary>The verdict plus the sentence to show for it.</summary>
/// <param name="Detail">One line, in the Commander's terms.</param>
public readonly record struct SecretCheck(SecretVerdict Verdict, string Detail)
{
    public static readonly SecretCheck Untested = new(SecretVerdict.Untested, "Not checked yet.");

    public static SecretCheck Works(string detail) => new(SecretVerdict.Works, detail);

    public static SecretCheck Rejected(string detail) => new(SecretVerdict.Rejected, detail);

    public static SecretCheck Unreachable(string detail) => new(SecretVerdict.Unreachable, detail);

    /// <summary>Whether this verdict should stop a guided run from moving on.</summary>
    public bool Blocks => Verdict == SecretVerdict.Rejected;
}
