[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'This startup check supports Windows only.' }
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$build = Join-Path $project 'Builds\GreedLastPrototype'
$info = Get-Content -LiteralPath (Join-Path $build 'VerifiedBuildInfo.json') -Raw | ConvertFrom-Json
if ($info.executablePath -cne 'GreedLastPrototype.exe' -or
    $info.runtimeAssemblyPath -cne 'GreedLastPrototype_Data/Managed/Assembly-CSharp.dll' -or
    $info.target -ne 'StandaloneWindows64' -or $info.developmentBuild -isnot [bool] -or
    $info.developmentBuild -or $info.smokePassed -isnot [bool] -or !$info.smokePassed) {
    throw 'A verified nondevelopment Windows prototype is required.'
}
$exe = Join-Path $build $info.executablePath
foreach ($file in @(
    @{Path=$exe; Hash=$info.executableSha256},
    @{Path=(Join-Path $build $info.runtimeAssemblyPath); Hash=$info.runtimeAssemblySha256}
)) {
    if ($file.Hash -notmatch '^[A-Fa-f0-9]{64}$' -or
        (Get-FileHash -LiteralPath $file.Path -Algorithm SHA256).Hash -ne $file.Hash) {
        throw 'The executable or runtime assembly differs from its verified metadata.'
    }
}

# The read-only safety gate must never silently inspect a different product's empty registry key.
$settings = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings\ProjectSettings.asset') -Raw
if ($settings -notmatch '(?m)^  companyName: DefaultCompany\r?$' -or
    $settings -notmatch '(?m)^  productName: greed last\r?$') {
    throw 'The expected PlayerPrefs company/product changed; review the safety gate before running.'
}
$prefsPath = 'HKCU:\Software\DefaultCompany\greed last'
function Read-PersonalPreferences {
    $values = [ordered]@{}
    if (Test-Path -LiteralPath $prefsPath) {
        $key = Get-Item -LiteralPath $prefsPath
        foreach ($name in @($key.GetValueNames() | Sort-Object)) {
            if ($name.StartsWith('GreedLast.', [StringComparison]::Ordinal)) {
                $values[$name] = @{Kind=$key.GetValueKind($name).ToString(); Value=$key.GetValue($name)}
            }
        }
    }
    return $values
}

$before = Read-PersonalPreferences
$running = @(Get-Process -Name 'GreedLastPrototype' -ErrorAction SilentlyContinue)
if ($before.Count -gt 0 -or $running.Count -gt 0) {
    [ordered]@{Skipped=$true; Reason='Existing personal Greed values or player process; left untouched.';
        PersonalValues=$before.Count; ExistingPlayers=$running.Count} | ConvertTo-Json
    return
}
if (!$PSCmdlet.ShouldProcess($exe, 'Start an isolated headless 20-second check and stop only its own process')) { return }

$directory = Join-Path $project 'Logs\StandaloneChecks'
[void][System.IO.Directory]::CreateDirectory($directory)
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff', [Globalization.CultureInfo]::InvariantCulture)
$log = Join-Path $directory ('startup-' + $stamp + '.log')
$reportPath = Join-Path $directory ('report-startup-' + $stamp + '.json')
$report = [ordered]@{Scope='Headless Windows startup only, not rendered frames, audible output, hardware input or latency. GreedLast-namespaced registry values are read only.';
    StartedUtc=[DateTime]::UtcNow.ToString('O'); CompletedUtc=''; Succeeded=$false; Skipped=$false;
    VerifiedBuildUtc=([DateTimeOffset]$info.verifiedAtUtc).ToUniversalTime().ToString('O'); RuntimeAssemblySha256=$info.runtimeAssemblySha256;
    Log=$log; ProcessId=0; AliveFor20Seconds=$false; OwnProcessStopped=$false;
    PersonalValuesBefore=$before.Count; PersonalValuesAfter=-1; PersonalPreferencesUnchanged=$false;
    ExceptionLines=0; Error=''}
$player = $null
try {
    $player = Start-Process -FilePath $exe -ArgumentList @('-batchmode','-nographics','-logFile', ('"' + $log + '"')) -WindowStyle Hidden -PassThru
    $report.ProcessId = $player.Id
    $report.AliveFor20Seconds = !$player.WaitForExit(20000)
    if (!$report.AliveFor20Seconds) { throw 'The startup process exited before the 20-second observation completed.' }
}
catch { $report.Error = $_.Exception.Message }
finally {
    if ($null -ne $player) {
        try {
            $player.Refresh()
            if (!$player.HasExited) { $player.Kill(); [void]$player.WaitForExit(5000) }
            $player.Refresh()
            $report.OwnProcessStopped = $player.HasExited
        }
        catch { $report.Error += ' Own-process cleanup failed: ' + $_.Exception.Message }
        finally { $player.Dispose() }
    }
    try {
        $after = Read-PersonalPreferences
        $report.PersonalValuesAfter = $after.Count
        $report.PersonalPreferencesUnchanged = ($before | ConvertTo-Json -Depth 8 -Compress) -ceq ($after | ConvertTo-Json -Depth 8 -Compress)
        if (!(Test-Path -LiteralPath $log)) { throw 'The startup log was not produced.' }
        $logText = Get-Content -LiteralPath $log -Raw
        $report.ExceptionLines = [regex]::Matches($logText, '(?m)^[^\r\n]*(?:Exception(?=:|[\s(])|error CS\d+)[^\r\n]*$').Count
    }
    catch { $report.Error += ' Result collection failed: ' + $_.Exception.Message }
    $report.CompletedUtc = [DateTime]::UtcNow.ToString('O')
    $report.Succeeded = $report.AliveFor20Seconds -and $report.OwnProcessStopped -and
        $report.PersonalPreferencesUnchanged -and $report.ExceptionLines -eq 0 -and !$report.Error
    [System.IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 8))
}
[ordered]@{Report=$reportPath; Sha256=(Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash; Result=$report} | ConvertTo-Json -Depth 9
if (!$report.Succeeded) { throw 'Startup check failed; inspect its preserved report/log. Personal data was not restored or deleted.' }
