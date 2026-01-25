$ErrorActionPreference = 'Stop'

# Loads connection settings from .env and sends 100 messages to test-topic (subscription test-sub).

function Get-EnvValue([string]$Key, [string]$Default = $null) {
    $envPath = Join-Path (Get-Location) '.env'
    if (-not (Test-Path $envPath)) {
        throw ".env not found; expected $Key"
    }

    $line = Get-Content $envPath | Where-Object { $_ -like "$Key=*" } | Select-Object -First 1
    if ($line) {
        $value = $line.Substring($Key.Length + 1)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return $Default
}

function Get-ConnectionString {
    $sas = Get-EnvValue 'SAS_KEY_VALUE'
    if ([string]::IsNullOrWhiteSpace($sas)) {
        throw "SAS_KEY_VALUE missing in .env"
    }

    $emulatorHost = Get-EnvValue 'EMULATOR_HOST' 'localhost'
    return "Endpoint=sb://${emulatorHost};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=$sas;UseDevelopmentEmulator=true;"
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$modulePath = Join-Path $repoRoot 'src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1'
if (-not (Test-Path $modulePath)) {
    $modulePath = Join-Path $repoRoot 'src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1'
}

if (-not (Test-Path $modulePath)) {
    throw "Module manifest not found. Build the project first."
}

Import-Module $modulePath -Force
$connectionString = Get-ConnectionString

$messages = New-SBMessage -Body (1..100 | ForEach-Object { "msg$($_)" }) -CustomProperties @{ source = 'manual-send' }

Write-Host "Sending 100 messages to test-topic..."
Send-SBMessage -Topic 'test-topic' -Message $messages -ServiceBusConnectionString $connectionString
Write-Host "Done."
