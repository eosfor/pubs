# pubs ![CI](https://github.com/eosfor/pubs/actions/workflows/ci.yml/badge.svg)

PowerShell module for Azure Service Bus and local Service Bus Emulator workflows.

## Features
- `New-SBMessage` - create message template(s) with SessionId and application properties.
- `Send-SBMessage` - send to queue or topic; supports per-session parallel sending (`-PerSessionThreadAuto` or `-PerSessionThread`).
- `Receive-SBMessage` - read from queue or subscription; supports peek (`-Peek`) without removal and `-NoComplete` for manual settlement; automatically switches to a session receiver when the entity requires sessions; supports `-SessionContext` to reuse an open session receiver.
- `Receive-SBDLQMessage` - read dead-letter queue/subscription; supports `-Peek`, `-NoComplete`, `-MaxMessages`; automatically switches to session DLQ when needed.
- `Receive-SBDeferredMessage` - receive deferred messages by SequenceNumber (sessions are supported).
- `Set-SBMessage` - manually complete/abandon/defer/dead-letter received messages.
- `New-SBSessionState` - create a strongly typed session state object (DSO) from primitives.
- `Get-SBSessionState`, `Set-SBSessionState` - read/write session state as DSO (BinaryData transport, JSON under the hood), without PowerShell JSON flattening issues.
- `New-SBSessionContext`, `Close-SBSessionContext` - open and reuse a session receiver to perform receive/settle/state operations within the same lock.
- `Clear-SBQueue`, `Clear-SBSubscription` - clear queue or subscription in batches.
- `Get-SBTopic` - list topics with SDK metadata (`TopicProperties`) and runtime data.
- `Get-SBSubscription` - list subscriptions for a topic with metadata (`SubscriptionProperties`) and runtime data, including message counts.

## Requirements
- .NET 8/9 SDK for build.
- PowerShell 7+.
- For local runs: Service Bus Emulator + SQL Edge (`docker-compose.sbus.yml`) and `.env` with `SAS_KEY_VALUE`, `SQL_PASSWORD`, `ACCEPT_EULA` (optional: `EMULATOR_HOST`, `EMULATOR_HTTP_PORT`, `EMULATOR_AMQP_PORT`, `SQL_WAIT_INTERVAL`).

Example `.env`:
```dotenv
SAS_KEY_VALUE=LocalEmulatorKey123!
SQL_PASSWORD=Pa55w0rd1!
ACCEPT_EULA=Y
EMULATOR_HOST=localhost
EMULATOR_HTTP_PORT=5300
EMULATOR_AMQP_PORT=5672
```

## Quick Start (Emulator)
```bash
docker compose -f docker-compose.sbus.yml up -d
dotnet build          # builds the module into src/SBPowerShell/bin/Debug/net8.0
pwsh -NoLogo          # run the commands below in PowerShell
```

Import module from build output:
```pwsh
$module = "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
Import-Module $module -Force
```

Send and receive:
```pwsh
$conn = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=LocalEmulatorKey123!;UseDevelopmentEmulator=true;"
$messages = New-SBMessage -Body "hello","world" -CustomProperties @{ prop="v1" }
Send-SBMessage -Queue "test-queue" -Message $messages -ServiceBusConnectionString $conn
Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 2

# Topic/subscription
$topicMsgs = New-SBMessage -Body "t1","t2"
Send-SBMessage -Topic "test-topic" -Message $topicMsgs -ServiceBusConnectionString $conn
Receive-SBMessage -Topic "test-topic" -Subscription "test-sub" -ServiceBusConnectionString $conn -MaxMessages 2
```

- For topic send, use `-Topic`. For topic read, use both `-Topic` and `-Subscription`. For queue scenarios, `-Queue` is enough.
- `-Subscription` is only used when receiving from a topic.

### WaitSeconds Behavior
- `-MaxMessages` and `-WaitSeconds` are mutually exclusive modes (different parameter sets). Use only one in a single call.
- `WaitSeconds` sets the upper wait bound for one receive call: if no messages arrive in that window, the command returns an empty collection.
- If neither `-MaxMessages` nor `-WaitSeconds` is provided, the command does continuous polling until cancelled (for example `Ctrl+C`).
- For stream-style processing, call receive in a loop with your own exit logic:
  ```pwsh
  while ($true) {
      $batch = @(Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -BatchSize 50 -WaitSeconds 2)
      if ($batch.Count -eq 0) { break } # or custom break/sleep logic
      # process $batch
  }
  ```
