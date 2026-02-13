param(
    [Parameter(Mandatory)] [string]$ConnStr,
    [Parameter(Mandatory)] [string]$SessionId,
    [string]$InputTopic = "NO_SESSION",
    [string]$InputSubscription = "STATE_SUB",
    [string]$OutputTopic = "ORDERED_TOPIC",
    [int]$ReceiveBatchSize = 200,
    [int]$PollWaitSeconds = 2,
    [int]$CrashDeferredThreshold = 0
)

$ErrorActionPreference = "Stop"
$module = "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
if (-not (Test-Path $module)) {
    $module = "src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1"
}
Import-Module $module -Force

function Get-StateObject {
    param([string]$Raw)

    if ([string]::IsNullOrWhiteSpace($Raw)) {
        return [pscustomobject]@{ LastSeenOrder = 0; Deferred = @() }
    }

    try {
        $state = $Raw | ConvertFrom-Json
        if (-not $state.PSObject.Properties.Name.Contains("Deferred")) {
            $state | Add-Member -NotePropertyName Deferred -NotePropertyValue @()
        }
        if (-not $state.PSObject.Properties.Name.Contains("LastSeenOrder")) {
            if ($state.PSObject.Properties.Name.Contains("LastSeenOrderNum")) {
                $state | Add-Member -NotePropertyName LastSeenOrder -NotePropertyValue ([int]$state.LastSeenOrderNum)
            }
            else {
                $state | Add-Member -NotePropertyName LastSeenOrder -NotePropertyValue 0
            }
        }
        return $state
    }
    catch {
        return [pscustomobject]@{ LastSeenOrder = 0; Deferred = @() }
    }
}

function Save-State {
    param([object]$State)

    $payload = @{ LastSeenOrder = [int]$State.LastSeenOrder; Deferred = @($State.Deferred) } | ConvertTo-Json -Compress -Depth 20
    Set-SBSessionState -SessionContext $script:ctx -State $payload
}

function Forward-And-Complete {
    param([Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message)

    Send-SBMessage -ServiceBusConnectionString $ConnStr -Topic $OutputTopic -ReceivedInputObject $Message -BatchSize 100 | Out-Null
    $Message | Set-SBMessage -SessionContext $script:ctx -Complete | Out-Null
}

function Defer-Current {
    param(
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message,
        [int]$Order
    )

    $script:state.Deferred += @(@{ Order = [int]$Order; SeqNumber = [int64]$Message.SequenceNumber })
    $Message | Set-SBMessage -SessionContext $script:ctx -Defer | Out-Null

    if ($CrashDeferredThreshold -gt 0 -and $script:state.Deferred.Count -ge $CrashDeferredThreshold) {
        Save-State -State $script:state
        throw "Simulated crash: Deferred count reached threshold $CrashDeferredThreshold"
    }
}

function Drain-Deferred {
    while ($true) {
        $expected = [int]$script:state.LastSeenOrder + 1
        $ordered = @($script:state.Deferred | Sort-Object { [int]$_.Order })
        $next = $ordered | Where-Object { [int]$_.Order -eq $expected } | Select-Object -First 1
        if (-not $next) { break }

        $dm = Receive-SBDeferredMessage -SessionContext $script:ctx -SequenceNumber @([int64]$next.SeqNumber) -ChunkSize 1 -ErrorAction SilentlyContinue
        if (-not $dm) { break }

        Forward-And-Complete -Message $dm
        $script:state.LastSeenOrder = [int]$next.Order
        $script:state.Deferred = @($script:state.Deferred | Where-Object { [int64]$_.SeqNumber -ne [int64]$next.SeqNumber })
    }
}

$ctx = New-SBSessionContext -Topic $InputTopic -Subscription $InputSubscription -SessionId $SessionId -ServiceBusConnectionString $ConnStr
$script:ctx = $ctx
try {
    $raw = Get-SBSessionState -SessionContext $ctx -AsString -ErrorAction SilentlyContinue
    $script:state = Get-StateObject -Raw $raw
    Write-Host "Loaded state: LastSeenOrder=$($script:state.LastSeenOrder) DeferredCount=$(@($script:state.Deferred).Count)"

    while ($true) {
        $batch = @(Receive-SBMessage -SessionContext $ctx -NoComplete -WaitSeconds $PollWaitSeconds -BatchSize $ReceiveBatchSize)
        if ($batch.Count -eq 0) {
            Write-Host "No messages received, exiting consumer loop"
            break
        }

        foreach ($msg in $batch) {
            $order = [int]$msg.ApplicationProperties["order"]
            $expected = [int]$script:state.LastSeenOrder + 1

            if ([int]$script:state.LastSeenOrder -eq 0) {
                Forward-And-Complete -Message $msg
                $script:state.LastSeenOrder = $order
                Drain-Deferred
                Save-State -State $script:state
                continue
            }

            if ($order -eq $expected) {
                Forward-And-Complete -Message $msg
                $script:state.LastSeenOrder = $order
                Drain-Deferred
                Save-State -State $script:state
                continue
            }

            if ($order -lt $expected) {
                $msg | Set-SBMessage -SessionContext $ctx -DeadLetter -DeadLetterReason "Order lower than expected" | Out-Null
                continue
            }

            Defer-Current -Message $msg -Order $order
            Save-State -State $script:state
        }

        Write-Host "Progress: LastSeenOrder=$($script:state.LastSeenOrder) DeferredCount=$(@($script:state.Deferred).Count)"
    }
}
finally {
    Close-SBSessionContext -Context $ctx
}
