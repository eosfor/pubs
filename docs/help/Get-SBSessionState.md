---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# Get-SBSessionState

## SYNOPSIS
Reads and returns Service Bus SBSessionState operations.

## SYNTAX

### Queue (Default)
```
Get-SBSessionState [-ServiceBusConnectionString <String>] -SessionId <String> -Queue <String> [-AsString]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```
Get-SBSessionState [-ServiceBusConnectionString <String>] -SessionId <String> -Topic <String>
 -Subscription <String> [-AsString] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Context
```
Get-SBSessionState [-AsString] -SessionContext <SessionContext> [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Get-SBSessionState.
The command supports parameter sets: 'Context', 'Queue', 'Subscription'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Context)
```powershell
PS C:\\> Get-SBSessionState -SessionContext <SessionContext>
```

Runs Get-SBSessionState using the 'Context' parameter set.

### Example 2 (Queue)
```powershell
PS C:\\> Get-SBSessionState -Queue '<queue-name>' -SessionId '<session-id>'
```

Runs Get-SBSessionState using the 'Queue' parameter set.


## PARAMETERS

### -AsString
Specifies the AsString value for this command.

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

### -ServiceBusConnectionString
Connection string for the target Service Bus namespace or emulator.

```yaml
Type: String
Parameter Sets: Queue, Subscription
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
Parameter Sets: Queue, Subscription
Aliases:

Required: True
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
Parameter Sets: Subscription
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

### SBPowerShell.Models.SessionContext
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
