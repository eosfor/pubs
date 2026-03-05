---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Send-SBScheduledMessage

## SYNOPSIS
Sends Service Bus SBScheduledMessage operations.

## SYNTAX

### Topic (Default)
```
Send-SBScheduledMessage [-Message <PSMessage[]>] [-ReceivedInputObject <ServiceBusReceivedMessage[]>]
 [-Topic <String>] -ScheduleAtUtc <DateTimeOffset> [-ServiceBusConnectionString <String>]
 [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### Queue
```
Send-SBScheduledMessage [-Message <PSMessage[]>] [-ReceivedInputObject <ServiceBusReceivedMessage[]>]
 [-Queue <String>] -ScheduleAtUtc <DateTimeOffset> [-ServiceBusConnectionString <String>]
 [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Send-SBScheduledMessage.
The command supports parameter sets: 'Queue', 'Topic'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Queue)
```powershell
PS C:\\> Send-SBScheduledMessage -ScheduleAtUtc (Get-Date).ToUniversalTime().AddMinutes(5)
```

Runs Send-SBScheduledMessage using the 'Queue' parameter set.

### Example 2 (Topic)
```powershell
PS C:\\> Send-SBScheduledMessage -ScheduleAtUtc (Get-Date).ToUniversalTime().AddMinutes(5)
```

Runs Send-SBScheduledMessage using the 'Topic' parameter set.


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

### -Queue
Queue name to target.

```yaml
Type: String
Parameter Sets: Queue
Aliases:

Required: False
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

### -ScheduleAtUtc
UTC timestamp when a scheduled message should be enqueued.

```yaml
Type: DateTimeOffset
Parameter Sets: (All)
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

### -Topic
Topic name to target.

```yaml
Type: String
Parameter Sets: Topic
Aliases:

Required: False
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

### -Context
Specifies the Context value for this command.

```yaml
Type: SBContext
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoContext
Specifies the NoContext value for this command.

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

### -IgnoreCertificateChainErrors
Specifies the IgnoreCertificateChainErrors value for this command.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

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

### SBPowerShell.Models.ScheduledMessageResult
## NOTES

## RELATED LINKS
