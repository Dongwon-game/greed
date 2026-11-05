[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReferenceProjectPath,
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe',
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$reference = (Resolve-Path -LiteralPath $ReferenceProjectPath).Path
$unity = (Resolve-Path -LiteralPath $UnityEditorPath).Path
$data = Join-Path (Split-Path $unity -Parent) 'Data'
$hostExe = Join-Path $data 'netcorerun/netcorerun.exe'
$compiler = Join-Path $data 'DotNetSdkRoslyn/csc.dll'
foreach ($path in @($hostExe,$compiler)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw 'The bundled C# compiler is unavailable.' }
}
foreach ($input in @('ProjectSettings/ProjectVersion.txt','Packages/manifest.json','Packages/packages-lock.json')) {
    if ((Get-FileHash -LiteralPath (Join-Path $project $input)).Hash -cne
        (Get-FileHash -LiteralPath (Join-Path $reference $input)).Hash) {
        throw ('Reference environment differs: ' + $input)
    }
}
if (@(Get-ChildItem -LiteralPath (Join-Path $project 'Assets') -Recurse -Filter '*.asmdef').Count) {
    throw 'Review assembly-specific compilation before using this runner with asmdefs.'
}
$candidates = @(Get-ChildItem -LiteralPath (Join-Path $reference 'Library/Bee/artifacts') -Recurse -Filter 'Assembly-CSharp.rsp')
function Read-Defines([string]$Path) {
    @(Get-Content -LiteralPath $Path | Where-Object { $_ -match '^[-/]define:' } | ForEach-Object {
        $_.Substring($_.IndexOf(':')+1).Trim('"') -split '[;,]' | ForEach-Object { $_.Trim() }
    })
}
$editorRsp = @($candidates | Where-Object { (Read-Defines $_.FullName) -contains 'UNITY_EDITOR' })
$playerRsp = @($candidates | Where-Object {
    $defines = Read-Defines $_.FullName
    $defines -contains 'UNITY_STANDALONE_WIN' -and $defines -notcontains 'UNITY_EDITOR' -and $defines -notcontains 'DEVELOPMENT_BUILD'
})
if ($editorRsp.Count -ne 1 -or $playerRsp.Count -ne 1) { throw 'Require one unambiguous editor and nondevelopment Windows reference response file.' }
$editorChecksRsp = Join-Path $editorRsp[0].DirectoryName 'Assembly-CSharp-Editor.rsp'
if (!(Test-Path -LiteralPath $editorChecksRsp)) { throw 'Editor check compilation references are missing.' }
$runtimeSources = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Scripts') -Recurse -Filter '*.cs' | Sort-Object FullName)
$checkSources = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Editor') -Recurse -Filter '*.cs' | Sort-Object FullName)
if (!$runtimeSources.Count -or !$checkSources.Count) { throw 'Require nonempty runtime and editor source sets; an empty assembly is not a project check.' }
if ($ValidateOnly) {
    [pscustomobject]@{Validated=$true;CompilerInvoked=$false;UnityInvoked=$false;
        PlannedCompilations=@('EditorRuntime','WindowsReleaseRuntime','EditorChecks')} | ConvertTo-Json -Compress
    return
}
$root = Join-Path $project ('Logs/SourceCompilationChecks/' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$sourceHashes = @($runtimeSources + $checkSources | ForEach-Object {
    @{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
$watch = @((Join-Path $reference 'ProjectSettings/ProjectSettings.asset'), (Join-Path $project 'ProjectSettings/ProjectSettings.asset'),
    $editorRsp[0].FullName,$playerRsp[0].FullName,$editorChecksRsp)
$before = @($watch | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
$report = [ordered]@{BaseHead=(git -C $project rev-parse HEAD).Trim();SourceTreeStatus=@(git -C $project status --porcelain -- Assets Packages ProjectSettings);Succeeded=$false;Error='';CompilerSHA256=(Get-FileHash -LiteralPath $compiler).Hash;RunnerSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;
    Scope='C# compilation against an existing matching Unity reference cache only. No Unity editor/player launch, UI, asset import, Play mode, build, save access or gameplay verification.';
    StartedUtc=[DateTime]::UtcNow.ToString('o');CompletedUtc='';Inputs=$sourceHashes;Compilations=@();WatchedInputsUnchanged=$false}
function Compile([string]$Name, [string]$RspPath, $Sources, [string]$AssemblyName, [string]$RuntimeRef='') {
    $output = Join-Path $root $Name
    [void][IO.Directory]::CreateDirectory($output)
    $arguments = [Collections.Generic.List[string]]::new()
    $runtimeReferenceCount = 0
    foreach ($line in (Get-Content -LiteralPath $RspPath)) {
        if ($line -match '^"Assets/.+\.cs"$' -or $line -match '^[-/](out|refout|errorlog|pdb|doc|generatedfilesout):') { continue }
        if ($line.StartsWith('@') -or $line -match '^[-/]recurse:') { throw 'Nested response files or recursive source inputs require review.' }
        if ($RuntimeRef -and $line -match '^[-/]r:"[^"]*/Assembly-CSharp\.ref\.dll"$') {
            $runtimeReferenceCount++
            $arguments.Add('-r:"' + $RuntimeRef.Replace('\','/') + '"')
        } else { $arguments.Add($line) }
    }
    if ($RuntimeRef -and $runtimeReferenceCount -ne 1) { throw 'Editor checks must reference exactly one newly compiled runtime assembly.' }
    $arguments.Add('-out:"' + (Join-Path $output ($AssemblyName+'.dll')).Replace('\','/') + '"')
    $arguments.Add('-refout:"' + (Join-Path $output ($AssemblyName+'.ref.dll')).Replace('\','/') + '"')
    $arguments.Add('-errorlog:"' + (Join-Path $output 'diagnostics.sarif').Replace('\','/') + '"')
    foreach ($file in $Sources) { $arguments.Add('"' + $file.FullName.Replace('\','/') + '"') }
    $response = Join-Path $output 'compile.rsp'
    [IO.File]::WriteAllLines($response,$arguments,[Text.UTF8Encoding]::new($false))
    Push-Location -LiteralPath $reference
    try { & $hostExe $compiler ('@'+$response) *> (Join-Path $output 'compiler.log'); $exitCode=$LASTEXITCODE }
    finally { Pop-Location }
    $report.Compilations += @{Name=$Name;ExitCode=$exitCode;SourceCount=@($Sources).Count;ReferenceRspSHA256=(Get-FileHash -LiteralPath $RspPath).Hash}
    if ($exitCode -ne 0) { throw ($Name+' C# compilation failed; inspect preserved diagnostics.') }
    foreach ($path in @((Join-Path $output ($AssemblyName+'.dll')), (Join-Path $output ($AssemblyName+'.ref.dll')))) {
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw ($Name+' returned without a usable compiled assembly.')
        }
    }
}
try {
    Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $root 'runner.ps1')
    Compile 'EditorRuntime' $editorRsp[0].FullName $runtimeSources 'Assembly-CSharp'
    Compile 'WindowsReleaseRuntime' $playerRsp[0].FullName $runtimeSources 'Assembly-CSharp'
    Compile 'EditorChecks' $editorChecksRsp $checkSources 'Assembly-CSharp-Editor' (Join-Path $root 'EditorRuntime/Assembly-CSharp.ref.dll')
    $report.Succeeded=$true
} catch { $report.Error=$_.Exception.Message }
finally {
    try {
        $after = @($watch | ForEach-Object { (Get-FileHash -LiteralPath $_).Hash })
        $sameSources = @($sourceHashes | Where-Object { (Get-FileHash -LiteralPath $_.Path).Hash -cne $_.SHA256 }).Count -eq 0
        $report.WatchedInputsUnchanged=($before -join ',') -ceq ($after -join ',') -and $sameSources
    } catch { $report.Error += ' Input preservation check failed: '+$_.Exception.Message }
    $report.Succeeded=$report.Succeeded -and $report.WatchedInputsUnchanged
    $report.CompletedUtc=[DateTime]::UtcNow.ToString('o')
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'report.json') -Encoding utf8
}
[pscustomobject]@{Report=(Join-Path $root 'report.json');Succeeded=$report.Succeeded;Error=$report.Error}|ConvertTo-Json -Compress
if (!$report.Succeeded) { throw 'Static C# checks did not pass. No Unity process was launched.' }
