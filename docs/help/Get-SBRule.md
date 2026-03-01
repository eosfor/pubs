---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# Get-SBRule

## SYNOPSIS
Reads and returns Service Bus SBRule operations.

## SYNTAX

### All (Default)
```
Get-SBRule -ServiceBusConnectionString <String> -Topic <String> -Subscription <String>
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### ByName
```
Get-SBRule -ServiceBusConnectionString <String> -Topic <String> -Subscription <String> [[-Rule] <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Get-SBRule.
The command supports parameter sets: 'All', 'ByName'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (All)
```powershell
PS C:\\> Get-SBRule -ServiceBusConnectionString '<connection-string>' -Subscription '<subscription-name>' -Topic '<topic-name>'
```

Runs Get-SBRule using the 'All' parameter set.

### Example 2 (ByName)
```powershell
PS C:\\> Get-SBRule -ServiceBusConnectionString '<connection-string>' -Subscription '<subscription-name>' -Topic '<topic-name>'
```

Runs Get-SBRule using the 'ByName' parameter set.


## PARAMETERS

### -Rule
Rule name to target.

```yaml
Type: String
Parameter Sets: ByName
Aliases: Name, RuleName

Required: False
Position: 0
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

### -Subscription
Subscription name to target.

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
Parameter Sets: (All)
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

### Azure.Messaging.ServiceBus.Administration.RuleProperties
## NOTES

## RELATED LINKS
