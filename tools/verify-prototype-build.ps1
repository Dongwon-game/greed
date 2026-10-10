[CmdletBinding()]
param(
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$PackagePath,
    [string]$UnityEditorPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-StreamHash {
    param([System.IO.Stream]$Stream)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($Stream)).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Get-AllTypes {
    param($Types)
    foreach ($type in $Types) {
        $type
        Get-AllTypes $type.NestedTypes
    }
}

function Assert-DevToolPolicy {
    param($Method)
    $instructions = @($Method.Body.Instructions)
    $codes = @($instructions | ForEach-Object { $_.OpCode.Code.ToString() })
    if (!$Method.IsStatic -or $Method.ReturnType.FullName -ne 'System.Boolean' -or $Method.Parameters.Count -ne 2 -or
        $Method.Parameters[0].ParameterType.FullName -ne 'System.Boolean' -or $Method.Parameters[1].ParameterType.FullName -ne 'System.Boolean' -or
        ($codes -join ',') -cne 'Ldarg_0,Ldarg_1,Or,Ret') {
        throw 'Development policy must return isEditor OR isDebugBuild; update its explicit IL contract if the compiler changes.'
    }
}

function Assert-ModeGetter {
    param($Method)
    $instructions = @($Method.Body.Instructions)
    $expectedCalls = @(
        'System.Boolean UnityEngine.Application::get_isEditor()',
        'System.Boolean UnityEngine.Debug::get_isDebugBuild()',
        'System.Boolean GreedLast.GreedLastDevToolPolicy::ShouldEnable(System.Boolean,System.Boolean)'
    )
    if (!$Method.IsStatic -or $Method.ReturnType.FullName -ne 'System.Boolean' -or $Method.Parameters.Count -ne 0 -or
        $instructions.Count -ne 4 -or $instructions[3].OpCode.Code.ToString() -ne 'Ret') {
        throw ('Development mode getter must return only the shared runtime policy: ' + $Method.DeclaringType.Name)
    }
    for ($index = 0; $index -lt 3; $index++) {
        if ($instructions[$index].OpCode.Code.ToString() -ne 'Call' -or $instructions[$index].Operand.FullName -cne $expectedCalls[$index]) {
            throw ('Development mode getter inputs differ from actual editor/build state: ' + $Method.DeclaringType.Name)
        }
    }
}

function Assert-ConnectionGuard {
    param($Method)
    $instructions = @($Method.Body.Instructions)
    $backend = $Method.DeclaringType.Name -eq 'GreedLastMockBackend'
    $expected = if ($backend) { 'GreedLastDevToolPolicy::ShouldEnable' } else { 'GreedLastApp::get_DevToolsEnabled' }
    $gate = -1
    for ($index = 0; $index -lt $instructions.Count; $index++) {
        $instruction = $instructions[$index]
        if ($instruction.OpCode.Code.ToString() -in @('Call', 'Callvirt') -and $instruction.Operand.FullName.Contains($expected)) {
            $gate = $index
            break
        }
    }
    if ($gate -lt 0 -or $gate + 1 -ge $instructions.Count) { throw 'Development connection guard missing.' }
    # Inspect the false path, not just a call anywhere in the method.
    $branch = $instructions[$gate + 1]
    if ($backend) {
        $calls = @($instructions | Where-Object { $_.OpCode.Code.ToString() -in @('Call', 'Callvirt') } | ForEach-Object { $_.Operand.FullName })
        if (!($calls -match 'Application::get_isEditor') -or !($calls -match 'Debug::get_isDebugBuild') -or $branch.OpCode.Code.ToString() -notin @('Brtrue', 'Brtrue_S')) {
            throw 'Backend mode inputs or false-path branch missing.'
        }
        $falsePath = @($instructions | Where-Object { $_.Offset -gt $branch.Offset -and $_.Offset -lt $branch.Operand.Offset })
        if ($falsePath.Count -ne 3 -or $falsePath[0].OpCode.Code.ToString() -ne 'Ldarg_0' -or $falsePath[1].OpCode.Code.ToString() -ne 'Ldfld' -or $falsePath[1].Operand.Name -ne 'online' -or $falsePath[2].OpCode.Code.ToString() -ne 'Ret') {
            throw 'Nondevelopment backend must return its unchanged online state.'
        }
    }
    elseif ($branch.OpCode.Code.ToString() -notin @('Brfalse', 'Brfalse_S') -or $branch.Operand.OpCode.Code.ToString() -ne 'Ret') {
        throw 'Nondevelopment app must return before invoking the backend.'
    }
    $mutationsBeforeGate = @($instructions | Where-Object { $_.Offset -lt $branch.Offset -and $_.OpCode.Code.ToString() -in @('Stfld', 'Stsfld') })
    if ($mutationsBeforeGate.Count -ne 0) { throw 'Connection state can change before its development gate.' }
}

function Assert-SnapshotToolGuard {
    param($Method)
    $instructions = @($Method.Body.Instructions)
    $expectedCalls = @('Application::get_isEditor', 'Debug::get_isDebugBuild', 'GreedLastDevToolPolicy::ShouldEnable')
    for ($index = 0; $index -lt 3; $index++) {
        if ($instructions[$index].OpCode.Code.ToString() -ne 'Call' -or !$instructions[$index].Operand.FullName.Contains($expectedCalls[$index])) {
            throw ('Development snapshot tool mode inputs missing: ' + $Method.Name)
        }
    }
    $branch = $instructions[3]
    if ($branch.OpCode.Code.ToString() -notin @('Brtrue', 'Brtrue_S')) { throw 'Snapshot tool development branch missing.' }
    $falsePath = @($instructions | Where-Object { $_.Offset -gt $branch.Offset -and $_.Offset -lt $branch.Operand.Offset })
    $calls = @($falsePath | Where-Object { $_.OpCode.Code.ToString() -in @('Call', 'Callvirt') })
    $writes = @($falsePath | Where-Object { $_.OpCode.Code.ToString() -in @('Stfld', 'Stsfld') })
    if ($calls.Count -ne 1 -or !$calls[0].Operand.FullName.Contains('GreedLastMockBackend::LoadSelectedSaveSlotView()') -or $writes.Count -ne 0 -or $falsePath[-1].OpCode.Code.ToString() -ne 'Ret') {
        throw ('Nondevelopment snapshot tool must return without persistent mutation: ' + $Method.Name)
    }
}

function Assert-AppToolGuard {
    param($Method)
    $instructions = @($Method.Body.Instructions)
    if ($instructions[0].OpCode.Code.ToString() -ne 'Call' -or !$instructions[0].Operand.FullName.Contains('GreedLastApp::get_DevToolsEnabled()')) {
        throw ('App development tool must check its mode before state access: ' + $Method.Name)
    }
    $branch = $instructions[1]
    if ($branch.OpCode.Code.ToString() -in @('Brfalse', 'Brfalse_S')) {
        if ($branch.Operand.OpCode.Code.ToString() -ne 'Ret') { throw 'App tool false branch does not return.' }
    }
    elseif ($branch.OpCode.Code.ToString() -in @('Brtrue', 'Brtrue_S')) {
        $falsePath = @($instructions | Where-Object { $_.Offset -gt $branch.Offset -and $_.Offset -lt $branch.Operand.Offset })
        if ($falsePath.Count -ne 1 -or $falsePath[0].OpCode.Code.ToString() -ne 'Ret') { throw 'App tool false path can execute game commands.' }
    }
    else { throw 'App development tool branch missing.' }
}

function Assert-SaveResolutionGuards {
    param($Method, [string]$MutationName)
    $instructions = @($Method.Body.Instructions)
    $mutationSignature = 'System.String GreedLast.GreedLastMockBackend::' + $MutationName + '()'
    $mutations = @($instructions | Where-Object {
        $_.OpCode.Code.ToString() -in @('Call', 'Callvirt') -and $_.Operand.FullName -ceq $mutationSignature
    })
    if ($mutations.Count -ne 2 -or $Method.Body.ExceptionHandlers.Count -ne 0) {
        throw ('Save resolution mutation paths changed; update the explicit IL contract: ' + $Method.Name)
    }
    foreach ($mutation in $mutations) {
        $end = [Array]::IndexOf($instructions, $mutation)
        if ($end -lt 5) { throw ('Save resolution input cancellation missing: ' + $Method.Name) }
        $block = @($instructions[($end - 5)..$end])
        $codes = @($block | ForEach-Object { $_.OpCode.Code.ToString() })
        if (($codes -join ',') -cne 'Ldarg_0,Ldfld,Callvirt,Ldarg_0,Ldfld,Callvirt' -or
            $block[1].Operand.FullName -cne 'GreedLast.GreedLastRuntimeUi GreedLast.GreedLastApp::runtimeUi' -or
            $block[2].Operand.FullName -cne 'System.Void GreedLast.GreedLastRuntimeUi::CancelPendingUiInput()' -or
            $block[4].Operand.FullName -cne 'GreedLast.GreedLastMockBackend GreedLast.GreedLastApp::backend') {
            throw ('Save resolution must cancel its own UI input immediately before mutation: ' + $Method.Name)
        }
        # Reject a branch into the guarded block that could skip cancellation.
        foreach ($instruction in $instructions) {
            $operandType = $instruction.OpCode.OperandType.ToString()
            if ($operandType -notin @('InlineBrTarget', 'ShortInlineBrTarget', 'InlineSwitch')) { continue }
            foreach ($target in @($instruction.Operand)) {
                if ($target.Offset -gt $block[0].Offset -and $target.Offset -le $mutation.Offset) {
                    throw ('Save resolution branch bypasses input cancellation: ' + $Method.Name)
                }
            }
        }
    }
}

$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$root = Join-Path $project 'Builds\GreedLastPrototype'
$infoPath = Join-Path $root 'VerifiedBuildInfo.json'
$info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
$versionLine = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
if (@($versionLine).Count -ne 1) { throw 'Project editor version is ambiguous.' }
$version = $versionLine.Substring('m_EditorVersion: '.Length).Trim()
if ($info.unityVersion -ne $version -or $info.target -ne 'StandaloneWindows64' -or $info.smokePassed -isnot [bool] -or !$info.smokePassed -or $info.developmentBuild -isnot [bool] -or $info.developmentBuild) {
    throw 'Metadata does not identify a smoke-tested nondevelopment Windows build for this project.'
}
$verifiedAt = if ($info.verifiedAtUtc -is [DateTime] -or $info.verifiedAtUtc -is [DateTimeOffset]) {
    [DateTimeOffset]$info.verifiedAtUtc
}
else { [DateTimeOffset]::Parse([string]$info.verifiedAtUtc, [Globalization.CultureInfo]::InvariantCulture) }
if ($verifiedAt -gt [DateTimeOffset]::UtcNow.AddMinutes(5)) { throw 'Build metadata is dated in the future.' }
$files = @(
    @{ Path = 'GreedLastPrototype.exe'; PathField = 'executablePath'; HashField = 'executableSha256' },
    @{ Path = 'GreedLastPrototype_Data/Managed/Assembly-CSharp.dll'; PathField = 'runtimeAssemblyPath'; HashField = 'runtimeAssemblySha256' },
    @{ Path = 'NotoSansKR-OFL.txt'; PathField = 'bundledFontLicensePath'; HashField = 'bundledFontLicenseSha256' },
    @{ Path = 'NotoSansSymbols2-OFL.txt'; PathField = 'bundledSymbolFontLicensePath'; HashField = 'bundledSymbolFontLicenseSha256' },
    @{ Path = 'NotoSansKR-NOTICE.txt'; PathField = 'bundledFontNoticePath'; HashField = 'bundledFontNoticeSha256' }
)
foreach ($file in $files) {
    $expectedHash = [string]$info.($file.HashField)
    if ($info.($file.PathField) -cne $file.Path -or $expectedHash -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Unexpected metadata file path or digest.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root $file.Path) -Algorithm SHA256).Hash -ne $expectedHash) { throw ('Build digest mismatch: ' + $file.Path) }
}

