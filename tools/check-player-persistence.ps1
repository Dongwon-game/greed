[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe',
    [switch]$CaptureFrames,
    [switch]$InteractiveWindow
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'This cross-process probe supports Windows only.' }
if ($CaptureFrames -and $InteractiveWindow) { throw 'Choose offscreen capture or interactive window checks, not both.' }
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$unity = (Resolve-Path -LiteralPath $UnityEditorPath).Path
$settings = Join-Path $project 'ProjectSettings\ProjectSettings.asset'
$settingsHash = (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash
$runtimeInputs = @('Assets/Scripts', 'Assets/Scenes', 'Assets/Resources', 'Packages', 'ProjectSettings')
$dirty = @(git -C $project status --porcelain --untracked-files=all -- $runtimeInputs)
if ($LASTEXITCODE -ne 0 -or $dirty.Count) { throw 'Commit or review changed game inputs before exporting a probe.' }
$head = (git -C $project rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'A committed source identity is required.' }
$existing = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'GreedLastPrototype.exe' -or
        ($_.Name -eq 'Unity.exe' -and $_.CommandLine -like ('*' + $project + '*'))
})
if ($existing.Count) { throw 'Leave the existing personal Greed editor/player untouched.' }

function Read-PersonalPreferences {
    $snapshot = [ordered]@{}
    foreach ($path in @('HKCU:\Software\DefaultCompany\greed last',
        'HKCU:\Software\Unity\UnityEditor\DefaultCompany\greed last')) {
        $values = [ordered]@{}
        if (Test-Path -LiteralPath $path) {
            $key = Get-Item -LiteralPath $path
            try {
                foreach ($name in @($key.GetValueNames() | Sort-Object)) {
                    if ($name.StartsWith('GreedLast.', [StringComparison]::OrdinalIgnoreCase)) {
                        $values[$name] = @{Kind=$key.GetValueKind($name).ToString(); Value=$key.GetValue($name)}
                    }
                }
            }
            finally { $key.Dispose() }
        }
        $snapshot[$path] = $values
    }
    return ($snapshot | ConvertTo-Json -Depth 8 -Compress)
}

$operation = if ($InteractiveWindow) { 'Build an isolated interactive test player; await external practice/pause/resume and window closure, then verify fresh-process persistence' }
    else { 'Export an isolated test project, build its unique product and run ten normal-exit/relaunch phases' }
