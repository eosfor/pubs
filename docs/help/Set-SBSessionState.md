---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# Set-SBSessionState

## SYNOPSIS
Updates Service Bus SBSessionState operations.

## SYNTAX

### Queue (Default)
```
Set-SBSessionState [-ServiceBusConnectionString <String>] -SessionId <String> -Queue <String> -State <Object>
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```
Set-SBSessionState [-ServiceBusConnectionString <String>] -SessionId <String> -Topic <String>
 -Subscription <String> -State <Object> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Context
```
Set-SBSessionState -State <Object> -SessionContext <SessionContext> [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Set-SBSessionState.
The command supports parameter sets: 'Context', 'Queue', 'Subscription'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Context)
```powershell
PS C:\\> Set-SBSessionState -SessionContext <SessionContext> -State @{ key='value' }
```

Runs Set-SBSessionState using the 'Context' parameter set.

### Example 2 (Queue)
```powershell
PS C:\\> Set-SBSessionState -Queue '<queue-name>' -SessionId '<session-id>' -State @{ key='value' }
```

Runs Set-SBSessionState using the 'Queue' parameter set.


## PARAMETERS

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

### -State
Session state payload to persist for the selected session.

```yaml
Type: Object
Parameter Sets: (All)
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

### System.Object
### SBPowerShell.Models.SessionContext
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
