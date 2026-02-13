param(
    [string]$SessionId = ("clean-session-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int]$TotalMessages = 14000,
    [int]$MissingOrder = 2,
    [int]$ProducerBatchSize = 300,
    [int]$ProducerInterBatchPauseMs = 5,
    [int]$ProducerWarmupBatches = 3,
    [int]$ProducerWarmupPauseMs = 25,
    [int]$ProducerMaxRetryCount = 8,
    [int]$ConsumerBatchSize = 200,
    [int]$PollWaitSeconds = 1,
    [int]$DeferredFreezeThreshold = 8050,
    [int]$MaxRunMinutes = 25,
    [int]$SampleCount = 120,
    [int]$CanarySeconds = 120,
    [string]$Topic = "NO_SESSION",
    [string]$Subscription = "STATE_SUB",
    [string]$OrderedTopic = "ORDERED_TOPIC",
    [string]$OrderedSubscription = "SESS_SUB",
    [string]$OutputRoot = "./out/clean-experiment",
    [string]$TtlIso8601 = "PT1H",
    [string]$FallbackTtlIso8601 = "PT1H",
    [switch]$NoDockerReset
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".." "..")).Path
Set-Location $repoRoot

$module = Join-Path $repoRoot "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
if (-not (Test-Path $module)) {
    $module = Join-Path $repoRoot "src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1"
}

$sas = if ($env:SAS_KEY_VALUE) { $env:SAS_KEY_VALUE } else { "LocalEmulatorKey123!" }
$connStr = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=$sas;UseDevelopmentEmulator=true;"

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDir = Join-Path $OutputRoot "$runStamp-$SessionId"
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

$orchLog = Join-Path $runDir "orchestrator.log"
function Write-Run {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    $line | Tee-Object -FilePath $orchLog -Append | Out-Host
}

function Invoke-Native {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [string]$Command,
        [string]$LogFile
    )

    Write-Run "RUN: $Name"
    if ($LogFile) {
        & zsh -lc $Command 2>&1 | Tee-Object -FilePath $LogFile -Append | Out-Host
    }
    else {
        & zsh -lc $Command 2>&1 | Out-Host
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

function Start-PwshScript {
    param(
        [Parameter(Mandatory)] [string]$ScriptPath,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [Parameter(Mandatory)] [string]$LogFile
    )

    $argList = @("-NoLogo", "-NoProfile", "-File", $ScriptPath) + $Arguments
    $errFile = "$LogFile.err"
    Start-Process -FilePath "pwsh" -ArgumentList $argList -WorkingDirectory $repoRoot -RedirectStandardOutput $LogFile -RedirectStandardError $errFile -PassThru
}

function Stop-IfRunning {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) {
        return $null
    }

    try {
        if (-not $Process.HasExited) {
            Stop-Process -Id $Process.Id -Force -ErrorAction Stop
            Start-Sleep -Seconds 1
        }
    }
    catch {
    }

    try {
        if ($Process.HasExited) {
            return $Process.ExitCode
        }
    }
    catch {
    }

    return $null
}

function New-SessionContextWithRetry {
    param(
        [Parameter(Mandatory)] [string]$Session,
        [int]$Retries = 20
    )

    for ($i = 1; $i -le $Retries; $i++) {
        try {
            return New-SBSessionContext -Topic $Topic -Subscription $Subscription -SessionId $Session -ServiceBusConnectionString $connStr
        }
        catch {
            $msg = $_.Exception.Message
            if ($msg -match "SessionCannotBeLocked|cannot be accepted|Connection refused|ServiceCommunicationProblem") {
                Start-Sleep -Seconds 1
                continue
            }
            throw
        }
    }

    throw "Cannot acquire session lock for $Session after $Retries retries"
}

