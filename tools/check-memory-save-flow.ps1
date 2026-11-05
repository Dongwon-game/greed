#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CompilationReport,
    [Parameter(Mandatory=$true)][string]$UnityManagedPath,
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$reportPath = (Resolve-Path -LiteralPath $CompilationReport).Path
$compiled = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (!$compiled.Succeeded -or !$compiled.WatchedInputsUnchanged) { throw 'Require a successful preserved-input compilation.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Scripts') -Filter '*.cs' -Recurse)
$runtimeCompilation = @($compiled.Compilations | Where-Object {$_.Name -eq 'WindowsReleaseRuntime'})
if (!$sources.Count -or $runtimeCompilation.Count -ne 1 -or $runtimeCompilation[0].ExitCode -ne 0 -or
    $runtimeCompilation[0].SourceCount -ne $sources.Count) {
    throw 'Require the current complete Windows runtime source set; added or removed files need a new compilation.'
}
foreach ($source in $sources) {
    $inputEntry = @($compiled.Inputs | Where-Object {$_.Path -eq $source.FullName})
    if ($inputEntry.Count -ne 1 -or (Get-FileHash -LiteralPath $source.FullName).Hash -cne $inputEntry[0].SHA256) {
        throw ('Compiled source differs from current source: '+$source.Name)
    }
}
$assemblyPath = Join-Path (Split-Path $reportPath -Parent) 'WindowsReleaseRuntime/Assembly-CSharp.dll'
$context = [Runtime.Loader.AssemblyLoadContext]::new(('GreedMemorySave-'+[Guid]::NewGuid().ToString('N')),$true)
$libraries = @()
foreach ($name in @('UnityEngine.CoreModule.dll','UnityEngine.JSONSerializeModule.dll')) {
    $path = (Resolve-Path -LiteralPath (Join-Path $UnityManagedPath $name)).Path
    $libraries += @{Name=$name;SHA256=(Get-FileHash -LiteralPath $path).Hash}
    [void]$context.LoadFromAssemblyPath($path)
}
$assembly = $context.LoadFromAssemblyPath($assemblyPath)
$backendType = $assembly.GetType('GreedLast.GreedLastMockBackend',$true)
$recordType = $assembly.GetType('GreedLast.GreedLastRunRecord',$true)
$private = [Reflection.BindingFlags]'Instance,NonPublic'
$checks = [Collections.Generic.List[string]]::new()
function Check([string]$Name,[bool]$Pass) {
    if (!$Pass) {throw ('Memory backend check failed: '+$Name)}
    $checks.Add($Name)
}
function New-MemoryBackend {
    # Both explicit false arguments are essential. Never use the default constructor.
    $value = $backendType.GetConstructor([type[]]@([bool],[bool])).Invoke([object[]]@($false,$false))
    Check 'persistence disabled before use' (!$backendType.GetField('persistData',$private).GetValue($value))
    $value
}
function Call-Backend($Backend,[string]$Name,[object[]]$Arguments=@()) {
    if ($Name -notin @('LoadLobby','LoadSelectedSaveSlotView','RegisterClear','SelectSaveLoadoutSlot',
            'ConfirmSaveLoadout','DiscardSaveLoadoutCandidate','BuildInfiniteStartReadyBriefText','TryConsumeSelectedSaveLoadout')) {
        throw 'Only reviewed memory-only backend operations are allowed.'
    }
    if ($backendType.GetField('persistData',$private).GetValue($Backend)) {throw 'Refuse a persistent backend.'}
    $backendType.GetMethod($Name).Invoke($Backend,$Arguments)
}
function Remaining-Uses($Backend,[int]$Slot) {
    $backendType.GetField('saveLoadoutUsesRemaining',$private).GetValue($Backend)[$Slot]
}
function New-ClearRecord([int]$Score=1000) {
    $recordType.GetConstructors()[0].Invoke([object[]]@($true,$Score,3,5,0,8,10,2,0,[single]100,
        '메모리 검사 조합','생존 축','집중 축','사막의 잔상','생존 축 / 집중 축 / 사막의 잔상',''))
}
$failure = ''
try {
    $backend = New-MemoryBackend
    $lobby = Call-Backend $backend 'LoadLobby'
    Check 'new backend has three empty slots' ($lobby.SaveSlotOccupied.Count -eq 3 -and @($lobby.SaveSlotOccupied | Where-Object {$_}).Count -eq 0)
    Check 'new backend has no candidate or infinite entry' (!$lobby.SaveLoadoutCandidateAvailable -and !$lobby.InfiniteUnlocked)
    $empty = $recordType.GetProperty('Empty').GetValue($null)
    $lobby = Call-Backend $backend 'RegisterClear' @($empty)
    Check 'invalid clear cannot unlock saving' (!$lobby.SaveLoadoutUnlocked -and !$lobby.SaveLoadoutCandidateAvailable)
    $record = New-ClearRecord
    $lobby = Call-Backend $backend 'RegisterClear' @($record)
    Check 'clear creates candidate before infinite unlock' ($lobby.SaveLoadoutCandidateAvailable -and $lobby.SaveLoadoutUnlocked -and !$lobby.InfiniteUnlocked)
    [void](Call-Backend $backend 'SelectSaveLoadoutSlot' @(1))
    $lobby = Call-Backend $backend 'ConfirmSaveLoadout'
    Check 'confirm saves only the selected S2' (!$lobby.SaveSlotOccupied[0] -and $lobby.SaveSlotOccupied[1] -and !$lobby.SaveSlotOccupied[2])
    Check 'confirmed candidate is consumed once' (!$lobby.SaveLoadoutCandidateAvailable -and $lobby.InfiniteUnlocked)
    Check 'new saved slot has two uses' ((Remaining-Uses $backend 1) -eq 2)
    [void](Call-Backend $backend 'ConfirmSaveLoadout')
    [void](Call-Backend $backend 'DiscardSaveLoadoutCandidate')
    Check 'repeat candidate actions do not change slot uses' ((Remaining-Uses $backend 1) -eq 2)
    $brief = Call-Backend $backend 'BuildInfiniteStartReadyBriefText'
    [void](Call-Backend $backend 'LoadSelectedSaveSlotView')
    [void](Call-Backend $backend 'LoadLobby')
    Check 'readiness and menu reads do not consume uses' ((Remaining-Uses $backend 1) -eq 2)
    Check 'ready brief describes selected slot and start cost' ($brief.Contains('S2') -and $brief.Contains('시작 시 -1회'))
    $consume = [object[]]@($null,$null,$null,$null,$null)
    Check 'first actual consume succeeds' ([bool](Call-Backend $backend 'TryConsumeSelectedSaveLoadout' $consume))
    Check 'first consume reduces only selected slot to one use' ((Remaining-Uses $backend 1) -eq 1 -and (Remaining-Uses $backend 0) -eq 0 -and (Remaining-Uses $backend 2) -eq 0)
    Check 'saved direction keeps gifts and excludes relic' ($consume[0].StartGift -eq '생존 축' -and $consume[0].PrimaryGift -eq '집중 축' -and $consume[0].Relic -eq '')
    Check 'saved history also excludes run-local relic' (!$consume[0].ChoiceSummary.Contains('사막의 잔상'))
    Check 'normal clear source keeps its relic' ($record.Relic -eq '사막의 잔상')
    Check 'first consume reports cost and remaining last use' ($consume[1].Contains('이번 입장으로 1회 사용') -and $consume[1].Contains('마지막 1회') -and $consume[2] -eq '')
    [void](Call-Backend $backend 'ConfirmSaveLoadout')
    Check 'reopening resolved candidate cannot refill a used slot' ((Remaining-Uses $backend 1) -eq 1)
    $brief = Call-Backend $backend 'BuildInfiniteStartReadyBriefText'
    Check 'last-use brief warns about auto clear without consuming' ($brief.Contains('시작 후 자동 비움') -and (Remaining-Uses $backend 1) -eq 1)
    Check 'second actual consume succeeds' ([bool](Call-Backend $backend 'TryConsumeSelectedSaveLoadout' $consume))
    $startedRecord = $consume[0]
    $lobby = Call-Backend $backend 'LoadLobby'
    Check 'second start clears the depleted slot' (@($lobby.SaveSlotOccupied | Where-Object {$_}).Count -eq 0 -and (Remaining-Uses $backend 1) -eq 0)
    Check 'consumed start record remains valid after slot clear' ($startedRecord.IsValid -and $startedRecord.Score -eq 1000 -and $startedRecord.StartGift -eq '생존 축')
    Check 'empty slots require refill instead of infinite unlock' (!$lobby.InfiniteUnlocked -and $lobby.Message.Contains('보충'))
    Check 'second consume explicitly reports auto clear' ($consume[1].Contains('슬롯 자동 비움'))
    Check 'third start cannot consume an empty slot' (![bool](Call-Backend $backend 'TryConsumeSelectedSaveLoadout' $consume))
    Check 'failed consume returns no record and a reason' (!$consume[0].IsValid -and $consume[1] -eq '' -and ![string]::IsNullOrEmpty($consume[2]))
    $discarded = New-MemoryBackend
    [void](Call-Backend $discarded 'RegisterClear' @((New-ClearRecord)))
    [void](Call-Backend $discarded 'DiscardSaveLoadoutCandidate')
    $lobby = Call-Backend $discarded 'ConfirmSaveLoadout'
    Check 'explicitly discarded candidate cannot later be saved' (!$lobby.SaveLoadoutCandidateAvailable -and !$lobby.InfiniteUnlocked -and @($lobby.SaveSlotOccupied | Where-Object {$_}).Count -eq 0)
    foreach ($index in @(-1,3,[int]::MinValue,[int]::MaxValue)) {
        $lobby = Call-Backend $discarded 'SelectSaveLoadoutSlot' @($index)
        Check ('invalid slot clamps safely '+$index) ($lobby.SelectedSaveSlotIndex -ge 0 -and $lobby.SelectedSaveSlotIndex -lt 3)
    }
    $multiple = New-MemoryBackend
    [void](Call-Backend $multiple 'RegisterClear' @((New-ClearRecord)))
    [void](Call-Backend $multiple 'SelectSaveLoadoutSlot' @(1))
    [void](Call-Backend $multiple 'ConfirmSaveLoadout')
    [void](Call-Backend $multiple 'RegisterClear' @((New-ClearRecord 2000)))
    [void](Call-Backend $multiple 'SelectSaveLoadoutSlot' @(2))
    [void](Call-Backend $multiple 'ConfirmSaveLoadout')
    [void](Call-Backend $multiple 'SelectSaveLoadoutSlot' @(1))
    Check 'second candidate preserves the other saved slot' ((Remaining-Uses $multiple 1) -eq 2 -and (Remaining-Uses $multiple 2) -eq 2)
    $consume = [object[]]@($null,$null,$null,'연쇄 축','황금 박동')
    Check 'prepared start consumes selected slot once' ([bool](Call-Backend $multiple 'TryConsumeSelectedSaveLoadout' $consume) -and (Remaining-Uses $multiple 1) -eq 1 -and (Remaining-Uses $multiple 2) -eq 2)
    Check 'prepared start returns the saved source for engine preparation' ($consume[0].Score -eq 1000 -and $consume[0].Relic -eq '' -and !$consume[0].ChoiceSummary.Contains('연쇄 축'))
    Check 'prepared display is produced without rewriting stored gifts' ($consume[1].Contains('성향:') -and $consume[1].Contains('시작 상태:'))
    Check 'selected second use succeeds with another slot present' ([bool](Call-Backend $multiple 'TryConsumeSelectedSaveLoadout' $consume))
    $lobby = Call-Backend $multiple 'LoadLobby'
    Check 'depleted slot automatically selects remaining usable S3' (!$lobby.SaveSlotOccupied[1] -and $lobby.SaveSlotOccupied[2] -and $lobby.SelectedSaveSlotIndex -eq 2 -and $lobby.InfiniteUnlocked)
    Check 'auto selection alone preserves other slot uses' ((Remaining-Uses $multiple 2) -eq 2)
    Check 'next start uses the newly selected record' ([bool](Call-Backend $multiple 'TryConsumeSelectedSaveLoadout' $consume) -and $consume[0].Score -eq 2000 -and (Remaining-Uses $multiple 2) -eq 1)
} catch { $failure=$_.Exception.Message }
finally { $context.Unload() }
$output = Join-Path $project ('Logs/MemorySaveFlowChecks/'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
$result = [ordered]@{Succeeded=($failure -eq '');Error=$failure;CheckCount=$checks.Count;Checks=@($checks);CompilationReport=$reportPath;
    AssemblySHA256=(Get-FileHash -LiteralPath $assemblyPath).Hash;Libraries=$libraries;RunnerSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;
    Scope='Real backend in memory with loading and persistence explicitly disabled; selected reviewed methods and synthetic records only. Real Unity managed libraries, no Unity process, UI, native engine loop or disk-save/persistence verification.'}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding utf8
[pscustomobject]@{Succeeded=$result.Succeeded;CheckCount=$checks.Count;Report=(Join-Path $output 'report.json');Error=$failure} | ConvertTo-Json -Compress
if ($failure) {throw $failure}
