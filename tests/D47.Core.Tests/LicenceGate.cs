using System.Xml.Linq;

namespace D47.Core.Tests;

/// <summary>
/// What a NuGet package says its licence is, and whether that is one d47 may ship.
/// <para>
/// <b>This reads package metadata and nothing else.</b> It is given a package folder from the
/// NuGet cache and reads the <c>.nuspec</c> in it. It cannot be pointed at a file in this
/// repository, which is deliberate — see <see cref="PackageLicenceGateTests"/> for why that
/// distinction is the one thing here that must not be got wrong.
/// </para>
/// </summary>
internal static class LicenceGate
{
    /// <summary>
    /// The identifiers d47 may ship. An allowlist rather than a list of things to refuse, because
    /// the failure that matters is the licence nobody thought of — a denylist passes everything it
    /// has not heard of, which is the wrong way round for a gate.
    /// </summary>
    private static readonly HashSet<string> Permissive = new(StringComparer.OrdinalIgnoreCase)
    {
        "MIT", "MIT-0",
        "Apache-2.0",
        "BSD-2-Clause", "BSD-3-Clause", "0BSD", "BSD-Source-Code",
        "ISC",
        "MS-PL",
        "BSL-1.0",
        "Zlib",
        "Unlicense",
        "CC0-1.0",
        "PostgreSQL",
        "Python-2.0",
    };

    /// <summary>
    /// Phrases that mean copyleft whatever the surrounding metadata claims, checked before
    /// anything else and against the licence text itself.
    /// <para>
    /// MPL and EPL are here with the GNU family. They are weak, file-scope copyleft rather than
    /// viral, and a project may well decide either is fine — but that is a decision, and a gate
    /// that made it silently would be deciding it for everybody. It fails, and the message says
    /// what was found so somebody can say yes on the record.
    /// </para>
    /// </summary>
    private static readonly (string Marker, string Name)[] Copyleft =
    [
        ("GNU AFFERO GENERAL PUBLIC LICENSE", "AGPL"),
        ("GNU LESSER GENERAL PUBLIC LICENSE", "LGPL"),
        ("GNU GENERAL PUBLIC LICENSE", "GPL"),
        ("MOZILLA PUBLIC LICENSE", "MPL"),
        ("ECLIPSE PUBLIC LICENSE", "EPL"),
        ("COMMON DEVELOPMENT AND DISTRIBUTION LICENSE", "CDDL"),
        ("EUROPEAN UNION PUBLIC LICENCE", "EUPL"),
        ("SERVER SIDE PUBLIC LICENSE", "SSPL"),
    ];

    /// <summary>
    /// A distinctive sentence from each permissive licence d47 actually meets in a packed file,
    /// so a package that names a file instead of an SPDX expression can still be resolved rather
    /// than merely reported as unreadable.
    /// </summary>
    private static readonly (string Phrase, string Name)[] KnownTexts =
    [
        ("PERMISSION IS HEREBY GRANTED, FREE OF CHARGE", "MIT"),
        ("REDISTRIBUTION AND USE IN SOURCE AND BINARY FORMS", "BSD"),
        ("PERMISSION TO USE, COPY, MODIFY, AND/OR DISTRIBUTE THIS SOFTWARE", "ISC"),
        ("LICENSED UNDER THE APACHE LICENSE", "Apache-2.0"),
        ("APACHE LICENSE\n                           VERSION 2.0", "Apache-2.0"),
        ("BOOST SOFTWARE LICENSE", "BSL-1.0"),
    ];

    /// <summary>
    /// Reads one package's licence claim. <paramref name="folder"/> is the extracted package in
    /// the NuGet cache; a package that is not on disk is <see cref="LicenceVerdict.Unknown"/>
    /// rather than assumed to be anything.
    /// </summary>
    public static Licence Resolve(PackageId package, string? folder)
    {
        if (folder is null)
        {
            return new Licence(
                LicenceVerdict.Unknown,
                "the package is not in the local NuGet cache",
                "no package folder");
        }

        var nuspec = Directory
            .EnumerateFiles(folder, "*.nuspec", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (nuspec is null)
        {
            return new Licence(LicenceVerdict.Unknown, "the package carries no .nuspec", folder);
        }

        var metadata = XDocument.Load(nuspec).Root?
            .Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");

        if (metadata is null)
        {
            return new Licence(LicenceVerdict.Unknown, "the .nuspec carries no metadata", nuspec);
        }

        var licence = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "license");
        var kind = licence?.Attribute("type")?.Value;
        var value = licence?.Value.Trim();

        if (string.Equals(kind, "expression", StringComparison.OrdinalIgnoreCase) && value is { Length: > 0 })
        {
            return FromExpression(value);
        }

        if (string.Equals(kind, "file", StringComparison.OrdinalIgnoreCase) && value is { Length: > 0 })
        {
            return FromFile(Path.Combine(folder, value.Replace('\\', Path.DirectorySeparatorChar)), value);
        }

        var url = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "licenseUrl")?.Value.Trim();

