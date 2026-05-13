[CmdletBinding()]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$VaultPath = (Join-Path $env:USERPROFILE 'OneDrive\문서\Obsidian Vault'),
    [string]$TaskName = 'Codex Plus Checkpoint - New project',
    [int]$IntervalMinutes = 15,
    [int]$NoChangeMinutes = 60
)

$ErrorActionPreference = 'Stop'

if ($IntervalMinutes -lt 5) {
    throw 'IntervalMinutes must be at least 5.'
}

$projectFull = (Resolve-Path -LiteralPath $ProjectPath).Path
$checkpointScript = Join-Path $PSScriptRoot 'codex-checkpoint.ps1'

if (-not (Test-Path -LiteralPath $checkpointScript)) {
    throw "Missing checkpoint script: $checkpointScript"
}

if (-not (Test-Path -LiteralPath $VaultPath)) {
    New-Item -ItemType Directory -Path $VaultPath -Force | Out-Null
}

$vaultFull = (Resolve-Path -LiteralPath $VaultPath).Path

$argument = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', "`"$checkpointScript`"",
    '-ProjectPath', "`"$projectFull`"",
    '-VaultPath', "`"$vaultFull`"",
    '-NoChangeMinutes', $NoChangeMinutes
) -join ' '

$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argument
$trigger = New-ScheduledTaskTrigger `
    -Once `
    -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes) `
    -RepetitionDuration (New-TimeSpan -Days 3650)

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew

$description = "Writes Codex/Git checkpoints for $projectFull to Obsidian every $IntervalMinutes minutes."

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description $description `
    -Force | Out-Null

Write-Output "Scheduled task registered: $TaskName"
Write-Output "Interval: $IntervalMinutes minutes"
Write-Output "Project: $projectFull"
Write-Output "Vault: $vaultFull"