function Wait-ServiceBusReady {
    param([int]$Retries = 60)

    function Test-SbManagementReachable {
        param([int]$TimeoutSeconds = 5)

        $job = Start-Job -ScriptBlock {
            param($cs, $modulePath)
            try {
                Import-Module $modulePath -Force
                $null = Get-SBTopic -ServiceBusConnectionString $cs
                return $true
            }
            catch {
                return $false
            }
        } -ArgumentList $connStr, $module

        try {
            if (-not (Wait-Job -Job $job -Timeout $TimeoutSeconds)) {
                Stop-Job -Job $job -ErrorAction SilentlyContinue | Out-Null
                return $false
            }
            $r = Receive-Job -Job $job -ErrorAction SilentlyContinue
            return [bool]($r -contains $true)
        }
        finally {
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue | Out-Null
        }
    }

    $stableChecks = 0
    for ($i = 1; $i -le $Retries; $i++) {
        $isRunning = $false
        $runningText = (& zsh -lc "docker inspect -f '{{.State.Running}}' servicebus-emulator 2>/dev/null")
        if ($LASTEXITCODE -eq 0 -and ($runningText -join "") -match "true") {
            $isRunning = $true
        }

        if (-not $isRunning) {
            if ($i -eq 1 -or ($i % 10) -eq 0) {
                Write-Run "Waiting for emulator container to run (attempt $i/$Retries)"
            }
            $stableChecks = 0
            Start-Sleep -Seconds 2
            continue
        }

        $tcpOk = $false
        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $async = $client.ConnectAsync("127.0.0.1", 5672)
            $tcpOk = $async.Wait(1200)
        }
        catch {
            $tcpOk = $false
        }
        finally {
            try { $client.Dispose() } catch {}
        }

        if ($tcpOk) {
            if (Test-SbManagementReachable -TimeoutSeconds 5) {
                $stableChecks++
                if ($stableChecks -ge 2) {
                    Write-Run "Service Bus is ready and stable (attempt $i/$Retries)"
                    return
                }
                Start-Sleep -Seconds 1
                continue
            }
        }

        if ($i -eq 1 -or ($i % 10) -eq 0) {
            Write-Run "Waiting for Service Bus management readiness (attempt $i/$Retries)"
        }
        $stableChecks = 0
        Start-Sleep -Seconds 2
    }

    throw "Service Bus emulator is not ready after $Retries attempts"
}

function Set-EmulatorConfigTtl {
    param(
        [Parameter(Mandatory)] [string]$ConfigPath,
        [Parameter(Mandatory)] [string]$TtlValue
    )

    $cfg = Get-Content -Path $ConfigPath -Raw | ConvertFrom-Json -Depth 100
    foreach ($ns in $cfg.UserConfig.Namespaces) {
        foreach ($q in $ns.Queues) {
            if ($q.Properties.DefaultMessageTimeToLive) {
                $q.Properties.DefaultMessageTimeToLive = $TtlValue
            }
        }
        foreach ($t in $ns.Topics) {
            if ($t.Properties.DefaultMessageTimeToLive) {
                $t.Properties.DefaultMessageTimeToLive = $TtlValue
            }
            foreach ($s in $t.Subscriptions) {
                if ($s.Properties.DefaultMessageTimeToLive) {
                    $s.Properties.DefaultMessageTimeToLive = $TtlValue
                }
            }
        }
    }
    ($cfg | ConvertTo-Json -Depth 100) | Set-Content -Path $ConfigPath -Encoding UTF8
}

function Restart-Emulator {
    param([Parameter(Mandatory)] [string]$RunDir)

    Invoke-Native -Name "docker down -v" -Command "docker compose -f docker-compose.sbus.yml down -v" -LogFile (Join-Path $RunDir "docker-down.log")
    Invoke-Native -Name "docker up -d" -Command "docker compose -f docker-compose.sbus.yml up -d" -LogFile (Join-Path $RunDir "docker-up.log")
    Start-Sleep -Seconds 10
}

function Capture-EmulatorLogs {
    param(
        [Parameter(Mandatory)] [string]$RunDir,
        [string]$FileName = "emulator.startup.log",
        [int]$Tail = 500
    )

    $logPath = Join-Path $RunDir $FileName
    $cmd = "docker compose -f docker-compose.sbus.yml logs --no-color --tail=$Tail sb-emulator"
    $content = & zsh -lc $cmd 2>&1
    $content | Set-Content -Path $logPath -Encoding UTF8
    return [pscustomobject]@{
        Path = $logPath
        Text = ($content -join "`n")
    }
}

