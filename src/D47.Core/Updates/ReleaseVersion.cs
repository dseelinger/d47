namespace D47.Core.Updates;

/// <summary>
/// A released version parsed from a git tag such as "v0.1.0" (release.yml's whole tagging
/// scheme). Comparison is plain major.minor.patch; anything after a '+' or '-' is discarded
/// rather than given comparison meaning, since the project has never tagged a pre-release and
/// the only suffix seen in practice is the SDK's own build-metadata SHA.
/// </summary>
public readonly record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
{
    /// <summary>
    /// Just the release out of a build stamp — <c>0.78.0</c> from
    /// <c>0.78.0+4b18aaecbe2510b0aeae95d3f19583edd18ea205</c>, and the string unchanged when it
    /// does not parse as a version at all.
    /// <para>
    /// <b>Here so that "the version" and "the stamp" cannot be the same value by accident</b>
    /// (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>). About's Version and Build
    /// rows both printed the full stamp for as long as About has been a settings area, because
    /// the composition root handed one string to both and nothing could observe that it had.
    /// Deriving one from the other makes the two rows structurally unable to agree, which is a
    /// stronger guarantee than a caller remembering which of two parameters is which.
    /// </para>
    /// <para>
    /// Falling back to the input rather than to empty: a build with no parseable version — a
    /// local one, or <c>"unknown"</c> — is better described by whatever it does say than by
    /// nothing at all.
    /// </para>
    /// </summary>
    public static string Semantic(string? stamp) =>
        TryParse(stamp, out var version) ? version.ToString() : stamp ?? string.Empty;

    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > 0 && (trimmed[0] == 'v' || trimmed[0] == 'V'))
        {
            trimmed = trimmed[1..];
        }

        // Strip SemVer build metadata and pre-release suffixes. The SDK appends
        // "+<git-sha>" to AssemblyInformationalVersion by default whenever the project sits
        // in a git repository, so even a plain "0.1.0" tag arrives here as "0.1.0+<sha>" -
        // build metadata is noise for comparison purposes, not part of the version.
        var suffixIndex = trimmed.IndexOfAny(['+', '-']);
        if (suffixIndex >= 0)
        {
            trimmed = trimmed[..suffixIndex];
        }

        var parts = trimmed.Split('.');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public bool IsNewerThan(ReleaseVersion other) => CompareTo(other) > 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
