[CmdletBinding()]
param([string]$ProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$probe = Get-Content -LiteralPath (Join-Path $ProjectPath 'Assets/Editor/GreedLastPlayerPersistenceProbe.cs.txt') -Raw
$method = [regex]::Match($probe, '(?s)        private static int ParseMenuWaitSeconds.*?(?=\r?\n        private static string Argument)').Value
if (!$method) { throw 'Require the actual probe argument parser.' }
$name = 'MenuWaitBudget_' + [guid]::NewGuid().ToString('N')
$source = 'using System; public static class ' + $name + ' {' + $method.Replace('private static int', 'public static int') + '}'
$type = Add-Type -TypeDefinition $source -PassThru
$key = '-greed-probe-menu-wait-minutes'
$cases = @(
    @{Args=@(); Phase='window-menu'; Expected=600},
    @{Args=@(); Phase='clear'; Expected=0},
    @{Args=@($key,'1'); Phase='window-menu'; Expected=60},
    @{Args=@($key,'30'); Phase='window-menu'; Expected=1800},
    @{Args=@($key,'60'); Phase='window-menu'; Expected=3600},
    @{Args=@($key,'0'); Phase='window-menu'; Expected=-1},
    @{Args=@($key,'61'); Phase='window-menu'; Expected=-1},
    @{Args=@($key,'-1'); Phase='window-menu'; Expected=-1},
    @{Args=@($key,'1.5'); Phase='window-menu'; Expected=-1},
    @{Args=@($key,' 30'); Phase='window-menu'; Expected=-1},
    @{Args=@($key); Phase='window-menu'; Expected=-1},
    @{Args=@($key,'1',$key,'60'); Phase='window-menu'; Expected=-1},
    @{Args=@($key,'30'); Phase='clear'; Expected=-1}
)
foreach ($case in $cases) {
    $actual = -1
    try { $actual = $type::ParseMenuWaitSeconds([string[]]$case.Args, $case.Phase) }
    catch { if ($case.Expected -ne -1) { throw } }
    if ($actual -ne $case.Expected) { throw 'Probe menu wait argument boundary failed.' }
}
$runner = Join-Path $ProjectPath 'tools/check-player-persistence.ps1'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($runner, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Runner must parse.' }
$assignments = @($ast.FindAll({param($node)
    $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -ceq '$timeout'
}, $true))
if ($assignments.Count -ne 1) { throw 'Require exactly one actual player process timeout assignment.' }
$budget = [scriptblock]::Create($assignments[0].Extent.Text + '; [int]$timeout')
$budgets = @(
    @{Phase='window-menu'; Minutes=1; Interactive=$true; Expected=690000},
    @{Phase='window-menu'; Minutes=30; Interactive=$true; Expected=2430000},
    @{Phase='window-menu'; Minutes=60; Interactive=$true; Expected=4230000},
    @{Phase='window-loadout'; Minutes=30; Interactive=$true; Expected=630000},
    @{Phase='window-play'; Minutes=30; Interactive=$true; Expected=360000},
    @{Phase='clear'; Minutes=30; Interactive=$false; Expected=180000}
)
foreach ($case in $budgets) {
    $phase=$case.Phase; $MenuWaitMinutes=$case.Minutes; $interactive=$case.Interactive
    if ((& $budget) -ne $case.Expected) { throw 'Actual process timeout does not cover preparation plus operator wait.' }
}
foreach ($value in @(0,61,30)) {
    $options = @{MenuWaitMinutes=$value; WhatIf=$true}
    $rejected = $false
    try { & $runner @options | Out-Null }
    catch {
        $rejected = if ($value -eq 30) { $_.Exception.Message -eq 'MenuWaitMinutes requires InteractiveMenus.' }
            else { $_.FullyQualifiedErrorId -like 'ParameterArgumentValidationError*' }
    }
    if (!$rejected) { throw ('Expected early menu wait rejection for: ' + $value) }
}
[pscustomobject]@{Succeeded=$true; ProbeArgumentCases=$cases.Count; ProcessBudgetCases=$budgets.Count; RunnerRejections=3;
    Scope='Actual parser and process-budget expression; no Unity, windows, preferences or gameplay.'} | ConvertTo-Json
