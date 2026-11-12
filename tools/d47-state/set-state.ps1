<#
.SYNOPSIS
    Sets state in the installed d47's data folder so a callout or feature can be shown again.

.DESCRIPTION
    Each command edits view-state.json in the data folder. d47 reads that file at startup and writes
    it while running, so any running d47 is stopped first and started again afterwards from the same
    executable. If none was running, none is started.

    Commands:
      bgs-tick [minutes]   Hold a BGS tick that many minutes ago (0-59, default 2) and clear the record
                           of having said it. The next d47 start says "The BGS has ticked." (up to 15
                           minutes) or "The BGS ticked N minutes ago." (16-59).
      bgs-tick clear       Remove both fields; d47 fetches the real tick again and records it.

.EXAMPLE
    set-state.ps1 bgs-tick
    set-state.ps1 bgs-tick 30
    set-state.ps1 bgs-tick clear
#>

param(
    [Parameter(Mandatory, Position = 0)]
    [string] $Command,

    [Parameter(Position = 1)]
    [string] $Value,

    [string] $DataRoot = (Join-Path $env:LOCALAPPDATA 'Programs\d47\data')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$viewState = Join-Path $DataRoot 'view-state.json'

# PowerShell binds JsonObject's indexer and Remove to its list interfaces, so the string overloads are
# called through reflection.
$jsonObject = [System.Text.Json.Nodes.JsonObject]
$removeMethod = $jsonObject.GetMethod('Remove', [type[]] @([string]))
$setMethod = $jsonObject.GetMethod('set_Item', [type[]] @([string], [System.Text.Json.Nodes.JsonNode]))

function Remove-Field {
    param($Node, [string] $Name)
    [void] $removeMethod.Invoke($Node, @($Name))
}

function Set-Field {
    param($Node, [string] $Name, [string] $Text)
    [void] $setMethod.Invoke($Node, @($Name, [System.Text.Json.Nodes.JsonValue]::Create($Text)))
}

function Edit-ViewState {
    param([scriptblock] $Change)

    # Assigned in each branch: a JsonObject returned through the pipeline is unrolled into its pairs.
    $node = [System.Text.Json.Nodes.JsonObject]::new()
    if (Test-Path $viewState) {
        $node = [System.Text.Json.Nodes.JsonNode]::Parse((Get-Content $viewState -Raw))
    }

    & $Change $node

    $options = [System.Text.Json.JsonSerializerOptions]::new()
    $options.WriteIndented = $true
    [System.IO.File]::WriteAllText($viewState, $node.ToJsonString($options))
}

function Set-BgsTick {
    param([string] $Value)

    if ($Value -eq 'clear') {
        Edit-ViewState {
            param($node)
            Remove-Field $node 'bgsTickLast'
            Remove-Field $node 'bgsTickSaidFor'
        }
        return 'BGS tick cleared; d47 fetches the real tick and records it without speaking unless it is under an hour old.'
    }

    $minutes = 2
    if ($Value) {
        if (-not [int]::TryParse($Value, [ref] $minutes) -or $minutes -lt 0 -or $minutes -gt 59) {
            throw "bgs-tick takes a number of minutes from 0 to 59, or 'clear'. Got '$Value'."
        }
    }

    $tick = (Get-Date).ToUniversalTime().AddMinutes(-$minutes).ToString('yyyy-MM-ddTHH:mm:ss.fffffffZ')
    Edit-ViewState {
        param($node)
        Set-Field $node 'bgsTickLast' $tick
        Remove-Field $node 'bgsTickSaidFor'
    }

    $line = if ($minutes -le 15) { '"The BGS has ticked."' } else { "`"The BGS ticked about $minutes minutes ago.`" (rounded to five)" }
    return "BGS tick set to $tick ($minutes minutes ago). The next d47 start says $line, if it starts within $(60 - $minutes) minutes."
}

$handlers = @{
    'bgs-tick' = { param($v) Set-BgsTick $v }
}

if (-not $handlers.ContainsKey($Command)) {
    throw "Unknown command '$Command'. Known: $($handlers.Keys -join ', ')."
}

if (-not (Test-Path $DataRoot)) {
    throw "There is no d47 data folder at $DataRoot."
}

$running = @(Get-Process d47 -ErrorAction SilentlyContinue)
$restart = @($running | ForEach-Object { $_.Path } | Where-Object { $_ } | Select-Object -Unique)

if ($running.Count -gt 0) {
    Write-Host "Stopping d47 (pid $($running.Id -join ', '))"
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 20
}

Write-Host (& $handlers[$Command] $Value)

foreach ($exe in $restart) {
    Write-Host "Starting $exe"
    Start-Process -FilePath $exe
}

if ($restart.Count -eq 0) {
    Write-Host 'd47 was not running; start it when ready.'
}
