---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Set-SBContext

## SYNOPSIS
Updates Service Bus SBContext operations.

## SYNTAX

### Namespace
```
Set-SBContext -ServiceBusConnectionString <String> [-Strict] [-NoClobber] [-PassThru]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Queue
```
Set-SBContext [-ServiceBusConnectionString <String>] -Queue <String> [-Strict] [-NoClobber] [-PassThru]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```
Set-SBContext [-ServiceBusConnectionString <String>] -Topic <String> -Subscription <String> [-Strict]
 [-NoClobber] [-PassThru] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### FromObject
```
Set-SBContext -InputObject <SBContext> [-Strict] [-NoClobber] [-PassThru] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Set-SBContext.
The command supports parameter sets: 'FromObject', 'Namespace', 'Queue', 'Subscription'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (FromObject)
```powershell
PS C:\\> Set-SBContext -InputObject <SBContext>
```

Runs Set-SBContext using the 'FromObject' parameter set.

### Example 2 (Namespace)
```powershell
PS C:\\> Set-SBContext -ServiceBusConnectionString '<connection-string>'
```

Runs Set-SBContext using the 'Namespace' parameter set.


## PARAMETERS

### -InputObject
Specifies the InputObject value for this command.

```yaml
Type: SBContext
Parameter Sets: FromObject
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -NoClobber
Specifies the NoClobber value for this command.

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

### -PassThru
Specifies the PassThru value for this command.

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
Parameter Sets: Namespace
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

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

### -Strict
Specifies the Strict value for this command.

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

### SBPowerShell.Models.SBContext
## OUTPUTS

### SBPowerShell.Models.SBContext
## NOTES

## RELATED LINKS
