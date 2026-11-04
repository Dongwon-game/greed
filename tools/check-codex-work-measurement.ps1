$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('greed-work-time-' + [Guid]::NewGuid().ToString('N') + '.jsonl')
function Event([string]$stamp, [string]$kind, [string]$id, $duration) {
    @{timestamp=$stamp;type='event_msg';payload=@{type=$kind;turn_id=$id;duration_ms=$duration}} | ConvertTo-Json -Compress
}
try {
    @(
        (Event '2026-11-04T10:00:00Z' 'task_started' 'older' $null)
        (Event '2026-11-04T10:30:00Z' 'task_complete' 'older' 1800000)
        (Event '2026-11-04T11:00:00Z' 'task_started' 'first' $null)
        (Event '2026-11-04T11:07:00Z' 'task_complete' 'first' 420000)
        (Event '2026-11-04T12:00:00Z' 'task_started' 'second' $null)
        (Event '2026-11-04T12:04:00Z' 'task_complete' 'second' 240000)
        (Event '2026-11-04T12:04:00Z' 'task_complete' 'second' 240000)
        (Event '2026-11-04T12:10:00Z' 'task_started' 'pending' $null)
        '{partial'
    ) | Set-Content -LiteralPath $fixture -Encoding utf8
    $result = & "$PSScriptRoot/measure-codex-work.ps1" -SessionPath $fixture -StartTurnId first | ConvertFrom-Json
    if ($result.CompletedOrInterruptedRuntimeSeconds -ne 660) { throw 'Older, duplicate or pending work was counted.' }
    if ($result.ExcludedBetweenTurnSeconds -ne 3540) { throw 'Between-turn gaps were not excluded.' }
    if ($result.MinimumUnfulfilledSeconds -ne 1740) { throw 'Eleven minutes incorrectly fulfilled forty minutes.' }
    if ($result.ActiveWorkTargetProven -or $null -ne $result.ActualActiveWorkSeconds) { throw 'Runtime was mislabeled as active work.' }
    if ($result.UnknownDurationTurns -ne 1 -or $result.InvalidOrPartialLines -ne 1) { throw 'Incomplete data was not disclosed.' }
    $bounded = & "$PSScriptRoot/measure-codex-work.ps1" -SessionPath $fixture -StartTurnId first -EndTurnId second | ConvertFrom-Json
    if ($bounded.Turns.Count -ne 2 -or $bounded.ExcludedBetweenTurnSeconds -ne 3180) { throw 'Explicit audit end was not respected.' }
    if (([DateTimeOffset]$bounded.Turns[0].StartedUtc).ToUnixTimeSeconds() -ne ([DateTimeOffset]'2026-11-04T11:00:00Z').ToUnixTimeSeconds()) { throw 'UTC was reinterpreted as local time.' }
    $rejected = $false
    try { & "$PSScriptRoot/measure-codex-work.ps1" -SessionPath $fixture -StartTurnId missing | Out-Null }
    catch { $rejected = $true }
    if (!$rejected) { throw 'Missing start was guessed.' }
    $rejected = $false
    try { & "$PSScriptRoot/measure-codex-work.ps1" -SessionPath $fixture -StartTurnId first -EndTurnId pending | Out-Null }
    catch { $rejected = $true }
    if (!$rejected) { throw 'Pending end was treated as completed.' }
    'PASS: 7 + 4 = 11 minutes; 59 minutes of gaps excluded; older/duplicate/pending data not credited; active work not fabricated; missing start rejected.'
}
finally { Remove-Item -LiteralPath $fixture -ErrorAction SilentlyContinue }
