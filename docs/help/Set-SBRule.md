---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Set-SBRule

## SYNOPSIS
Updates Service Bus SBRule operations.

## SYNTAX

### Sql (Default)
```
Set-SBRule -ServiceBusConnectionString <String> -Topic <String> -Subscription <String> [-Rule] <String>
 [-SqlFilter <String>] [-SqlAction <String>] [-ClearAction] [-ProgressAction <ActionPreference>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

### Correlation
```
Set-SBRule -ServiceBusConnectionString <String> -Topic <String> -Subscription <String> [-Rule] <String>
 [-CorrelationId <String>] [-MessageId <String>] [-To <String>] [-ReplyTo <String>] [-Subject <String>]
 [-SessionId <String>] [-ReplyToSessionId <String>] [-ContentType <String>] [-CorrelationProperty <Hashtable>]
 [-SqlAction <String>] [-ClearAction] [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Set-SBRule.
The command supports parameter sets: 'Correlation', 'Sql'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Correlation)
```powershell
PS C:\\> Set-SBRule -ServiceBusConnectionString '<connection-string>' -Subscription '<subscription-name>' -Topic '<topic-name>' -Rule '<rule-name>'
```

Runs Set-SBRule using the 'Correlation' parameter set.

### Example 2 (Sql)
```powershell
PS C:\\> Set-SBRule -ServiceBusConnectionString '<connection-string>' -Subscription '<subscription-name>' -Topic '<topic-name>' -Rule '<rule-name>'
```

Runs Set-SBRule using the 'Sql' parameter set.


## PARAMETERS

### -ClearAction
Specifies the ClearAction value for this command.

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

### -ContentType
Specifies the ContentType value for this command.

```yaml
Type: String
Parameter Sets: Correlation
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CorrelationId
Correlation identifier used in message filters or correlation matching.

```yaml
Type: String
Parameter Sets: Correlation
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CorrelationProperty
Hashtable of correlation properties used by correlation filters.

```yaml
Type: Hashtable
Parameter Sets: Correlation
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MessageId
Specifies the MessageId value for this command.

```yaml
Type: String
Parameter Sets: Correlation
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplyTo
Specifies the ReplyTo value for this command.

```yaml
Type: String
Parameter Sets: Correlation
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplyToSessionId
Specifies the ReplyToSessionId value for this command.

```yaml
Type: String
Parameter Sets: Correlation
Aliases:

Required: False
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
Aliases: Name, RuleName

Required: True
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

### -SessionId
Session identifier for session-enabled entities.

```yaml
Type: String
Parameter Sets: Correlation
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SqlAction
SQL action expression executed when a rule matches.

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

### -SqlFilter
SQL filter expression used by a rule or subscription creation path.

```yaml
Type: String
Parameter Sets: Sql
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Subject
Specifies the Subject value for this command.

```yaml
Type: String
Parameter Sets: Correlation
Aliases:

Required: False
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

### -To
Specifies the To value for this command.

```yaml
Type: String
Parameter Sets: Correlation
Aliases:

Required: False
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### Azure.Messaging.ServiceBus.Administration.RuleProperties
## NOTES

## RELATED LINKS
