---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Get-SBSession

## SYNOPSIS
Reads and returns Service Bus SBSession operations.

## SYNTAX

### Subscription (Default)
```
Get-SBSession [[-Topic] <String>] [[-Subscription] <String>] [-ActiveOnly] [-LastUpdatedSince <DateTime>]
 [-OperationTimeoutSec <Int32>] [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Queue
```
Get-SBSession [[-Queue] <String>] [-ActiveOnly] [-LastUpdatedSince <DateTime>] [-OperationTimeoutSec <Int32>]
 [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Context
```
Get-SBSession -SessionContext <SessionContext> [-ServiceBusConnectionString <String>] [-Context <SBContext>]
 [-NoContext] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Get-SBSession.
The command supports parameter sets: 'Context', 'Queue', 'Subscription'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (Context)
```powershell
PS C:\\> Get-SBSession -SessionContext <SessionContext>
```

Runs Get-SBSession using the 'Context' parameter set.


## PARAMETERS

### -ActiveOnly
Specifies the ActiveOnly value for this command.

```yaml
Type: SwitchParameter
Parameter Sets: Subscription, Queue
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -LastUpdatedSince
Specifies the LastUpdatedSince value for this command.

```yaml
Type: DateTime
Parameter Sets: Subscription, Queue
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OperationTimeoutSec
Specifies the OperationTimeoutSec value for this command.

```yaml
Type: Int32
Parameter Sets: Subscription, Queue
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Queue
Queue name to target.

```yaml
Type: String
Parameter Sets: Queue
Aliases:

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

### -SessionContext
Session context object returned by New-SBSessionContext.

```yaml
Type: SessionContext
Parameter Sets: Context
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Subscription
Subscription name to target.

```yaml
Type: String
Parameter Sets: Subscription
Aliases:

Required: False
Position: 1
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

Required: False
Position: 0
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### SBPowerShell.Models.SessionContext
## OUTPUTS

### SBPowerShell.Models.SBSessionInfo
## NOTES

## RELATED LINKS
