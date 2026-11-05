$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$project=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$source=Join-Path $project 'Assets/Scripts/GreedLast/GreedLastPlayerInputPolicy.cs'
$sourceHash=(Get-FileHash -LiteralPath $source).Hash
# Compile the real dependency-free presentation policy; no Unity types or UI are loaded.
if ('GreedLast.GreedLastPlayerInputPolicy' -as [type]) {
    throw 'Run policy checks in a fresh PowerShell process; an existing GreedLast policy type could be stale.'
}
Add-Type -Path $source
$checks=0
function Require([bool]$Condition,[string]$Message) {
    if (!$Condition) { throw $Message }
    $script:checks++
}
foreach($dev in @($false,$true)) {
    foreach($mobile in @($false,$true)) {
        $keyboard=[GreedLast.GreedLastPlayerInputPolicy]::ShowKeyboardHints($dev,$mobile)
        $haptics=[GreedLast.GreedLastPlayerInputPolicy]::ShowHapticsSetting($dev,$mobile)
        if(!$dev -and !$mobile) {
            Require ($keyboard -and !$haptics) 'A PC player needs keyboard help but no unsupported vibration setting.'
        }
        if(!$dev -and $mobile) {
            Require (!$keyboard -and $haptics) 'A touch player must not inherit desktop keyboard help.'
        }
        if($dev) { Require ($keyboard -and $haptics) 'Development checks must retain both input presentations.' }
        foreach($paused in @($false,$true)) {
            foreach($countdown in @($false,$true)) {
                $label=[GreedLast.GreedLastPlayerInputPolicy]::PauseButtonLabel($paused,$countdown,$dev,$mobile)
                if($countdown) { Require ($label -eq '재개 준비') 'Countdown must not advertise an available pause/resume command.' }
                elseif($paused) { Require ($label -eq '재개') 'A paused run needs a resume action rather than a stale menu label.' }
                else { Require (($label.Contains('ESC/P')) -eq $keyboard) 'Live pause help must match the actual player input platform.' }
                Require ($label -notmatch 'F\d|Q/|E/|PgUp|PgDn') 'Player help must not expose development-only commands.'
            }
        }
        $resume=[GreedLast.GreedLastPlayerInputPolicy]::ResumeHint($dev,$mobile)
        $primary=[GreedLast.GreedLastPlayerInputPolicy]::PrimaryActionPrefix($dev,$mobile)
        Require ($resume.Contains('ESC/P') -eq $keyboard) 'Resume instructions lost the player keyboard capability.'
        $instruction=[GreedLast.GreedLastPlayerInputPolicy]::ResumeInstruction($dev,$mobile)
        Require ($instruction.Contains('ESC/P') -eq $keyboard) 'Pause header instructions lost the player keyboard capability.'
        Require ($instruction.Contains('카운트와 앞 박자 여유')) 'Resume help must preserve the countdown and safe lead-in contract.'
        Require ($primary.Contains('Enter') -eq $keyboard) 'Menu confirmation instructions lost the player keyboard capability.'
        foreach($paused in @($false,$true)) {
            $action=[GreedLast.GreedLastPlayerInputPolicy]::PauseActionLabel($paused,$dev,$mobile)
            $expected=if($paused){'재개'}else{'일시정지'}
            if($keyboard){$expected+=' (ESC/P)'}
            Require ($action -ceq $expected) 'The large pause/resume action must advertise the actual keyboard shortcut.'
        }
    }
}
$keys=@(0..2|ForEach-Object {[GreedLast.GreedLastPlayerInputPolicy]::ChannelKeyHint($_)})
Require ($keys[0] -eq 'A/←' -and $keys[1] -eq 'S/Space' -and $keys[2] -eq 'D/→') 'The three response channels must retain the actual supported keys in lane order.'
Require (@($keys|Select-Object -Unique).Count -eq 3) 'Channel hints must not duplicate another lane.'
foreach($invalid in @(-1,3,[int]::MinValue,[int]::MaxValue)) {
    Require ([GreedLast.GreedLastPlayerInputPolicy]::ChannelKeyHint($invalid) -eq '') 'An invalid lane must not advertise another channel key.'
}
Require ((Get-FileHash -LiteralPath $source).Hash -ceq $sourceHash) 'The policy source changed during the check.'
$root=Join-Path $project ('Logs/PlayerInputPolicyChecks/'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
$report=[ordered]@{Succeeded=$true;Checks=$checks;PolicySourceSHA256=$sourceHash;
    Scope='Real dependency-free C# presentation policy only; no Unity process, UI, PlayerPrefs or gameplay verification.'}
$report|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'report.json') -Encoding utf8
[pscustomobject]@{Report=(Join-Path $root 'report.json');Result=$report}|ConvertTo-Json -Depth 4