if (!$UnityEditorPath) { $UnityEditorPath = Join-Path $env:ProgramFiles ('Unity\Hub\Editor\' + $version + '\Editor\Unity.exe') }
$cecil = Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $UnityEditorPath).Path) 'Data\Managed\Unity.Cecil.dll'
Add-Type -Path $cecil
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root $info.runtimeAssemblyPath))
try {
    $types = @(Get-AllTypes $assembly.MainModule.Types)
    $editorRefs = @($assembly.MainModule.AssemblyReferences | Where-Object { $_.Name -like 'UnityEditor*' })
    $editorTypes = @($types | Where-Object { $_.Namespace -eq 'GreedLast.Editor' -or $_.FullName -match 'GreedLast\.Editor\.' })
    $resetApis = @($types.Methods | Where-Object { $_.Name -in @('CreatePersistenceTestBackendForEditor', 'ClearPersistedProgressForEditor', 'ResetPrototypeProgressForEditor') })
    $connectionMethods = @($types.Methods | Where-Object { $_.Name -eq 'ToggleOfflineForEditorCheck' })
    if ($editorRefs.Count -ne 0 -or $editorTypes.Count -ne 0 -or $resetApis.Count -ne 0 -or $connectionMethods.Count -ne 2) { throw 'Unexpected release assembly surface.' }
    $policies = @($types.Methods | Where-Object { $_.DeclaringType.FullName -eq 'GreedLast.GreedLastDevToolPolicy' -and $_.Name -eq 'ShouldEnable' })
    $modeGetters = @($types.Methods | Where-Object { $_.DeclaringType.FullName -in @('GreedLast.GreedLastApp', 'GreedLast.GreedLastRuntimeUi') -and $_.Name -eq 'get_DevToolsEnabled' })
    if ($policies.Count -ne 1 -or $modeGetters.Count -ne 2) { throw 'Development policy/getter surface changed; update its explicit verification contract.' }
    Assert-DevToolPolicy $policies[0]
    foreach ($method in $modeGetters) { Assert-ModeGetter $method }
    foreach ($method in $connectionMethods) { Assert-ConnectionGuard $method }
    $snapshotNames = @('AddSampleSaveLoadoutForTest', 'AddSampleClearCandidateForTest', 'ConsumeSelectedSaveLoadoutUseForTest', 'AddGrowthPointsForTest', 'MaxGrowthForTest', 'ResetGrowthForTest')
    $snapshotTools = @($types.Methods | Where-Object { $_.DeclaringType.Name -eq 'GreedLastMockBackend' -and $_.Name -in $snapshotNames })
    $appNames = @('ToggleRunInvincible', 'RequestInfiniteTestStop', 'RequestFocusMaxForTest', 'RequestThreatUpForTest', 'RequestThreatDownForTest', 'RequestRunDevShortcut')
    $appTools = @($types.Methods | Where-Object { $_.DeclaringType.Name -eq 'GreedLastApp' -and $_.Name -in $appNames })
    if ($snapshotTools.Count -ne 6 -or $appTools.Count -ne 6) { throw 'Development tool surface changed; update its explicit verification contract.' }
    foreach ($method in $snapshotTools) { Assert-SnapshotToolGuard $method }
    foreach ($method in $appTools) { Assert-AppToolGuard $method }
    $saveResolutions = @($types.Methods | Where-Object {
        $_.DeclaringType.FullName -eq 'GreedLast.GreedLastApp' -and $_.Name -in @('RequestRenameSaveLoadoutSlot', 'RequestDeleteSaveLoadoutSlot')
    })
    if ($saveResolutions.Count -ne 2) { throw 'Save resolution command surface changed.' }
    foreach ($method in $saveResolutions) {
        $mutationName = if ($method.Name -eq 'RequestRenameSaveLoadoutSlot') { 'RenameSelectedSaveLoadoutSlot' } else { 'DeleteSelectedSaveLoadoutSlot' }
        Assert-SaveResolutionGuards $method $mutationName
    }
}
finally { $assembly.Dispose() }

