[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviousAssemblyPath,
    [string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path,
    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$checker = Join-Path $project 'tools/verify-prototype-build.ps1'
$paths = @((Join-Path $project 'Builds/GreedLastPrototype/GreedLastPrototype_Data/Managed/Assembly-CSharp.dll'),
    (Resolve-Path -LiteralPath $PreviousAssemblyPath).Path)
function Hash-Bytes([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-','') }
    finally { $sha.Dispose() }
}
$checkerBytes = [IO.File]::ReadAllBytes($checker)
$checkerHash = Hash-Bytes $checkerBytes
$tokens = $null; $errors = $null
$source = [Text.Encoding]::UTF8.GetString($checkerBytes).TrimStart([char]0xFEFF)
$ast = [Management.Automation.Language.Parser]::ParseInput($source,[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw 'The artifact verifier must parse before extracting its contract.' }
$definitions = @($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
    $node.Name -ceq 'Assert-MenuWheelFrameGuard'},$true))
if ($definitions.Count -ne 1) { throw 'Require exactly the actual wheel artifact contract.' }
Invoke-Expression $definitions[0].Extent.Text
Add-Type -Path (Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $UnityEditorPath).Path) 'Data/Managed/Unity.Cecil.dll')
$inputs = @($paths | ForEach-Object {
    $bytes = [IO.File]::ReadAllBytes($_)
    [pscustomobject]@{Path=$_; Bytes=$bytes; SHA256=(Hash-Bytes $bytes)}
})
$id = [Guid]::NewGuid().ToString('N')
$root = Join-Path $project ('Logs/WheelArtifactChecks/' + $id)
if (Test-Path -LiteralPath $root) { throw 'Preserve any existing artifact check.' }
[void][IO.Directory]::CreateDirectory($root)
$cases = [Collections.Generic.List[object]]::new()
$report = [ordered]@{Id=$id; StartedUtc=[DateTime]::UtcNow.ToString('O'); CompletedUtc=''; Succeeded=$false;
    CheckerSHA256=$checkerHash; Inputs=@($inputs | Select-Object Path,SHA256); Cases=$cases; Error='';
    CheckerUnchanged=$false; BinaryInputsUnchanged=$false;
    Scope='Read-only actual DLL snapshots and in-memory Cecil mutations of the real verifier contract. No assembly execution/serialization, Unity/player launch, desktop input/capture or preference access; not a gameplay/device test.'}
try {
    foreach ($name in @('current','previous-release','playing-branch','frame-branch','cancel-field','disable-cancel','base-call')) {
        $inputIndex = if ($name -ceq 'previous-release') { 1 } else { 0 }
        $stream = [IO.MemoryStream]::new($inputs[$inputIndex].Bytes,$false)
        $assembly = $null
        try {
            $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($stream)
            $types = @($assembly.MainModule.Types | Where-Object FullName -CEQ 'GreedLast.GreedLastMenuScrollRect')
            if ($types.Count -ne 1) { throw 'Artifact fixture type missing.' }
            $methods = @($types[0].Methods)
            if ($inputIndex -eq 0) {
                $wheel = @($methods | Where-Object Name -CEQ 'OnScroll')[0]
                $cancel = @($methods | Where-Object Name -CEQ 'CancelPendingGestures')[0]
                $disable = @($methods | Where-Object Name -CEQ 'OnDisable')[0]
                switch ($name) {
                    'playing-branch' { $wheel.Body.Instructions[1].Operand = $wheel.Body.Instructions[6] }
                    'frame-branch' { $wheel.Body.Instructions[5].OpCode = [Mono.Cecil.Cil.OpCodes]::Beq_S }
                    'cancel-field' { $cancel.Body.Instructions[4].Operand = @($types[0].Fields | Where-Object Name -CEQ 'activePointer')[0] }
                    'disable-cancel' { $disable.Body.Instructions[1].OpCode = [Mono.Cecil.Cil.OpCodes]::Nop; $disable.Body.Instructions[1].Operand = $null }
                    'base-call' { $wheel.Body.Instructions[9].Operand = $wheel }
                }
            }
            $accepted = $false; $reason = ''
            try { Assert-MenuWheelFrameGuard $methods; $accepted = $true }
            catch {
                $reason = $_.Exception.Message
                if (!$reason.StartsWith('Menu wheel ',[StringComparison]::Ordinal)) { throw }
            }
            $expected = $name -ceq 'current'
            $cases.Add([pscustomobject]@{Name=$name; ExpectedAccepted=$expected; Accepted=$accepted; Passed=($expected -eq $accepted); Reason=$reason})
            if ($expected -ne $accepted) { throw ('Artifact wheel contract control failed: ' + $name) }
        }
        finally { if ($null -ne $assembly) { $assembly.Dispose() }; $stream.Dispose() }
    }
    $report.Succeeded = $true
}
catch { $report.Error = $_.Exception.ToString() }
finally {
    $report.CheckerUnchanged = $checkerHash -ceq (Get-FileHash -LiteralPath $checker).Hash
    $report.BinaryInputsUnchanged = @($inputs | Where-Object { $_.SHA256 -cne (Get-FileHash -LiteralPath $_.Path).Hash }).Count -eq 0
    $report.Succeeded = $report.Succeeded -and $report.CheckerUnchanged -and $report.BinaryInputsUnchanged
    $report.CompletedUtc = [DateTime]::UtcNow.ToString('O')
    $reportPath = Join-Path $root 'report.json'
    [IO.File]::WriteAllText($reportPath,($report | ConvertTo-Json -Depth 7),[Text.UTF8Encoding]::new($false))
}
[ordered]@{Report=$reportPath; SHA256=(Get-FileHash -LiteralPath $reportPath).Hash; Succeeded=$report.Succeeded;
    Cases=$cases.Count; CheckerUnchanged=$report.CheckerUnchanged; BinaryInputsUnchanged=$report.BinaryInputsUnchanged; Error=$report.Error} | ConvertTo-Json
if (!$report.Succeeded) { throw 'The read-only artifact controls did not complete; preserve their report.' }
