[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe',
    [switch]$CueOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'This isolated editor runner supports Windows only.' }
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$unity = (Resolve-Path -LiteralPath $UnityEditorPath).Path
$inputs = @('Assets/Scripts','Assets/Editor','Assets/Scenes','Assets/Resources','Packages','ProjectSettings')
$dirty = @(git -C $project status --porcelain --untracked-files=all -- $inputs)
if ($LASTEXITCODE -ne 0 -or $dirty.Count) { throw 'Review and commit changed game/editor inputs before exporting a check.' }
$head = (git -C $project rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'A committed source identity is required.' }
$mode = if ($CueOnly) { 'cue' } else { 'full' }
if (!$PSCmdlet.ShouldProcess($project, ('Export a private test product, seed its asset index and run the '+$mode+' engine check'))) { return }
$projectArgument = '(?i)(?:^|\s)-projectPath\s+(?:"'+[regex]::Escape($project.Replace('\','/'))+'"|'+[regex]::Escape($project.Replace('\','/'))+'(?=\s|$))'
$existing = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'GreedLastPrototype.exe' -or ($_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.Replace('\','/') -match $projectArgument)
})
if ($existing.Count) { throw 'Leave an existing personal Greed editor/player untouched.' }
if ((Get-PSDrive -Name ([IO.Path]::GetPathRoot($project).Substring(0,1))).Free -lt 2GB) { throw 'Keep at least 2 GB free for the isolated editor.' }

function Read-PersonalPreferences {
    $snapshot = [ordered]@{}
    foreach ($path in @('HKCU:\Software\DefaultCompany\greed last','HKCU:\Software\Unity\UnityEditor\DefaultCompany\greed last')) {
        $values = [ordered]@{}
        if (Test-Path -LiteralPath $path) {
            $key = Get-Item -LiteralPath $path
            try {
                foreach ($name in @($key.GetValueNames() | Sort-Object)) {
                    if ($name.StartsWith('GreedLast.', [StringComparison]::OrdinalIgnoreCase)) {
                        $values[$name] = @{Kind=$key.GetValueKind($name).ToString();Value=$key.GetValue($name)}
                    }
                }
            }
            finally { $key.Dispose() }
        }
        $snapshot[$path] = $values
    }
    return ($snapshot | ConvertTo-Json -Depth 8 -Compress)
}

$before = Read-PersonalPreferences
$settings = Join-Path $project 'ProjectSettings\ProjectSettings.asset'
$settingsHash = (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash
$id = [Guid]::NewGuid().ToString('N')
$parent = [IO.Path]::GetFullPath((Join-Path $project 'Logs\EngineLoopChecks'))
$root = [IO.Path]::GetFullPath((Join-Path $parent $id))
if ([IO.Path]::GetDirectoryName($root) -cne $parent -or (Test-Path -LiteralPath $root)) { throw 'A fresh owned engine export is required.' }
$product = 'EngineLoop-' + $id
if (Test-Path -LiteralPath ('HKCU:\Software\Unity\UnityEditor\GreedLastPrototypeChecks\'+$product)) { throw 'The isolated product must be new.' }
[void][IO.Directory]::CreateDirectory($root)
$processes = [Collections.Generic.List[object]]::new()
$report = [ordered]@{Id=$id;SourceHead=$head;RunnerSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;Kind=$mode;
    StartedUtc=[DateTime]::UtcNow.ToString('O');CompletedUtc='';Succeeded=$false;Error='';
    Scope='Fresh exported editor product; seeded asset-index definition, engine frames and synthetic input in two reload modes. Not native OS input, new screen captures, audible output or device performance.';
    Product=$product;Processes=$processes;EngineReport='';EngineReportSHA256='';IndexSHA256='';SourceArchiveSHA256='';
    PersonalPreferencesUnchanged=$false;ProjectSettingsUnchanged=$false;OwnTestPreferencesRetained=$true}

function Invoke-OwnedEditor([string[]]$Arguments, [int]$Timeout, [string]$Kind) {
    $p = Start-Process -FilePath $unity -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    $entry = [pscustomobject]@{Kind=$Kind;PID=$p.Id;ExitCode=$null;Stopped=$false;TimedOut=$false}
    $processes.Add($entry)
    try {
        if (!$p.WaitForExit($Timeout)) {
            $entry.TimedOut=$true; $p.Kill(); [void]$p.WaitForExit(10000)
            throw ($Kind+' timed out; only its owned editor was stopped.')
        }
        $p.Refresh(); $entry.ExitCode=$p.ExitCode
        if ($p.ExitCode -ne 0) { throw ($Kind+' failed; inspect its preserved log/report.') }
    }
    finally { $p.Refresh(); $entry.Stopped=$p.HasExited; $p.Dispose() }
}

try {
    [IO.File]::Copy($PSCommandPath,(Join-Path $root 'runner.ps1'),$false)
    [IO.File]::WriteAllText((Join-Path $root 'prepared.json'),($report|ConvertTo-Json -Depth 8))
    $archive = Join-Path $root 'source.zip'
    git -C $project archive --format=zip ('--output='+$archive) $head
    if ($LASTEXITCODE -ne 0) { throw 'Source export failed.' }
    $report.SourceArchiveSHA256=(Get-FileHash -LiteralPath $archive).Hash
    $copy = Join-Path $root 'Project'
    Expand-Archive -LiteralPath $archive -DestinationPath $copy
    $userSettings = Join-Path $copy 'UserSettings'
    [void][IO.Directory]::CreateDirectory($userSettings)
    $index = Join-Path $userSettings 'Search.index'
    if (Test-Path -LiteralPath $index) { throw 'Do not replace an exported user index.' }
    $definition = [ordered]@{name='Assets';roots=@('Assets');includes=@();excludes=@('Assets/Temp/','Assets/External/');
        options=[ordered]@{types=$true;properties=$true;extended=$false;dependencies=$true};baseScore=999}
    [IO.File]::WriteAllText($index,($definition|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
    $report.IndexSHA256=(Get-FileHash -LiteralPath $index).Hash
    Invoke-OwnedEditor @('-batchmode','-nographics','-quit','-projectPath',('"'+$copy+'"'),
        '-executeMethod','GreedLast.Editor.GreedLastEngineLoopBootstrap.Prepare','-greed-engine-id',$id,
        '-logFile',('"'+(Join-Path $root 'prepare.log')+'"')) 600000 'prepare'
    $prepared = Get-Content -LiteralPath (Join-Path $root 'prepared-engine-project.json') -Raw | ConvertFrom-Json
    if ($prepared.id -cne $id -or $prepared.company -cne 'GreedLastPrototypeChecks' -or $prepared.product -cne $product) {
        throw 'Prepared identity mismatch.'
    }
    $timeout = if ($CueOnly) { 600000 } else { 1200000 }
    Invoke-OwnedEditor @('-batchmode','-force-d3d11','-projectPath',('"'+$copy+'"'),
        '-executeMethod','GreedLast.Editor.GreedLastEngineLoopBootstrap.Run','-greed-engine-id',$id,'-greed-engine-kind',$mode,
        '-logFile',('"'+(Join-Path $root 'engine.log')+'"')) $timeout 'engine'
    $engineDirectory = Join-Path $copy 'Logs\PlayModeChecks'
    $engine = Get-Content -LiteralPath (Join-Path $engineDirectory 'latest-report.json') -Raw | ConvertFrom-Json
    if (!$engine.succeeded -or !$engine.personalPreferencesUnchanged -or $engine.practicePreferenceRestorationPending -or
        @($engine.cycles).Count -ne 2 -or $engine.trapCueOnly -ne [bool]$CueOnly -or $engine.fullLoopIncluded -eq [bool]$CueOnly) {
        throw 'Required engine coverage or fixture preference restoration did not complete.'
    }
    if ([IO.Path]::GetFileName($engine.archivedReport) -cne $engine.archivedReport) { throw 'Unsafe archived report path.' }
    $report.EngineReport=Join-Path $engineDirectory $engine.archivedReport
    $report.EngineReportSHA256=(Get-FileHash -LiteralPath $report.EngineReport).Hash
    $report.Succeeded=$true
}
catch { $report.Error=$_.Exception.ToString() }
finally {
    $report.PersonalPreferencesUnchanged=$before -ceq (Read-PersonalPreferences)
    $report.ProjectSettingsUnchanged=$settingsHash -ceq (Get-FileHash -LiteralPath $settings).Hash
    $report.Succeeded=$report.Succeeded -and $report.PersonalPreferencesUnchanged -and $report.ProjectSettingsUnchanged -and
        @($processes|Where-Object{!$_.Stopped -or $_.TimedOut -or $_.ExitCode -ne 0}).Count -eq 0
    $report.CompletedUtc=[DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $root 'report.json'),($report|ConvertTo-Json -Depth 9))
}
[ordered]@{Report=(Join-Path $root 'report.json');SHA256=(Get-FileHash -LiteralPath (Join-Path $root 'report.json')).Hash;
    Succeeded=$report.Succeeded;Kind=$mode;PersonalPreferencesUnchanged=$report.PersonalPreferencesUnchanged;
    ProjectSettingsUnchanged=$report.ProjectSettingsUnchanged;Error=$report.Error}|ConvertTo-Json -Depth 4
if (!$report.Succeeded) { throw 'Isolated engine check did not complete. Preserve its reports; no personal preferences were reset.' }
