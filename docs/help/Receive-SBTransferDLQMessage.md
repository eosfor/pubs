---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Receive-SBTransferDLQMessage

## SYNOPSIS
Receives messages from Service Bus transfer dead-letter queues (queue transfer DLQ or subscription transfer DLQ).

## SYNTAX

### Queue (Default)
```
Receive-SBTransferDLQMessage [-ServiceBusConnectionString <String>] -Queue <String> [-BatchSize <Int32>]
 [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### QueueMax
```
Receive-SBTransferDLQMessage [-ServiceBusConnectionString <String>] -Queue <String> -MaxMessages <Int32>
 [-BatchSize <Int32>] [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### QueueWait
```
Receive-SBTransferDLQMessage [-ServiceBusConnectionString <String>] -Queue <String> [-BatchSize <Int32>]
 -WaitSeconds <Int32> [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```
Receive-SBTransferDLQMessage [-ServiceBusConnectionString <String>] -Topic <String> -Subscription <String>
 [-BatchSize <Int32>] [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### SubscriptionMax
```
Receive-SBTransferDLQMessage [-ServiceBusConnectionString <String>] -Topic <String> -Subscription <String>
 -MaxMessages <Int32> [-BatchSize <Int32>] [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### SubscriptionWait
```
Receive-SBTransferDLQMessage [-ServiceBusConnectionString <String>] -Topic <String> -Subscription <String>
 [-BatchSize <Int32>] -WaitSeconds <Int32> [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Receives messages from transfer dead-letter subqueues for queues and topic subscriptions.
Use `-MaxMessages` for count-limited receive, `-WaitSeconds` for deadline-based receive, or no limit switches for continuous polling until cancellation.
In `-WaitSeconds` mode, internal SDK timeout/retry settings are bounded so execution time stays close to the requested deadline.

## EXAMPLES

### Example 1 (Queue)
```powershell
PS C:\\> Receive-SBTransferDLQMessage -Queue '<queue-name>'
```

Continuously receives messages from the queue transfer dead-letter subqueue until cancelled.

### Example 2 (QueueMax)
```powershell
PS C:\\> Receive-SBTransferDLQMessage -MaxMessages 10 -Queue '<queue-name>'
```

Receives up to 10 messages from the queue transfer dead-letter subqueue and then stops.


## PARAMETERS

### -BatchSize
Maximum number of messages to process in a single receive batch.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxMessages
Maximum number of messages to receive or process before the command stops.

```yaml
Type: Int32
Parameter Sets: QueueMax, SubscriptionMax
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoComplete
Prevents automatic completion of received messages.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Peek
Returns messages without completing them in the broker.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Queue
Queue name to target.

```yaml
Type: String
Parameter Sets: Queue, QueueMax, QueueWait
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ServiceBusConnectionString
Connection string for the target Service Bus namespace or emulator.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Subscription
Subscription name to target.

```yaml
Type: String
Parameter Sets: Subscription, SubscriptionMax, SubscriptionWait
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Topic
Topic name to target.

```yaml
Type: String
Parameter Sets: Subscription, SubscriptionMax, SubscriptionWait
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WaitSeconds
Deadline (in seconds) for bounded polling mode. Returns empty when no messages arrive before the deadline.

```yaml
Type: Int32
Parameter Sets: QueueWait, SubscriptionWait
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction
Controls how progress records are handled.

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### Azure.Messaging.ServiceBus.ServiceBusReceivedMessage
## NOTES

## RELATED LINKS
