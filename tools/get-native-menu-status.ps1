[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$CheckId,
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$ArtifactsDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
if ([IO.Path]::GetPathRoot($project) -ine 'C:\') { throw 'Run Greed checks from a project on the C drive.' }
$artifactBase = if ($ArtifactsDirectory) {
    $directory = Get-Item -LiteralPath $ArtifactsDirectory
    if ($directory -isnot [IO.DirectoryInfo]) { throw 'ArtifactsDirectory must be an existing filesystem directory.' }
    $directory.FullName
} else { $project }
if ([IO.Path]::GetPathRoot($artifactBase) -ine 'C:\') { throw 'Keep Greed check artifacts on the C drive.' }
$root = Join-Path $artifactBase ('Logs/PlayerPersistenceChecks/' + $CheckId)
$preparedPath = Join-Path $root 'prepared.json'
if (!(Test-Path -LiteralPath $preparedPath -PathType Leaf)) { throw 'The requested isolated check has not been prepared.' }
$prepared = Get-Content -LiteralPath $preparedPath -Raw | ConvertFrom-Json
$menus = $prepared.PSObject.Properties.Name -contains 'InteractiveMenusRequested' -and $prepared.InteractiveMenusRequested -eq $true
$boards = $prepared.PSObject.Properties.Name -contains 'InteractiveBoardsRequested' -and $prepared.InteractiveBoardsRequested -eq $true
if ($prepared.Id -cne $CheckId -or $menus -eq $boards) {
    throw 'Choose an identified native menu or board check, not another player or personal product.'
}
$phase = if ($boards) { 'window-boards' } else { 'window-menu' }
$exe = Join-Path $root 'Player/GreedLastPersistenceProbe.exe'
# Process presence is queried now; a ready file or prior conversation is not a live handle.
$owned = @(Get-CimInstance Win32_Process -Filter "Name='GreedLastPersistenceProbe.exe'" |
    Where-Object { $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -ieq [IO.Path]::GetFullPath($exe) })
$native = @($owned | Where-Object { $_.CommandLine -match ('-greed-probe-phase\s+"?' + $phase + '"?(?:\s|$)') })
$readyPath = Join-Path $root ($phase + '-ready.json')
$ready = if (Test-Path -LiteralPath $readyPath -PathType Leaf) { Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json } else { $null }
if ($ready -and ($ready.phase -cne $phase -or $ready.product -cne ('Persistence-' + $CheckId))) {
    throw 'Native menu observation identity does not match this check.'
}
$reportPath = Join-Path $root 'report.json'
$report = if (Test-Path -LiteralPath $reportPath -PathType Leaf) { Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json } else { $null }
if ($report -and $report.Id -cne $CheckId) { throw 'Final report identity does not match this check.' }
$completedPath = Join-Path $root ($phase + '-completed.json')
$completed = $false
if (Test-Path -LiteralPath $completedPath -PathType Leaf) {
    $observation = Get-Content -LiteralPath $completedPath -Raw | ConvertFrom-Json
    if ($observation.phase -cne $phase -or $observation.product -cne ('Persistence-' + $CheckId)) {
        throw 'Completion observation identity does not match this check.'
    }
    $required = if ($boards) { @('nativeRecordBoardObserved','nativeBoardsCompleted') }
        else { @('nativeSlotChoiceObserved','nativeMenuComparisonObserved','nativeMenuBodyDragObserved',
            'nativeMenuHandleBeforeFocus','nativeMenuFocusLossObserved','nativeMenuHandleAfterFocus','nativeMenuCandidatePreserved') }
    foreach ($field in $required) { if ($observation.$field -ne $true) { throw ('Incomplete gesture assertion: ' + $field) } }
    if ($boards -and $observation.nativeProfilePagesMask -ne 15) { throw 'Incomplete profile page coverage.' }
    $completed = $true
}
$endedPath = Join-Path $root ($phase + '.json')
$phaseEnded = Test-Path -LiteralPath $endedPath -PathType Leaf
$ended = $null
if ($phaseEnded) {
    $ended = Get-Content -LiteralPath $endedPath -Raw | ConvertFrom-Json
    if ($ended.phase -cne $phase -or $ended.product -cne ('Persistence-' + $CheckId) -or !$ended.completedUtc) {
        throw 'Ended phase identity or terminal timestamp is invalid.'
    }
}
$status = if ($report) { if ($report.Succeeded) { 'Passed' } else { 'Failed' } }
    elseif ($completed -and $native.Count) { 'AwaitingNormalClose' }
    elseif ($phaseEnded) { 'PostWindowChecks' }
    elseif ($ready -and $native.Count) { if ($boards) { 'AwaitingBoardInput' } else { 'AwaitingGestures' } }
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
$chordStatus = $null; $chords = @()
if ($boards) {
    if ($report -and $report.PSObject.Properties.Name -contains 'NativeRecordChordStatus') {
        $chordStatus = $report.NativeRecordChordStatus; $chords = @($report.NativeRecordChords)
    } else {
        $latest = $ended
        if (!$latest) {
            $entry = Get-ChildItem -LiteralPath $root -File -Filter 'window-boards-chords-*.json' |
                Where-Object Name -cmatch '^window-boards-chords-[1-4]\.json$' |
                Sort-Object Name -Descending | Select-Object -First 1
            if ($entry) { $latest = Get-Content -LiteralPath $entry.FullName -Raw | ConvertFrom-Json }
            else { $latest = $ready }
        }
        if ($latest -and ($latest.phase -cne $phase -or $latest.product -cne ('Persistence-' + $CheckId))) {
            throw 'Record chord observation identity does not match this check.'
        }
        if ($latest -and $latest.PSObject.Properties.Name -contains 'nativeRecordChordStatus') {
            $chordStatus = $latest.nativeRecordChordStatus; $chords = @($latest.nativeRecordChords)
        }
    }
    if ($chordStatus -and $chordStatus -cnotin @('Unobserved','Verified','ObservedWrongTransition')) {
        throw 'Record chord observation state is invalid.'
    }
}
[pscustomobject]@{
    CheckId=$CheckId; Status=$status; NativePhase=$phase; ObservedUtc=[DateTime]::UtcNow.ToString('O')
    NativeProcessIds=@($native | ForEach-Object { $_.ProcessId }); OwnedProcessCount=$owned.Count
    ReadyObservationPresent=[bool]$ready; ConfiguredWaitSeconds=$waitSeconds; ReadyUtc=$readyUtc
    BodyEventKinds=@($events | Where-Object target -eq 'body' | Select-Object -ExpandProperty kind -Unique)
    HandleEventKinds=@($events | Where-Object target -eq 'handle' | Select-Object -ExpandProperty kind -Unique)
    IncompleteLastEventLine=$partial; AllGestureAssertionsComplete=($menus -and $completed)
    AllBoardAssertionsComplete=($boards -and $completed); RecordChordStatus=$chordStatus; RecordChords=$chords
    PersistenceReportPresent=[bool]$report
    Scope='Read-only process and isolated evidence snapshot. Event kinds alone do not prove accepted travel, persistence, audible output or physical timing.'
} | ConvertTo-Json -Depth 4
