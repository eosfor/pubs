---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Set-SBMessage

## SYNOPSIS
Updates Service Bus SBMessage operations.

## SYNTAX

### Queue (Default)
```
Set-SBMessage [-Queue <String>] -Message <ServiceBusReceivedMessage[]> [-Complete] [-Abandon] [-Defer]
 [-DeadLetter] [-DeadLetterReason <String>] [-DeadLetterErrorDescription <String>]
 [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors]
 [-Transport <SBTransport>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Context
```
Set-SBMessage [-Queue <String>] [-Topic <String>] [-Subscription <String>]
 -Message <ServiceBusReceivedMessage[]> [-Complete] [-Abandon] [-Defer] [-DeadLetter]
 [-DeadLetterReason <String>] [-DeadLetterErrorDescription <String>] -SessionContext <SessionContext>
 [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors]
 [-Transport <SBTransport>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```
Set-SBMessage [-Topic <String>] [-Subscription <String>] -Message <ServiceBusReceivedMessage[]> [-Complete]
 [-Abandon] [-Defer] [-DeadLetter] [-DeadLetterReason <String>] [-DeadLetterErrorDescription <String>]
 [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors]
 [-Transport <SBTransport>] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Set-SBMessage.
The command supports parameter sets: 'Context', 'Queue', 'Subscription'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Context)
```powershell
PS C:\\> Set-SBMessage -Message <PSMessage[]> -SessionContext <SessionContext>
```

Runs Set-SBMessage using the 'Context' parameter set.

### Example 2 (Queue)
```powershell
PS C:\\> Set-SBMessage -Message <PSMessage[]>
```

Runs Set-SBMessage using the 'Queue' parameter set.


## PARAMETERS

### -Abandon
Abandons message(s) and releases the lock.

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

### -Complete
Completes message(s) and removes them from the entity.

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

### -DeadLetter
Moves message(s) to dead-letter subqueue.

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

### -DeadLetterErrorDescription
Detailed dead-letter error description.

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

### -DeadLetterReason
Reason text stored when dead-lettering a message.

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

### -Defer
Defers message(s) for later retrieval by sequence number.

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

### -Message
Message objects to send or process.

```yaml
Type: ServiceBusReceivedMessage[]
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
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

### -Subscription
Subscription name to target.

```yaml
Type: String
Parameter Sets: Context, Subscription
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
Parameter Sets: Context, Subscription
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

### Azure.Messaging.ServiceBus.ServiceBusReceivedMessage[]
### SBPowerShell.Models.SessionContext
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
