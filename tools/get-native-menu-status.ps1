[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$CheckId,
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$root = Join-Path $project ('Logs/PlayerPersistenceChecks/' + $CheckId)
$preparedPath = Join-Path $root 'prepared.json'
if (!(Test-Path -LiteralPath $preparedPath -PathType Leaf)) { throw 'The requested isolated check has not been prepared.' }
$prepared = Get-Content -LiteralPath $preparedPath -Raw | ConvertFrom-Json
if ($prepared.Id -cne $CheckId -or !$prepared.InteractiveMenusRequested) {
    throw 'Choose an identified native menu check, not another player or personal product.'
}
$exe = Join-Path $root 'Player/GreedLastPersistenceProbe.exe'
# Process presence is queried now; a ready file or prior conversation is not a live handle.
$owned = @(Get-CimInstance Win32_Process -Filter "Name='GreedLastPersistenceProbe.exe'" |
    Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -ieq [IO.Path]::GetFullPath($exe) })
$native = @($owned | Where-Object { $_.CommandLine -match '-greed-probe-phase\s+"?window-menu"?(?:\s|$)' })
$readyPath = Join-Path $root 'window-menu-ready.json'
$ready = if (Test-Path -LiteralPath $readyPath -PathType Leaf) { Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json } else { $null }
if ($ready -and ($ready.phase -cne 'window-menu' -or $ready.product -cne ('Persistence-' + $CheckId))) {
    throw 'Native menu observation identity does not match this check.'
}
$reportPath = Join-Path $root 'report.json'
$report = if (Test-Path -LiteralPath $reportPath -PathType Leaf) { Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json } else { $null }
if ($report -and $report.Id -cne $CheckId) { throw 'Final report identity does not match this check.' }
$completedPath = Join-Path $root 'window-menu-completed.json'
$completed = $false
if (Test-Path -LiteralPath $completedPath -PathType Leaf) {
    $observation = Get-Content -LiteralPath $completedPath -Raw | ConvertFrom-Json
    if ($observation.phase -cne 'window-menu' -or $observation.product -cne ('Persistence-' + $CheckId)) {
        throw 'Completion observation identity does not match this check.'
    }
    $required = @('nativeSlotChoiceObserved','nativeMenuComparisonObserved','nativeMenuBodyDragObserved',
        'nativeMenuHandleBeforeFocus','nativeMenuFocusLossObserved','nativeMenuHandleAfterFocus','nativeMenuCandidatePreserved')
    foreach ($field in $required) { if ($observation.$field -ne $true) { throw ('Incomplete gesture assertion: ' + $field) } }
    $completed = $true
}
$endedPath = Join-Path $root 'window-menu.json'
$phaseEnded = Test-Path -LiteralPath $endedPath -PathType Leaf
if ($phaseEnded) {
    $ended = Get-Content -LiteralPath $endedPath -Raw | ConvertFrom-Json
    if ($ended.phase -cne 'window-menu' -or $ended.product -cne ('Persistence-' + $CheckId) -or !$ended.completedUtc) {
        throw 'Ended phase identity or terminal timestamp is invalid.'
    }
}
$status = if ($report) { if ($report.Succeeded) { 'Passed' } else { 'Failed' } }
    elseif ($completed -and $native.Count) { 'AwaitingNormalClose' }
    elseif ($phaseEnded) { 'PostWindowChecks' }
    elseif ($ready -and $native.Count) { 'AwaitingGestures' }
    elseif ($ready) { 'WindowNotRunning' }
    elseif ($native.Count) { 'NativeStarting' }
    else { 'NoNativePlayer' }
$events = @()
$partial = $false
$eventPath = Join-Path $root 'native-menu-gestures.jsonl'
if (Test-Path -LiteralPath $eventPath -PathType Leaf) {
    $lines = @(Get-Content -LiteralPath $eventPath)
    for ($i=0; $i -lt $lines.Count; $i++) {
        try { $events += $lines[$i] | ConvertFrom-Json }
        catch { if ($i -ne $lines.Count-1) { throw }; $partial=$true }
    }
}
$waitSeconds = $null; $readyUtc = $null
if ($ready -and $ready.PSObject.Properties.Name -contains 'nativeMenuWaitSeconds') {
    $waitSeconds = $ready.nativeMenuWaitSeconds; $readyUtc = $ready.nativeMenuReadyUtc
}
[pscustomobject]@{
    CheckId=$CheckId; Status=$status; ObservedUtc=[DateTime]::UtcNow.ToString('O')
    NativeProcessIds=@($native | ForEach-Object { $_.ProcessId }); OwnedProcessCount=$owned.Count
    ReadyObservationPresent=[bool]$ready; ConfiguredWaitSeconds=$waitSeconds; ReadyUtc=$readyUtc
    BodyEventKinds=@($events | Where-Object target -eq 'body' | Select-Object -ExpandProperty kind -Unique)
    HandleEventKinds=@($events | Where-Object target -eq 'handle' | Select-Object -ExpandProperty kind -Unique)
    IncompleteLastEventLine=$partial; AllGestureAssertionsComplete=$completed
    PersistenceReportPresent=[bool]$report
    Scope='Read-only process and isolated evidence snapshot. Event kinds alone do not prove accepted travel, persistence, audible output or physical timing.'
} | ConvertTo-Json -Depth 4
