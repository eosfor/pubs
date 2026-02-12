param(
    [Parameter(Mandatory)] [string]$ConnStr,
    [Parameter(Mandatory)] [string]$SessionId,
    [string]$Topic = "NO_SESSION",
    [string]$Subscription = "STATE_SUB",
    [string]$SnapDir = "./out/recovery-runs",
    [int]$ChunkSize = 200,
    [int]$SendBatchSize = 300,
    [int]$RetryCount = 6,
    [switch]$SkipStateReset
)

$ErrorActionPreference = "Stop"

$module = "./src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
if (-not (Test-Path $module)) {
    $module = "./src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1"
}
Import-Module $module -Force

New-Item -ItemType Directory -Path $SnapDir -Force | Out-Null

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = Join-Path $SnapDir "$SessionId-$runStamp"
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

$script:ctx = $null

function Reset-RecoveryContext {
    if ($script:ctx) {
        try { Close-SBSessionContext -Context $script:ctx } catch {}
        $script:ctx = $null
    }

    for ($i = 1; $i -le 10; $i++) {
        try {
            $script:ctx = New-SBSessionContext -Topic $Topic -Subscription $Subscription -SessionId $SessionId -ServiceBusConnectionString $ConnStr
            return
        }
        catch {
            $msg = $_.Exception.Message
            if ($msg -match "SessionCannotBeLocked|cannot be accepted" -and $i -lt 10) {
                Start-Sleep -Seconds ([Math]::Min(5, $i))
                continue
            }
            throw
        }
    }
}

function Invoke-WithRetry {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [scriptblock]$Operation
    )

    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        try {
            return & $Operation
        }
        catch {
            $msg = $_.Exception.Message
            $retryable = $msg -match "SessionLockLost|SessionCannotBeLocked|ServiceTimeout|owner is being closed|session lock has expired"
            if (-not $retryable -or $attempt -ge $RetryCount) {
                throw
            }

            Write-Warning "$Name failed (attempt $attempt/$RetryCount): $msg"
            Reset-RecoveryContext
            Start-Sleep -Seconds ([Math]::Min(5, $attempt))
        }
    }
}

function Get-SeqNumbers {
    param([object]$State)

    @(
        $State.Deferred | ForEach-Object {
            if ($_.PSObject.Properties.Name -contains "SeqNumber") { [int64]$_.SeqNumber }
            elseif ($_.PSObject.Properties.Name -contains "seq") { [int64]$_.seq }
            elseif ($_.PSObject.Properties.Name -contains "Seq") { [int64]$_.Seq }
        }
    ) | Where-Object { $_ -ne $null }
}

try {
    Reset-RecoveryContext

    $stateJson = Invoke-WithRetry -Name "GetSessionState(before)" -Operation {
        Get-SBSessionState -SessionContext $script:ctx -AsString
    }

    if ([string]::IsNullOrWhiteSpace($stateJson)) {
        throw "Session state is empty, nothing to recover."
    }

    $beforeFile = Join-Path $runDir "state.$SessionId.before.json"
    $stateJson | Set-Content $beforeFile -Encoding UTF8
    $state = $stateJson | ConvertFrom-Json

    $lastSeen = if ($state.PSObject.Properties.Name -contains "LastSeenOrder") {
        [int]$state.LastSeenOrder
    }
    else {
        [int]$state.LastSeenOrderNum
    }

    $seqs = @(Get-SeqNumbers -State $state)
    $total = $seqs.Count

    Write-Host "Deferred sequence count: $total"

    if (-not $SkipStateReset) {
        $compact = if ($state.PSObject.Properties.Name -contains "LastSeenOrder") {
            @{ LastSeenOrder = $lastSeen; Deferred = @() }
        }
        else {
            @{ LastSeenOrderNum = $lastSeen; Deferred = @() }
        }

        Invoke-WithRetry -Name "SetSessionState(reset)" -Operation {
            Set-SBSessionState -SessionContext $script:ctx -State ($compact | ConvertTo-Json -Compress)
        }
    }

    $recovered = 0
    $missing = 0
    $chunks = [Math]::Ceiling($total / [double]$ChunkSize)

    for ($i = 0; $i -lt $total; $i += $ChunkSize) {
        $count = [Math]::Min($ChunkSize, $total - $i)
        $chunk = @($seqs[$i..($i + $count - 1)])
        $messages = @()

        try {
            $messages = @(Invoke-WithRetry -Name "ReceiveDeferred(chunk)" -Operation {
                Receive-SBDeferredMessage -SessionContext $script:ctx -SequenceNumber $chunk -ChunkSize $ChunkSize
            })
        }
        catch {
            $msg = $_.Exception.Message
            if ($msg -match "MessageNotFound|Failed to lock one or more specified messages") {
                foreach ($seq in $chunk) {
                    try {
                        $one = @(Invoke-WithRetry -Name "ReceiveDeferred(single:$seq)" -Operation {
                            Receive-SBDeferredMessage -SessionContext $script:ctx -SequenceNumber @([int64]$seq) -ChunkSize 1
                        })
                        if ($one.Count -gt 0) { $messages += $one }
                        else { $missing++ }
                    }
                    catch {
                        $singleMsg = $_.Exception.Message
                        if ($singleMsg -match "MessageNotFound|Failed to lock one or more specified messages") {
                            $missing++
                            continue
                        }
                        throw
                    }
                }
            }
            else {
                throw
            }
        }

        if ($messages.Count -gt 0) {
            $sent = @(Invoke-WithRetry -Name "SendBackToActive" -Operation {
                $messages | Send-SBMessage -SessionContext $script:ctx -BatchSize $SendBatchSize -PassThru
            })

            if ($sent.Count -gt 0) {
                Invoke-WithRetry -Name "CompleteDeferredOriginals" -Operation {
                    Set-SBMessage -SessionContext $script:ctx -Message $sent -Complete | Out-Null
                }
                $recovered += $sent.Count
            }
        }

        $chunkNo = [int]([Math]::Floor($i / [double]$ChunkSize) + 1)
        Write-Host "Chunk $chunkNo/$chunks processed. recovered=$recovered missing=$missing"
    }

    $afterState = Invoke-WithRetry -Name "GetSessionState(after)" -Operation {
        Get-SBSessionState -SessionContext $script:ctx -AsString
    }

    $afterFile = Join-Path $runDir "state.$SessionId.after.json"
    $afterState | Set-Content $afterFile -Encoding UTF8

    $summary = [pscustomobject]@{
        SessionId = $SessionId
        DeferredTotal = $total
        RecoveredCount = $recovered
        NotFoundCount = $missing
        BeforeStateFile = $beforeFile
        AfterStateFile = $afterFile
        CompletedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }

    $summaryFile = Join-Path $runDir "summary.json"
    $summary | ConvertTo-Json -Depth 5 | Set-Content $summaryFile -Encoding UTF8

    Write-Host "Recovery completed. Summary: $summaryFile"
    Write-Output $summary
}
finally {
    if ($script:ctx) {
        try { Close-SBSessionContext -Context $script:ctx } catch {}
    }
}
