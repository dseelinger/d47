namespace D47.Core.Help;

/// <summary>
/// The changelog, inside the binary
/// (<a href="https://github.com/dseelinger/d47/issues/50">#50</a>).
/// <para>
/// <b>The comment this replaces said a self-contained app has no renderer for markdown worth
/// carrying, and that stopped being true.</b> <c>D47.Core.csproj</c> already embeds every
/// documentation page and <c>HelpLibrary</c> already parses them into articles the panel draws, so
/// there has been a markdown pipeline in the binary since the help pass. Embedding one more file
/// is one line.
/// </para>
/// <para>
/// <b>Read as text, not through <see cref="HelpLibrary"/>.</b> That parser expects a banded help
/// article — front matter, an ELI5 band, anchored sections — and this is a release list. Running it
/// through a parser it does not fit would produce either an error or a lie about its shape, and
/// what a Commander wants from a changelog is the words in the order they were written.
/// </para>
/// <para>
/// <b>It ships as it stood when this build was made</b>, which is the point: it reads with no
/// internet at all. It also means it can never show a release newer than the one running — that is
/// what the web link beside it is for, and why that link points at the branch rather than at a tag.
/// </para>
/// </summary>
public static class Changelog
{
    private const string ResourceName = "D47.Core.Changelog";

    private static readonly Lazy<string> Loaded = new(Read);

    /// <summary>The whole file, newest release first, exactly as it shipped.</summary>
    public static string Text => Loaded.Value;

    /// <summary>
    /// Whether there is one at all. False only in a build whose embedding went wrong, which is
    /// worth an absent button rather than an empty window.
    /// </summary>
    public static bool Exists => Text.Length > 0;

    private static string Read()
    {
        var assembly = typeof(Changelog).Assembly;

        using var stream = assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
