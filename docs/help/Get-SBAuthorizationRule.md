---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Get-SBAuthorizationRule

## SYNOPSIS
Reads and returns Service Bus SBAuthorizationRule operations.

## SYNTAX

### Queue
```
Get-SBAuthorizationRule [-Queue <String>] [-Rule <String>] [-ServiceBusConnectionString <String>]
 [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors] [-Transport <SBTransport>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Topic
```
Get-SBAuthorizationRule [-Topic <String>] [-Rule <String>] [-ServiceBusConnectionString <String>]
 [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors] [-Transport <SBTransport>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Get-SBAuthorizationRule.
The command supports parameter sets: 'Queue', 'Topic'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1
```powershell
PS C:\\> Get-SBAuthorizationRule
```

Runs Get-SBAuthorizationRule with default parameters.

## PARAMETERS

### -Queue
Queue name to target.

```yaml
Type: String
Parameter Sets: Queue
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
Parameter Sets: Topic
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

### -IgnoreCertificateChainErrors
Specifies the IgnoreCertificateChainErrors value for this command.

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

### -Transport
Specifies the Transport value for this command.

```yaml
Type: SBTransport
Parameter Sets: (All)
Aliases:

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