function Get-StateSnapshot {
    param(
        [Parameter(Mandatory)] [string]$Session,
        [Parameter(Mandatory)] [string]$Path
    )

    $ctx = New-SessionContextWithRetry -Session $Session
    try {
        $raw = Get-SBSessionState -SessionContext $ctx -AsString
        if ([string]::IsNullOrWhiteSpace($raw)) {
            throw "Session state is empty"
        }
        $raw | Set-Content -Path $Path -Encoding UTF8
        $obj = $raw | ConvertFrom-Json
        $lastSeen = if ($obj.PSObject.Properties.Name -contains "LastSeenOrder") {
            [int]$obj.LastSeenOrder
        }
        else {
            [int]$obj.LastSeenOrderNum
        }

        $deferred = @($obj.Deferred)
        [pscustomobject]@{
            Raw = $raw
            Object = $obj
            LastSeen = $lastSeen
            DeferredCount = $deferred.Count
            Utf8Bytes = [System.Text.Encoding]::UTF8.GetByteCount($raw)
        }
    }
    finally {
        Close-SBSessionContext -Context $ctx
    }
}

function Test-DeferredSample {
    param(
        [Parameter(Mandatory)] [string]$Session,
        [Parameter(Mandatory)] [object]$StateObj,
        [int]$Take = 120
    )

    $seqAll = @(
        $StateObj.Deferred | ForEach-Object {
            if ($_.PSObject.Properties.Name -contains "SeqNumber") { [int64]$_.SeqNumber }
            elseif ($_.PSObject.Properties.Name -contains "seq") { [int64]$_.seq }
            elseif ($_.PSObject.Properties.Name -contains "Seq") { [int64]$_.Seq }
        }
    ) | Where-Object { $_ -ne $null }

    $count = $seqAll.Count
    if ($count -eq 0) {
        return [pscustomobject]@{
            DeferredTotal = 0
            SampleCount = 0
            OkCount = 0
            MissCount = 0
            ErrorCount = 0
            SampleSeq = @()
        }
    }

    $sampleSize = [Math]::Min($Take, $count)
    $sample = New-Object System.Collections.Generic.List[long]
    if ($sampleSize -eq 1) {
        $sample.Add($seqAll[0]) | Out-Null
    }
    else {
        for ($i = 0; $i -lt $sampleSize; $i++) {
            $idx = [int][Math]::Floor($i * ($count - 1) / [double]($sampleSize - 1))
            $sample.Add([int64]$seqAll[$idx]) | Out-Null
        }
    }
    $sample = @($sample | Select-Object -Unique)

    $ok = 0
    $miss = 0
    $err = 0

    $ctx = New-SessionContextWithRetry -Session $Session
    try {
        foreach ($seq in $sample) {
            try {
                $m = @(Receive-SBDeferredMessage -SessionContext $ctx -SequenceNumber @([int64]$seq) -ChunkSize 1 -ErrorAction Stop)
                if ($m.Count -gt 0) {
                    $ok++
                }
                else {
                    $miss++
                }
            }
            catch {
                $msg = $_.Exception.Message
                if ($msg -match "MessageNotFound|Failed to lock one or more specified messages") {
                    $miss++
                }
                else {
                    $err++
                }
            }
        }
    }
    finally {
        Close-SBSessionContext -Context $ctx
    }

    [pscustomobject]@{
        DeferredTotal = $count
        SampleCount = $sample.Count
        OkCount = $ok
        MissCount = $miss
        ErrorCount = $err
        SampleSeq = $sample
    }
}

function Set-CompactState {
    param(
        [Parameter(Mandatory)] [string]$Session,
        [Parameter(Mandatory)] [int]$LastSeen,
        [Parameter(Mandatory)] [object]$StateObj
    )

    $ctx = New-SessionContextWithRetry -Session $Session
    try {
        $newState = if ($StateObj.PSObject.Properties.Name -contains "LastSeenOrder") {
            @{ LastSeenOrder = $LastSeen; Deferred = @() }
        }
        else {
            @{ LastSeenOrderNum = $LastSeen; Deferred = @() }
        }
        Set-SBSessionState -SessionContext $ctx -State ($newState | ConvertTo-Json -Compress)
    }
    finally {
        Close-SBSessionContext -Context $ctx
    }
}

