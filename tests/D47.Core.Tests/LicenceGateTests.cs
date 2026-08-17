using Xunit;

namespace D47.Core.Tests;

/// <summary>
/// Proof that <see cref="PackageLicenceGateTests"/> can fail.
/// <para>
/// The graph is clean, so the gate itself passes on every package in it and would pass just as
/// silently if its classifier answered "permissive" to everything. These are the cases that show
/// it does not — built as throwaway packages on disk and put through the same
/// <see cref="LicenceGate.Resolve"/> the gate calls, rather than through a seam opened for
/// testing.
/// </para>
/// <para>
/// The LGPL-in-a-packed-file case is the one worth having. d47 has met a package that declares a
/// permissive licence at the top of its nuspec and is not permissive underneath, so "the text is
/// read, not just the tag" is the behaviour that matters here.
/// </para>
/// </summary>
public class LicenceGateTests
{
    private const string MitText = """
        MIT License

        Copyright (c) 2026 Nobody

        Permission is hereby granted, free of charge, to any person obtaining a copy of this
        software and associated documentation files (the "Software"), to deal in the Software
        without restriction.
        """;

    private const string LgplText = """
                          GNU LESSER GENERAL PUBLIC LICENSE
                               Version 2.1, February 1999

         This version of the GNU Lesser General Public License incorporates the terms and
         conditions of version 3 of the GNU General Public License.
        """;

    [Theory]
    [InlineData("MIT")]
    [InlineData("Apache-2.0")]
    [InlineData("BSD-3-Clause")]
    [InlineData("MIT OR Apache-2.0")]
    [InlineData("Apache-2.0 WITH LLVM-exception")]
    [InlineData("(MIT OR Apache-2.0)")]
    [InlineData("MIT AND BSD-3-Clause")]
    public void APermissiveExpressionPasses(string expression)
    {
        Assert.Equal(LicenceVerdict.Permissive, Verdict(Package(expression: expression)));
    }

    [Theory]
    [InlineData("GPL-3.0-only")]
    [InlineData("LGPL-2.1-or-later")]
    [InlineData("AGPL-3.0")]
    [InlineData("MPL-2.0")]
    [InlineData("EPL-2.0")]
    [InlineData("MIT AND GPL-2.0-only")]
    public void ACopyleftExpressionIsCaught(string expression)
    {
        Assert.Equal(LicenceVerdict.Copyleft, Verdict(Package(expression: expression)));
    }

    /// <summary>
    /// <c>OR</c> is a choice the consumer makes, so one permissive branch is enough — but only
    /// <c>OR</c>. <c>AND</c> binds both at once and is covered above.
    /// </summary>
    [Fact]
    public void ADualLicenceWithOnePermissiveBranchIsAllowed()
    {
        Assert.Equal(LicenceVerdict.Permissive, Verdict(Package(expression: "GPL-2.0-only OR MIT")));
    }

    [Fact]
    public void AnIdentifierNobodyHasVettedIsRefusedRatherThanAssumed()
    {
        // Not copyleft as far as this gate knows, and that is exactly why it must not pass: an
        // allowlist that waved through what it had not heard of would not be a gate.
        Assert.Equal(LicenceVerdict.Unknown, Verdict(Package(expression: "SomeCompany-Proprietary-1.0")));
    }

    [Fact]
    public void APackedLicenceFileIsReadRatherThanTakenOnTrust()
    {
        Assert.Equal(
            LicenceVerdict.Permissive,
            Verdict(Package(file: "LICENSE.md", text: MitText)));
    }

    /// <summary>
    /// The case this gate exists for. Nothing in the metadata says "LGPL" — the nuspec names a
    /// file, and only the file says it.
    /// </summary>
    [Fact]
    public void ACopyleftLicenceInAPackedFileIsCaught()
    {
        var licence = LicenceGate.Resolve(
            new PackageId("Pretty.Wrapper", "1.0.0"),
            Package(file: "LICENSE", text: LgplText));

        Assert.Equal(LicenceVerdict.Copyleft, licence.Verdict);
        Assert.Contains("LGPL", licence.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ALicenceFileTheGateCannotRecogniseIsRefused()
    {
        Assert.Equal(
            LicenceVerdict.Unknown,
            Verdict(Package(file: "LICENSE", text: "You may use this if you are feeling lucky.")));
    }

    [Fact]
    public void ANuspecPointingAtAFileThatIsNotThereIsRefused()
    {
        Assert.Equal(LicenceVerdict.Unknown, Verdict(Package(file: "LICENSE", text: null)));
    }

    /// <summary>
    /// NuGet deprecated <c>licenseUrl</c> because a link is a claim about a web page rather than
    /// about the package. A gate that followed it would be checking the wrong artifact, and one
    /// that accepted it would be checking nothing.
    /// </summary>
    [Fact]
    public void TheDeprecatedLicenceUrlIsNotEnoughOnItsOwn()
    {
        Assert.Equal(
            LicenceVerdict.Unknown,
            Verdict(Package(licenseUrl: "https://example.invalid/licence")));
    }

    [Fact]
    public void APackageDeclaringNoLicenceAtAllIsRefused()
    {
        Assert.Equal(LicenceVerdict.Unknown, Verdict(Package()));
    }

    [Fact]
    public void APackageThatIsNotOnDiskIsRefusedRatherThanSkipped()
    {
        var licence = LicenceGate.Resolve(new PackageId("Gone", "1.0.0"), folder: null);

        Assert.Equal(LicenceVerdict.Unknown, licence.Verdict);
    }

    private static LicenceVerdict Verdict(string folder) =>
        LicenceGate.Resolve(new PackageId("Fixture.Package", "1.0.0"), folder).Verdict;

    /// <summary>
    /// A throwaway package folder shaped like one in the NuGet cache: a nuspec, and whatever
    /// licence file the nuspec names.
    /// </summary>
    private static string Package(
        string? expression = null,
        string? file = null,
        string? text = null,
        string? licenseUrl = null)
    {
        var folder = Path.Combine(
            Path.GetTempPath(), "d47-tests", "licences", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(folder);

        var declaration = (expression, file) switch
        {
            ({ } spdx, _) => $"""<license type="expression">{spdx}</license>""",
            (_, { } named) => $"""<license type="file">{named}</license>""",
            _ => string.Empty,
        };

        var url = licenseUrl is null ? string.Empty : $"<licenseUrl>{licenseUrl}</licenseUrl>";

        File.WriteAllText(
            Path.Combine(folder, "Fixture.Package.nuspec"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
               <metadata>
                 <id>Fixture.Package</id>
                 <version>1.0.0</version>
                 {declaration}
                 {url}
               </metadata>
             </package>
             """);

        if (file is not null && text is not null)
        {
            File.WriteAllText(Path.Combine(folder, file), text);
        }

        return folder;
    }
}
