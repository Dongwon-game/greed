[CmdletBinding()]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [switch]$ExpectedFailure
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$runner = Join-Path $project 'tools\check-engine-loop.ps1'
$tokens=$null
$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($runner,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'The engine runner must parse before reader checks.'}
$definitions=@($ast.FindAll({param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Read-OwnedEngineReport'
},$true))
if($definitions.Count -ne 1){throw 'Require the single authored engine report reader.'}
. ([scriptblock]::Create($definitions[0].Extent.Text))
$head=(git -C $project rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0){throw 'Require the current source identity.'}
$id=[Guid]::NewGuid().ToString('N')
$parent=[IO.Path]::GetFullPath((Join-Path $project 'Logs\EngineReportInputChecks'))
$root=[IO.Path]::GetFullPath((Join-Path $parent $id))
if([IO.Path]::GetDirectoryName($root) -cne $parent -or (Test-Path -LiteralPath $root)){throw 'Require a fresh owned fixture/report root.'}
[void][IO.Directory]::CreateDirectory($root)
$before=(Get-FileHash -LiteralPath $runner).Hash
$r=[ordered]@{SourceHead=$head;StartedUtc=[DateTime]::UtcNow.ToString('O');CompletedUtc='';
    Succeeded=$false;ExpectedFailure=[bool]$ExpectedFailure;Reproduced=$false;Error='';Cases=@();
    SourceRunnerSHA256=$before;DriverSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;SourceRunnerUnchanged=$false;
    Scope='Actual read-only engine report-reader function AST against generated JSON files only. No Unity, engine run, screen, source mutation or personal preference access.'}
[IO.File]::Copy($PSCommandPath,(Join-Path $root 'driver.ps1'),$false)
[IO.File]::Copy($runner,(Join-Path $root 'source-runner.ps1'),$false)
try{
    $names=if($ExpectedFailure){@('alias-drift')}else{@('success','failure','alias-drift','unsafe-path','missing-archive','self-alias')}
    foreach($name in $names){
        $copy=Join-Path $root ('fixtures\'+$name)
        $directory=Join-Path $copy 'Logs\PlayModeChecks'
        [void][IO.Directory]::CreateDirectory($directory)
        $archiveName=if($name -eq 'unsafe-path'){'../escape.json'}elseif($name -eq 'self-alias'){'latest-report.json'}else{'report-fixture.json'}
        $latestData=[ordered]@{archivedReport=$archiveName;succeeded=($name -ne 'failure');scope='generated reader fixture'}
        $archiveData=[ordered]@{archivedReport=$archiveName;succeeded=($name -notin @('failure','alias-drift'));scope='generated reader fixture'}
        $latest=Join-Path $directory 'latest-report.json'
        [IO.File]::WriteAllText($latest,($latestData|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
        $archive=Join-Path $directory 'report-fixture.json'
        if($name -notin @('unsafe-path','missing-archive','self-alias')){
            [IO.File]::WriteAllText($archive,($archiveData|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
        }
        $failure=''
        $read=$null
        try{$read=Read-OwnedEngineReport $copy}catch{$failure=$_.Exception.Message}
        if($ExpectedFailure){
            if($failure -or $null -eq $read -or !$read.Data.succeeded -or
                (Get-Content -LiteralPath $read.Path -Raw|ConvertFrom-Json).succeeded -or
                $read.SHA256 -cne (Get-FileHash -LiteralPath $archive).Hash){throw 'The mismatched success-data/archive-hash result was not reproduced.'}
            $r.Cases+=@{Name=$name;WrongSuccessDataReproduced=$true;ArchivedResultSucceeded=$false}
            $r.Reproduced=$true
            continue
        }
        if($name -in @('success','failure')){
            if($failure -or $null -eq $read -or $read.Data.succeeded -ne ($name -eq 'success') -or
                $read.Path -cne $archive -or $read.SHA256 -cne (Get-FileHash -LiteralPath $archive).Hash){throw ('Valid archived result changed: '+$name)}
            $r.Cases+=@{Name=$name;Matched=$true;SucceededValue=$read.Data.succeeded}
        }else{
            $expected=if($name -eq 'unsafe-path'){'Unsafe archived report path.'}elseif($name -eq 'missing-archive'){'The archived engine report is missing.'}
                elseif($name -eq 'self-alias'){'The archived engine report must be distinct from its latest alias.'}else{'The latest engine report must match its archived report.'}
            if($failure -cne $expected){throw ('Invalid report relationship was not rejected: '+$name+'; '+$failure)}
            $r.Cases+=@{Name=$name;Rejected=$true}
        }
    }
    $r.Succeeded=!$ExpectedFailure -and $r.Cases.Count -eq 6
}catch{$r.Error=$_.Exception.ToString()}
finally{
    $r.SourceRunnerUnchanged=$before -ceq (Get-FileHash -LiteralPath $runner).Hash
    $r.Succeeded=$r.Succeeded -and $r.SourceRunnerUnchanged
    $r.Reproduced=$r.Reproduced -and $r.SourceRunnerUnchanged -and $r.Error -ceq ''
    $r.CompletedUtc=[DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $root 'report.json'),($r|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
}
[ordered]@{Report=(Join-Path $root 'report.json');SHA256=(Get-FileHash -LiteralPath (Join-Path $root 'report.json')).Hash;
    Succeeded=$r.Succeeded;Reproduced=$r.Reproduced;Cases=$r.Cases.Count;SourceRunnerUnchanged=$r.SourceRunnerUnchanged;Error=$r.Error}|ConvertTo-Json
if((!$ExpectedFailure -and !$r.Succeeded) -or ($ExpectedFailure -and !$r.Reproduced)){throw 'Engine report identity controls failed; preserve fixture evidence.'}