function Get-ConsumerBehaviorStats {
    param([Parameter(Mandatory)] [string]$LogPath)

    if (-not (Test-Path $LogPath)) {
        return [pscustomobject]@{
            Samples = 0
            StartLastSeen = $null
            EndLastSeen = $null
            StartDeferred = $null
            EndDeferred = $null
            MaxDeferred = $null
            DeferredGrowth = $null
            LastSeenAdvance = $null
        }
    }

    $regex = [regex]'LastSeenOrder=(\d+)\s+DeferredCount=(\d+)'
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($line in Get-Content -Path $LogPath) {
        $m = $regex.Match($line)
        if ($m.Success) {
            $rows.Add([pscustomobject]@{
                LastSeen = [int]$m.Groups[1].Value
                Deferred = [int]$m.Groups[2].Value
            }) | Out-Null
        }
    }

    if ($rows.Count -eq 0) {
        return [pscustomobject]@{
            Samples = 0
            StartLastSeen = $null
            EndLastSeen = $null
            StartDeferred = $null
            EndDeferred = $null
            MaxDeferred = $null
            DeferredGrowth = $null
            LastSeenAdvance = $null
        }
    }

    $arr = @($rows.ToArray())
    $start = $arr[0]
    $end = $arr[-1]
    $max = ($arr | Measure-Object -Property Deferred -Maximum).Maximum
    [pscustomobject]@{
        Samples = $arr.Count
        StartLastSeen = [int]$start.LastSeen
        EndLastSeen = [int]$end.LastSeen
        StartDeferred = [int]$start.Deferred
        EndDeferred = [int]$end.Deferred
        MaxDeferred = [int]$max
        DeferredGrowth = [int]$end.Deferred - [int]$start.Deferred
        LastSeenAdvance = [int]$end.LastSeen - [int]$start.LastSeen
    }
}

function Get-ProducerBehaviorStats {
    param([Parameter(Mandatory)] [string]$LogPath)

    if (-not (Test-Path $LogPath)) {
        return [pscustomobject]@{
            ProgressSamples = 0
            MaxSent = 0
            Target = 0
            Completed = $false
        }
    }

    $regex = [regex]'Sent\s+(\d+)\s*/\s*(\d+)'
    $sentValues = New-Object System.Collections.Generic.List[int]
    $target = 0
    $completed = $false

    foreach ($line in Get-Content -Path $LogPath) {
        $m = $regex.Match($line)
        if ($m.Success) {
            $sentValues.Add([int]$m.Groups[1].Value) | Out-Null
            $target = [int]$m.Groups[2].Value
        }
        if ($line -match "Load completed") {
            $completed = $true
        }
    }

    $vals = @($sentValues.ToArray())
    $maxSent = if ($vals.Count -gt 0) { ($vals | Measure-Object -Maximum).Maximum } else { 0 }
    [pscustomobject]@{
        ProgressSamples = $sentValues.Count
        MaxSent = [int]$maxSent
        Target = [int]$target
        Completed = [bool]$completed
    }
}

Write-Run "Run dir: $runDir"

$configPath = Join-Path $repoRoot "emulator/config.json"
$configBefore = Join-Path $runDir "config.before.json"
$configAfter = Join-Path $runDir "config.after.json"
Copy-Item -Path $configPath -Destination $configBefore -Force
$requestedTtl = $TtlIso8601
$effectiveTtl = $TtlIso8601
$ttlFallbackApplied = $false

Set-EmulatorConfigTtl -ConfigPath $configPath -TtlValue $effectiveTtl
Copy-Item -Path $configPath -Destination $configAfter -Force
Write-Run "Set emulator TTL request to $requestedTtl"

if (-not $NoDockerReset) {
    Restart-Emulator -RunDir $runDir
}

