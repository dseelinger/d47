namespace D47.Core.Updates;

/// <summary>
/// A released version parsed from a git tag such as "v0.1.0" (release.yml's whole tagging
/// scheme). Comparison is plain major.minor.patch - the project has never tagged a
/// pre-release, so there is no suffix grammar to get wrong.
/// </summary>
public readonly record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
{
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
