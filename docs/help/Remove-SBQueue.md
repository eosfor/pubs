---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Remove-SBQueue

## SYNOPSIS
Removes Service Bus SBQueue operations.

## SYNTAX

```
Remove-SBQueue [[-Queue] <String>] [-Force] [-ServiceBusConnectionString <String>] [-Context <SBContext>]
 [-NoContext] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Remove-SBQueue.
The command supports parameter sets: '__AllParameterSets'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1
```powershell
PS C:\\> Remove-SBQueue
```

Runs Remove-SBQueue with default parameters.

## PARAMETERS

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Bypasses confirmation prompts where supported.

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
Parameter Sets: (All)
Aliases: Name, QueueName

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

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: False
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