Invoke-Native -Name "dotnet build" -Command "dotnet build" -LogFile (Join-Path $runDir "build.log")
Import-Module $module -Force

try {
    Wait-ServiceBusReady
}
catch {
    $emuLog = Capture-EmulatorLogs -RunDir $runDir -FileName "emulator.startup.failed.log"
    $ttlBoundIssue = $emuLog.Text -match "less than or equal to 1h|Expected time to be less than or equal to 1h"
    if ($ttlBoundIssue -and $effectiveTtl -ne $FallbackTtlIso8601) {
        $ttlFallbackApplied = $true
        $effectiveTtl = $FallbackTtlIso8601
        Write-Run "Emulator rejected TTL $requestedTtl. Falling back to $effectiveTtl and restarting."
        Set-EmulatorConfigTtl -ConfigPath $configPath -TtlValue $effectiveTtl
        Copy-Item -Path $configPath -Destination $configAfter -Force
        Restart-Emulator -RunDir $runDir
        Wait-ServiceBusReady
    }
    else {
        throw
    }
}

$producerLog = Join-Path $runDir "producer.log"
$consumerLog = Join-Path $runDir "consumer.log"

$producer = Start-PwshScript -ScriptPath "./scripts/stress/send-state-sub-load.ps1" -Arguments @(
    "-ConnStr", $connStr,
    "-Topic", $Topic,
    "-SessionId", $SessionId,
    "-TotalMessages", "$TotalMessages",
    "-MissingOrder", "$MissingOrder",
    "-BatchSize", "$ProducerBatchSize",
    "-InterBatchPauseMs", "$ProducerInterBatchPauseMs",
    "-WarmupBatches", "$ProducerWarmupBatches",
    "-WarmupPauseMs", "$ProducerWarmupPauseMs",
    "-MaxRetryCount", "$ProducerMaxRetryCount"
) -LogFile $producerLog

$consumer = Start-PwshScript -ScriptPath "./scripts/stress/run-state-sub-consumer.ps1" -Arguments @(
    "-ConnStr", $connStr,
    "-SessionId", $SessionId,
    "-InputTopic", $Topic,
    "-InputSubscription", $Subscription,
    "-OutputTopic", $OrderedTopic,
    "-ReceiveBatchSize", "$ConsumerBatchSize",
    "-PollWaitSeconds", "$PollWaitSeconds",
    "-CrashDeferredThreshold", "0"
) -LogFile $consumerLog

Write-Run "Producer PID=$($producer.Id), Consumer PID=$($consumer.Id)"

$watchStart = Get-Date
$latestDeferred = -1
$trigger = "timeout"

while ($true) {
    Start-Sleep -Seconds 5

    if (Test-Path $consumerLog) {
        $tail = Get-Content $consumerLog -Tail 80
        $line = $tail | Select-String -Pattern "DeferredCount=(\d+)" | Select-Object -Last 1
        if ($line) {
            $latestDeferred = [int]$line.Matches[0].Groups[1].Value
        }
    }

    $producerAlive = -not $producer.HasExited
    $consumerAlive = -not $consumer.HasExited
    Write-Run ("Monitor: producerAlive={0} consumerAlive={1} latestDeferred={2}" -f $producerAlive, $consumerAlive, $latestDeferred)

    if ($latestDeferred -ge $DeferredFreezeThreshold) {
        $trigger = "deferred-threshold"
        break
    }

    if (-not $consumerAlive) {
        $trigger = "consumer-exited"
        break
    }

    $elapsed = (Get-Date) - $watchStart
    if ($elapsed.TotalMinutes -ge $MaxRunMinutes) {
        $trigger = "timeout"
        break
    }
}

Write-Run "Freeze trigger: $trigger"

$producerExit = Stop-IfRunning -Process $producer
$consumerExit = Stop-IfRunning -Process $consumer
Write-Run "Freeze done. producerExit=$producerExit consumerExit=$consumerExit"

$beforePath = Join-Path $runDir "state.$SessionId.before.json"
$before = Get-StateSnapshot -Session $SessionId -Path $beforePath
Write-Run ("Before state: lastSeen={0} deferred={1} bytes={2}" -f $before.LastSeen, $before.DeferredCount, $before.Utf8Bytes)

