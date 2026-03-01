---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# Get-SBQueue

## SYNOPSIS
Reads and returns Service Bus SBQueue operations.

## SYNTAX

### All (Default)
```
Get-SBQueue -ServiceBusConnectionString <String> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### ByName
```
Get-SBQueue -ServiceBusConnectionString <String> [[-Queue] <String>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Get-SBQueue.
The command supports parameter sets: 'All', 'ByName'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (All)
```powershell
PS C:\\> Get-SBQueue -ServiceBusConnectionString '<connection-string>'
```

Runs Get-SBQueue using the 'All' parameter set.

### Example 2 (ByName)
```powershell
PS C:\\> Get-SBQueue -ServiceBusConnectionString '<connection-string>'
```

Runs Get-SBQueue using the 'ByName' parameter set.


## PARAMETERS

### -Queue
Queue name to target.

```yaml
Type: String
Parameter Sets: ByName
Aliases: Name, QueueName

Required: False
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
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

### Azure.Messaging.ServiceBus.Administration.QueueProperties
## NOTES

## RELATED LINKS
