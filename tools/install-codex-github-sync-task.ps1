[CmdletBinding()]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$VaultPath = (Join-Path $env:USERPROFILE 'OneDrive\문서\Obsidian Vault'),
    [string]$TaskName = 'Codex Plus GitHub Sync - New project',
    [int]$IntervalMinutes = 60
)

$ErrorActionPreference = 'Stop'

if ($IntervalMinutes -lt 15) {
    throw 'IntervalMinutes must be at least 15.'
}

$projectFull = (Resolve-Path -LiteralPath $ProjectPath).Path
$finishScript = Join-Path $PSScriptRoot 'codex-finish-work.ps1'

if (-not (Test-Path -LiteralPath $finishScript)) {
    throw "Missing finish script: $finishScript"
}

if (-not (Test-Path -LiteralPath $VaultPath)) {
    New-Item -ItemType Directory -Path $VaultPath -Force | Out-Null
}

$vaultFull = (Resolve-Path -LiteralPath $VaultPath).Path

$argument = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', "`"$finishScript`"",
    '-ProjectPath', "`"$projectFull`"",
    '-VaultPath', "`"$vaultFull`""
) -join ' '

$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argument
$trigger = New-ScheduledTaskTrigger `
    -Once `
    -At (Get-Date).AddMinutes(2) `
    -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes) `
    -RepetitionDuration (New-TimeSpan -Days 3650)

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -MultipleInstances IgnoreNew

$description = "Commits and pushes Codex checkpoints for $projectFull every $IntervalMinutes minutes when files changed."

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