$sample = Test-DeferredSample -Session $SessionId -StateObj $before.Object -Take $SampleCount
Write-Run ("Sample result: deferredTotal={0} sample={1} ok={2} miss={3} err={4}" -f $sample.DeferredTotal, $sample.SampleCount, $sample.OkCount, $sample.MissCount, $sample.ErrorCount)

$decision = if ($sample.OkCount -gt 0) { "Replay" } else { "RecoveryV2_StaleIndex" }
Write-Run "Decision: $decision"

$recoveryLog = Join-Path $runDir "recovery.log"
if ($decision -eq "Replay") {
    $recovery = Start-PwshScript -ScriptPath "./scripts/stress/recover-session-state-overflow.ps1" -Arguments @(
        "-ConnStr", $connStr,
        "-SessionId", $SessionId,
        "-Topic", $Topic,
        "-Subscription", $Subscription,
        "-SnapDir", $runDir
    ) -LogFile $recoveryLog
    $recovery.WaitForExit()
    Write-Run "Replay recovery exit=$($recovery.ExitCode)"
}
else {
    Set-CompactState -Session $SessionId -LastSeen $before.LastSeen -StateObj $before.Object
    "Decision=RecoveryV2_StaleIndex; Action=CompactOnly; LastSeen=$($before.LastSeen)" | Set-Content -Path $recoveryLog -Encoding UTF8
    Write-Run "Applied Recovery v2 compact state"
}

$afterRecoveryPath = Join-Path $runDir "state.$SessionId.after-recovery.json"
$afterRecovery = Get-StateSnapshot -Session $SessionId -Path $afterRecoveryPath
Write-Run ("After recovery: lastSeen={0} deferred={1} bytes={2}" -f $afterRecovery.LastSeen, $afterRecovery.DeferredCount, $afterRecovery.Utf8Bytes)

$canaryLog = Join-Path $runDir "consumer.canary.log"
$canary = Start-PwshScript -ScriptPath "./scripts/stress/run-state-sub-consumer.ps1" -Arguments @(
    "-ConnStr", $connStr,
    "-SessionId", $SessionId,
    "-InputTopic", $Topic,
    "-InputSubscription", $Subscription,
    "-OutputTopic", $OrderedTopic,
    "-ReceiveBatchSize", "$ConsumerBatchSize",
    "-PollWaitSeconds", "$PollWaitSeconds",
    "-CrashDeferredThreshold", "0"
) -LogFile $canaryLog

Start-Sleep -Seconds $CanarySeconds
$canaryExit = Stop-IfRunning -Process $canary
Write-Run "Canary done. exit=$canaryExit durationSec=$CanarySeconds"

$afterCanaryPath = Join-Path $runDir "state.$SessionId.after-canary.json"
$afterCanary = Get-StateSnapshot -Session $SessionId -Path $afterCanaryPath
Write-Run ("After canary: lastSeen={0} deferred={1} bytes={2}" -f $afterCanary.LastSeen, $afterCanary.DeferredCount, $afterCanary.Utf8Bytes)

$inputStats = Get-SBSubscription -ServiceBusConnectionString $connStr -Topic $Topic -Subscription $Subscription
$outputStats = Get-SBSubscription -ServiceBusConnectionString $connStr -Topic $OrderedTopic -Subscription $OrderedSubscription
$producerStats = Get-ProducerBehaviorStats -LogPath $producerLog
$consumerStats = Get-ConsumerBehaviorStats -LogPath $consumerLog

