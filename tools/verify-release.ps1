<#
.SYNOPSIS
    Rebuilds a release tag and compares the result with the published d47.zip.

.DESCRIPTION
    Exit codes: 0 every entry matches, 1 an entry differs, 2 the comparison could not run
    (B: in use, download or hash failure, build failure), 3 the release was built before releases
    were built from B:\ and cannot be compared.

    -ZipPath compares against a local zip instead of downloading d47.zip and d47.zip.sha256.

.EXAMPLE
    tools\verify-release.ps1 -Version 1.64.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$ZipPath
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Version must be X.Y.Z with no leading v and no label. Got '$Version'." -ForegroundColor Red
    exit 2
}

if (Test-Path -LiteralPath 'B:\') {
    Write-Host 'The drive letter B: is already in use. Remove the mapping or the drive and run this again.' -ForegroundColor Red
    exit 2
}

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class D47Bundle
{
    static readonly byte[] Signature = {
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38, 0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18, 0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae };

    public static bool ContainsAscii(string path, string text)
    {
        return FindAll(path, Encoding.ASCII.GetBytes(text), true).Count > 0;
    }

    static List<long> FindAll(string path, byte[] pattern, bool firstOnly)
    {
        var found = new List<long>();
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
        {
            var buffer = new byte[(1 << 22) + pattern.Length];
            long basePosition = 0;
            int carry = 0;
            while (true)
            {
                int read = file.Read(buffer, carry, buffer.Length - carry);
                if (read <= 0) break;
                int total = carry + read;
                for (int i = 0; i <= total - pattern.Length; i++)
                {
                    if (buffer[i] != pattern[0]) continue;
                    int j = 1;
                    while (j < pattern.Length && buffer[i + j] == pattern[j]) j++;
                    if (j != pattern.Length) continue;
                    found.Add(basePosition + i);
                    if (firstOnly) return found;
                }
                int keep = Math.Min(pattern.Length - 1, total);
                Buffer.BlockCopy(buffer, total - keep, buffer, 0, keep);
                basePosition += total - keep;
                carry = keep;
            }
        }
        return found;
    }

    // Path -> SHA-256 of each entry in a .NET single-file bundle (version 6 layout).
    public static SortedDictionary<string, string> Hashes(string path)
    {
        var length = new FileInfo(path).Length;
        foreach (var at in FindAll(path, Signature, false))
        {
            if (at < 8) continue;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file, Encoding.UTF8))
            {
                file.Position = at - 8;
                long header = reader.ReadInt64();
                if (header <= 0 || header >= length) continue;

                file.Position = header;
                uint major = reader.ReadUInt32();
                reader.ReadUInt32();
                int count = reader.ReadInt32();
                reader.ReadString();
                if (major >= 2)
                {
                    reader.ReadInt64(); reader.ReadInt64(); reader.ReadInt64(); reader.ReadInt64();
                    reader.ReadUInt64();
                }

                var entries = new List<object[]>();
                for (int i = 0; i < count; i++)
                {
                    long offset = reader.ReadInt64();
                    long size = reader.ReadInt64();
                    long compressed = major >= 6 ? reader.ReadInt64() : 0;
                    reader.ReadByte();
                    string name = reader.ReadString();
                    entries.Add(new object[] { offset, compressed > 0 ? compressed : size, name });
                }

                var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in entries)
                {
                    file.Position = (long)entry[0];
                    long remaining = (long)entry[1];
                    using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                    {
                        var chunk = new byte[81920];
                        while (remaining > 0)
                        {
                            int read = file.Read(chunk, 0, (int)Math.Min(chunk.Length, remaining));
                            if (read <= 0) throw new EndOfStreamException("Bundle entry runs past the end of " + path);
                            sha.AppendData(chunk, 0, read);
                            remaining -= read;
                        }
                        result[(string)entry[2]] = BitConverter.ToString(sha.GetHashAndReset()).Replace("-", "");
                    }
                }
                return result;
            }
        }
        throw new InvalidDataException("No single-file bundle header found in " + path);
    }
}
'@

$tag = "v$Version"
$work = Join-Path $env:TEMP "d47-verify-$Version"
$mapped = $false
$code = 2

