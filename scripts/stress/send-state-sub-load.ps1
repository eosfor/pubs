param(
    [Parameter(Mandatory)] [string]$ConnStr,
    [string]$Topic = "NO_SESSION",
    [string]$SessionId = "stress-session-1",
    [int]$TotalMessages = 10000,
    [int]$MissingOrder = 2,
    [int]$BatchSize = 300,
    [int]$InterBatchPauseMs = 5,
    [int]$WarmupBatches = 3,
    [int]$WarmupPauseMs = 25,
    [int]$MaxRetryCount = 8,
    [int]$BaseBackoffMs = 40,
    [int]$MaxBackoffMs = 1500,
    [switch]$SendMissingAtEnd
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." "..")).Path
$module = Join-Path $repoRoot "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
if (-not (Test-Path $module)) {
    $module = Join-Path $repoRoot "src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1"
}
Import-Module $module -Force

if ($TotalMessages -lt 2) {
    throw "TotalMessages must be >= 2"
}

function Invoke-SendWithRetry {
    param(
        [Parameter(Mandatory)] [object]$Message,
        [Parameter(Mandatory)] [int]$EffectiveBatchSize,
        [Parameter(Mandatory)] [string]$Label
    )

    $toSend = @($Message)

    for ($attempt = 1; $attempt -le $MaxRetryCount; $attempt++) {
        try {
            Send-SBMessage -ServiceBusConnectionString $ConnStr -Topic $Topic -Message $toSend -BatchSize $EffectiveBatchSize
            return
        }
        catch {
            $msg = $_.Exception.Message
            $retryable = $msg -match "ServerBusy|ServiceBusy|throttle|TooManyRequests|Timeout|Connection refused|ServiceCommunicationProblem|QuotaExceeded"
            if (-not $retryable -or $attempt -ge $MaxRetryCount) {
                throw
            }

            $exp = [Math]::Pow(2, $attempt - 1)
            $delay = [int][Math]::Min($MaxBackoffMs, ($BaseBackoffMs * $exp) + (Get-Random -Minimum 0 -Maximum 30))
            Write-Host "Retry send [$Label] attempt $attempt/$MaxRetryCount; sleep ${delay}ms"
            Start-Sleep -Milliseconds $delay
        }
    }
}

$maxOrder = $TotalMessages + 1
$orders = 1..$maxOrder | Where-Object { $_ -ne $MissingOrder }

if ($orders[0] -ne 1) {
    throw "Order 1 must be present. Adjust MissingOrder/TotalMessages."
}

# Send the first in-order message so processor initializes LastSeenOrder.
$first = New-SBMessage -Body (@{ sessionId = $SessionId; order = 1 } | ConvertTo-Json -Compress) -SessionId $SessionId -CustomProperties @{ order = 1; sessionId = $SessionId }
Invoke-SendWithRetry -Message $first -EffectiveBatchSize 1 -Label "first"

$tail = $orders | Where-Object { $_ -ne 1 } | Sort-Object { Get-Random }
$sent = 1
$batchNo = 0

$chunk = New-Object System.Collections.Generic.List[object]
foreach ($order in $tail) {
    $msg = New-SBMessage -Body (@{ sessionId = $SessionId; order = [int]$order } | ConvertTo-Json -Compress) -SessionId $SessionId -CustomProperties @{ order = [int]$order; sessionId = $SessionId }
    [void]$chunk.Add($msg)

    if ($chunk.Count -ge $BatchSize) {
        $batchNo++
        Invoke-SendWithRetry -Message ($chunk.ToArray()) -EffectiveBatchSize $BatchSize -Label "chunk-$batchNo"
        $sent += $chunk.Count
        Write-Host "Sent $sent / $TotalMessages"
        if ($batchNo -le $WarmupBatches) {
            Start-Sleep -Milliseconds $WarmupPauseMs
        }
        elseif ($InterBatchPauseMs -gt 0) {
            Start-Sleep -Milliseconds $InterBatchPauseMs
        }
        $chunk.Clear()
    }
}

if ($chunk.Count -gt 0) {
    $batchNo++
    Invoke-SendWithRetry -Message ($chunk.ToArray()) -EffectiveBatchSize $BatchSize -Label "chunk-$batchNo"
    $sent += $chunk.Count
    Write-Host "Sent $sent / $TotalMessages"
}

if ($SendMissingAtEnd) {
    $missing = New-SBMessage -Body (@{ sessionId = $SessionId; order = [int]$MissingOrder } | ConvertTo-Json -Compress) -SessionId $SessionId -CustomProperties @{ order = [int]$MissingOrder; sessionId = $SessionId }
    Invoke-SendWithRetry -Message $missing -EffectiveBatchSize 1 -Label "missing"
    Write-Host "Sent missing order $MissingOrder"
}

Write-Host "Load completed. SessionId=$SessionId Sent=$sent MissingOrder=$MissingOrder"
