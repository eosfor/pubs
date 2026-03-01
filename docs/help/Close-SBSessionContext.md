---
external help file: SBPowerShell.dll-Help.xml
Module Name: SBPowerShell
online version:
schema: 2.0.0
---

# Close-SBSessionContext

## SYNOPSIS
Closes Service Bus SBSessionContext operations.

## SYNTAX

```
Close-SBSessionContext -Context <SessionContext[]> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Close-SBSessionContext.
The command supports parameter sets: '__AllParameterSets'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (__AllParameterSets)
```powershell
PS C:\\> Close-SBSessionContext -Context <SessionContext[]>
```

Runs Close-SBSessionContext using the '__AllParameterSets' parameter set.


## PARAMETERS

### -Context
Specifies the Context value for this command.

```yaml
Type: SessionContext[]
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
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

### SBPowerShell.Models.SessionContext[]
## OUTPUTS

### System.Object
## NOTES

## RELATED LINKS
