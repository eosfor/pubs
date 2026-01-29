# Вспомогательные функции для упорядочивания входного потока несессионных сообщений
# с использованием псевдо-сессий: состояние хранится в session state сущности ORDERED_TOPIC/SESS_SUB
# и содержит lastSeenOrderNum (int) и deferred (список пар [order, seqNumber]).
# Первое полученное сообщение фиксирует стартовый order; всё, что имеет order меньше стартового, будет отложено и никогда не выйдет на выход.

function Get-ReorderState {
    param(
        [string]$ConnStr,
        [string]$SessionId
    )

    Write-Verbose "Loading session state for SessionId='$SessionId'"
    $stateObj = Get-SBSessionState -ServiceBusConnectionString $ConnStr -SessionId $SessionId -Topic "ORDERED_TOPIC" -Subscription "SESS_SUB" -ErrorAction SilentlyContinue
    if (-not $stateObj) {
        Write-Verbose "No existing state, initializing defaults"
        $empty = [SBPowerShell.Models.SessionOrderingState]::new()
        $empty.LastSeenOrderNum = 0
        $empty.Deferred = [System.Collections.Generic.List[SBPowerShell.Models.OrderSeq]]::new()
        return $empty
    }

    if ($stateObj -isnot [SBPowerShell.Models.SessionOrderingState]) {
        throw "Unexpected session state format; expected SessionOrderingState but got $($stateObj.GetType().FullName)"
    }

    if (-not $stateObj.Deferred) {
        $stateObj.Deferred = [System.Collections.Generic.List[SBPowerShell.Models.OrderSeq]]::new()
    }

    Write-Verbose "Loaded state: lastSeen=$($stateObj.LastSeenOrderNum); deferredCount=$($stateObj.Deferred.Count)"
    return $stateObj
}

function Save-ReorderState {
    param(
        [string]$ConnStr,
        [string]$SessionId,
        [SBPowerShell.Models.SessionOrderingState]$State
    )

    Write-Verbose "Saving state for SessionId='$SessionId': lastSeen=$($State.LastSeenOrderNum); deferredCount=$($State.Deferred.Count)"
    $stateToSave = New-SBSessionState -LastSeenOrderNum $State.LastSeenOrderNum -Deferred ($State.Deferred | ForEach-Object { @{ order = $_.Order; seq = $_.Seq } })
    Set-SBSessionState -ServiceBusConnectionString $ConnStr -SessionId $SessionId -Topic "ORDERED_TOPIC" -Subscription "SESS_SUB" -State $stateToSave
}

function Complete-InputMessage {
    param(
        [string]$ConnStr,
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message
    )

    Write-Verbose "Complete msg seq=$($Message.SequenceNumber) order=$($Message.ApplicationProperties['order'])"
    Set-SBMessage -ServiceBusConnectionString $ConnStr -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -Message $Message -Complete | Out-Null
}

function Defer-InputMessage {
    param(
        [string]$ConnStr,
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message
    )

    Write-Verbose "Defer msg seq=$($Message.SequenceNumber) order=$($Message.ApplicationProperties['order'])"
    Set-SBMessage -ServiceBusConnectionString $ConnStr -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -Message $Message -Defer | Out-Null
}

function DeadLetter-InputMessage {
    param(
        [string]$ConnStr,
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message
    )

    Write-Verbose "Dead-letter msg seq=$($Message.SequenceNumber) order=$($Message.ApplicationProperties['order'])"
    Set-SBMessage -ServiceBusConnectionString $ConnStr -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -Message $Message -DeadLetter  -DeadLetterReason "The 'order' value is less than expected" | Out-Null
}

function Drain-DeferredMessages {
    param(
        [string]$ConnStr,
        [ref]$State,
        [int]$StartExpected
    )

    Write-Verbose "Draining deferred starting from order $StartExpected; count=$($State.Value.deferred.Count)"
    $remaining = [System.Collections.Generic.List[SBPowerShell.Models.OrderSeq]]::new()
    $ordered = $State.Value.Deferred | Sort-Object Order
    foreach ($pair in $ordered) {
        $order = [int]$pair.Order
        $seq = [int64]$pair.Seq
        if ($order -eq $StartExpected) {
            Write-Verbose "Attempting Receive-SBDeferredMessage for order=$order seq=$seq"
            $dm = Receive-SBDeferredMessage -ServiceBusConnectionString $ConnStr -SequenceNumber $seq -Topic "NO_SESSION" -Subscription "NO_SESS_SUB"
            if (-not $dm) {
                Write-Warning "Deferred msg seq=$seq order=$order not returned (expired/locked); will retry later"
                [void]$remaining.Add([SBPowerShell.Models.OrderSeq]::new($order, $seq))
                continue
            }

            Write-Verbose "Processing deferred msg seq=$($dm.SequenceNumber) order=$order"
            Send-SBMessage -ServiceBusConnectionString $ConnStr -ReceivedInputObject $dm -Topic "ORDERED_TOPIC"
            Complete-InputMessage -ConnStr $ConnStr -Message $dm
            $State.Value.LastSeenOrderNum = $order
            Write-Output $dm
            $StartExpected++
        }
        else {
            [void]$remaining.Add([SBPowerShell.Models.OrderSeq]::new($order, $seq))
        }
    }
    $State.Value.Deferred = $remaining
}

