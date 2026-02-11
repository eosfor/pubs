$ErrorActionPreference = 'Stop'

# Receives up to 100 messages from test-topic/test-sub subscription and writes them to console.

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

Write-Host "Receiving up to 100 messages from test-topic/test-sub..."
$buffer = [System.Collections.Generic.List[Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]]::new()

while ($buffer.Count -lt 100) {
    $batch = @(Receive-SBMessage -Topic 'test-topic' -Subscription 'test-sub' -ServiceBusConnectionString $connectionString -BatchSize 20 -WaitSeconds 2)
    if ($batch.Count -eq 0) {
        break
    }

    foreach ($item in $batch) {
        if ($buffer.Count -ge 100) {
            break
        }

        $buffer.Add($item)
    }
}

$received = @($buffer)

Write-Host "Received $($received.Count) message(s):"
$received | ForEach-Object {
    # Output as a shallow object summary for easy inspection
    [pscustomobject]@{
        Body      = $_.Body.ToString()
        SessionId = $_.SessionId
        Props     = $_.ApplicationProperties
        Enqueued  = $_.EnqueuedTime
    }
}
