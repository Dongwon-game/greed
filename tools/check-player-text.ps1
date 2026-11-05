[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CompilationReport,
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
)

# Execute only reviewed, string-only static methods. Never construct the backend,
# RunCore or UI: their constructors/native helpers can access Unity or saves.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$reportPath = (Resolve-Path -LiteralPath $CompilationReport).Path
$compiled = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if (!$compiled.Succeeded -or !$compiled.WatchedInputsUnchanged) { throw 'Require a successful preserved-input compilation.' }
$current = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Scripts') -Filter '*.cs' -Recurse)
foreach ($source in $current) {
    $matches = @($compiled.Inputs | Where-Object { $_.Path -eq $source.FullName })
    if ($matches.Count -ne 1 -or (Get-FileHash -LiteralPath $source.FullName).Hash -cne $matches[0].SHA256) {
        throw ('Compiled source differs from current source: '+$source.Name)
    }
}
$assemblyPath = Join-Path (Split-Path $reportPath -Parent) 'WindowsReleaseRuntime/Assembly-CSharp.dll'
$assemblyHash = (Get-FileHash -LiteralPath $assemblyPath).Hash
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$backend = $assembly.GetType('GreedLast.GreedLastMockBackend',$true)
$flags = [Reflection.BindingFlags]'Static,NonPublic,Public'
$checks = [Collections.Generic.List[string]]::new()
function Check([string]$Name,[bool]$Pass) {
    if (!$Pass) { throw ('Player text check failed: '+$Name) }
    $checks.Add($Name)
}
function Invoke-Text([string]$Name,[object[]]$Arguments) {
    if ($Name -notin @('BuildPrototypeInteractionChecklistTextForPlatform','FormatProfileNextActionTextForPlatform','CompactRecordBoardLine')) {
        throw 'Only the reviewed string-only methods are allowed.'
    }
    $method = $backend.GetMethod($Name,$flags)
    if (!$method) { throw ('Missing text method: '+$Name) }
    [string]$method.Invoke($null,$Arguments)
}
$failure = ''
try {
foreach ($dev in @($false,$true)) {
    foreach ($mobile in @($false,$true)) {
        $mode = "dev=$dev mobile=$mobile"
        $text = Invoke-Text 'BuildPrototypeInteractionChecklistTextForPlatform' @($dev,'準備∞','保存✓',$mobile)
        Check "$mode preserves supplied progress" ($text.Contains('準備∞') -and $text.Contains('保存✓'))
        Check "$mode completion loop" ($text.Contains('일반→저장→무한→기록'))
        Check "$mode ten-line budget" (($text -split "`n").Count -le 10)
        Check "$mode line width budget" (@($text -split "`n" | Where-Object {$_.Length -gt 92}).Count -eq 0)
        Check "$mode development keys" ($text.Contains('F1 무적') -eq $dev)
        Check "$mode sensory capabilities" ($text.Contains('진동') -eq ($dev -or $mobile))
        if (!$dev -and !$mobile) {
            foreach ($key in @('A/←','S/Space','D/→','ESC/P','Enter:')) { Check "PC release $key" ($text.Contains($key)) }
            foreach ($wrong in @('모바일','터치','진동','F1 무적','PgUp')) { Check "PC release omits $wrong" (!$text.Contains($wrong)) }
        }
        $prefix = if ($dev -or !$mobile) {'Enter/강조'} else {'강조 버튼'}
        Check "$mode next action" ((Invoke-Text 'FormatProfileNextActionTextForPlatform' @('일반모드','다음 구간',$dev,$mobile)) -ceq "$($prefix): 일반모드 - 다음 구간")
        Check "$mode empty action" ((Invoke-Text 'FormatProfileNextActionTextForPlatform' @('일반모드','',$dev,$mobile)) -ceq "$($prefix): 일반모드")
        Check "$mode no highlight" ((Invoke-Text 'FormatProfileNextActionTextForPlatform' @('','다음 구간',$dev,$mobile)) -ceq '다음 구간')
        Check "$mode long action bounded" ((Invoke-Text 'FormatProfileNextActionTextForPlatform' @('일반모드',('가'*100),$dev,$mobile)).EndsWith(('가'*63)+'...'))
    }
}
$records = @(
    @('[S] 1000점 / PC 조작: A/← · S/Space · D/→ / ESC/P 정지 / 콤9','[S] 1000점 / 콤9'),
    @('PC 조작: A/← · S/Space · D/→','기록 없음'),
    @('[S] 1120점 / 검증 모드: 에디터 테스트키 ON / 체크 6/7 / Enter/강조: 무한모드 / 콤10','[S] 1120점 / 콤10'),
    @('72.0m / 3구간 / 위협 4 / 콤9 / 실수 1','기록 없음'),
    @('[S] 1240점 / 복기: 무한 1구간 3/3 / 정확 2 · 보정 1 · 실수 0 / 콤11','[S] 1240점 / 콤11'),
    @('[A] 760점 / ESC/P 또는 재개 / ESC 또는 P / 재개 버튼 안내 / 앞 박자 여유 / 카운트 2 / 재개 1.0s / 콤3','[A] 760점 / 콤3'),
    @('[A] 777점 / 가이드 좌 점프 / 상태 동작 ↑ / 코칭 회피 점프 / 재개 목표 카운트 뒤 첫 함정 / 중앙/레인 큐+상태줄·코치 확인: 위험→동작·키·1박·보상 / 일시정지 재개목표·배경차단 / Enter/강조: 저장 조합 / 보정: +35ms / 감각: 소리 80%·진동 ON / 콤4','[A] 777점 / 콤4'),
    @('[A] 860점 / 최근 좌정·바→점 > 중압·상→숙 / 흐름: 피하기 확인 / 콤4','[A] 860점 / 콤4'),
    @('1위 / 72.0m / 3구간 / 콤9','기록 없음'),
    @('[S] 1000점 / PC 조작: A/← · S/Space · D/→ / ESC/P 정지 / 소리% / 키·클릭 / 콤9','[S] 1000점 / 콤9'),
    @('', '기록 없음')
)
$index = 0
foreach ($case in $records) {
    Check ('record cleanup '+(++$index)) ((Invoke-Text 'CompactRecordBoardLine' @($case[0])) -ceq $case[1])
}
# Reuse only literal input/expected pairs from existing Unity regression checks.
# Parse data, never evaluate C# or call any other smoke-check method under .NET.
$smokePath = Join-Path $project 'Assets/Editor/GreedLastPrototypeSmokeCheck.cs'
$smokeInput = @($compiled.Inputs | Where-Object { $_.Path -eq $smokePath })
if ($smokeInput.Count -ne 1 -or (Get-FileHash -LiteralPath $smokePath).Hash -cne $smokeInput[0].SHA256) {
    throw 'Existing record regression inputs differ from the compilation snapshot.'
}
$literal = '"(?:\\.|[^"\\])*"'
$pattern = 'string\s+(?<name>\w+)\s*=\s*InvokeMockBackendStaticLabel\(\s*"CompactRecordBoardLine",\s*(?<input>'+$literal+')\s*\);\s*Require\(\s*\k<name>\s*==\s*(?<expected>'+$literal+')'
$pairs = [regex]::Matches((Get-Content -LiteralPath $smokePath -Raw),$pattern)
Check 'existing record regression dataset available' ($pairs.Count -ge 250)
foreach ($pair in $pairs) {
    [string]$inputText = ConvertFrom-Json -InputObject $pair.Groups['input'].Value
    [string]$expectedText = ConvertFrom-Json -InputObject $pair.Groups['expected'].Value
    Check ('existing record '+$pair.Groups['name'].Value) ((Invoke-Text 'CompactRecordBoardLine' @($inputText)) -ceq $expectedText)
}
Check 'assembly unchanged' ((Get-FileHash -LiteralPath $assemblyPath).Hash -ceq $assemblyHash)
} catch { $failure = $_.Exception.Message }
$output = Join-Path $project ('Logs/PlayerTextChecks/'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
$result = [ordered]@{Succeeded=($failure -eq '');Error=$failure;CheckCount=$checks.Count;Checks=@($checks);AssemblySHA256=$assemblyHash;CompilationReport=$reportPath;RunnerSHA256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;Scope='Reviewed static string-only backend methods under .NET. No Unity process, native engine, UI, backend instance, save access or gameplay validation.'}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding utf8
[pscustomobject]@{Succeeded=$result.Succeeded;CheckCount=$checks.Count;Report=(Join-Path $output 'report.json');Error=$failure} | ConvertTo-Json -Compress
if ($failure) { throw $failure }
