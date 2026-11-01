[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe',
    [switch]$BuildApk,
    [switch]$PrerequisitesReviewed
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($BuildApk -and !$PrerequisitesReviewed) { throw 'Install and review the required SDK tools/licenses first; explicit APK builds require -PrerequisitesReviewed.' }
if ($PrerequisitesReviewed -and !$BuildApk) { throw '-PrerequisitesReviewed only applies to an explicitly requested -BuildApk.' }
if ($env:OS -ne 'Windows_NT') { throw 'The Android preview runner supports Windows-host Unity only.' }
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$unity = (Resolve-Path -LiteralPath $UnityEditorPath).Path
foreach ($path in @($project, $unity)) {
    $item = Get-Item -LiteralPath $path
    while ($null -ne $item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Use real source/editor paths, not directory links.' }
        $item = if ($item -is [IO.FileInfo]) { $item.Directory } else { $item.Parent }
    }
}
$dirty = @(git -C $project status --porcelain --untracked-files=all -- Assets Packages ProjectSettings)
if ($LASTEXITCODE -ne 0 -or $dirty.Count) { throw 'Review and commit changed game/editor inputs before exporting an Android preview.' }
$head = (git -C $project rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -cnotmatch '^[0-9a-f]{40}$') { throw 'A committed source identity is required.' }
$action = if ($BuildApk) { 'Inspect a private export, then build a separate-product APK without installing it' }
    else { 'Inspect Android prerequisites in a private export; do not prepare or build an APK' }
if (!$PSCmdlet.ShouldProcess($project, $action)) { return }
$minimumFree = if ($BuildApk) { 8GB } else { 2GB }
if ((Get-PSDrive -Name ([IO.Path]::GetPathRoot($project).Substring(0,1))).Free -lt $minimumFree) {
    throw 'Keep at least 2 GB free for inspection or 8 GB for an explicitly requested APK build.'
}

function Read-PersonalState {
    $state = [ordered]@{}
    foreach ($path in @('HKCU:\Software\DefaultCompany\greed last', 'HKCU:\Software\Unity\UnityEditor\DefaultCompany\greed last',
        'HKCU:\Software\GreedLastPrototypePreview\Greed Last Demo 20261030',
        'HKCU:\Software\GreedLastPrototypePreview\Greed Last Demo 20261031')) {
        $values = [ordered]@{}
        if (Test-Path -LiteralPath $path) {
            $key = Get-Item -LiteralPath $path
            try {
                foreach ($name in @($key.GetValueNames() | Sort-Object)) {
                    if ($name.StartsWith('GreedLast.', [StringComparison]::OrdinalIgnoreCase)) {
                        $values[$name] = @{ Kind=$key.GetValueKind($name).ToString(); Value=$key.GetValue($name) }
                    }
                }
            }
            finally { $key.Dispose() }
        }
        $state[$path] = $values
    }
    return ($state | ConvertTo-Json -Depth 8 -Compress)
}

$before = Read-PersonalState
$settings = Join-Path $project 'ProjectSettings\ProjectSettings.asset'
$settingsHash = (Get-FileHash -LiteralPath $settings).Hash
$windowsFiles = @{}
foreach ($relative in @('Builds\GreedLastPrototype\GreedLastPrototype_Data\Managed\Assembly-CSharp.dll',
    'Builds\GreedLastDemo-20261031\GreedLastDemo_Data\Managed\Assembly-CSharp.dll')) {
    $path = Join-Path $project $relative
    if (Test-Path -LiteralPath $path -PathType Leaf) { $windowsFiles[$path] = (Get-FileHash -LiteralPath $path).Hash }
}
$id = [Guid]::NewGuid().ToString('N')
$parent = [IO.Path]::GetFullPath((Join-Path $project 'Logs\AndroidPreviewBuilds'))
$root = [IO.Path]::GetFullPath((Join-Path $parent $id))
if ([IO.Path]::GetDirectoryName($root) -cne $parent -or (Test-Path -LiteralPath $root)) { throw 'A new owned Android export is required.' }
foreach ($path in @((Join-Path $project 'Logs'), $parent)) {
    if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Android evidence directories must not be links.'
    }
}
[void][IO.Directory]::CreateDirectory($root)
$processes = [Collections.Generic.List[object]]::new()
$report = [ordered]@{ Id=$id; SourceHead=$head; StartedUtc=[DateTime]::UtcNow.ToString('O'); CompletedUtc='';
    Status='Pending'; Succeeded=$false; InspectionSucceeded=$false; BuildRequested=[bool]$BuildApk; BuildAttempted=$false; ApkBuilt=$false;
    Error=''; SourceArchiveSHA256=''; RunnerSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;
    PrerequisitesSHA256=''; ModuleSupported=$false; BundledPrerequisitesPresent=$false; Apk=''; ApkSHA256='';
    BuildInfoSHA256=''; PrivateEditorIdentityEstablished=$false; PersonalPreferencesUnchanged=$false; ProjectSettingsUnchanged=$false; WindowsAssembliesUnchanged=$false;
    Processes=$processes; Scope='Private Windows-host Unity export. Default: prerequisite inspection only. Explicit -BuildApk -PrerequisitesReviewed: separate-package non-development ARM64 IL2CPP APK after the operator reviews installed tools/licenses. This runner does not invoke installers, accept licenses, change the personal target, run adb/install or assert store signing/hardware success. Unity toolchain behavior requires separate review.' }

