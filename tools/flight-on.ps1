<#
.SYNOPSIS
    Starts the installed d47 with the audio flight recorder on, for that run only.

.DESCRIPTION
    The recorder has been reachable since #164 and the road to it was a shell incantation:
    `$env:D47_FLIGHT_RECORDER = '1'` followed by the full path to the executable, which requires
    knowing PowerShell's syntax, knowing where d47 is installed, and knowing that the variable
    only reaches a d47 started from that same shell. Nobody launching from the Start menu ever
    gets there, and the first real attempt to use the recorder failed on exactly that (#180).

    So this is the incantation, written once. It does what the two lines did and says what it did.

    **The gate does not move.** Recording is still per-run and still not a setting: nothing here
    is remembered, and a d47 started the usual way afterwards has no recorder, no review pane, no
    wipe row and writes no file. That is deliberate - a permanent toggle would put "d47 can record
    audio" in front of every installation forever, which is the reading the gating exists to spare
    a Commander who never asked for it.

    **It refuses while d47 is running, and that is the point rather than caution.** d47 claims a
    single-instance mutex, so a second copy launched with the switch does not start recording - it
    surfaces the copy already running, which is not recording. Left to happen, that looks exactly
    like the switch not working.

.PARAMETER Restart
    Stop the d47 that is already running, then start the recording one. Without it a running d47
    is an error, for the reason above.

.PARAMETER InstallRoot
    Where the installed d47 lives. Defaults to the Programs folder the installer uses, and is here
    so a second install can be targeted rather than assumed.

.EXAMPLE
    flight-on
    flight-on -Restart
#>

param(
    [switch] $Restart,

    [string] $InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\d47')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step { param([string] $Text) Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Note { param([string] $Text) Write-Host "    $Text" -ForegroundColor DarkGray }

$exe = Join-Path $InstallRoot 'd47.exe'

if (-not (Test-Path $exe)) {
    Write-Error "There is no installed d47 at $InstallRoot. Install one first: get-ver latest"
}

# Wrapped at the call site: a one-item result comes back from the pipeline unrolled, and `.Count`
# on a bare object is a StrictMode error rather than 1.
$running = @(Get-Process d47 -ErrorAction SilentlyContinue)

if ($running.Count -gt 0) {
    if (-not $Restart) {
        Write-Error ("d47 is already running (pid $($running.Id -join ', ')). Starting a second copy " +
            'would surface that one, which is not recording. Close it, or pass -Restart.')
    }

    Write-Step 'Stopping the d47 that is running'
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 20
}

Write-Step 'Starting d47 with the flight recorder on'

# The switch rather than the variable, because a switch survives the launch: an environment
# variable set here reaches only a child of this shell, which is how the road being replaced came
# to depend on where d47 was started from.
Start-Process -FilePath $exe -ArgumentList '--flight-recorder'

Write-Note "What crossed the audio boundary is kept in $(Join-Path $InstallRoot 'data\flight')."
Write-Note 'Review it, and wipe it, in Settings under Privacy: "Recorded audio".'
Write-Note 'This run only. Start d47 the usual way and it is off, with no row to say it was ever on.'
