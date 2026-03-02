---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Get-SBTopic

## SYNOPSIS
Reads and returns Service Bus SBTopic operations.

## SYNTAX

### All (Default)
```
Get-SBTopic -ServiceBusConnectionString <String> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### ByName
```
Get-SBTopic -ServiceBusConnectionString <String> [[-Topic] <String>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Get-SBTopic.
The command supports parameter sets: 'All', 'ByName'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (All)
```powershell
PS C:\\> Get-SBTopic -ServiceBusConnectionString '<connection-string>'
```

Runs Get-SBTopic using the 'All' parameter set.

### Example 2 (ByName)
```powershell
PS C:\\> Get-SBTopic -ServiceBusConnectionString '<connection-string>'
```

Runs Get-SBTopic using the 'ByName' parameter set.


## PARAMETERS

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
Parameter Sets: ByName
Aliases: Name, TopicName

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
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

### System.String
## OUTPUTS

### Azure.Messaging.ServiceBus.Administration.TopicProperties
## NOTES

## RELATED LINKS
