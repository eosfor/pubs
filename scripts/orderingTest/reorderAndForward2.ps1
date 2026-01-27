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
    $stateStr = Get-SBSessionState -ServiceBusConnectionString $ConnStr -SessionId $SessionId -Topic "ORDERED_TOPIC" -Subscription "SESS_SUB" -AsString -ErrorAction SilentlyContinue
    if (-not $stateStr) {
        Write-Verbose "No existing state, initializing defaults"
        return @{
            lastSeenOrderNum = 0
            deferred        = @()
        }
    }

    $state = $stateStr | ConvertFrom-Json
    Write-Verbose "Loaded state: lastSeen=$($state.lastSeenOrderNum); deferredCount=$($state.deferred.Count)"
    if (-not $state.PSObject.Properties['deferred']) { $state | Add-Member -NotePropertyName deferred -NotePropertyValue @() }
    if (-not $state.PSObject.Properties['lastSeenOrderNum']) { $state | Add-Member -NotePropertyName lastSeenOrderNum -NotePropertyValue 0 }
    # ensure deferred is an array of primitive pairs [order, seq]
    $normalized = @()
    foreach ($item in ($state.deferred ?? @())) {
        if ($item -is [array] -and $item.Length -ge 2) {
            $normalized += ,@([int]$item[0], [int64]$item[1])
        }
    }
    $state.deferred = $normalized
    return @{
        lastSeenOrderNum = [int]$state.lastSeenOrderNum
        deferred        = @($state.deferred)
    }
}

function Save-ReorderState {
    param(
        [string]$ConnStr,
        [string]$SessionId,
        $State
    )

    Write-Verbose "Saving state for SessionId='$SessionId': lastSeen=$($State.lastSeenOrderNum); deferredCount=$($State.deferred.Count)"
    $json = (@{
        lastSeenOrderNum = $State.lastSeenOrderNum
        deferred        = $State.deferred
    } | ConvertTo-Json -Compress)
    Set-SBSessionState -ServiceBusConnectionString $ConnStr -SessionId $SessionId -Topic "ORDERED_TOPIC" -Subscription "SESS_SUB" -State $json
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

function Drain-DeferredMessages {
    param(
        [string]$ConnStr,
        [ref]$State,
        [int]$StartExpected
    )

    Write-Verbose "Draining deferred starting from order $StartExpected; count=$($State.Value.deferred.Count)"
    $remaining = New-Object System.Collections.Generic.List[object]
    $ordered = $State.Value.deferred | Sort-Object { $_[0] }
    foreach ($pair in $ordered) {
        $order = [int]$pair[0]
        $seq   = [int64]$pair[1]
        if ($order -eq $StartExpected) {
            $dm = Receive-SBDeferredMessage -ServiceBusConnectionString $ConnStr -SequenceNumber $seq -Topic "NO_SESSION" -Subscription "NO_SESS_SUB"
            Write-Verbose "Processing deferred msg seq=$($dm.SequenceNumber) order=$order"
            Send-SBMessage -ServiceBusConnectionString $ConnStr -ReceivedInputObject $dm -Topic "ORDERED_TOPIC"
            Complete-InputMessage -ConnStr $ConnStr -Message $dm
            $State.Value.lastSeenOrderNum = $order
            Write-Output $dm
            $StartExpected++
        } else {
            $remaining.Add(@($order, $seq))
        }
    }
    $State.Value.deferred = $remaining
}

function Handle-InOrder {
    param(
        [string]$ConnStr,
        [Azure.Messaging.ServiceBus.ServiceBusReceivedMessage]$Message,
        [ref]$State
    )

    Write-Verbose "In-order msg seq=$($Message.SequenceNumber) order=$($Message.ApplicationProperties['order']) expected=$($State.Value.lastSeenOrderNum + 1)"
    Send-SBMessage -ServiceBusConnectionString $ConnStr -ReceivedInputObject $Message -Topic "ORDERED_TOPIC"
    Complete-InputMessage -ConnStr $ConnStr -Message $Message
    $orderVal = [int]$Message.ApplicationProperties["order"]
    $State.Value.lastSeenOrderNum = $orderVal
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

    Write-Verbose "Out-of-order msg seq=$($Message.SequenceNumber) order=$($Message.ApplicationProperties['order']) expected=$($State.Value.lastSeenOrderNum + 1)"
    $State.Value.deferred += ,@([int]$Message.ApplicationProperties["order"], [int64]$Message.SequenceNumber)
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
    Save-ReorderState -ConnStr $ConnStr -SessionId $Message.SessionId -State @{
        lastSeenOrderNum = [int]$Message.ApplicationProperties["order"]
        deferred         = @()
    }
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

        if ($state.lastSeenOrderNum -eq 0) {
            Initialize-FirstMessage -ConnStr $ConnStr -Message $msg
            return
        }

        $expectedOrderNum = $state.lastSeenOrderNum + 1
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