if (!$PSCmdlet.ShouldProcess($project, $operation)) { return }
$before = Read-PersonalPreferences
$id = [Guid]::NewGuid().ToString('N')
$root = [IO.Path]::GetFullPath((Join-Path $project ('Logs\PlayerPersistenceChecks\' + $id)))
$expectedParent = [IO.Path]::GetFullPath((Join-Path $project 'Logs\PlayerPersistenceChecks'))
if ([IO.Path]::GetDirectoryName($root) -cne $expectedParent -or (Test-Path -LiteralPath $root)) {
    throw 'A new isolated directory inside the probe root is required.'
}
if ((Get-PSDrive -Name ([IO.Path]::GetPathRoot($root).Substring(0,1))).Free -lt 2GB) {
    throw 'Keep at least 2 GB free before creating the isolated source/build cache.'
}
$registryPath = 'HKCU:\Software\GreedLastPrototypeChecks\Persistence-' + $id
if (Test-Path -LiteralPath $registryPath) { throw 'The unique test product must not already have preferences.' }
[void][IO.Directory]::CreateDirectory($root)
$processes = [Collections.Generic.List[object]]::new()
$report = [ordered]@{Id=$id; SourceHead=$head; RunnerSHA256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;
    StartedUtc=[DateTime]::UtcNow.ToString('O'); CompletedUtc='';
    Succeeded=$false; Error=''; FailedPhase=''; PersonalPreferencesUnchanged=$false; ProjectSettingsUnchanged=$false;
    Scope='Isolated copied-source Windows player. Actual Application.Quit and fresh processes, real frames and app commands; not OS window-close input, forced-kill recovery, audible output, physical touch or device performance.';
    RegistryPath=$registryPath; Processes=$processes; Phases=@(); Inputs=@(); SafetyControls=@();
    OffscreenCaptureRequested=[bool]$CaptureFrames; InteractiveWindowRequested=[bool]$InteractiveWindow;
    PlayerLogChecks=@(); OwnTestPreferencesRetained=$true}
if ($InteractiveWindow) {
    $report.Scope = 'Interactive isolated player window phases and fresh-process persistence. External UI actions must be separately observed and recorded; this script does not identify the close trigger or claim physical timing, audible output or device performance.'
}

function Assert-CleanPlayerLog([string]$path) {
    $content = [IO.File]::ReadAllText($path)
    $count = [regex]::Matches($content, '(?m)^[^\r\n]*(?:Exception(?=:|[\s(])|error CS\d+)[^\r\n]*$').Count
    if ($count -ne 0) { throw ('Unexpected runtime exception lines in player log: ' + $count) }
    return @{Name=[IO.Path]::GetFileName($path); ExceptionLines=$count;
        SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
}

function Invoke-OwnedProcess([string]$file, [string[]]$arguments, [int]$timeout, [string]$kind, [int]$expectedExit = 0, [bool]$interactive = $false) {
    $style = if ($interactive) { 'Normal' } else { 'Hidden' }
    $p = Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle $style -PassThru
    $entry = [pscustomobject]@{Kind=$kind; ProcessId=$p.Id; ExitCode=$null; ExpectedExitCode=$expectedExit; Stopped=$false; TimedOut=$false}
    $processes.Add($entry)
    try {
        if (!$p.WaitForExit($timeout)) {
            $entry.TimedOut = $true
            $p.Kill()
            [void]$p.WaitForExit(10000)
            throw ($kind + ' timed out; only its own isolated process was stopped.')
        }
        $p.Refresh()
        $entry.ExitCode = $p.ExitCode
        if ($entry.ExitCode -ne $expectedExit) { throw ($kind + ' failed with exit code ' + $entry.ExitCode) }
    }
    finally {
        $p.Refresh()
        $entry.Stopped = $p.HasExited
        $p.Dispose()
    }
}

try {
    [IO.File]::Copy($PSCommandPath, (Join-Path $root 'runner.ps1'), $false)
    if ((Get-FileHash -LiteralPath (Join-Path $root 'runner.ps1') -Algorithm SHA256).Hash -cne $report.RunnerSHA256) {
        throw 'The preserved runner must match its report identity.'
    }
    $archive = Join-Path $root 'source.zip'
    git -C $project archive --format=zip ('--output=' + $archive) $head
    if ($LASTEXITCODE -ne 0) { throw 'Committed source export failed.' }
    $copy = Join-Path $root 'Project'
    Expand-Archive -LiteralPath $archive -DestinationPath $copy
    foreach ($asset in @(
        @{Source='Assets\Editor\GreedLastPlayerPersistenceBuilder.cs'; Destination='Assets\Editor\GreedLastPlayerPersistenceBuilder.cs'},
        @{Source='Assets\Editor\GreedLastPlayerPersistenceProbe.cs.txt'; Destination='Assets\Scripts\GreedLastPlayerPersistenceProbe.cs'}
    )) {
        $source = Join-Path $project $asset.Source
        $destination = Join-Path $copy $asset.Destination
        [IO.File]::Copy($source, $destination, $true)
        $digest = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -cne $digest) { throw 'Probe source copy mismatch.' }
        $report.Inputs += @{Path=$asset.Source; SHA256=$digest}
    }
    [IO.File]::WriteAllText((Join-Path $root 'prepared.json'), ($report | ConvertTo-Json -Depth 8))
    $buildLog = Join-Path $root 'build.log'
    Write-Verbose ('Building isolated persistence product: ' + $root)
    Invoke-OwnedProcess $unity @('-batchmode','-nographics','-quit','-projectPath',('"'+$copy+'"'),
        '-executeMethod','GreedLast.Editor.GreedLastPlayerPersistenceBuilder.Build','-greed-probe-id',$id,
        '-logFile',('"'+$buildLog+'"')) 900000 'isolated-build'
    $info = Get-Content -LiteralPath (Join-Path $root 'build.json') -Raw | ConvertFrom-Json
    if ($info.id -cne $id -or $info.company -cne 'GreedLastPrototypeChecks' -or $info.product -cne ('Persistence-' + $id)) {
        throw 'Isolated build identity mismatch.'
    }
    $exe = Join-Path $root 'Player\GreedLastPersistenceProbe.exe'
    if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -cne $info.executableSha256) { throw 'Isolated executable digest mismatch.' }
    $probeDll = Join-Path $root 'Player\GreedLastPersistenceProbe_Data\Managed\Assembly-CSharp.dll'
    if ((Get-FileHash -LiteralPath $probeDll -Algorithm SHA256).Hash -cne $info.runtimeSha256) { throw 'Isolated runtime digest mismatch.' }

    Write-Verbose 'Checking isolated builder and player output-path rejection.'
    $buildInfoHash = (Get-FileHash -LiteralPath (Join-Path $root 'build.json') -Algorithm SHA256).Hash
    $guardLog = Join-Path $root 'guard-builder.log'
    Invoke-OwnedProcess $unity @('-batchmode','-nographics','-quit','-projectPath',('"'+$copy+'"'),
        '-executeMethod','GreedLast.Editor.GreedLastPlayerPersistenceBuilder.Build',
        '-greed-probe-id','00000000000000000000000000000000','-logFile',('"'+$guardLog+'"')) 180000 'guard-builder' 1
    if (!(Select-String -LiteralPath $guardLog -SimpleMatch 'Refuse to change settings or build outside an isolated probe export.') -or
        (Get-FileHash -LiteralPath (Join-Path $root 'build.json') -Algorithm SHA256).Hash -cne $buildInfoHash) {
        throw 'The isolated builder must reject a mismatched identity before replacing its build.'
    }
    $report.SafetyControls += @{Name='builder-root'; Passed=$true; ExpectedExitCode=1}
    foreach ($guard in @('phase-path','output-root')) {
        $guardLog = Join-Path $root ('guard-' + $guard + '.log')
        $guardPhase = if ($guard -eq 'phase-path') { '../probe-escape-' + $id } else { 'clear' }
        $guardRoot = if ($guard -eq 'output-root') { Join-Path $root 'RejectedOutput' } else { $root }
        Invoke-OwnedProcess $exe @('-batchmode','-nographics','-logFile',('"'+$guardLog+'"'),
            '-greed-probe-id',$id,'-greed-probe-phase',$guardPhase,'-greed-probe-root',('"'+$guardRoot+'"')) 30000 ('guard-'+$guard) 1
        $message = if ($guard -eq 'phase-path') { 'The output phase must be a fixed safe filename.' }
            else { 'The probe must run inside its own build and isolated product, never the personal product.' }
        if (!(Select-String -LiteralPath $guardLog -SimpleMatch $message) -or
            (Test-Path -LiteralPath (Join-Path $root 'clear.json')) -or
            (Test-Path -LiteralPath (Join-Path $root 'RejectedOutput')) -or
            (Test-Path -LiteralPath (Join-Path $expectedParent ('probe-escape-'+$id+'.json')))) {
            throw ('The player must reject unsafe output arguments without creating output: '+$guard)
        }
        $report.SafetyControls += @{Name=$guard; Passed=$true; ExpectedExitCode=1}
    }

    # Deliberately mixed hashes reach the fixture exclusion gate; this artifact is never executed.
    $negativeProject = Join-Path $root 'FixtureLeakControl'
    $negativeBuild = Join-Path $negativeProject 'Builds\GreedLastPrototype'
    [void][IO.Directory]::CreateDirectory((Join-Path $negativeBuild 'GreedLastPrototype_Data\Managed'))
    [void][IO.Directory]::CreateDirectory((Join-Path $negativeProject 'ProjectSettings'))
    [IO.File]::Copy((Join-Path $project 'ProjectSettings\ProjectVersion.txt'), (Join-Path $negativeProject 'ProjectSettings\ProjectVersion.txt'))
    $release = Join-Path $project 'Builds\GreedLastPrototype'
    foreach ($name in @('GreedLastPrototype.exe','NotoSansKR-OFL.txt','NotoSansSymbols2-OFL.txt','NotoSansKR-NOTICE.txt')) {
        [IO.File]::Copy((Join-Path $release $name), (Join-Path $negativeBuild $name))
    }
    [IO.File]::Copy($probeDll, (Join-Path $negativeBuild 'GreedLastPrototype_Data\Managed\Assembly-CSharp.dll'))
    $negativeInfo = Get-Content -LiteralPath (Join-Path $release 'VerifiedBuildInfo.json') -Raw | ConvertFrom-Json
    $negativeInfo.runtimeAssemblySha256 = $info.runtimeSha256
    [IO.File]::WriteAllText((Join-Path $negativeBuild 'VerifiedBuildInfo.json'), ($negativeInfo | ConvertTo-Json -Depth 6))
    $rejected = $false
    try { $null = & (Join-Path $project 'tools\verify-prototype-build.ps1') -ProjectPath $negativeProject -UnityEditorPath $unity }
    catch {
        if ($_.Exception.Message -cne 'The isolated player-persistence fixture must never ship in the user release.') { throw }
        $rejected = $true
    }
    if (!$rejected) { throw 'A mixed artifact containing the persistence fixture must be rejected.' }
    $report.SafetyControls += @{Name='release-fixture-exclusion'; Passed=$true; Scope='Fabricated mixed artifact, static verification only; never executed.'}
    if ($report.SafetyControls.Count -ne 4) { throw 'Required probe safety controls are incomplete.' }

    if ($CaptureFrames) {
        Write-Verbose 'Capturing seven offscreen live-UI camera renders in the isolated player practice control.'
        $report.FailedPhase = 'render'
        $renderLog = Join-Path $root 'render.log'
        Invoke-OwnedProcess $exe @('-batchmode','-screen-fullscreen','0','-screen-width','1280','-screen-height','720',
            '-logFile',('"'+$renderLog+'"'),'-greed-probe-id',$id,'-greed-probe-phase','render',
            '-greed-probe-root',('"'+$root+'"')) 180000 'render'
        $report.PlayerLogChecks += Assert-CleanPlayerLog $renderLog
        $renderPath = Join-Path $root 'render.json'
        $render = Get-Content -LiteralPath $renderPath -Raw | ConvertFrom-Json
        if (!$render.succeeded -or $render.phase -cne 'render' -or $render.product -cne $info.product -or $render.frames.Count -ne 7 -or
            $render.candidate -or $render.clears -ne 0 -or $render.infiniteRuns -ne 0 -or $render.growthPoints -ne 0) {
            throw 'Actual rendered practice must pass without creating normal/infinite progression.'
        }
        $report.RenderedFrames = @($render.frames | ForEach-Object {
            $imagePath = Join-Path $root ('frame-'+$_.name+'.png')
            @{Name=$_.name; Width=$_.width; Height=$_.height; SampledColors=$_.sampledColors;
                SHA256=(Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash}
        })
        $report.RenderReportSHA256 = (Get-FileHash -LiteralPath $renderPath -Algorithm SHA256).Hash
        if (@($report.RenderedFrames.SHA256 | Select-Object -Unique).Count -ne 7) {
            throw 'Different real player states must not silently reuse the same captured image.'
        }
    }

    $phases = if ($InteractiveWindow) { @('window-play','window-clear','window-verify','window-verify-again') }
        else { @('clear','save','first-use','last-use','verify','verify-again',
            'live-normal','normal-fail','verify-failure','verify-failure-again') }
    foreach ($phase in $phases) {
        $report.FailedPhase = $phase
        Write-Verbose ('Starting a fresh owned player process: ' + $phase)
        $log = Join-Path $root ($phase + '.log')
        $interactive = $phase -in @('window-play','window-clear')
        $modeArguments = if ($interactive) { @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720') }
            else { @('-batchmode','-nographics') }
        $playerArguments = $modeArguments + @('-logFile',('"'+$log+'"'),
            '-greed-probe-id',$id,'-greed-probe-phase',$phase,'-greed-probe-root',('"'+$root+'"'))
        $timeout = if ($interactive) { 360000 } else { 180000 }
        Invoke-OwnedProcess $exe $playerArguments $timeout $phase 0 $interactive
        $report.PlayerLogChecks += Assert-CleanPlayerLog $log
        $path = Join-Path $root ($phase + '.json')
        $result = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if (!$result.succeeded -or $result.phase -cne $phase -or $result.product -cne $info.product) {
            throw ('Phase result identity or assertions failed: ' + $phase)
        }
        $report.Phases += @{Phase=$phase; SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;
            Clears=$result.clears; InfiniteRuns=$result.infiniteRuns; Growth=$result.growthPoints;
            Uses=$result.uses; AutomaticResumes=$result.automaticResumes}
    }
    $report.FailedPhase = ''
    $report.Succeeded = $true
}
catch {
    $report.Error = $_.Exception.ToString()
    if ($report.FailedPhase) {
        $failedResult = Join-Path $root ($report.FailedPhase + '.json')
        if (Test-Path -LiteralPath $failedResult) {
            $failure = Get-Content -LiteralPath $failedResult -Raw | ConvertFrom-Json
            $report.Error += ' Phase state: ' + $failure.screen + '; ' + $failure.error
        }
    }
}
finally {
    $report.PersonalPreferencesUnchanged = $before -ceq (Read-PersonalPreferences)
    $report.ProjectSettingsUnchanged = $settingsHash -ceq (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash
    $stopped = @($processes | Where-Object { !$_.Stopped -or $_.TimedOut -or $_.ExitCode -ne $_.ExpectedExitCode }).Count -eq 0
    $report.CompletedUtc = [DateTime]::UtcNow.ToString('O')
    $report.Succeeded = $report.Succeeded -and $report.PersonalPreferencesUnchanged -and $report.ProjectSettingsUnchanged -and $stopped
    [IO.File]::WriteAllText((Join-Path $root 'report.json'), ($report | ConvertTo-Json -Depth 9))
}
[ordered]@{Report=(Join-Path $root 'report.json'); SHA256=(Get-FileHash -LiteralPath (Join-Path $root 'report.json') -Algorithm SHA256).Hash;
    Succeeded=$report.Succeeded; Phases=$report.Phases.Count; SafetyControls=$report.SafetyControls.Count; PersonalPreferencesUnchanged=$report.PersonalPreferencesUnchanged;
    ProjectSettingsUnchanged=$report.ProjectSettingsUnchanged; Error=$report.Error} | ConvertTo-Json -Depth 5
if (!$report.Succeeded) { throw 'Isolated player persistence failed; inspect preserved reports/logs. No personal preferences were restored or deleted.' }
