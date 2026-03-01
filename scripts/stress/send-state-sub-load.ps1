param(
    [Parameter(Mandatory)] [string]$ConnStr,
    [string]$Topic = "NO_SESSION",
    [string]$SessionId = "stress-session-1",
    [int]$TotalMessages = 10000,
    [int]$MissingOrder = 2,
    [int]$BatchSize = 200,
    [switch]$SendMissingAtEnd
)

$ErrorActionPreference = "Stop"
$module = "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
if (-not (Test-Path $module)) {
    $module = "src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1"
}
Import-Module $module -Force

if ($TotalMessages -lt 2) {
    throw "TotalMessages must be >= 2"
}

$maxOrder = $TotalMessages + 1
$orders = 1..$maxOrder | Where-Object { $_ -ne $MissingOrder }

if ($orders[0] -ne 1) {
    throw "Order 1 must be present. Adjust MissingOrder/TotalMessages."
}

# Send the first in-order message so processor initializes LastSeenOrder.
$first = New-SBMessage -Body (@{ sessionId = $SessionId; order = 1 } | ConvertTo-Json -Compress) -SessionId $SessionId -CustomProperties @{ order = 1; sessionId = $SessionId }
Send-SBMessage -ServiceBusConnectionString $ConnStr -Topic $Topic -Message $first -BatchSize 1

$tail = $orders | Where-Object { $_ -ne 1 } | Sort-Object { Get-Random }
$sent = 1

$chunk = New-Object System.Collections.Generic.List[object]
foreach ($order in $tail) {
    $msg = New-SBMessage -Body (@{ sessionId = $SessionId; order = [int]$order } | ConvertTo-Json -Compress) -SessionId $SessionId -CustomProperties @{ order = [int]$order; sessionId = $SessionId }
    [void]$chunk.Add($msg)

    if ($chunk.Count -ge $BatchSize) {
        Send-SBMessage -ServiceBusConnectionString $ConnStr -Topic $Topic -Message @($chunk) -BatchSize $BatchSize
        $sent += $chunk.Count
        Write-Host "Sent $sent / $TotalMessages"
        $chunk.Clear()
    }
}

if ($chunk.Count -gt 0) {
    Send-SBMessage -ServiceBusConnectionString $ConnStr -Topic $Topic -Message @($chunk) -BatchSize $BatchSize
    $sent += $chunk.Count
    Write-Host "Sent $sent / $TotalMessages"
}

if ($SendMissingAtEnd) {
    $missing = New-SBMessage -Body (@{ sessionId = $SessionId; order = [int]$MissingOrder } | ConvertTo-Json -Compress) -SessionId $SessionId -CustomProperties @{ order = [int]$MissingOrder; sessionId = $SessionId }
    Send-SBMessage -ServiceBusConnectionString $ConnStr -Topic $Topic -Message $missing -BatchSize 1
    Write-Host "Sent missing order $MissingOrder"
}

Write-Host "Load completed. SessionId=$SessionId Sent=$sent MissingOrder=$MissingOrder"
