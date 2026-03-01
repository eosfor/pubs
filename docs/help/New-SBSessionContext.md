---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# New-SBSessionContext

## SYNOPSIS
Creates Service Bus SBSessionContext operations.

## SYNTAX

### Queue (Default)
```
New-SBSessionContext -ServiceBusConnectionString <String> -SessionId <String> -Queue <String>
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```
New-SBSessionContext -ServiceBusConnectionString <String> -SessionId <String> -Topic <String>
 -Subscription <String> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for New-SBSessionContext.
The command supports parameter sets: 'Queue', 'Subscription'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Queue)
```powershell
PS C:\\> New-SBSessionContext -Queue '<queue-name>' -ServiceBusConnectionString '<connection-string>' -SessionId '<session-id>'
```

Runs New-SBSessionContext using the 'Queue' parameter set.

### Example 2 (Subscription)
```powershell
PS C:\\> New-SBSessionContext -ServiceBusConnectionString '<connection-string>' -SessionId '<session-id>' -Subscription '<subscription-name>' -Topic '<topic-name>'
```

Runs New-SBSessionContext using the 'Subscription' parameter set.


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
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SessionId
Session identifier for session-enabled entities.

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

### None
## OUTPUTS

### SBPowerShell.Models.SessionContext
## NOTES

## RELATED LINKS
