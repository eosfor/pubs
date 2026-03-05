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
$manifestPath = Join-Path $projectDir 'pubs.psd1'

if (-not (Test-Path $projectFullPath)) {
    throw "Project file not found: $projectFullPath"
}

if (-not (Test-Path $manifestPath)) {
    throw "Module manifest not found: $manifestPath"
}

$manifestData = Import-PowerShellDataFile -Path $manifestPath
$sourceVersion = $manifestData.ModuleVersion.ToString()

if (-not $Version) {
    $Version = $sourceVersion
}

$moduleName = [System.IO.Path]::GetFileNameWithoutExtension($manifestPath)

Write-Host "Building $ProjectPath ($Configuration, $Framework)..."
dotnet build $projectFullPath -c $Configuration /p:TargetFramework=$Framework | Out-Host

$buildDir = Join-Path $projectDir "bin/$Configuration/$Framework"
if (-not (Test-Path (Join-Path $buildDir 'SBPowerShell.dll'))) {
    throw "Build output not found at $buildDir. Ensure the build succeeded."
}

$targetDir = Join-Path $repoRoot "$OutputDir/$moduleName/$Version"
if (Test-Path $targetDir) {
    Remove-Item -Path $targetDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

# copy manifest first
Copy-Item -Path $manifestPath -Destination $targetDir -Force
$stagedManifestPath = Join-Path $targetDir "$moduleName.psd1"

# Align staged manifest version with requested package version.
if ($Version -ne $sourceVersion) {
    $manifestContent = Get-Content -Raw -Path $stagedManifestPath
    $updatedManifestContent = $manifestContent -replace "(?m)^(\s*ModuleVersion\s*=\s*)'[^']+'", "`$1'$Version'"
    if ($updatedManifestContent -eq $manifestContent) {
        throw "Unable to update ModuleVersion in staged manifest: $stagedManifestPath"
    }

    Set-Content -Path $stagedManifestPath -Value $updatedManifestContent -Encoding UTF8
}

# copy binaries and dependency payload
Get-ChildItem -Path $buildDir -File |
    Where-Object { $_.Extension -in '.dll', '.pdb', '.json', '.xml', '.ps1xml' } |
    ForEach-Object { Copy-Item -Path $_.FullName -Destination $targetDir -Force }

# copy localized external help if present (for Get-Help)
$localizedHelpDir = Join-Path $buildDir 'en-US'
if (Test-Path $localizedHelpDir) {
    Copy-Item -Path $localizedHelpDir -Destination $targetDir -Recurse -Force
}

Test-ModuleManifest -Path $stagedManifestPath | Out-Null

Write-Host "Module packed to $targetDir"
Write-Host "Import with: Import-Module '$targetDir/$moduleName.psd1'"