$summary = [ordered]@{
    RunDir = (Resolve-Path $runDir).Path
    SessionId = $SessionId
    RequestedTtl = $requestedTtl
    EffectiveTtl = $effectiveTtl
    TtlFallbackApplied = $ttlFallbackApplied
    TotalMessages = $TotalMessages
    MissingOrder = $MissingOrder
    FreezeTrigger = $trigger
    LatestDeferredBeforeFreeze = $latestDeferred
    BeforeLastSeen = $before.LastSeen
    BeforeDeferred = $before.DeferredCount
    BeforeUtf8Bytes = $before.Utf8Bytes
    SampleCount = $sample.SampleCount
    SampleOk = $sample.OkCount
    SampleMiss = $sample.MissCount
    SampleError = $sample.ErrorCount
    Decision = $decision
    AfterRecoveryLastSeen = $afterRecovery.LastSeen
    AfterRecoveryDeferred = $afterRecovery.DeferredCount
    AfterRecoveryUtf8Bytes = $afterRecovery.Utf8Bytes
    AfterCanaryLastSeen = $afterCanary.LastSeen
    AfterCanaryDeferred = $afterCanary.DeferredCount
    AfterCanaryUtf8Bytes = $afterCanary.Utf8Bytes
    ProducerProgressSamples = $producerStats.ProgressSamples
    ProducerMaxSent = $producerStats.MaxSent
    ProducerTarget = $producerStats.Target
    ProducerCompleted = $producerStats.Completed
    ProducerBatchSize = $ProducerBatchSize
    ProducerInterBatchPauseMs = $ProducerInterBatchPauseMs
    ProducerWarmupBatches = $ProducerWarmupBatches
    ProducerWarmupPauseMs = $ProducerWarmupPauseMs
    ProducerMaxRetryCount = $ProducerMaxRetryCount
    ConsumerProgressSamples = $consumerStats.Samples
    ConsumerStartLastSeen = $consumerStats.StartLastSeen
    ConsumerEndLastSeen = $consumerStats.EndLastSeen
    ConsumerStartDeferred = $consumerStats.StartDeferred
    ConsumerEndDeferred = $consumerStats.EndDeferred
    ConsumerMaxDeferred = $consumerStats.MaxDeferred
    ConsumerDeferredGrowth = $consumerStats.DeferredGrowth
    ConsumerLastSeenAdvance = $consumerStats.LastSeenAdvance
    InputActive = $inputStats.RuntimeProperties.ActiveMessageCount
    InputTotal = $inputStats.RuntimeProperties.TotalMessageCount
    OutputActive = $outputStats.RuntimeProperties.ActiveMessageCount
    OutputTotal = $outputStats.RuntimeProperties.TotalMessageCount
    FinishedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
}

$summaryPath = Join-Path $runDir "summary.json"
$summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding UTF8

$ruReport = @"
# Чистый эксперимент: recovery при overflow session state

## Что мы сделали
- Полностью обнулили эмулятор (docker down -v + up).
- Поставили TTL в конфиге: запросили $requestedTtl, реально использовали $effectiveTtl.
- Запустили producer/consumer для сессии $SessionId.
- Дошли до stop-point и сразу сделали freeze (остановили оба процесса).
- До любых изменений state выполнили sample-проверку deferred.
- По результату sample выбрали ветку: $decision.
- Выполнили recovery и затем canary-прогон на $CanarySeconds секунд.

## Что увидели
- До recovery: LastSeen=$($before.LastSeen), Deferred=$($before.DeferredCount), Utf8Bytes=$($before.Utf8Bytes).
- Sample: ok=$($sample.OkCount), miss=$($sample.MissCount), err=$($sample.ErrorCount) (sample size $($sample.SampleCount)).
- После recovery: LastSeen=$($afterRecovery.LastSeen), Deferred=$($afterRecovery.DeferredCount), Utf8Bytes=$($afterRecovery.Utf8Bytes).
- После canary: LastSeen=$($afterCanary.LastSeen), Deferred=$($afterCanary.DeferredCount), Utf8Bytes=$($afterCanary.Utf8Bytes).
- Producer: samples=$($producerStats.ProgressSamples), sentMax=$($producerStats.MaxSent)/$($producerStats.Target), completed=$($producerStats.Completed).
- Consumer: samples=$($consumerStats.Samples), LastSeen $($consumerStats.StartLastSeen)->$($consumerStats.EndLastSeen), Deferred $($consumerStats.StartDeferred)->$($consumerStats.EndDeferred), maxDeferred=$($consumerStats.MaxDeferred).

## Простой вывод
"@

