<#
.SYNOPSIS
    Uploads files to VirusTotal and reports each file's verdicts. Reports only; never fails.

.DESCRIPTION
    Reads the API key from VIRUSTOTAL_API_KEY. With the key unset it prints one warning and exits 0.
    Per file it prints the SHA-256, the report URL, the malicious and suspicious counts and every
    engine that flagged it. An analysis still running after 20 minutes is reported as not finished.
    Lines are also appended to $env:GITHUB_STEP_SUMMARY when it is set. The exit code is always 0.

.EXAMPLE
    tools\virustotal-report.ps1 d47.zip installer\d47-setup-1.2.3.exe
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, ValueFromRemainingArguments)]
    [string[]]$Path = @(),

    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'

$api = 'https://www.virustotal.com/api/v3'
$smallLimit = 32MB
# The public API allows 4 requests a minute; one request per 16 seconds stays under it.
$minGap = [TimeSpan]::FromSeconds(16)
$script:lastRequest = [DateTime]::MinValue

function Write-Report([string]$Line) {
    Write-Host $Line
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $Line -Encoding utf8 }
}

function Invoke-Vt([string]$Method, [string]$Uri, $Form) {
    $wait = $minGap - ([DateTime]::UtcNow - $script:lastRequest)
    if ($wait -gt [TimeSpan]::Zero) { Start-Sleep -Milliseconds ([int]$wait.TotalMilliseconds) }
    $script:lastRequest = [DateTime]::UtcNow
    $params = @{ Method = $Method; Uri = $Uri; Headers = @{ 'x-apikey' = $env:VIRUSTOTAL_API_KEY } }
    if ($Form) { $params.Form = $Form }
    try {
        Invoke-RestMethod @params
    }
    catch {
        $message = $_.Exception.Message
        try { $message = ($_.ErrorDetails.Message | ConvertFrom-Json).error.message } catch { }
        throw "VirusTotal $Method $($Uri -replace '\?.*$', '') failed: $message"
    }
}

if (-not $env:VIRUSTOTAL_API_KEY) {
    Write-Warning 'VIRUSTOTAL_API_KEY is not set; nothing was sent to VirusTotal.'
    exit 0
}

if ($Path.Count -eq 0) {
    Write-Warning 'No files were given; nothing was sent to VirusTotal.'
    exit 0
}

$deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
$submitted = @()

foreach ($item in $Path) {
    try {
        if (-not (Test-Path -LiteralPath $item -PathType Leaf)) { throw "$item does not exist." }
        $file = Get-Item -LiteralPath $item
        $sha = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLower()
        $uri = "$api/files"
        if ($file.Length -gt $smallLimit) { $uri = (Invoke-Vt 'GET' "$api/files/upload_url" $null).data }
        $upload = Invoke-Vt 'POST' $uri @{ file = $file }
        $submitted += [pscustomobject]@{ Name = $file.Name; Sha = $sha; Analysis = $upload.data.id }
    }
    catch {
        Write-Report "VirusTotal: $item was not scanned. $($_.Exception.Message)"
    }
}

foreach ($entry in $submitted) {
    $report = "https://www.virustotal.com/gui/file/$($entry.Sha)"
    try {
        $analysis = $null
        while ($true) {
            $analysis = (Invoke-Vt 'GET' "$api/analyses/$($entry.Analysis)" $null).data.attributes
            if ($analysis.status -eq 'completed' -or [DateTime]::UtcNow -ge $deadline) { break }
        }
        Write-Report "VirusTotal: $($entry.Name)"
        Write-Report "  SHA-256: $($entry.Sha)"
        Write-Report "  Report: $report"
        if ($analysis.status -ne 'completed') {
            Write-Report "  Analysis not finished after $TimeoutMinutes minutes; see the report URL."
            continue
        }
        Write-Report "  Malicious: $($analysis.stats.malicious)  Suspicious: $($analysis.stats.suspicious)"
        foreach ($engine in $analysis.results.PSObject.Properties) {
            if ($engine.Value.category -in 'malicious', 'suspicious') {
                Write-Report "  $($engine.Name): $($engine.Value.result) ($($engine.Value.category))"
            }
        }
    }
    catch {
        Write-Report "VirusTotal: no result for $($entry.Name). Report: $report. $($_.Exception.Message)"
    }
}

exit 0
