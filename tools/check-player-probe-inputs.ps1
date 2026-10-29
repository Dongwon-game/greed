[CmdletBinding()]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [switch]$ExpectedFailure
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$runner = Join-Path $projectRoot 'tools\check-player-persistence.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'The player runner must parse before staging controls.' }
$loops = @($ast.FindAll({ param($node)
    $node -is [Management.Automation.Language.ForEachStatementAst] -and
        $node.Variable.VariablePath.UserPath -ceq 'asset'
}, $true))
if ($loops.Count -ne 1) { throw 'Require exactly one authored player probe staging loop.' }
$stage = [scriptblock]::Create($loops[0].Extent.Text)
$head = (git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Require the current source identity.' }
$id = [Guid]::NewGuid().ToString('N')
$parent = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Logs\ProbeInputChecks'))
$root = [IO.Path]::GetFullPath((Join-Path $parent $id))
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixtureRoot = [IO.Path]::GetFullPath((Join-Path $tempParent ('GreedProbeInputs-'+$id)))
if ([IO.Path]::GetDirectoryName($root) -cne $parent -or
    [IO.Path]::GetDirectoryName($fixtureRoot).TrimEnd('\') -cne $tempParent.TrimEnd('\') -or
    (Test-Path -LiteralPath $root) -or (Test-Path -LiteralPath $fixtureRoot)) { throw 'Require fresh owned report/fixture roots.' }
[void][IO.Directory]::CreateDirectory($root)
[void][IO.Directory]::CreateDirectory($fixtureRoot)
[void][IO.Directory]::CreateDirectory((Join-Path $fixtureRoot 'empty-hooks'))
$before = (Get-FileHash -LiteralPath $runner).Hash
$r = [ordered]@{ SourceHead=$head; StartedUtc=[DateTime]::UtcNow.ToString('O'); CompletedUtc='';
    Succeeded=$false; ExpectedFailure=[bool]$ExpectedFailure; Reproduced=$false; Error=''; Cases=@();
    RunnerSHA256=$before; DriverSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;
    FixtureRoot=$fixtureRoot; SourceRunnerUnchanged=$false;
    Scope='Actual player staging foreach AST only, against tiny generated Git fixtures. Committed exports and later worktree edits are separate; no Unity, game process, personal preferences, capture or actual source edits.' }
[IO.File]::Copy($PSCommandPath,(Join-Path $root 'driver.ps1'),$false)
[IO.File]::Copy($runner,(Join-Path $root 'source-runner.ps1'),$false)
$paths = @('Assets/Editor/GreedLastPlayerPersistenceBuilder.cs','Assets/Editor/GreedLastPlayerPersistenceProbe.cs.txt')
function Fixture-Git([string]$Directory, [string[]]$Arguments) {
    $gitArguments = @('-c',('core.hooksPath='+(Join-Path $fixtureRoot 'empty-hooks')),'-c','commit.gpgsign=false',
        '-c','user.name=GreedProbeFixture','-c','user.email=fixture@example.invalid','-C',$Directory)+$Arguments
    $value = @(git @gitArguments)
    if ($LASTEXITCODE -ne 0) { throw 'A fixture-only Git operation failed.' }
    return $value
}
function Run-Case([string]$Name) {
    $caseRoot = Join-Path $fixtureRoot $Name
    $project = Join-Path $caseRoot 'work'
    $copy = Join-Path $caseRoot 'export'
    [void][IO.Directory]::CreateDirectory($project)
    [void](Fixture-Git $project @('init','-q'))
    $attributes = "* text=auto eol=lf`n"
    if ($Name -eq 'crlf-export') { $attributes += "*.cs text eol=crlf`n*.cs.txt text eol=crlf`n" }
    [IO.File]::WriteAllText((Join-Path $project '.gitattributes'),$attributes,[Text.UTF8Encoding]::new($false))
    foreach ($path in $paths) {
        $file = Join-Path $project $path
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file))
        [IO.File]::WriteAllText($file,('// committed '+$path+"`n"),[Text.UTF8Encoding]::new($false))
    }
    [void](Fixture-Git $project (@('add','--')+$paths+@('.gitattributes')))
    [void](Fixture-Git $project @('commit','-q','-m','fixture'))
    $revisions = @(Fixture-Git $project @('rev-parse','HEAD'))
    $head = $revisions[0].Trim()
    if ($head -cnotmatch '^[0-9a-f]{40}$') { throw 'Require one complete fixture commit identity.' }
    $archive = Join-Path $caseRoot 'source.zip'
    [void](Fixture-Git $project @('archive','--format=zip',('--output='+$archive),$head))
    Expand-Archive -LiteralPath $archive -DestinationPath $copy
    [void][IO.Directory]::CreateDirectory((Join-Path $copy 'Assets/Scripts'))
    $expected = @{}
    foreach ($path in $paths) { $expected[$path] = (Get-FileHash -LiteralPath (Join-Path $copy $path)).Hash }
    if ($Name -eq 'worktree-race') {
        foreach ($path in $paths) {
            [IO.File]::WriteAllText((Join-Path $project $path),('// later uncommitted '+$path+"`r`n"),[Text.UTF8Encoding]::new($false))
        }
    }
    elseif ($Name -eq 'tampered-export') {
        [IO.File]::WriteAllText((Join-Path $copy $paths[1]),'tampered exported fixture',[Text.UTF8Encoding]::new($false))
    }
    elseif ($Name -eq 'missing-export') { Remove-Item -LiteralPath (Join-Path $copy $paths[1]) }
    $workBefore = @{}
    foreach ($path in $paths) { $workBefore[$path] = (Get-FileHash -LiteralPath (Join-Path $project $path)).Hash }
    $report = [ordered]@{ Inputs=@() }
    $failure = ''
    try { & $stage } catch { $failure = $_.Exception.Message }
    foreach ($path in $paths) {
        if ((Get-FileHash -LiteralPath (Join-Path $project $path)).Hash -cne $workBefore[$path]) { throw 'A fixture worktree edit was modified.' }
    }
    $runtime = Join-Path $copy 'Assets/Scripts/GreedLastPlayerPersistenceProbe.cs'
    if ($Name -in @('tampered-export','missing-export')) {
        $message = if ($Name -eq 'tampered-export') { 'Exported probe source differs from its committed blob.' }
            else { 'Exported committed probe source is missing.' }
        if ($failure -cne $message -or (Test-Path -LiteralPath $runtime)) { throw ('Unsafe exported inputs were not rejected before runtime staging: '+$Name) }
        return @{ Name=$Name; Rejected=$true; WorktreeUnchanged=$true; Staged=$false }
    }
    if ($failure) { throw $failure }
    $matches = (Get-FileHash -LiteralPath (Join-Path $copy $paths[0])).Hash -ceq $expected[$paths[0]] -and
        (Get-FileHash -LiteralPath $runtime).Hash -ceq $expected[$paths[1]]
    if ($report.Inputs.Count -ne 2) { throw 'Both actual staged inputs must be reported.' }
    if (!$matches -and !$ExpectedFailure) { throw ('Staged player inputs did not come from the committed export: '+$Name) }
    if ($ExpectedFailure -and ($Name -ne 'worktree-race' -or $matches)) { throw 'The original working-tree race was not reproduced.' }
    if (!$ExpectedFailure -and @($report.Inputs | Where-Object { !$_.ContainsKey('CanonicalBlob') -or $_.CanonicalBlob.Length -ne 40 }).Count) {
        throw 'Both staged input provenance records need a canonical committed blob.'
    }
    return @{ Name=$Name; CommittedInputsMatched=$matches; WorktreeUnchanged=$true; Staged=$true }
}
try {
    $names = if ($ExpectedFailure) { @('worktree-race') } else { @('clean','crlf-export','worktree-race','tampered-export','missing-export') }
    foreach ($name in $names) { $r.Cases += Run-Case $name }
    $r.Reproduced = [bool]$ExpectedFailure
    $r.Succeeded = !$ExpectedFailure -and $r.Cases.Count -eq 5
}
catch { $r.Error = $_.Exception.ToString() }
finally {
    $r.SourceRunnerUnchanged = $before -ceq (Get-FileHash -LiteralPath $runner).Hash
    $r.Succeeded = $r.Succeeded -and $r.SourceRunnerUnchanged
    $r.Reproduced = $r.Reproduced -and $r.SourceRunnerUnchanged -and $r.Error -ceq ''
    $r.CompletedUtc = [DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $root 'report.json'),($r | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
}
[ordered]@{Report=(Join-Path $root 'report.json'); SHA256=(Get-FileHash -LiteralPath (Join-Path $root 'report.json')).Hash;
    Succeeded=$r.Succeeded; Reproduced=$r.Reproduced; Cases=$r.Cases.Count; SourceRunnerUnchanged=$r.SourceRunnerUnchanged; Error=$r.Error} | ConvertTo-Json
if ((!$ExpectedFailure -and !$r.Succeeded) -or ($ExpectedFailure -and !$r.Reproduced)) { throw 'Player input staging controls failed; preserve the fixture evidence.' }