function Handle-InOrder {
    param(
        [string]$ConnStr,
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message,
        [ref]$State
    )

    Write-Verbose "In-order msg seq=$($Message.SequenceNumber) order=$($Message.ApplicationProperties['order']) expected=$($State.Value.LastSeenOrderNum + 1)"
    Send-SBMessage -ServiceBusConnectionString $ConnStr -ReceivedInputObject $Message -Topic "ORDERED_TOPIC"
    Complete-InputMessage -ConnStr $ConnStr -Message $Message
    $orderVal = [int]$Message.ApplicationProperties["order"]
    $State.Value.LastSeenOrderNum = $orderVal
    Write-Output $Message

    Drain-DeferredMessages -ConnStr $ConnStr -State $State -StartExpected ($orderVal + 1)
    Save-ReorderState -ConnStr $ConnStr -SessionId $Message.SessionId -State $State.Value
}

function Handle-OutOfOrder {
    param(
        [string]$ConnStr,
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message,
        [ref]$State
    )

    $expectedOrderNum = $State.Value.LastSeenOrderNum + 1
    $orderVal = [int]$Message.ApplicationProperties["order"]

    Write-Verbose "Out-of-order msg seq=$($Message.SequenceNumber) order=$orderVal expected=$expectedOrderNum"

    if ($orderVal -lt $expectedOrderNum) {
        DeadLetter-InputMessage -ConnStr $ConnStr -Message $Message
        return
    }

    [void]$State.Value.Deferred.Add([SBPowerShell.Models.OrderSeq]::new($orderVal, [int64]$Message.SequenceNumber))
    Defer-InputMessage -ConnStr $ConnStr -Message $Message
    Save-ReorderState -ConnStr $ConnStr -SessionId $Message.SessionId -State $State.Value
}

function Initialize-FirstMessage {
    param(
        [string]$ConnStr,
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message
    )

    Write-Verbose "Initializing session with first msg seq=$($Message.SequenceNumber) order=$($Message.ApplicationProperties['order'])"
    Send-SBMessage -ServiceBusConnectionString $ConnStr -ReceivedInputObject $Message -Topic "ORDERED_TOPIC"
    Complete-InputMessage -ConnStr $ConnStr -Message $Message
    Write-Output $Message
    $initialState = New-SBSessionState -LastSeenOrderNum ([int]$Message.ApplicationProperties["order"]) -Deferred @()
    Save-ReorderState -ConnStr $ConnStr -SessionId $Message.SessionId -State $initialState
}

<#
.SYNOPSIS
    функция реализует подход из статьи: https://devblogs.microsoft.com/premier-developer/ordering-messages-in-azure-service-bus/
    примеры кода: https://github.com/hgjura/blog-azuerservicebus-ordering

.DESCRIPTION
Long description

.PARAMETER msg
Parameter description

.PARAMETER ConnStr
Parameter description

.EXAMPLE
An example

.NOTES
General notes
#>
function Process-Message {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromPipeline)]
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$msg,
        [Parameter(Mandatory)]
        [string]$ConnStr
    )
    
    begin {}
    
    process {
        # получаем текущее состояние сессии (или создаём новое)
        $state = Get-ReorderState -ConnStr $ConnStr -SessionId $msg.SessionId

        if ($state.LastSeenOrderNum -eq 0) {
            Initialize-FirstMessage -ConnStr $ConnStr -Message $msg
            return
        }

        $expectedOrderNum = $state.LastSeenOrderNum + 1
        $stateRef = [ref]$state

        $orderVal = [int]$msg.ApplicationProperties["order"]

        if ($orderVal -eq $expectedOrderNum) {
            # сообщение пришло в ожидаемом порядке
            Handle-InOrder -ConnStr $ConnStr -Message $msg -State $stateRef
        }
        else {
            # сообщение вне очереди — откладываем и сохраняем состояние
            Handle-OutOfOrder -ConnStr $ConnStr -Message $msg -State $stateRef
        }
    }
    
    end {}
}