$package = $null
if ($PackagePath) {
    $zipPath = (Resolve-Path -LiteralPath $PackagePath).Path
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            if (!$names.Add($name) -or $name -match '^/|(^|/)\.\.?(/|$)|:' -or $name -match '(^|/)(Assets|Logs|Library|Docs|\.git)/|\.(cs|log)$') { throw ('Unexpected archive entry: ' + $name) }
        }
        foreach ($file in $files) {
            $entry = $archive.GetEntry($file.Path)
            if (!$entry) { throw ('Archive file missing: ' + $file.Path) }
            $stream = $entry.Open()
            try { $digest = Get-StreamHash $stream } finally { $stream.Dispose() }
            if ($digest -ne $info.($file.HashField)) { throw ('Archive digest mismatch: ' + $file.Path) }
        }
        $metadataEntry = $archive.GetEntry('VerifiedBuildInfo.json')
        if (!$metadataEntry -or !$archive.GetEntry('UnityPlayer.dll')) { throw 'Archive metadata or Unity runtime missing.' }
        $stream = $metadataEntry.Open()
        try { $metadataDigest = Get-StreamHash $stream } finally { $stream.Dispose() }
        if ($metadataDigest -ne (Get-FileHash -LiteralPath $infoPath -Algorithm SHA256).Hash) { throw 'Archive metadata belongs to a different build.' }
        $buildFiles = @(Get-ChildItem -LiteralPath $root -File -Recurse)
        $archiveFiles = @($archive.Entries | Where-Object { !$_.FullName.EndsWith('/') })
        if ($buildFiles.Count -ne $archiveFiles.Count) { throw 'Archive file count differs from the current build.' }
        foreach ($buildFile in $buildFiles) {
            $relative = $buildFile.FullName.Substring($root.Length + 1).Replace('\', '/')
            $entry = $archive.GetEntry($relative)
            if (!$entry -or $entry.Length -ne $buildFile.Length) { throw ('Archive dependency missing or truncated: ' + $relative) }
            $stream = $entry.Open()
            try { $digest = Get-StreamHash $stream } finally { $stream.Dispose() }
            if ($digest -ne (Get-FileHash -LiteralPath $buildFile.FullName -Algorithm SHA256).Hash) { throw ('Archive dependency digest mismatch: ' + $relative) }
        }
        $package = [ordered]@{ Path = $zipPath; Entries = $archive.Entries.Count; VerifiedFiles = $buildFiles.Count; Bytes = (Get-Item -LiteralPath $zipPath).Length; Sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash }
    }
    finally { $archive.Dispose() }
}

[ordered]@{
    VerifiedAtUtc = $verifiedAt.ToUniversalTime().ToString('O')
    UnityVersion = $version
    VerifiedFileDigests = $files.Count
    RuntimeAssemblySha256 = $info.runtimeAssemblySha256
    EditorReferences = $editorRefs.Count
    EditorTestTypes = $editorTypes.Count
    EditorResetApis = $resetApis.Count
    VerifiedDevelopmentPolicies = $policies.Count
    VerifiedDevelopmentModeGetters = $modeGetters.Count
    GuardedDevelopmentConnections = $connectionMethods.Count
    GuardedDevelopmentSnapshotTools = $snapshotTools.Count
    GuardedDevelopmentRunCommands = $appTools.Count
    GuardedSaveConfirmationMutations = $saveResolutions.Count * 2
    Package = $package
    Scope = 'Read-only artifact/IL checks. Does not run the player, modify progress, upload files or measure hardware performance.'
} | ConvertTo-Json -Depth 4
