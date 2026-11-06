#requires -Version 7.0
<#
  Sends one whitelisted command to a running lab session and waits for its JSON result.

  Protocol (commands are implemented in Source/Lab/LabCommands.cs):
    request line : <id> <command> [key=value ...]
    result line  : one JSON object per command, keyed by the same id
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$LabDir,
    [Parameter(Mandatory)]
    [string]$Command,
    [string]$Id,
    [int]$TimeoutSec = 20
)

$ErrorActionPreference = 'Stop'

$labRoot = (Resolve-Path -LiteralPath $LabDir).Path
$requestPath = Join-Path $labRoot 'requests.txt'
$resultPath = Join-Path $labRoot 'results.jsonl'

if (-not (Test-Path -LiteralPath (Join-Path $labRoot 'lab-status.json'))) {
    Write-Warning "lab-status.json not found in $labRoot; the game may not be running with -wotflab=$labRoot"
}

if (-not $Id) {
    $Id = 'c' + [DateTime]::UtcNow.ToString('HHmmssfff') + (Get-Random -Minimum 100 -Maximum 999)
}

Add-Content -LiteralPath $requestPath -Value "$Id $Command" -Encoding utf8

$needle = '"id":"' + $Id + '"'
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $resultPath) {
        $hit = Get-Content -LiteralPath $resultPath -Tail 300 -ErrorAction SilentlyContinue |
            Where-Object { $_.Contains($needle) } |
            Select-Object -Last 1
        if ($hit) {
            Write-Output $hit
            exit 0
        }
    }

    Start-Sleep -Milliseconds 100
}

Write-Error "Timed out after $TimeoutSec s waiting for id=$Id in $resultPath"
if (Test-Path -LiteralPath $resultPath) {
    Write-Host 'Last results:'
    Get-Content -LiteralPath $resultPath -Tail 5
}

exit 2