- For strict work with a specific session, use `-SessionContext` to avoid cross-session wait interference.

View topic/subscription metadata:
```pwsh
# all topics (TopicProperties + RuntimeProperties)
Get-SBTopic -ServiceBusConnectionString $conn

# all subscriptions for a topic, with runtime data (ActiveMessageCount, etc.)
Get-SBSubscription -ServiceBusConnectionString $conn -Topic "test-topic"

# single subscription filter
Get-SBSubscription -ServiceBusConnectionString $conn -Topic "test-topic" -Subscription "test-sub"

# pipeline: pass TopicProperties objects downstream
Get-SBTopic -ServiceBusConnectionString $conn | Get-SBSubscription -ServiceBusConnectionString $conn
```

Per-session sending with auto parallelism:
```pwsh
$s1 = New-SBMessage -SessionId "sess-1" -Body "a1","a2","a3"
$s2 = New-SBMessage -SessionId "sess-2" -Body "b1","b2","b3"
Send-SBMessage -Queue "session-queue" -Message ($s1 + $s2) -ServiceBusConnectionString $conn -PerSessionThreadAuto
Receive-SBMessage -Queue "session-queue" -ServiceBusConnectionString $conn -MaxMessages 6

# Same pattern for topic/subscription with a session-enabled entity:
# Send-SBMessage -Topic "session-topic" -Message ($s1 + $s2) -ServiceBusConnectionString $conn -PerSessionThreadAuto
# Receive-SBMessage -Topic "session-topic" -Subscription "session-sub" -ServiceBusConnectionString $conn -MaxMessages 6
```

Message creation in loops with explicit type casting:
```pwsh
# When using 1..10 in CustomProperties, cast to [int] explicitly,
# because 1..10 can produce PSObject wrappers that the Service Bus SDK cannot serialize correctly.
1..10 | ForEach-Object {
    New-SBMessage -Body "hello world $_" -CustomProperties @{ prop="v1"; order=[int]$_ } -SessionId "myLovelySession"
} | ForEach-Object {
    Send-SBMessage -ServiceBusConnectionString $conn -Topic "NO_SESSION" -Message $_
    Start-Sleep -Milliseconds 1500
}
```

Peek without removal:
```pwsh
$peeked = Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 5 -Peek
# messages remain in the queue and can be received later in a normal receive call

# For topic:
# $peeked = Receive-SBMessage -Topic "test-topic" -Subscription "test-sub" -ServiceBusConnectionString $conn -MaxMessages 5 -Peek

# Dead-letter queue/subscription
Receive-SBDLQMessage -Queue "test-queue" -ServiceBusConnectionString $conn -Peek -MaxMessages 10
Receive-SBDLQMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 10     # reads and Completes

Receive-SBDLQMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -Peek -MaxMessages 10
Receive-SBDLQMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -MaxMessages 10
# Default behavior: DLQ messages are completed (removed). Use -Peek to read without removal.
# For manual settlement flow, use -NoComplete and then pipe to Set-SBMessage.
# Without -MaxMessages and -WaitSeconds, the command does continuous polling until cancelled (Ctrl+C).
# For bounded runs, use either -MaxMessages or -WaitSeconds. Session DLQ is handled automatically.
```

Manual settlement/defer flow:
```pwsh
# Receive without Complete
$msg = Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 1 -NoComplete

# Defer and then fetch by SequenceNumber
$deferredSeq = ($msg | Set-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -Defer).SequenceNumber
$again = Receive-SBDeferredMessage -Queue "test-queue" -ServiceBusConnectionString $conn -SequenceNumber $deferredSeq

# Or complete manually
$msg | Set-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -Complete
```

SessionContext reuse:
```pwsh
# Open a session receiver once
$ctx = New-SBSessionContext -Queue "session-queue" -SessionId "sess-1" -ServiceBusConnectionString $conn

Receive-SBMessage -SessionContext $ctx -MaxMessages 5 -NoComplete |
    Set-SBMessage -SessionContext $ctx -Complete

# Work with session state under the same lock
Get-SBSessionState -SessionContext $ctx
Set-SBSessionState -SessionContext $ctx -State @{ Progress = 42 }

Close-SBSessionContext -Context $ctx
```

