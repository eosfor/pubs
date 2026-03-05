---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Clear-SBDLQ

## SYNOPSIS
Clears Service Bus SBDLQ operations.

## SYNTAX

### Queue
```
Clear-SBDLQ [-Queue <String>] [-TransferDeadLetter] [-BatchSize <Int32>] [-WaitSeconds <Int32>]
 [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```
Clear-SBDLQ [-Topic <String>] [-Subscription <String>] [-TransferDeadLetter] [-BatchSize <Int32>]
 [-WaitSeconds <Int32>] [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Clear-SBDLQ.
The command supports parameter sets: 'Queue', 'Subscription'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1
```powershell
PS C:\\> Clear-SBDLQ
```

Runs Clear-SBDLQ with default parameters.

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

### -TransferDeadLetter
Uses transfer dead-letter subqueue instead of the regular dead-letter subqueue.

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

### -WaitSeconds
Maximum number of seconds to wait for messages when polling.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
