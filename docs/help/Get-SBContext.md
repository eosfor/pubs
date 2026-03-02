---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Get-SBContext

## SYNOPSIS
Reads and returns Service Bus SBContext operations.

## SYNTAX

### Default (Default)
```
Get-SBContext [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Raw
```
Get-SBContext [-Raw] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### ConnectionString
```
Get-SBContext [-AsConnectionString] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Returns current runspace-local SBContext. Default output is a safe view without secrets; use -Raw for the full object or -AsConnectionString for the raw connection string.

## EXAMPLES

### Example 1
```powershell
PS C:\\> Get-SBContext
```

Runs Get-SBContext with default parameters.

## PARAMETERS

### -AsConnectionString
Specifies the AsConnectionString value for this command.

```yaml
Type: SwitchParameter
Parameter Sets: ConnectionString
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Raw
Specifies the Raw value for this command.

```yaml
Type: SwitchParameter
Parameter Sets: Raw
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### System.Management.Automation.PSObject
### SBPowerShell.Models.SBContext
### System.String
## NOTES

## RELATED LINKS
