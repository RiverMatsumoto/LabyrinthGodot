param(
    [Parameter(Mandatory = $true)]
    [string] $TargetDir,

    [string] $LogicName,
    [string] $StateName,
    [string[]] $States = @("Idle"),
    [string] $Namespace = "Labyrinth",
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-PascalCase([string] $Value) {
    $parts = @($Value -split "[^A-Za-z0-9]+" | Where-Object { $_ -ne "" })
    if ($parts.Count -eq 0) {
        throw "Could not derive a name from '$Value'."
    }

    return ($parts | ForEach-Object {
        if ($_.Length -eq 1) {
            $_.ToUpperInvariant()
        }
        else {
            $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1)
        }
    }) -join ""
}

function Write-ScaffoldFile([string] $Path, [string] $Content) {
    if ((Test-Path -LiteralPath $Path) -and -not $Force) {
        Write-Host "Skipped existing file: $Path"
        return
    }

    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
    Write-Host "Wrote: $Path"
}

$resolvedTarget = $TargetDir.TrimEnd("\", "/")
$featureName = Split-Path -Leaf $resolvedTarget
$featurePascal = ConvertTo-PascalCase $featureName

if ([string]::IsNullOrWhiteSpace($LogicName)) {
    $LogicName = "${featurePascal}Logic"
}

$stateFileBase = "${LogicName}State"

if ([string]::IsNullOrWhiteSpace($StateName)) {
    $StateName = if ($featureName -eq "game" -and $LogicName -eq "GameLogic") {
        "GameState"
    }
    else {
        $stateFileBase
    }
}

$stateNames = @(($States -join ",") -split "," |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { ConvertTo-PascalCase $_.Trim() })

if ($stateNames.Count -eq 0) {
    throw "At least one state name is required."
}

$logicPath = Join-Path $resolvedTarget "$LogicName.cs"
$stateDir = Join-Path $resolvedTarget "state"
$statesDir = Join-Path $stateDir "states"
$baseStatePath = Join-Path $stateDir "$stateFileBase.cs"
$inputPath = Join-Path $stateDir "${stateFileBase}Input.cs"
$outputPath = Join-Path $stateDir "${stateFileBase}Output.cs"

$setLines = ($stateNames | ForEach-Object { "        Set(new $StateName.$_());" }) -join [Environment]::NewLine

Write-ScaffoldFile $logicPath @"
namespace $Namespace;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public interface I$LogicName : ILogicBlock;

[Meta]
public partial class $LogicName : LogicBlock, I$LogicName
{
    public $LogicName()
    {
$setLines
    }
}
"@

Write-ScaffoldFile $baseStatePath @"
namespace $Namespace;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, StateDiagram]
public abstract partial record $StateName : LogicBlockState;
"@

Write-ScaffoldFile $inputPath @"
namespace $Namespace;

public partial record $StateName
{
    public static class Input
    {

    }
}
"@

Write-ScaffoldFile $outputPath @"
namespace $Namespace;

public partial record $StateName
{
    public static class Output
    {

    }
}
"@

foreach ($state in $stateNames) {
    $statePath = Join-Path $statesDir "$state.cs"
    Write-ScaffoldFile $statePath @"
namespace $Namespace;

using Chickensoft.LogicBlocks;

public partial record $StateName
{
    public record $state : $StateName
    {
        public $state()
        {

        }
    }
}
"@
}

Write-Host ""
Write-Host "Scaffolded $LogicName in $resolvedTarget"
