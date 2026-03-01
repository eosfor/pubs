---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# Get-SBAuthorizationRule

## SYNOPSIS
Reads and returns Service Bus SBAuthorizationRule operations.

## SYNTAX

### Queue
```
Get-SBAuthorizationRule -ServiceBusConnectionString <String> -Queue <String> [-Rule <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Topic
```
Get-SBAuthorizationRule -ServiceBusConnectionString <String> -Topic <String> [-Rule <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Get-SBAuthorizationRule.
The command supports parameter sets: 'Queue', 'Topic'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Queue)
```powershell
PS C:\\> Get-SBAuthorizationRule -Queue '<queue-name>' -ServiceBusConnectionString '<connection-string>'
```

Runs Get-SBAuthorizationRule using the 'Queue' parameter set.

### Example 2 (Topic)
```powershell
PS C:\\> Get-SBAuthorizationRule -ServiceBusConnectionString '<connection-string>' -Topic '<topic-name>'
```

Runs Get-SBAuthorizationRule using the 'Topic' parameter set.


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

### -Rule
Rule name to target.

```yaml
Type: String
Parameter Sets: (All)
Aliases: Name

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

### None
## OUTPUTS

### Azure.Messaging.ServiceBus.Administration.AuthorizationRule
## NOTES

## RELATED LINKS
