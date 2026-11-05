$ErrorActionPreference='Stop'
$project=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$root=Join-Path $project ('Logs/SourceCompilationInputChecks/'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$passed=0
function Fixture([string]$Name) {
    $dir=Join-Path $root $Name
    foreach($file in @('ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Packages/packages-lock.json')) {
        $target=Join-Path $dir $file
        [void][IO.Directory]::CreateDirectory((Split-Path $target -Parent))
        Copy-Item -LiteralPath (Join-Path $project $file) -Destination $target
    }
    foreach($mode in @('editor','player')) {
        [void][IO.Directory]::CreateDirectory((Join-Path $dir ('Library/Bee/artifacts/'+$mode)))
    }
    '-define:"UNITY_STANDALONE_WIN;UNITY_EDITOR"'|Set-Content (Join-Path $dir 'Library/Bee/artifacts/editor/Assembly-CSharp.rsp')
    '-target:library'|Set-Content (Join-Path $dir 'Library/Bee/artifacts/editor/Assembly-CSharp-Editor.rsp')
    '-define:"UNITY_STANDALONE_WIN"'|Set-Content (Join-Path $dir 'Library/Bee/artifacts/player/Assembly-CSharp.rsp')
    return $dir
}
function Reject([string]$Directory,[string]$Expected) {
    $message=''
    try { & "$PSScriptRoot/check-source-compilation.ps1" -ReferenceProjectPath $Directory -ValidateOnly | Out-Null }
    catch { $message=$_.Exception.Message }
    if(!$message.Contains($Expected)) { throw ('Unexpected acceptance or failure: '+$message) }
    $script:passed++
}
$valid=Fixture 'valid'
$result=& "$PSScriptRoot/check-source-compilation.ps1" -ReferenceProjectPath $valid -ValidateOnly|ConvertFrom-Json
if(!$result.Validated -or $result.CompilerInvoked -or $result.UnityInvoked -or $result.PlannedCompilations.Count -ne 3) {
    throw 'Validation must understand combined defines without invoking a compiler or Unity.'
}
$passed++
$debug=Fixture 'development-player'
'-define:"UNITY_STANDALONE_WIN;DEVELOPMENT_BUILD"'|Set-Content (Join-Path $debug 'Library/Bee/artifacts/player/Assembly-CSharp.rsp')
Reject $debug 'unambiguous editor and nondevelopment Windows'
$ambiguous=Fixture 'ambiguous'
$duplicate=Join-Path $ambiguous 'Library/Bee/artifacts/second-player'
[void][IO.Directory]::CreateDirectory($duplicate)
Copy-Item -LiteralPath (Join-Path $ambiguous 'Library/Bee/artifacts/player/Assembly-CSharp.rsp') -Destination $duplicate
Reject $ambiguous 'unambiguous editor and nondevelopment Windows'
$mismatch=Fixture 'package-mismatch'
'{}'|Set-Content (Join-Path $mismatch 'Packages/manifest.json')
Reject $mismatch 'Reference environment differs: Packages/manifest.json'
$missing=Fixture 'missing-editor-checks'
Remove-Item -LiteralPath (Join-Path $missing 'Library/Bee/artifacts/editor/Assembly-CSharp-Editor.rsp')
Reject $missing 'Editor check compilation references are missing'
[pscustomobject]@{Succeeded=$true;Cases=$passed;Scope='Synthetic read-only preflight fixtures; no compiler, Unity or UI invoked.'}|
    ConvertTo-Json|Set-Content (Join-Path $root 'report.json') -Encoding utf8
Get-Content (Join-Path $root 'report.json')
