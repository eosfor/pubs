---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# Receive-SBDLQMessage

## SYNOPSIS
Receives Service Bus SBDLQMessage operations.

## SYNTAX

### Queue (Default)
```
Receive-SBDLQMessage [-ServiceBusConnectionString <String>] -Queue <String> [-BatchSize <Int32>] [-Peek]
 [-NoComplete] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### QueueMax
```
Receive-SBDLQMessage [-ServiceBusConnectionString <String>] -Queue <String> -MaxMessages <Int32>
 [-BatchSize <Int32>] [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### QueueWait
```
Receive-SBDLQMessage [-ServiceBusConnectionString <String>] -Queue <String> [-BatchSize <Int32>]
 -WaitSeconds <Int32> [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```
Receive-SBDLQMessage [-ServiceBusConnectionString <String>] -Topic <String> -Subscription <String>
 [-BatchSize <Int32>] [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### SubscriptionMax
```
Receive-SBDLQMessage [-ServiceBusConnectionString <String>] -Topic <String> -Subscription <String>
 -MaxMessages <Int32> [-BatchSize <Int32>] [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### SubscriptionWait
```
Receive-SBDLQMessage [-ServiceBusConnectionString <String>] -Topic <String> -Subscription <String>
 [-BatchSize <Int32>] -WaitSeconds <Int32> [-Peek] [-NoComplete] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Receive-SBDLQMessage.
The command supports parameter sets: 'Queue', 'QueueMax', 'QueueWait', 'Subscription', 'SubscriptionMax', 'SubscriptionWait'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Queue)
```powershell
PS C:\\> Receive-SBDLQMessage -Queue '<queue-name>'
```

Runs Receive-SBDLQMessage using the 'Queue' parameter set.

### Example 2 (QueueMax)
```powershell
PS C:\\> Receive-SBDLQMessage -MaxMessages 10 -Queue '<queue-name>'
```

Runs Receive-SBDLQMessage using the 'QueueMax' parameter set.


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
Maximum number of seconds to wait for messages when polling.

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