        if (url is { Length: > 0 })
        {
            // The legacy field. NuGet deprecated it precisely because a URL is a promise about a
            // web page rather than a statement about the package, so it is never enough on its own.
            return new Licence(
                LicenceVerdict.Unknown,
                $"declares only the deprecated licenseUrl, {url}",
                Path.GetFileName(nuspec));
        }

        return new Licence(LicenceVerdict.Unknown, "declares no licence at all", Path.GetFileName(nuspec));
    }

    /// <summary>
    /// An SPDX expression. <c>OR</c> is a choice, so one permissive branch is enough;
    /// <c>AND</c> binds every term at once, so all of them must be. A <c>WITH</c> exception only
    /// ever grants more than the licence it qualifies, so the licence is what decides.
    /// </summary>
    private static Licence FromExpression(string expression)
    {
        var terms = expression
            .Replace("(", " ", StringComparison.Ordinal)
            .Replace(")", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var choice = terms.Any(term => string.Equals(term, "OR", StringComparison.OrdinalIgnoreCase));

        var identifiers = new List<string>();
        var skip = false;

        foreach (var term in terms)
        {
            if (skip)
            {
                skip = false;
                continue;
            }

            if (string.Equals(term, "WITH", StringComparison.OrdinalIgnoreCase))
            {
                skip = true;
                continue;
            }

            if (!string.Equals(term, "OR", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(term, "AND", StringComparison.OrdinalIgnoreCase))
            {
                identifiers.Add(term.TrimEnd('+'));
            }
        }

        if (identifiers.Count == 0)
        {
            return new Licence(LicenceVerdict.Unknown, $"unreadable SPDX expression '{expression}'", "nuspec");
        }

        var allowed = choice
            ? identifiers.Any(Permissive.Contains)
            : identifiers.All(Permissive.Contains);

        if (allowed)
        {
            return new Licence(LicenceVerdict.Permissive, expression, "nuspec license expression");
        }

        var offending = identifiers.Where(id => !Permissive.Contains(id)).ToArray();

        var copyleft = offending.Any(id =>
            id.StartsWith("GPL", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("LGPL", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("AGPL", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("MPL", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("EPL", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("CDDL", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("EUPL", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("SSPL", StringComparison.OrdinalIgnoreCase));

        return new Licence(
            copyleft ? LicenceVerdict.Copyleft : LicenceVerdict.Unknown,
            expression,
            "nuspec license expression");
    }

    /// <summary>
    /// A licence packed as a file. Copyleft is looked for first and in the text itself, because a
    /// package that names a file rather than an expression has told the tooling nothing, and the
    /// text is the only thing left that can say what it is.
    /// </summary>
    private static Licence FromFile(string path, string declared)
    {
        if (!File.Exists(path))
        {
            return new Licence(
                LicenceVerdict.Unknown,
                $"the nuspec names a licence file '{declared}' that is not in the package",
                declared);
        }

        var text = File.ReadAllText(path);
        var folded = text.ToUpperInvariant().Replace("\r\n", "\n", StringComparison.Ordinal);

        foreach (var (marker, name) in Copyleft)
        {
            if (folded.Contains(marker, StringComparison.Ordinal))
            {
                return new Licence(LicenceVerdict.Copyleft, $"{name}, from the text of {declared}", declared);
            }
        }

        foreach (var (phrase, name) in KnownTexts)
        {
            if (folded.Contains(phrase, StringComparison.Ordinal))
            {
                return new Licence(LicenceVerdict.Permissive, $"{name}, from the text of {declared}", declared);
            }
        }

        return new Licence(
            LicenceVerdict.Unknown,
            $"packs a licence file '{declared}' whose text matches nothing this gate recognises",
            declared);
    }
}