Deferred -> active recovery within one session:
```pwsh
param(
    [Parameter(Mandatory)] [string]$ConnStr,
    [Parameter(Mandatory)] [string]$SessionId,
    [string]$Topic = "NO_SESSION",
    [string]$Subscription = "STATE_SUB",
    [string]$SnapDir = "./out",
    [int]$ChunkSize = 200,
    [int]$BatchSize = 100
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Path $SnapDir -Force | Out-Null
$snapFile = Join-Path $SnapDir "state.$SessionId.before.json"

$ctx = New-SBSessionContext -Topic $Topic -Subscription $Subscription -SessionId $SessionId -ServiceBusConnectionString $ConnStr
try {
    # 1) backup state
    $stateJson = Get-SBSessionState -SessionContext $ctx -AsString
    $stateJson | Set-Content $snapFile -Encoding UTF8
    $state = $stateJson | ConvertFrom-Json

    # 2) collect deferred sequence numbers
    $seqs = @(
        $state.Deferred | ForEach-Object {
            if ($_.PSObject.Properties.Name -contains "SeqNumber") { [int64]$_.SeqNumber }
            elseif ($_.PSObject.Properties.Name -contains "seq")   { [int64]$_.seq }
            elseif ($_.PSObject.Properties.Name -contains "Seq")   { [int64]$_.Seq }
        }
    ) | Where-Object { $_ -ne $null }

    if ($seqs.Count -gt 0) {
        # 3) deferred -> send active copy -> complete old deferred
        Receive-SBDeferredMessage -SessionContext $ctx -SequenceNumber $seqs -ChunkSize $ChunkSize |
            Send-SBMessage -SessionContext $ctx -BatchSize $BatchSize -PassThru |
            Set-SBMessage -SessionContext $ctx -Complete | Out-Null
    }

    # 4) clear deferred in state (supports both LastSeenOrder and LastSeenOrderNum)
    $lastSeen = if ($state.PSObject.Properties.Name -contains "LastSeenOrder") {
        [int]$state.LastSeenOrder
    } else {
        [int]$state.LastSeenOrderNum
    }

    if ($state.PSObject.Properties.Name -contains "LastSeenOrder") {
        $newState = @{ LastSeenOrder = $lastSeen; Deferred = @() }
    } else {
        $newState = @{ LastSeenOrderNum = $lastSeen; Deferred = @() }
    }

    Set-SBSessionState -SessionContext $ctx -State ($newState | ConvertTo-Json -Compress)
}
finally {
    Close-SBSessionContext -Context $ctx
}
```

Stress test for full cycle (producer + consumer + recovery):
```pwsh
# Window 1: producer (send 10k, keep a gap at order=2)
$cs = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=LocalEmulatorKey123!;UseDevelopmentEmulator=true;"
$sid = "stress-session-1"
pwsh ./scripts/stress/send-state-sub-load.ps1 -ConnStr $cs -SessionId $sid -TotalMessages 10000

# Window 2: consumer (crash when Deferred grows to simulate failure)
pwsh ./scripts/stress/run-state-sub-consumer.ps1 -ConnStr $cs -SessionId $sid -CrashDeferredThreshold 3000

# After producer is done, send missing order=2:
$m2 = New-SBMessage -Body (@{ sessionId = $sid; order = 2 } | ConvertTo-Json -Compress) -SessionId $sid -CustomProperties @{ order = 2; sessionId = $sid }
Send-SBMessage -ServiceBusConnectionString $cs -Topic "NO_SESSION" -Message $m2

# Recovery deferred -> active:
pwsh ./scripts/stress/recover-state-sub-deferred.ps1 -ConnStr $cs -SessionId $sid -Topic "NO_SESSION" -Subscription "STATE_SUB"

# Run consumer again to drain recovered active messages:
pwsh ./scripts/stress/run-state-sub-consumer.ps1 -ConnStr $cs -SessionId $sid

# Diagnostics:
pwsh ./scripts/stress/inspect-state-sub.ps1 -ConnStr $cs -SessionId $sid
```

Notes:
- This scenario uses `NO_SESSION/STATE_SUB` (session-enabled), added to `emulator/config.json`.
- If the session is already in hard-fail (`AcceptSession` cannot open), `SessionContext` recovery will not work; use an external runbook / republish into a new session first.

