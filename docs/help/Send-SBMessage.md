---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Send-SBMessage

## SYNOPSIS
Sends Service Bus SBMessage operations.

## SYNTAX

### Topic (Default)
```
Send-SBMessage [-Message <PSMessage[]>] [-ReceivedInputObject <ServiceBusReceivedMessage[]>] [-Topic <String>]
 [-PerSessionThreadAuto] [-PerSessionThread <Int32>] [-BatchSize <Int32>] [-PassThru]
 [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors]
 [-Transport <SBTransport>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Queue
```
Send-SBMessage [-Message <PSMessage[]>] [-ReceivedInputObject <ServiceBusReceivedMessage[]>] [-Queue <String>]
 [-PerSessionThreadAuto] [-PerSessionThread <Int32>] [-BatchSize <Int32>] [-PassThru]
 [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors]
 [-Transport <SBTransport>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Context
```
Send-SBMessage [-Message <PSMessage[]>] [-ReceivedInputObject <ServiceBusReceivedMessage[]>] [-Queue <String>]
 [-Topic <String>] -SessionContext <SessionContext> [-PerSessionThreadAuto] [-PerSessionThread <Int32>]
 [-BatchSize <Int32>] [-PassThru] [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext]
 [-IgnoreCertificateChainErrors] [-Transport <SBTransport>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Send-SBMessage.
The command supports parameter sets: 'Context', 'Queue', 'Topic'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Context)
```powershell
PS C:\\> Send-SBMessage -SessionContext <SessionContext>
```

Runs Send-SBMessage using the 'Context' parameter set.


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
Parameter Sets: Queue, Context
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
Parameter Sets: Topic, Context
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

### -PassThru
Specifies the PassThru value for this command.

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

### -SessionContext
Session context object returned by New-SBSessionContext.

```yaml
Type: SessionContext
Parameter Sets: Context
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
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
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Transport
Specifies the Transport value for this command.

```yaml
Type: SBTransport
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

### System.Object
## NOTES

## RELATED LINKS
