[CmdletBinding()]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$VaultPath = (Join-Path $env:USERPROFILE 'OneDrive\문서\Obsidian Vault'),
    [string]$Message
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Message)) {
    $Message = "checkpoint: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
}

$checkpointScript = Join-Path $PSScriptRoot 'codex-checkpoint.ps1'

& powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File $checkpointScript `
    -ProjectPath $ProjectPath `
    -VaultPath $VaultPath `
    -Commit `
    -Push `
    -CommitMessage $Message
