---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Get-SBSubscription

## SYNOPSIS
Reads and returns Service Bus SBSubscription operations.

## SYNTAX

### ByName (Default)
```
Get-SBSubscription -ServiceBusConnectionString <String> [-Topic] <String> [-Subscription <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### ByTopicObject
```
Get-SBSubscription -ServiceBusConnectionString <String> -InputObject <TopicProperties> [-Subscription <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Use this cmdlet to perform Service Bus management or data-plane tasks for Get-SBSubscription.
The command supports parameter sets: 'ByName', 'ByTopicObject'.
Provide -ServiceBusConnectionString where required and target the appropriate queue, topic, subscription, or rule parameters.

## EXAMPLES

### Example 1 (ByName)
```powershell
PS C:\\> Get-SBSubscription -ServiceBusConnectionString '<connection-string>' -Topic '<topic-name>'
```

Runs Get-SBSubscription using the 'ByName' parameter set.

### Example 2 (ByTopicObject)
```powershell
PS C:\\> Get-SBSubscription -InputObject <TopicProperties> -ServiceBusConnectionString '<connection-string>'
```

Runs Get-SBSubscription using the 'ByTopicObject' parameter set.


## PARAMETERS

### -InputObject
Specifies the InputObject value for this command.

```yaml
Type: TopicProperties
Parameter Sets: ByTopicObject
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
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

### -Subscription
Subscription name to target.

```yaml
Type: String
Parameter Sets: (All)
Aliases: SubscriptionName, SubscriptionMame

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Topic
Topic name to target.

```yaml
Type: String
Parameter Sets: ByName
Aliases: TopicName, Name

Required: True
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
### Azure.Messaging.ServiceBus.Administration.TopicProperties
## OUTPUTS

### Azure.Messaging.ServiceBus.Administration.SubscriptionProperties
## NOTES

## RELATED LINKS
