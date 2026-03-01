---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# Send-SBMessage

## SYNOPSIS
Sends Service Bus SBMessage operations.

## SYNTAX

### Topic (Default)
```
Send-SBMessage [-Message <PSMessage[]>] [-ReceivedInputObject <ServiceBusReceivedMessage[]>]
 -ServiceBusConnectionString <String> -Topic <String> [-PerSessionThreadAuto] [-PerSessionThread <Int32>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Queue
```
Send-SBMessage [-Message <PSMessage[]>] [-ReceivedInputObject <ServiceBusReceivedMessage[]>]
 -ServiceBusConnectionString <String> -Queue <String> [-PerSessionThreadAuto] [-PerSessionThread <Int32>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Send-SBMessage.
The command supports parameter sets: 'Queue', 'Topic'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Queue)
```powershell
PS C:\\> Send-SBMessage -Queue '<queue-name>' -ServiceBusConnectionString '<connection-string>'
```

Runs Send-SBMessage using the 'Queue' parameter set.

### Example 2 (Topic)
```powershell
PS C:\\> Send-SBMessage -ServiceBusConnectionString '<connection-string>' -Topic '<topic-name>'
```

Runs Send-SBMessage using the 'Topic' parameter set.


## PARAMETERS

### -Message
Message objects to send or process.

```yaml
Type: PSMessage[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -PerSessionThread
Number of parallel workers used per session for send operations.

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

### -PerSessionThreadAuto
Automatically determines per-session parallelism for send operations.

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
Parameter Sets: Queue
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReceivedInputObject
Previously received Service Bus messages passed through the pipeline.

```yaml
Type: ServiceBusReceivedMessage[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ServiceBusConnectionString
Connection string for the target Service Bus namespace or emulator.

```yaml
Type: String
Parameter Sets: (All)
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
Parameter Sets: Topic
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

### SBPowerShell.Models.PSMessage[]
### Azure.Messaging.ServiceBus.ServiceBusReceivedMessage[]
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
