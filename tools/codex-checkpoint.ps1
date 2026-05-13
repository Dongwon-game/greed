[CmdletBinding()]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$VaultPath = (Join-Path $env:USERPROFILE 'OneDrive\문서\Obsidian Vault'),
    [string]$NoteSubdir = 'Greed\Codex Logs',
    [int]$NoChangeMinutes = 60,
    [switch]$Commit,
    [switch]$Push,
    [string]$CommitMessage
)

$ErrorActionPreference = 'Stop'

function Get-HashText {
    param([string]$Text)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return [System.BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$GitArgs,
        [switch]$IgnoreErrors
    )

    $oldErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & git -C $script:ProjectFull @GitArgs 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldErrorActionPreference
    }

    $text = ($output | Out-String).Trim()

    if ($exitCode -ne 0 -and -not $IgnoreErrors) {
        throw "git $($GitArgs -join ' ') failed: $text"
    }

    if ($exitCode -ne 0) {
        return ''
    }

    return $text
}

function Get-CurrentHead {
    $oldErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & git -C $script:ProjectFull rev-parse --verify --short HEAD 2>$null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldErrorActionPreference
    }

    if ($exitCode -eq 0) {
        return (($output | Out-String).Trim())
    }

    return '(no commits yet)'
}

function ConvertTo-MarkdownList {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return '- none'
    }

    return (($Text -split "`r?`n") | ForEach-Object { "- $_" }) -join "`n"
}

$ProjectFull = (Resolve-Path -LiteralPath $ProjectPath).Path

if (-not (Test-Path -LiteralPath $VaultPath)) {
    New-Item -ItemType Directory -Path $VaultPath -Force | Out-Null
}

$VaultFull = (Resolve-Path -LiteralPath $VaultPath).Path
$NoteDir = Join-Path $VaultFull $NoteSubdir
New-Item -ItemType Directory -Path $NoteDir -Force | Out-Null

$now = Get-Date
$today = $now.ToString('yyyy-MM-dd')
$time = $now.ToString('HH:mm:ss')
$notePath = Join-Path $NoteDir "$today Codex Checkpoints.md"

$stateRoot = Join-Path $env:LOCALAPPDATA 'CodexCheckpoints'
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
$projectKey = (Get-HashText $ProjectFull.ToLowerInvariant()).Substring(0, 16)
$statePath = Join-Path $stateRoot "$projectKey.json"

$isRepo = $false
try {
    $inside = Invoke-Git -GitArgs @('rev-parse', '--is-inside-work-tree') -IgnoreErrors
    $isRepo = ($inside -eq 'true')
}
catch {
    $isRepo = $false
}

$branch = '(not a git repo)'
$head = '(not a git repo)'
$remote = '(none)'
$status = ''
$diffStat = ''
$commitOutput = ''
$pushOutput = ''
$actionNote = 'Obsidian log only. GitHub push is disabled unless -Commit and -Push are passed.'

if ($isRepo) {
    $branch = Invoke-Git -GitArgs @('branch', '--show-current') -IgnoreErrors
    if ([string]::IsNullOrWhiteSpace($branch)) {
        $branch = '(detached)'
    }

    $head = Get-CurrentHead

    $remoteLines = Invoke-Git -GitArgs @('remote', '-v') -IgnoreErrors
    if (-not [string]::IsNullOrWhiteSpace($remoteLines)) {
        $remote = (($remoteLines -split "`r?`n") | Select-Object -First 1)
    }

    $status = Invoke-Git -GitArgs @('status', '--short') -IgnoreErrors
    $diffStat = Invoke-Git -GitArgs @('diff', '--stat') -IgnoreErrors
    $cachedStat = Invoke-Git -GitArgs @('diff', '--cached', '--stat') -IgnoreErrors

    if (-not [string]::IsNullOrWhiteSpace($cachedStat)) {
        if ([string]::IsNullOrWhiteSpace($diffStat)) {
            $diffStat = $cachedStat
        }
        else {
            $diffStat = "$diffStat`n$cachedStat"
        }
    }

    $hasChanges = -not [string]::IsNullOrWhiteSpace($status)

    if ($Commit -and $hasChanges) {
        Invoke-Git -GitArgs @('add', '-A') | Out-Null

        if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
            $CommitMessage = "checkpoint: $today $time"
        }

        $commitOutput = Invoke-Git -GitArgs @('commit', '-m', $CommitMessage) -IgnoreErrors
        $status = Invoke-Git -GitArgs @('status', '--short') -IgnoreErrors
        $head = Get-CurrentHead
        $actionNote = "Committed local checkpoint: $CommitMessage"

        if ($Push) {
            if ($remote -eq '(none)') {
                $pushOutput = 'Skipped push: no git remote is configured.'
            }
            elseif ($branch -eq '(detached)') {
                $pushOutput = 'Skipped push: repository is in detached HEAD state.'
            }
            else {
                $upstream = Invoke-Git -GitArgs @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}') -IgnoreErrors
                if ([string]::IsNullOrWhiteSpace($upstream)) {
                    $pushOutput = Invoke-Git -GitArgs @('push', '-u', 'origin', $branch) -IgnoreErrors
                }
                else {
                    $pushOutput = Invoke-Git -GitArgs @('push') -IgnoreErrors
                }
            }
        }
    }
    elseif ($Commit -and -not $hasChanges) {
        $actionNote = 'Skipped commit: working tree is clean.'
    }
}

$snapshot = @"
project=$ProjectFull
branch=$branch
head=$head
remote=$remote
status=$status
diff=$diffStat
"@

$snapshotHash = Get-HashText $snapshot
$shouldLog = $true

if (Test-Path -LiteralPath $statePath) {
    try {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if ($state.hash -eq $snapshotHash) {
            $lastLogged = [datetime]::Parse($state.loggedAt)
            $minutesSinceLast = ($now - $lastLogged).TotalMinutes
            if ($minutesSinceLast -lt $NoChangeMinutes -and -not $Commit -and -not $Push) {
                $shouldLog = $false
            }
        }
    }
    catch {
        $shouldLog = $true
    }
}

if (-not $shouldLog) {
    Write-Output "No checkpoint written; no changes since last log. State: $statePath"
    exit 0
}

if (-not (Test-Path -LiteralPath $notePath)) {
    @"
# $today Codex Checkpoints

"@ | Set-Content -LiteralPath $notePath -Encoding UTF8
}

$statusBlock = ConvertTo-MarkdownList $status
$diffBlock = if ([string]::IsNullOrWhiteSpace($diffStat)) { 'none' } else { $diffStat }
$commitBlock = if ([string]::IsNullOrWhiteSpace($commitOutput)) { 'none' } else { $commitOutput }
$pushBlock = if ([string]::IsNullOrWhiteSpace($pushOutput)) { 'none' } else { $pushOutput }

$entry = @"
## $time

- Project: ``$ProjectFull``
- Branch: ``$branch``
- HEAD: ``$head``
- Remote: ``$remote``
- Action: $actionNote

### Changed files
$statusBlock

### Diff stat
~~~text
$diffBlock
~~~

### Commit
~~~text
$commitBlock
~~~

### Push
~~~text
$pushBlock
~~~

"@

Add-Content -LiteralPath $notePath -Value $entry -Encoding UTF8

@{
    hash = $snapshotHash
    loggedAt = $now.ToString('o')
    notePath = $notePath
    projectPath = $ProjectFull
} | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8

Write-Output "Checkpoint written: $notePath"