## Streaming Reorder for a Non-Session Topic (`reorderAndForward2.ps1`)
### What It Does
`scripts/orderingTest/reorderAndForward2.ps1` reorders incoming messages from a non-session subscription (`NO_SESSION/NO_SESS_SUB`) by `ApplicationProperties.order`, using session state in `ORDERED_TOPIC/SESS_SUB`.
State is persisted as DSO `SessionOrderingState` (`LastSeenOrderNum:int`, `Deferred: List<OrderSeq{Order:int,Seq:long}>`), so no manual `ConvertFrom/To-Json` handling is required.
The script forwards ordered output messages to `ORDERED_TOPIC` by itself.

### Why Use It
Use this when your source does not guarantee order, but downstream consumers require an ordered stream.
The script defers out-of-order messages until missing `order` values arrive.
Messages with `order` lower than the current expected value are treated as stale and sent to DLQ.

### How To Try It
1. Ensure emulator entities exist: `NO_SESSION/NO_SESS_SUB` and `ORDERED_TOPIC/SESS_SUB` (present by default in `emulator/config.json`).
2. Open 2 PowerShell windows. In both windows, load the module and set the connection string. In the second window, also load the script:
```pwsh
$module = "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
Import-Module $module -Force
$cs = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=LocalEmulatorKey123!;UseDevelopmentEmulator=true;"

# required in the window where Process-Message is called
. ./scripts/orderingTest/reorderAndForward2.ps1
```
3. In the second window, start the processing stream:
```pwsh
Receive-SBMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $cs -NoComplete |
    Process-Message -ConnStr $cs
```
4. In the first window, send shuffled messages (each message must have `SessionId` and integer `order` property):
```pwsh
1..10 |
  ForEach-Object { New-SBMessage -Body "hello world $_" -CustomProperties @{ order = [int]$_; prop = "v1" } -SessionId "myLovelySession" } |
  Sort-Object { Get-Random } |
  ForEach-Object { $_; Send-SBMessage -ServiceBusConnectionString $cs -Topic "NO_SESSION" -Message $_; Start-Sleep -Milliseconds 1500 }
```
5. Stop the processing stream in the second window with `Ctrl+C` after sending is complete.
6. Read and inspect ordered output:
```pwsh
$ordered = @(Receive-SBMessage -Topic "ORDERED_TOPIC" -Subscription "SESS_SUB" -ServiceBusConnectionString $cs -MaxMessages 20)
$ordered | Select-Object @{N='order';E={$_.ApplicationProperties['order']}}, SessionId, SequenceNumber, @{N='Body';E={$_.Body.ToString()}}
```
7. Optionally inspect DLQ for stale `order` values:
```pwsh
Receive-SBDLQMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $cs -Peek -MaxMessages 20
```

## Full Emulator Reset
```bash
docker compose -f docker-compose.sbus.yml down -v   # stop and remove data
docker compose -f docker-compose.sbus.yml pull      # update images
docker compose -f docker-compose.sbus.yml up -d     # start
docker compose -f docker-compose.sbus.yml ps        # check status
```

## Tests
- Pester: `pwsh -NoLogo -File tests/SBPowerShell.Tests.ps1` (uses emulator).
- C# xUnit integration: `dotnet test tests/SBPowerShell.IntegrationTests/SBPowerShell.IntegrationTests.csproj` (also requires a running emulator and `.env`).

## Manual Checks
- `scripts/manual/send-100.ps1` - sends 100 messages `msg1..msg100` to topic `test-topic`.
- `scripts/manual/receive-100.ps1` - receives up to 100 messages from `test-topic` / `test-sub` and prints a short object summary.

Run:
```pwsh
pwsh scripts/manual/send-100.ps1
pwsh scripts/manual/receive-100.ps1
```

## Module Packaging
Script packs module into `out/SBPowerShell/<version>` with dependencies:
```pwsh
./scripts/pack-module.ps1            # Release, net8.0, version from psd1
./scripts/pack-module.ps1 -Configuration Debug
./scripts/pack-module.ps1 -Version 0.1.0 -Framework net8.0
```

Import after packaging:
```pwsh
Import-Module ./out/SBPowerShell/0.1.0/SBPowerShell.psd1
```
