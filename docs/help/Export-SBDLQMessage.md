---
external help file: SBPowerShell.dll-Help.xml
Module Name: pubs
online version:
schema: 2.0.0
---

# Export-SBDLQMessage

## SYNOPSIS
Exports dead-lettered Service Bus messages to `json` or `jsonl` without modifying broker state.

## SYNTAX

### Queue
```powershell
Export-SBDLQMessage -Queue <String> -OutputPath <String> [-Format <SBExportFormat>] [-MaxMessages <Int32>]
 [-FromSequenceNumber <Int64>] [-CheckpointPath <String>] [-ServiceBusConnectionString <String>]
 [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors] [-Transport <SBTransport>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### Subscription
```powershell
Export-SBDLQMessage -Topic <String> -Subscription <String> -OutputPath <String> [-Format <SBExportFormat>]
 [-MaxMessages <Int32>] [-FromSequenceNumber <Int64>] [-CheckpointPath <String>]
 [-ServiceBusConnectionString <String>] [-Context <SBContext>] [-NoContext]
 [-IgnoreCertificateChainErrors] [-Transport <SBTransport>] [-ProgressAction <ActionPreference>]
 [<CommonParameters>]
```

### Context
```powershell
Export-SBDLQMessage -OutputPath <String> [-Format <SBExportFormat>] [-MaxMessages <Int32>]
 [-FromSequenceNumber <Int64>] [-CheckpointPath <String>] [-ServiceBusConnectionString <String>]
 [-Context <SBContext>] [-NoContext] [-IgnoreCertificateChainErrors] [-Transport <SBTransport>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Exports queue or subscription dead-letter queue messages using `Peek` only. The command does not lock, complete, abandon, or otherwise settle messages. Output includes all high-level `ServiceBusReceivedMessage` fields surfaced by the current SDK, application properties, and the message body in lossless base64 form plus best-effort UTF-8 text.

`Jsonl` is intended for large or resumable exports. `Json` writes a single JSON array and is convenient for bounded exports.

## EXAMPLES

### Example 1
```powershell
PS C:\> Export-SBDLQMessage -Topic "sales" -Subscription "audit" -OutputPath "./audit-dlq.json" -Format Json -MaxMessages 5000
```

Exports up to 5000 dead-lettered messages from `sales/audit` to a JSON array.

### Example 2
```powershell
PS C:\> Export-SBDLQMessage -Queue "orders" -OutputPath "./orders-dlq.jsonl" -FromSequenceNumber 120000 -CheckpointPath "./orders-dlq.checkpoint.json"
```

Exports queue DLQ messages to JSONL and resumes from a checkpoint file when present.

### Example 3
```powershell
PS C:\> Export-SBDLQMessage -OutputPath "./current-dlq.jsonl"
```

Exports dead-letter messages from the current `SBContext` target.

## PARAMETERS

### -CheckpointPath
Optional checkpoint file. Supported only with `Jsonl`. When present, export resumes from the saved `LastSequenceNumber + 1`.

### -Format
Output format. Allowed values: `Json`, `Jsonl`.

### -FromSequenceNumber
Starting sequence number for peek-based export pagination.

### -MaxMessages
Maximum number of messages to export.

### -OutputPath
Destination file path for the export.
