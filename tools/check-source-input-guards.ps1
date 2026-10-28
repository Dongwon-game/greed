[CmdletBinding()]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe',
    [switch]$ExpectedFailure
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$unity = (Resolve-Path -LiteralPath $UnityEditorPath).Path
$head = (git -C $project rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Committed runner identity is required.' }
$id = [Guid]::NewGuid().ToString('N')
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixtureRoot = [IO.Path]::GetFullPath((Join-Path $tempParent ('GreedInputGuard-' + $id)))
$reportParent = [IO.Path]::GetFullPath((Join-Path $project 'Logs\SourceInputGuardChecks'))
$root = [IO.Path]::GetFullPath((Join-Path $reportParent $id))
if ([IO.Path]::GetDirectoryName($root) -cne $reportParent -or
    [IO.Path]::GetDirectoryName($fixtureRoot).TrimEnd('\') -cne $tempParent.TrimEnd('\') -or
    (Test-Path -LiteralPath $root) -or (Test-Path -LiteralPath $fixtureRoot)) { throw 'Fresh owned test directories are required.' }
[void][IO.Directory]::CreateDirectory($root)
[void][IO.Directory]::CreateDirectory($fixtureRoot)
$runners = @(
    @{Name='engine'; Path=(Join-Path $project 'tools\check-engine-loop.ps1'); Message='Review and commit changed game/editor inputs before exporting a check.'},
    @{Name='player'; Path=(Join-Path $project 'tools\check-player-persistence.ps1'); Message='Commit or review changed game inputs before exporting a probe.'}
)
$report = [ordered]@{Id=$id; SourceHead=$head; StartedUtc=[DateTime]::UtcNow.ToString('O'); CompletedUtc='';
    ExpectedFailure=[bool]$ExpectedFailure; Succeeded=$false; Reproduced=$false; Error=''; FixtureRoot=$fixtureRoot;
    FixturesRetained=$true; AllNoExports=$false; MissedDirtyGuards=0; Cases=@(); Inputs=@();
    Scope='Two real runner preflights with -WhatIf against seven tiny synthetic Git repositories. No Unity process, source export, personal preferences, screen input/capture, game build or real font mutation.'}

function Write-Fixture([string]$Directory, [string]$Relative, [string]$Text) {
    $path = [IO.Path]::GetFullPath((Join-Path $Directory $Relative))
    if (!$path.StartsWith($Directory + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture path.' }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    [IO.File]::WriteAllText($path, $Text, [Text.UTF8Encoding]::new($false))
}

function Invoke-FixtureGit([string]$Directory, [string[]]$Arguments) {
    $output = @(git -C $Directory @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw ('Fixture Git failed: ' + ($output -join ' ')) }
}

try {
    [IO.File]::Copy($PSCommandPath, (Join-Path $root 'runner.ps1'), $false)
    foreach ($runner in $runners) {
        $report.Inputs += @{Name=$runner.Name; SHA256=(Get-FileHash -LiteralPath $runner.Path).Hash}
        [IO.File]::Copy($runner.Path, (Join-Path $root ($runner.Name + '-runner.ps1')), $false)
    }
    $fixtures = [ordered]@{
        clean=''
        script='Assets\Scripts\marker.txt'
        font='Assets\ThirdParty\fixture-font.txt'
        new_asset='Assets\Unlisted\new-texture.asset'
        folder_meta='Assets\ThirdParty.meta'
        package='Packages\manifest.json'
        settings='ProjectSettings\ProjectSettings.asset'
    }
    foreach ($case in $fixtures.GetEnumerator()) {
        $directory = Join-Path $fixtureRoot $case.Key
        [void][IO.Directory]::CreateDirectory($directory)
        foreach ($relative in @('Assets\Scripts\marker.txt', 'Assets\ThirdParty\fixture-font.txt',
            'Assets\ThirdParty.meta', 'Packages\manifest.json', 'ProjectSettings\ProjectSettings.asset')) {
            Write-Fixture $directory $relative "synthetic fixture baseline`n"
        }
        Invoke-FixtureGit $directory @('init', '-b', 'main')
        Invoke-FixtureGit $directory @('add', '--', '.')
        Invoke-FixtureGit $directory @('-c','user.name=Greed Input Guard Fixture','-c','user.email=fixture@example.invalid',
            'commit','-m','Synthetic input guard baseline')
        if ($case.Value) { Write-Fixture $directory $case.Value "synthetic changed input`n" }
        $before = @{}
        foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.FullName -notlike '*\.git\*' }) {
            $before[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash
        }
        foreach ($runner in $runners) {
            $blocked = $false
            try { $null = & $runner.Path -ProjectPath $directory -UnityEditorPath $unity -WhatIf }
            catch {
                if ($_.Exception.Message -cne $runner.Message) { throw }
                $blocked = $true
            }
            $expectedBlock = [bool]$case.Value
            $report.Cases += @{Runner=$runner.Name; Fixture=$case.Key; ExpectedDirtyBlock=$expectedBlock;
                DirtyBlocked=$blocked; Passed=($blocked -eq $expectedBlock)}
            if ($expectedBlock -and !$blocked) { $report.MissedDirtyGuards++ }
            if ($case.Key -in @('clean','script','package','settings') -and $blocked -ne $expectedBlock) {
                throw 'Clean and existing tracked-input controls must retain their real preflight behavior.'
            }
        }
        foreach ($path in $before.Keys) {
            if ((Get-FileHash -LiteralPath $path).Hash -cne $before[$path]) { throw 'Preflight modified a fixture source file.' }
        }
        if (Test-Path -LiteralPath (Join-Path $directory 'Logs')) { throw 'A -WhatIf preflight created export directories.' }
    }
    $report.AllNoExports = $true
    if ($ExpectedFailure) {
        if ($report.MissedDirtyGuards -ne 6 -or @($report.Cases | Where-Object { !$_.Passed -and $_.Fixture -notin @('font','new_asset','folder_meta') }).Count) {
            throw 'Require exactly the six omitted Asset-path guards to reproduce.'
        }
        $report.Reproduced = $true
    }
    else {
        if (@($report.Cases | Where-Object { !$_.Passed }).Count) { throw 'Every changed Asset/package/settings input must block before export.' }
        $report.Succeeded = $true
    }
}
catch { $report.Error = $_.Exception.ToString() }
finally {
    $report.CompletedUtc = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $root 'report.json'), ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
}
[pscustomobject]@{Report=(Join-Path $root 'report.json'); SHA256=(Get-FileHash -LiteralPath (Join-Path $root 'report.json')).Hash;
    Succeeded=$report.Succeeded; Reproduced=$report.Reproduced; Cases=$report.Cases.Count;
    MissedDirtyGuards=$report.MissedDirtyGuards; AllNoExports=$report.AllNoExports; Error=$report.Error} | ConvertTo-Json
if ($report.Error -or (!$report.Succeeded -and !$report.Reproduced)) { throw 'Source input preflight check did not complete.' }