if ($ttlFallbackApplied) {
    $ruReport += "- Важно: эмулятор не принял TTL $requestedTtl (его лимит 1h), поэтому запуск автоматически переведен на $effectiveTtl.`n"
}

if ($sample.OkCount -eq 0) {
    $ruReport += @"
- Список deferred в state не подтвердился в брокере (ok=0), поэтому replay не запускали.
- Пошли по Recovery v2 (compact + canary).
"@
}
else {
    $ruReport += @"
- Deferred sample подтвердился (ok>0), поэтому был запущен replay.
"@
}

if ($afterCanary.DeferredCount -gt $afterRecovery.DeferredCount) {
    $ruReport += "- Во время canary deferred снова растет. Значит проблема порядка полностью не снята.`n"
}
else {
    $ruReport += "- Во время canary deferred не растет. Состояние стабилизировалось.`n"
}

$ruReport += @"

## Где смотреть детали
- Общий лог: orchestrator.log
- Summary JSON: summary.json
- Логи producer/consumer/recovery в этой же папке run.
"@

$enReport = @"
# Clean experiment: recovery for session-state overflow

## What we did
- Fully reset emulator (docker down -v + up).
- Set config TTL: requested $requestedTtl, actually used $effectiveTtl.
- Started producer/consumer for session $SessionId.
- Reached stop-point and immediately froze both processes.
- Ran deferred sample check before any state change.
- Chose branch by sample result: $decision.
- Executed recovery and then a $CanarySeconds-second canary run.

## What we saw
- Before recovery: LastSeen=$($before.LastSeen), Deferred=$($before.DeferredCount), Utf8Bytes=$($before.Utf8Bytes).
- Sample: ok=$($sample.OkCount), miss=$($sample.MissCount), err=$($sample.ErrorCount) (sample size $($sample.SampleCount)).
- After recovery: LastSeen=$($afterRecovery.LastSeen), Deferred=$($afterRecovery.DeferredCount), Utf8Bytes=$($afterRecovery.Utf8Bytes).
- After canary: LastSeen=$($afterCanary.LastSeen), Deferred=$($afterCanary.DeferredCount), Utf8Bytes=$($afterCanary.Utf8Bytes).
- Producer: samples=$($producerStats.ProgressSamples), sentMax=$($producerStats.MaxSent)/$($producerStats.Target), completed=$($producerStats.Completed).
- Consumer: samples=$($consumerStats.Samples), LastSeen $($consumerStats.StartLastSeen)->$($consumerStats.EndLastSeen), Deferred $($consumerStats.StartDeferred)->$($consumerStats.EndDeferred), maxDeferred=$($consumerStats.MaxDeferred).

## Plain-language result
"@

if ($ttlFallbackApplied) {
    $enReport += "- Important: emulator rejected TTL $requestedTtl (1h limit), so run automatically switched to $effectiveTtl.`n"
}

if ($sample.OkCount -eq 0) {
    $enReport += @"
- Deferred list from state was not confirmed in broker (ok=0), so replay was skipped.
- Recovery v2 path was used (compact + canary).
"@
}
else {
    $enReport += @"
- Deferred sample was valid (ok>0), so replay was executed.
"@
}

if ($afterCanary.DeferredCount -gt $afterRecovery.DeferredCount) {
    $enReport += "- Deferred grew again during canary. Ordering issue is not fully resolved.`n"
}
else {
    $enReport += "- Deferred did not grow during canary. State looks stable.`n"
}

$enReport += @"

## Where to inspect details
- Main log: orchestrator.log
- Summary JSON: summary.json
- Producer/consumer/recovery logs in the same run folder.
"@

$ruPath = Join-Path $runDir "EXPERIMENT_REPORT.ru.md"
$enPath = Join-Path $runDir "EXPERIMENT_REPORT.en.md"
$ruReport | Set-Content -Path $ruPath -Encoding UTF8
$enReport | Set-Content -Path $enPath -Encoding UTF8

Write-Run "Reports written:"
Write-Run " - $ruPath"
Write-Run " - $enPath"
Write-Run "Summary: $summaryPath"

$summary
