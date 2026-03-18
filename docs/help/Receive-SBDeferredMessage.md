---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Receive-SBDeferredMessage

## SYNOPSIS
Receives Service Bus SBDeferredMessage operations.

## SYNTAX

### Queue (Default)
```
Receive-SBDeferredMessage -SequenceNumber <Int64[]> [-ChunkSize <Int32>] [-Queue <String>]
 [-SessionId <String>] [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext]
 [-IgnoreCertificateChainErrors] [-Transport <SBTransport>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### Subscription
```
Receive-SBDeferredMessage -SequenceNumber <Int64[]> [-ChunkSize <Int32>] [-Topic <String>]
 [-Subscription <String>] [-SessionId <String>] [-ServiceBusConnectionString <String>] [-Context <SBContext>]
 [-NoContext] [-IgnoreCertificateChainErrors] [-Transport <SBTransport>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### Context
```
Receive-SBDeferredMessage -SequenceNumber <Int64[]> [-ChunkSize <Int32>] [-SessionId <String>]
 -SessionContext <SessionContext> [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext]
 [-IgnoreCertificateChainErrors] [-Transport <SBTransport>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Receive-SBDeferredMessage.
The command supports parameter sets: 'Context', 'Queue', 'Subscription'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Context)
```powershell
PS C:\\> Receive-SBDeferredMessage -SequenceNumber @(1) -SessionContext <SessionContext>
```

Runs Receive-SBDeferredMessage using the 'Context' parameter set.

### Example 2 (Queue)
```powershell
PS C:\\> Receive-SBDeferredMessage -SequenceNumber @(1)
```

Runs Receive-SBDeferredMessage using the 'Queue' parameter set.


## PARAMETERS

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

### -SequenceNumber
Sequence number values used to target specific scheduled or deferred messages.

```yaml
Type: Int64[]
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

### -SessionId
Session identifier for session-enabled entities.

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
Parameter Sets: Subscription
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
Parameter Sets: Subscription
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

### -ChunkSize
Specifies the ChunkSize value for this command.

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

### SBPowerShell.Models.SessionContext
## OUTPUTS

### Azure.Messaging.ServiceBus.ServiceBusReceivedMessage
## NOTES

## RELATED LINKS