try {
  do {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
    New-Item -ItemType Directory -Path $work | Out-Null

    if ($ZipPath) {
        $zip = (Resolve-Path -LiteralPath $ZipPath).Path
    }
    else {
        $base = "https://github.com/dseelinger/d47/releases/download/$tag/"
        $zip = Join-Path $work 'd47.zip'
        $sumFile = Join-Path $work 'd47.zip.sha256'
        Invoke-WebRequest -Uri ($base + 'd47.zip') -OutFile $zip
        Invoke-WebRequest -Uri ($base + 'd47.zip.sha256') -OutFile $sumFile

        # The first space-delimited field of the first line, 64 hex characters: UpdateInstaller.ParseChecksum.
        $first = (Get-Content -LiteralPath $sumFile -TotalCount 1)
        $expected = if ($first) { ($first.Trim() -split '\s+')[0] } else { $null }
        if ($expected -notmatch '^[0-9a-fA-F]{64}$') { throw 'd47.zip.sha256 does not hold a SHA-256.' }
        $actual = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
        if ($actual -ne $expected.ToUpperInvariant()) { throw "d47.zip does not match d47.zip.sha256 (expected $expected, got $actual)." }
    }

    $unpacked = Join-Path $work 'published'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $unpacked)
    $publishedExe = Join-Path $unpacked 'd47.exe'
    if (-not (Test-Path -LiteralPath $publishedExe)) { throw 'd47.zip holds no d47.exe.' }

    if (-not [D47Bundle]::ContainsAscii($publishedExe, 'B:\src\D47.Core\obj\Release\')) {
        Write-Host "$tag was built before releases were built from B:\ and cannot be compared"
        $code = 3
        break
    }

    $clone = Join-Path $work 'src'
    git -c core.autocrlf=true -c core.longpaths=true clone --depth 1 --branch $tag https://github.com/dseelinger/d47.git $clone
    if ($LASTEXITCODE -ne 0) { throw "git clone of $tag failed with $LASTEXITCODE" }

    subst B: $clone
    if ($LASTEXITCODE -ne 0) { throw "subst failed with $LASTEXITCODE" }
    $mapped = $true

    Push-Location 'B:\'
    try {
        if (-not (Test-Path 'B:\d47.slnx')) { throw 'B:\d47.slnx is missing; the subst mapping did not carry over.' }
        dotnet build d47.slnx -c Release -p:Version=$Version --disable-build-servers
        if ($LASTEXITCODE -ne 0) { throw "Build failed with $LASTEXITCODE" }
        dotnet publish src/D47.App/D47.App.csproj -c Release -p:Version=$Version --no-restore --disable-build-servers
        if ($LASTEXITCODE -ne 0) { throw "Publish failed with $LASTEXITCODE" }
    }
    finally {
        Pop-Location
    }

    $publish = 'B:\src\D47.App\bin\Release\publish'
    $differing = [System.Collections.Generic.List[string]]::new()
    $total = 0
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try {
        foreach ($entry in $archive.Entries) {
            if (-not $entry.Name) { continue }
            $total++
            $relative = $entry.FullName.Replace('/', '\')
            $rebuilt = Join-Path $publish $relative
            if (-not (Test-Path -LiteralPath $rebuilt -PathType Leaf)) { $differing.Add("$relative (not in the rebuild)"); continue }
            $stream = $entry.Open()
            try {
                $sha = [System.Security.Cryptography.SHA256]::Create()
                $hash = [Convert]::ToHexString($sha.ComputeHash($stream))
            }
            finally { $stream.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath $rebuilt -Algorithm SHA256).Hash) { $differing.Add($relative) }
        }
    }
    finally {
        $archive.Dispose()
    }

    if ($differing.Count -eq 0) {
        Write-Host "${tag}: $total of $total entries in d47.zip match a rebuild of the tag"
        $code = 0
        break
    }

    Write-Host "${tag}: $($differing.Count) of $total entries in d47.zip differ from a rebuild of the tag:" -ForegroundColor Red
    foreach ($name in $differing) { Write-Host "  $name" }

    if ($differing -contains 'd47.exe') {
        $theirs = [D47Bundle]::Hashes($publishedExe)
        $ours = [D47Bundle]::Hashes((Join-Path $publish 'd47.exe'))
        Write-Host 'Entries inside d47.exe that differ:'
        foreach ($name in (@($theirs.Keys) + @($ours.Keys) | Sort-Object -Unique)) {
            if (-not $theirs.ContainsKey($name)) { Write-Host "  $name (only in the rebuild)" }
            elseif (-not $ours.ContainsKey($name)) { Write-Host "  $name (only in the published exe)" }
            elseif ($theirs[$name] -ne $ours[$name]) { Write-Host "  $name" }
        }
    }
    $code = 1
  } while ($false)
}
catch {
    Write-Host "The comparison did not run: $_" -ForegroundColor Red
    $code = 2
}
finally {
    if ($mapped) { subst B: /d | Out-Null }
}

if ($code -eq 0) { Remove-Item -LiteralPath $work -Recurse -Force }
elseif ($code -eq 1) { Write-Host "Kept $work" }
exit $code
