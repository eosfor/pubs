---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# New-SBMessage

## SYNOPSIS
Creates Service Bus SBMessage operations.

## SYNTAX

### ByParts (Default)
```
New-SBMessage -Body <String[]> [-SessionId <String>] [-CustomProperties <Hashtable[]>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### ByHashTable
```
New-SBMessage -HashTable <Hashtable[]> [-NeedSessionId] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### FromPipeline
```
New-SBMessage [-NeedSessionId] -InputObject <Hashtable> [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for New-SBMessage.
The command supports parameter sets: 'ByHashTable', 'ByParts', 'FromPipeline'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (ByHashTable)
```powershell
PS C:\\> New-SBMessage -HashTable <Hashtable[]>
```

Runs New-SBMessage using the 'ByHashTable' parameter set.

### Example 2 (ByParts)
```powershell
PS C:\\> New-SBMessage -Body @('message-body')
```

Runs New-SBMessage using the 'ByParts' parameter set.


## PARAMETERS

### -Body
Message body content.

```yaml
Type: String[]
Parameter Sets: ByParts
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CustomProperties
Application properties added to the message.

```yaml
Type: Hashtable[]
Parameter Sets: ByParts
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HashTable
Specifies the HashTable value for this command.

```yaml
Type: Hashtable[]
Parameter Sets: ByHashTable
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Specifies the InputObject value for this command.

```yaml
Type: Hashtable
Parameter Sets: FromPipeline
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -NeedSessionId
Specifies the NeedSessionId value for this command.

```yaml
Type: SwitchParameter
Parameter Sets: ByHashTable, FromPipeline
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -SessionId
Session identifier for session-enabled entities.

```yaml
Type: String
Parameter Sets: ByParts
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

### System.Collections.Hashtable
## OUTPUTS

### SBPowerShell.Models.PSMessage
## NOTES

## RELATED LINKS