function Invoke-OwnedEditor([string[]]$Arguments, [int]$Timeout, [string]$Kind) {
    $process = Start-Process -FilePath $unity -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    $entry = [pscustomobject]@{ Kind=$Kind; PID=$process.Id; ExitCode=$null; Stopped=$false; TimedOut=$false }
    $processes.Add($entry)
    try {
        if (!$process.WaitForExit($Timeout)) {
            $entry.TimedOut=$true
            $process.Kill()
            [void]$process.WaitForExit(10000)
            throw ($Kind+' timed out; only its owned Unity process was stopped.')
        }
        $process.Refresh()
        $entry.ExitCode=$process.ExitCode
        if ($entry.ExitCode -ne 0) { throw ($Kind+' failed; preserve its log and the export for review.') }
    }
    finally { $process.Refresh(); $entry.Stopped=$process.HasExited; $process.Dispose() }
}

try {
    [IO.File]::Copy($PSCommandPath, (Join-Path $root 'runner.ps1'), $false)
    $marker = [ordered]@{ schema=1; id=$id; sourceHead=$head; sourceProject=$project }
    [IO.File]::WriteAllText((Join-Path $root 'prepared.json'), ($marker | ConvertTo-Json))
    $archive = Join-Path $root 'source.zip'
    git -C $project archive --format=zip ('--output='+$archive) $head
    if ($LASTEXITCODE -ne 0) { throw 'The committed Android source export failed.' }
    $report.SourceArchiveSHA256=(Get-FileHash -LiteralPath $archive).Hash
    $copy = Join-Path $root 'Project'
    Expand-Archive -LiteralPath $archive -DestinationPath $copy
    $copySettings = Join-Path $copy 'ProjectSettings\ProjectSettings.asset'
    $common = @('-batchmode','-nographics','-quit','-projectPath',('"'+$copy+'"'),'-greed-android-id',$id)
    Invoke-OwnedEditor ($common + @('-executeMethod','GreedLast.Editor.GreedLastAndroidPreviewBuilder.Initialize',
        '-logFile',('"'+(Join-Path $root 'initialize.log')+'"'))) 600000 'initialize-private-editor'
    $editorIdentity=Get-Content -LiteralPath (Join-Path $root 'editor-identity.json') -Raw | ConvertFrom-Json
    if ($editorIdentity.id -cne $id -or $editorIdentity.company -cne 'GreedLastPrototypeChecks' -or
        $editorIdentity.product -cne ('AndroidPreview-'+$id)) { throw 'The initial private Editor identity did not match.' }
    $copyHash = (Get-FileHash -LiteralPath $copySettings).Hash
    Invoke-OwnedEditor ($common + @('-executeMethod','GreedLast.Editor.GreedLastAndroidPreviewBuilder.Inspect',
        '-logFile',('"'+(Join-Path $root 'inspect.log')+'"'))) 600000 'inspect'
    $report.PrivateEditorIdentityEstablished=$true
    if ((Get-FileHash -LiteralPath $copySettings).Hash -cne $copyHash) { throw 'Read-only inspection changed exported PlayerSettings.' }
    $prerequisites = Join-Path $root 'prerequisites.json'
    $readiness = Get-Content -LiteralPath $prerequisites -Raw | ConvertFrom-Json
    if ($readiness.unityVersion -cne '6000.4.3f1' -or @($readiness.files).Count -ne 9) { throw 'Unexpected prerequisite evidence.' }
    $report.PrerequisitesSHA256=(Get-FileHash -LiteralPath $prerequisites).Hash
    $report.ModuleSupported=[bool]$readiness.moduleSupported
    $report.BundledPrerequisitesPresent=[bool]$readiness.bundledPrerequisitesPresent
    $report.InspectionSucceeded=$true
    if (!$report.ModuleSupported -or !$report.BundledPrerequisitesPresent) {
        $report.Status='PrerequisitesMissing'
        $report.Succeeded=!$BuildApk
        if ($BuildApk) { $report.Error='Android prerequisites are missing; no preparation, target switch or APK build was attempted.' }
    }
    elseif (!$BuildApk) { $report.Status='ReadyForExplicitBuild'; $report.Succeeded=$true }
    else {
        Invoke-OwnedEditor ($common + @('-executeMethod','GreedLast.Editor.GreedLastAndroidPreviewBuilder.Prepare',
            '-logFile',('"'+(Join-Path $root 'prepare.log')+'"'))) 600000 'prepare'
        $identity = Get-Content -LiteralPath (Join-Path $root 'prepared-android-project.json') -Raw | ConvertFrom-Json
        if ($identity.id -cne $id -or $identity.company -cne 'GreedLastPrototypeChecks' -or $identity.product -cne ('AndroidPreview-'+$id) -or
            $identity.packageId -cne 'com.greedlast.prototype.mobilepreview') { throw 'The prepared private identity did not match.' }
        $report.BuildAttempted=$true
        Invoke-OwnedEditor ($common + @('-buildTarget','Android','-executeMethod','GreedLast.Editor.GreedLastAndroidPreviewBuilder.Build',
            '-logFile',('"'+(Join-Path $root 'build.log')+'"'))) 1800000 'apk-build'
        $infoPath = Join-Path $root 'build.json'
        $info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
        if ($info.id -cne $id -or $info.sourceHead -cne $head -or $info.packageId -cne 'com.greedlast.prototype.mobilepreview' -or
            $info.apkPath -cne 'Player/GreedLastMobilePreview.apk' -or !$info.smokePassed -or $info.developmentBuild -or
            !$info.privateEditorIdentityRetained -or $info.company -cne 'GreedLastPrototypePreview' -or
            $info.product -cne 'Greed Last Mobile Preview' -or $info.editorCompany -cne 'GreedLastPrototypeChecks' -or
            $info.editorProduct -cne ('AndroidPreview-'+$id)) { throw 'APK build identity or coverage did not match.' }
        $report.Apk=Join-Path $root 'Player\GreedLastMobilePreview.apk'
        $report.ApkSHA256=(Get-FileHash -LiteralPath $report.Apk).Hash
        if ($report.ApkSHA256 -cne $info.apkSha256) { throw 'The APK hash differs from its build evidence.' }
        $report.BuildInfoSHA256=(Get-FileHash -LiteralPath $infoPath).Hash
        $report.ApkBuilt=$true
        $report.Status='ApkBuilt'
        $report.Succeeded=$true
    }
}
catch { $report.Succeeded=$false; $report.Status='Failed'; $report.Error=$_.Exception.ToString() }
finally {
    $report.PersonalPreferencesUnchanged=$before -ceq (Read-PersonalState)
    $report.ProjectSettingsUnchanged=$settingsHash -ceq (Get-FileHash -LiteralPath $settings).Hash
    $report.WindowsAssembliesUnchanged=@($windowsFiles.Keys | Where-Object {
        !(Test-Path -LiteralPath $_ -PathType Leaf) -or (Get-FileHash -LiteralPath $_).Hash -cne $windowsFiles[$_]
    }).Count -eq 0
    $report.Succeeded=($report.Succeeded -and $report.PrivateEditorIdentityEstablished -and $report.PersonalPreferencesUnchanged -and $report.ProjectSettingsUnchanged -and
        $report.WindowsAssembliesUnchanged -and @($processes | Where-Object { !$_.Stopped -or $_.TimedOut -or $_.ExitCode -ne 0 }).Count -eq 0)
    if (!$report.PersonalPreferencesUnchanged -or !$report.ProjectSettingsUnchanged -or !$report.WindowsAssembliesUnchanged) {
        $report.Status='PreservationFailed'
        $report.Error+=' Personal state changed during the check; review concurrent work and preserve the evidence.'
    }
    $report.CompletedUtc=[DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $root 'report.json'), ($report | ConvertTo-Json -Depth 9))
}
[pscustomobject]@{ Report=(Join-Path $root 'report.json'); SHA256=(Get-FileHash -LiteralPath (Join-Path $root 'report.json')).Hash;
    Succeeded=$report.Succeeded; Status=$report.Status; InspectionSucceeded=$report.InspectionSucceeded; ApkBuilt=$report.ApkBuilt;
    PersonalPreferencesUnchanged=$report.PersonalPreferencesUnchanged; ProjectSettingsUnchanged=$report.ProjectSettingsUnchanged;
    WindowsAssembliesUnchanged=$report.WindowsAssembliesUnchanged; Error=$report.Error } | ConvertTo-Json
if (!$report.Succeeded) { throw 'Android preview work did not complete; inspect the preserved report. No personal data was reset.' }
