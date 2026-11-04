[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SessionPath,
    [Parameter(Mandatory = $true)][string]$StartTurnId,
    [string]$EndTurnId = '',
    [ValidateRange(1, 1440)][int]$TargetMinutes = 40
)

# Read metadata only. Never export message bodies, commands, or tool results.
# Turn runtime is an upper bound on active work, not proof of productive time.
$ErrorActionPreference = 'Stop'
$stream = [IO.FileStream]::new((Resolve-Path -LiteralPath $SessionPath).Path,
    [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
$reader = [IO.StreamReader]::new($stream)
$turns = [Collections.Generic.List[object]]::new()
$seen = [Collections.Generic.HashSet[string]]::new()
$selected = $false
$endFound = [string]::IsNullOrEmpty($EndTurnId)
$current = $null
$invalidLines = 0
try {
    while (($line = $reader.ReadLine()) -ne $null) {
        try { $record = $line | ConvertFrom-Json -ErrorAction Stop }
        catch { $invalidLines++; continue }
        if ($record.type -ne 'event_msg') { continue }
        $event = $record.payload
        if ($event.type -in @('task_started', 'turn_started')) {
            if ($event.turn_id -eq $StartTurnId) { $selected = $true }
            if (!$selected) { continue }
            if ($null -ne $current) { $turns.Add($current) }
            $current = [pscustomobject]@{
                TurnId = [string]$event.turn_id
                StartedUtc = ([DateTimeOffset]$record.timestamp).ToUniversalTime()
                EndedUtc = $null
                RuntimeMs = $null
                Status = 'incomplete'
            }
        }
        elseif ($selected -and $event.type -in @('task_complete', 'turn_complete', 'turn_aborted')) {
            if ($null -eq $current) { continue }
            if ($event.turn_id -and $event.turn_id -ne $current.TurnId) { continue }
            if (!$seen.Add($current.TurnId)) { $current = $null; continue }
            $current.EndedUtc = ([DateTimeOffset]$record.timestamp).ToUniversalTime()
            $current.Status = if ($event.type -eq 'turn_aborted') { 'interrupted' } else { 'completed' }
            if ($null -ne $event.duration_ms -and [double]$event.duration_ms -ge 0) {
                $current.RuntimeMs = [double]$event.duration_ms
            }
            $turns.Add($current)
            $current = $null
            if ($event.turn_id -eq $EndTurnId) { $endFound = $true; break }
        }
    }
}
finally { $reader.Dispose() }
if (!$selected) { throw 'StartTurnId was not found; refusing to guess a start time.' }
if (!$endFound) { throw 'EndTurnId has no completed/interrupted event; refusing to guess an end.' }
if ($null -ne $current) { $turns.Add($current) }
$runtime = 0.0
$gap = 0.0
$lastEnd = $null
foreach ($turn in $turns) {
    if ($null -ne $lastEnd -and $turn.StartedUtc -gt $lastEnd) {
        $gap += ($turn.StartedUtc - $lastEnd).TotalMilliseconds
    }
    if ($null -ne $turn.RuntimeMs) { $runtime += $turn.RuntimeMs }
    $lastEnd = $turn.EndedUtc
}
[pscustomobject]@{
    SchemaVersion = 1
    TargetMinutes = $TargetMinutes
    CompletedOrInterruptedRuntimeSeconds = [Math]::Round($runtime / 1000, 3)
    ExcludedBetweenTurnSeconds = [Math]::Round($gap / 1000, 3)
    MinimumUnfulfilledSeconds = [Math]::Round([Math]::Max(0.0, [double]($TargetMinutes * 60 - $runtime / 1000)), 3)
    ActualActiveWorkSeconds = $null
    ActiveWorkTargetProven = $false
    UnknownDurationTurns = @($turns | Where-Object { $null -eq $_.RuntimeMs }).Count
    InvalidOrPartialLines = $invalidLines
    Scope = 'Recorded turn runtime only; includes in-turn tool/server waiting. Gaps excluded. Active work and quota consumption cannot be inferred.'
    Turns = @($turns)
} | ConvertTo-Json -Depth 5
