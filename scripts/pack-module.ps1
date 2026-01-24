param(
    [string]$Configuration = 'Release',
    [string]$Framework = 'net8.0',
    [string]$ProjectPath = 'src/SBPowerShell/SBPowerShell.csproj',
    [string]$OutputDir = 'out',
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectFullPath = Join-Path $repoRoot $ProjectPath
$projectDir = Split-Path $projectFullPath -Parent
$manifestPath = Join-Path $projectDir 'SBPowerShell.psd1'

if (-not (Test-Path $projectFullPath)) {
    throw "Project file not found: $projectFullPath"
}

if (-not $Version) {
    $manifestData = Import-PowerShellDataFile -Path $manifestPath
    $Version = $manifestData.ModuleVersion.ToString()
}

Write-Host "Building $ProjectPath ($Configuration, $Framework)..."
dotnet build $projectFullPath -c $Configuration /p:TargetFramework=$Framework | Out-Host

$buildDir = Join-Path $projectDir "bin/$Configuration/$Framework"
if (-not (Test-Path (Join-Path $buildDir 'SBPowerShell.dll'))) {
    throw "Build output not found at $buildDir. Ensure the build succeeded."
}

$targetDir = Join-Path $repoRoot "$OutputDir/SBPowerShell/$Version"
if (Test-Path $targetDir) {
    Remove-Item -Path $targetDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

# copy manifest first
Copy-Item -Path $manifestPath -Destination $targetDir -Force

# copy binaries and dependency payload
Get-ChildItem -Path $buildDir -File |
    Where-Object { $_.Extension -in '.dll', '.pdb', '.json' } |
    ForEach-Object { Copy-Item -Path $_.FullName -Destination $targetDir -Force }

Test-ModuleManifest -Path (Join-Path $targetDir 'SBPowerShell.psd1') | Out-Null

Write-Host "Module packed to $targetDir"
Write-Host "Import with: Import-Module '$targetDir/SBPowerShell.psd1'"
