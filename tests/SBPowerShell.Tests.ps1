$ErrorActionPreference = 'Stop'

$script:repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $script:repoRoot) { $script:repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $script:repoRoot) { $script:repoRoot = (Get-Location).Path }
Write-Host "repoRoot=$script:repoRoot"
$modulePath = Join-Path $repoRoot 'src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1'
if (-not (Test-Path $modulePath)) {
    Write-Host "Build Debug first; falling back to Release."
    $modulePath = Join-Path $repoRoot 'src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1'
}

if (-not (Test-Path $modulePath)) {
    throw "Module manifest not found. Run 'dotnet build src/SBPowerShell/SBPowerShell.csproj'."
}

Import-Module $modulePath -Force

function script:Get-EnvValue([string]$Key) {
    $envPath = Join-Path (Get-Location) '.env'
    if (-not (Test-Path $envPath)) {
        throw ".env not found; please create it with $Key."
    }
    $line = Get-Content $envPath | Where-Object { $_ -like "$Key=*" } | Select-Object -First 1
    if (-not $line) { throw "Key $Key not found in .env" }
    return $line.Substring($Key.Length + 1)
}

function script:Get-EnvValueOrDefault([string]$Key, [string]$Default) {
    try {
        return Get-EnvValue $Key
    }
    catch {
        return $Default
    }
}

function script:Get-ConnectionString {
    $sas = Get-EnvValue 'SAS_KEY_VALUE'
    if ([string]::IsNullOrWhiteSpace($sas)) {
        throw "SAS_KEY_VALUE is empty; check .env"
    }
    $emulatorHost = Get-EnvValueOrDefault 'EMULATOR_HOST' 'localhost'
    return "Endpoint=sb://${emulatorHost};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=$sas;UseDevelopmentEmulator=true;"
}

function script:Get-AdminConnectionString([string]$BaseConnectionString) {
    # The emulator hosts management plane on the HTTP port (default 5300).
    # The SDK admin client otherwise tries to call port 80 when given the
    # development connection string, leading to connection-refused warnings.
    if ($BaseConnectionString -notmatch 'UseDevelopmentEmulator=true') {
        return $BaseConnectionString
    }

    $httpPort = [int](Get-EnvValueOrDefault 'EMULATOR_HTTP_PORT' '5300')
    return ($BaseConnectionString -replace 'Endpoint=sb://([^;]+);', "Endpoint=sb://`$1:$httpPort;")
}

function script:Wait-ForEmulator {
    param(
        [string]$EmulatorHost,
        [int]$HttpPort = 5300,
        [int]$AmqpPort = 5672,
        [int]$TimeoutSeconds = 180
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    Write-Host ("Waiting for emulator health on http://{0}:{1}/health and AMQP {2} ..." -f $EmulatorHost, $HttpPort, $AmqpPort)

    while ([DateTime]::UtcNow -lt $deadline) {
        $httpOk = $false
        $httpErr = $null
        try {
            $resp = Invoke-RestMethod -UseBasicParsing -Method Get -TimeoutSec 5 -Uri ("http://{0}:{1}/health" -f $EmulatorHost, $HttpPort)
            $httpOk = $true
        } catch {
            $httpErr = $_.Exception.Message
        }

        $tcpOk = $false
        try {
            $client = [System.Net.Sockets.TcpClient]::new()
            $async = $client.BeginConnect($EmulatorHost, $AmqpPort, $null, $null)
            $completed = $async.AsyncWaitHandle.WaitOne(2000)
            if ($completed -and $client.Connected) { $tcpOk = $true }
            $client.Close()
        } catch {}

        if ($httpOk -and $tcpOk) {
            Write-Host "Emulator ready."
            return
        }

        Start-Sleep -Seconds 2
    }

    Write-Warning ("Emulator not ready after {0}s; last HTTP error: {1}" -f $TimeoutSeconds, $httpErr)
    throw "Emulator not ready after $TimeoutSeconds seconds."
}

function script:Ensure-TopicSubscription {
    param(
        [Parameter(Mandatory)][string]$Topic,
        [Parameter(Mandatory)][string]$Subscription,
        [bool]$RequiresSession = $false
    )

    try {
        $adminConn = Get-AdminConnectionString $script:connectionString
        $admin = [Azure.Messaging.ServiceBus.Administration.ServiceBusAdministrationClient]::new($adminConn)

        if (-not ($admin.TopicExistsAsync($Topic).GetAwaiter().GetResult())) {
            $admin.CreateTopicAsync($Topic).GetAwaiter().GetResult() | Out-Null
        }

        if (-not ($admin.SubscriptionExistsAsync($Topic, $Subscription).GetAwaiter().GetResult())) {
            $options = [Azure.Messaging.ServiceBus.Administration.CreateSubscriptionOptions]::new($Topic, $Subscription)
            $options.RequiresSession = $RequiresSession
            $admin.CreateSubscriptionAsync($options).GetAwaiter().GetResult() | Out-Null
        }
    }
    catch {
        Write-Warning "Ensure-TopicSubscription skipped: $($_.Exception.Message)"
    }
}

function script:Ensure-EmulatorUp {
    $root = if ($script:repoRoot) { $script:repoRoot } else { (Get-Location).Path }
    $compose = Join-Path $root 'docker-compose.sbus.yml'
    if (-not (Test-Path $compose)) {
        Write-Warning "docker-compose.sbus.yml not found at $compose"
        return
    }

    try {
        Write-Host "Starting/recreating Service Bus emulator containers..."
        docker compose -f $compose up -d --force-recreate --pull never | Out-Null
    }
    catch {
        Write-Warning "docker compose up failed: $($_.Exception.Message)"
    }
}

$connectionString = Get-ConnectionString
Write-Host "sasKey from env=$(Get-EnvValue 'SAS_KEY_VALUE')"
Write-Host "connectionString=$connectionString"
Write-Host "connectionStringLength=$($connectionString.Length)"

Describe "SBPowerShell cmdlets against emulator" {
    BeforeAll {
        if (-not $script:repoRoot) {
            $script:repoRoot = Split-Path -Parent $PSScriptRoot
            if (-not $script:repoRoot) { $script:repoRoot = (Get-Location).Path }
        }

        $script:modulePath = Join-Path $script:repoRoot 'src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1'
        if (-not (Test-Path $script:modulePath)) {
            Write-Host "Build Debug first; falling back to Release."
            $script:modulePath = Join-Path $script:repoRoot 'src/SBPowerShell/bin/Release/net8.0/SBPowerShell.psd1'
        }
        if (-not (Test-Path $script:modulePath)) {
            throw "Module manifest not found. Run 'dotnet build src/SBPowerShell/SBPowerShell.csproj'."
        }

        $script:connectionString = Get-ConnectionString
        Write-Host "BeforeAll conn length=$($script:connectionString.Length)"
        $sbHost = Get-EnvValueOrDefault 'EMULATOR_HOST' 'localhost'
        $amqpPort = [int](Get-EnvValueOrDefault 'EMULATOR_AMQP_PORT' '5672')
        $httpPort = [int](Get-EnvValueOrDefault 'EMULATOR_HTTP_PORT' '5300')
        Ensure-EmulatorUp
        Wait-ForEmulator -EmulatorHost $sbHost -AmqpPort $amqpPort -HttpPort $httpPort -TimeoutSeconds 90
        Clear-SBQueue -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString | Out-Null
        Clear-SBQueue -Queue 'session-queue' -ServiceBusConnectionString $script:connectionString | Out-Null
        Ensure-TopicSubscription -Topic 'test-topic' -Subscription 'test-sub' -RequiresSession:$false
        Ensure-TopicSubscription -Topic 'session-topic' -Subscription 'session-sub' -RequiresSession:$true
        Clear-SBSubscription -Topic 'test-topic' -Subscription 'test-sub' -ServiceBusConnectionString $script:connectionString | Out-Null
        Clear-SBSubscription -Topic 'session-topic' -Subscription 'session-sub' -ServiceBusConnectionString $script:connectionString | Out-Null
    }

    It "sends and receives non-session queue messages" {
        $messages = New-SBMessage -CustomProperties @{ prop = 'v1' } -Body 'hello', 'world'
        Send-SBMessage -Queue 'test-queue' -Message $messages -ServiceBusConnectionString $script:connectionString

        $received = @(Receive-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 2)
        $received.Count | Should -Be 2
        ($received | ForEach-Object { $_.Body.ToString() }) | Should -BeIn @('hello','world')
        $received[0].ApplicationProperties['prop'] | Should -Be 'v1'
    }

    It "sends and receives session queue messages preserving SessionId" {
        $messages = New-SBMessage -SessionId 'sess-1' -Body 's1','s2'
        Send-SBMessage -Queue 'session-queue' -Message $messages -ServiceBusConnectionString $script:connectionString -PerSessionThreadAuto

        $received = @(Receive-SBMessage -Queue 'session-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 2)
        $received.Count | Should -Be 2
        $received | ForEach-Object { $_.SessionId | Should -Be 'sess-1' }
    }

    It "sends to topic and receives from subscription" {
        $messages = New-SBMessage -Body 'topic-msg'
        Send-SBMessage -Topic 'test-topic' -Message $messages -ServiceBusConnectionString $script:connectionString

        $received = @(Receive-SBMessage -ServiceBusConnectionString $script:connectionString -Topic 'test-topic' -Subscription 'test-sub' -MaxMessages 1)
        $received.Count | Should -Be 1
        $received[0].Body.ToString() | Should -Be 'topic-msg'
    }

    It "sends and receives session topic messages preserving SessionId" {
        Clear-SBSubscription -Topic 'session-topic' -Subscription 'session-sub' -ServiceBusConnectionString $script:connectionString | Out-Null

        $messages = New-SBMessage -SessionId 'sess-topic' -Body 'ts1','ts2'
        Send-SBMessage -Topic 'session-topic' -Message $messages -ServiceBusConnectionString $script:connectionString -PerSessionThreadAuto

        $received = @(Receive-SBMessage -ServiceBusConnectionString $script:connectionString -Topic 'session-topic' -Subscription 'session-sub' -MaxMessages 2)
        $received.Count | Should -Be 2
        $received | ForEach-Object { $_.SessionId | Should -Be 'sess-topic' }
    }

    It "receives multiple messages from subscription" {
        Clear-SBSubscription -Topic 'test-topic' -Subscription 'test-sub' -ServiceBusConnectionString $script:connectionString | Out-Null

        $messages = New-SBMessage -Body 't-m1','t-m2'
        Send-SBMessage -Topic 'test-topic' -Message $messages -ServiceBusConnectionString $script:connectionString

        $received = @(Receive-SBMessage -ServiceBusConnectionString $script:connectionString -Topic 'test-topic' -Subscription 'test-sub' -MaxMessages 2 -WaitSeconds 1)
        $received.Count | Should -Be 2
        ($received | ForEach-Object { $_.Body.ToString() } | Sort-Object) | Should -Be @('t-m1','t-m2')
    }

    It "peeks subscription messages without removing them" {
        Clear-SBSubscription -Topic 'test-topic' -Subscription 'test-sub' -ServiceBusConnectionString $script:connectionString | Out-Null

        $messages = New-SBMessage -Body 'peek-topic-a','peek-topic-b'
        Send-SBMessage -Topic 'test-topic' -Message $messages -ServiceBusConnectionString $script:connectionString

        $peeked = @(Receive-SBMessage -ServiceBusConnectionString $script:connectionString -Topic 'test-topic' -Subscription 'test-sub' -MaxMessages 2 -Peek -WaitSeconds 1)
        $peeked.Count | Should -Be 2

        $received = @(Receive-SBMessage -ServiceBusConnectionString $script:connectionString -Topic 'test-topic' -Subscription 'test-sub' -MaxMessages 2 -WaitSeconds 1)
        $received.Count | Should -Be 2

        ($peeked | ForEach-Object { $_.Body.ToString() } | Sort-Object) | Should -Be ($received | ForEach-Object { $_.Body.ToString() } | Sort-Object)
    }

    It "lists topics with runtime properties" {
        $topics = @(Get-SBTopic -ServiceBusConnectionString $script:connectionString)
        $topics.Count | Should -BeGreaterThan 0

        $testTopic = @($topics | Where-Object { $_.Name -eq 'test-topic' })
        $testTopic.Count | Should -Be 1
        $testTopic[0].RuntimeProperties | Should -BeOfType ([Azure.Messaging.ServiceBus.Administration.TopicRuntimeProperties])
    }

    It "lists subscriptions with runtime counts and supports pipeline" {
        Clear-SBSubscription -Topic 'test-topic' -Subscription 'test-sub' -ServiceBusConnectionString $script:connectionString | Out-Null

        $messages = New-SBMessage -Body 'count-me'
        Send-SBMessage -Topic 'test-topic' -Message $messages -ServiceBusConnectionString $script:connectionString

        Start-Sleep -Seconds 1

        $subs = @(Get-SBSubscription -ServiceBusConnectionString $script:connectionString -Topic 'test-topic')
        $subs.Count | Should -BeGreaterThan 0

        $sub = @($subs | Where-Object { $_.SubscriptionName -eq 'test-sub' })
        $sub.Count | Should -Be 1
        $sub[0].RuntimeProperties | Should -BeOfType ([Azure.Messaging.ServiceBus.Administration.SubscriptionRuntimeProperties])
        $sub[0].RuntimeProperties.ActiveMessageCount | Should -BeGreaterThan 0

        # pipeline variant
        $pipelineSubs = @(Get-SBTopic -ServiceBusConnectionString $script:connectionString | Where-Object { $_.Name -eq 'test-topic' } | Get-SBSubscription -ServiceBusConnectionString $script:connectionString)
        $pipelineSubs.Count | Should -Be 1
        $pipelineSubs[0].SubscriptionName | Should -Be 'test-sub'

        Clear-SBSubscription -Topic 'test-topic' -Subscription 'test-sub' -ServiceBusConnectionString $script:connectionString | Out-Null
    }

    It "supports NoComplete with manual settle via Set-SBMessage" {
        Clear-SBQueue -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString | Out-Null
        $messages = New-SBMessage -Body 'manual-complete'
        Send-SBMessage -Queue 'test-queue' -Message $messages -ServiceBusConnectionString $script:connectionString

        Receive-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 1 -NoComplete |
            Set-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -Complete

        $shouldBeEmpty = @(Receive-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 1 -WaitSeconds 1)
        $shouldBeEmpty.Count | Should -Be 0
    }

    It "defers and fetches deferred messages" {
        Clear-SBQueue -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString | Out-Null
        $messages = New-SBMessage -Body 'needs-order'
        Send-SBMessage -Queue 'test-queue' -Message $messages -ServiceBusConnectionString $script:connectionString

        $deferredSeq = @(
            Receive-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 1 -NoComplete |
            Set-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -Defer |
            ForEach-Object { $_.SequenceNumber }
        )

        $fetched = @(Receive-SBDeferredMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -SequenceNumber $deferredSeq)
        $fetched.Count | Should -Be 1
        $fetched[0].Body.ToString() | Should -Be 'needs-order'

        $fetched | Set-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -Complete | Out-Null
    }

    It "orders messages using session context and defer" {
        Clear-SBQueue -Queue 'session-queue' -ServiceBusConnectionString $script:connectionString | Out-Null
        Clear-SBSubscription -Topic 'session-topic' -Subscription 'session-sub' -ServiceBusConnectionString $script:connectionString | Out-Null

        $sid = 'order-sess'
        $msgs = @()
        $msgs += New-SBMessage -SessionId $sid -Body 'second' -CustomProperties @{ Order = 2 }
        $msgs += New-SBMessage -SessionId $sid -Body 'first'  -CustomProperties @{ Order = 1 }
        $msgs += New-SBMessage -SessionId $sid -Body 'third'  -CustomProperties @{ Order = 3 }
        Send-SBMessage -Queue 'session-queue' -Message $msgs -ServiceBusConnectionString $script:connectionString -PerSessionThreadAuto

        $ctx = New-SBSessionContext -Queue 'session-queue' -SessionId $sid -ServiceBusConnectionString $script:connectionString

        $state = Get-SBSessionState -SessionContext $ctx
        if ($state -and $state.PSObject.TypeNames -contains 'System.Text.Json.JsonElement') {
            $state = [pscustomobject]@{
                LastMaxOrder = [int]$state.GetProperty('LastMaxOrder').GetInt32()
                Deferred     = @($state.GetProperty('Deferred') | ForEach-Object { $_.GetInt64() })
            }
        }
        if (-not $state) { $state = [pscustomobject]@{ LastMaxOrder = 0; Deferred = @() } }

        $orderedOut = New-Object System.Collections.Generic.List[object]

        function global:Handle-Ordered {
            param($m)
            $expected = $state.LastMaxOrder + 1
            $orderVal = [int]$m.ApplicationProperties['Order']
            if ($orderVal -eq $expected) {
                $state.LastMaxOrder = $orderVal
                $m | Set-SBMessage -SessionContext $ctx -Complete | Out-Null
                $orderedOut.Add($m) | Out-Null
            } else {
                $m | Set-SBMessage -SessionContext $ctx -Defer | Out-Null
                $state.Deferred += $m.SequenceNumber
            }
        }

        $batch = @(Receive-SBMessage -SessionContext $ctx -MaxMessages 3 -NoComplete)
        foreach ($m in $batch) { Handle-Ordered $m }

        while ($state.Deferred.Count -gt 0) {
            $nextSeq = ($state.Deferred | Sort-Object | Select-Object -First 1)
            $state.Deferred = $state.Deferred | Where-Object { $_ -ne $nextSeq }
            $deferred = Receive-SBDeferredMessage -SessionContext $ctx -SequenceNumber $nextSeq
            Handle-Ordered $deferred
        }

        $stateToSave = @{ LastMaxOrder = $state.LastMaxOrder; Deferred = $state.Deferred } | ConvertTo-Json -Compress
        Set-SBSessionState -SessionContext $ctx -State $stateToSave
        Close-SBSessionContext -Context $ctx

        $orderedOut | Send-SBMessage -Topic 'session-topic' -ServiceBusConnectionString $script:connectionString

        $received = @(Receive-SBMessage -ServiceBusConnectionString $script:connectionString -Topic 'session-topic' -Subscription 'session-sub' -MaxMessages 3 -WaitSeconds 2)
        ($received | ForEach-Object { $_.Body.ToString() }) | Should -Be @('first','second','third')
        $received | ForEach-Object { $_.SessionId | Should -Be $sid }
    }

    It "routes unordered non-session stream through pipeline into ordered session topic" {
        Ensure-TopicSubscription -Topic 'NO_SESSION' -Subscription 'NO_SESS_SUB' -RequiresSession:$false
        Ensure-TopicSubscription -Topic 'ORDERED_TOPIC' -Subscription 'SESS_SUB' -RequiresSession:$true
        Clear-SBSubscription -Topic 'NO_SESSION' -Subscription 'NO_SESS_SUB' -ServiceBusConnectionString $script:connectionString | Out-Null
        Clear-SBSubscription -Topic 'ORDERED_TOPIC' -Subscription 'SESS_SUB' -ServiceBusConnectionString $script:connectionString | Out-Null

        $sid = 'ordered-sess'
        $total = 3

        $jobModule = $script:modulePath
        if (-not (Test-Path $jobModule)) { throw "Module path not found: $jobModule" }

        $job = Start-Job -ArgumentList $jobModule, $script:connectionString, $sid -ScriptBlock {
            param([string]$modulePath, [string]$conn, [string]$sid)
            if (-not (Test-Path $modulePath)) { throw "modulePath not found: $modulePath" }
            Import-Module $modulePath -Force

            $msgs = @(
                New-SBMessage -Body 'second' -CustomProperties @{ sessionId = $sid; order = 2 }
                New-SBMessage -Body 'first'  -CustomProperties @{ sessionId = $sid; order = 1 }
                New-SBMessage -Body 'third'  -CustomProperties @{ sessionId = $sid; order = 3 }
            )

            Send-SBMessage -Topic 'NO_SESSION' -Message $msgs -ServiceBusConnectionString $conn
        }

        function global:Reorder-And-Forward {
            [CmdletBinding()]
            param(
                [Parameter(ValueFromPipeline)]$Message
            )
            begin {
                # state per pseudo-sessionId: LastMaxOrder, Deferred seq numbers
                $state = @{}
                function Get-State([string]$sid) {
                    if (-not $state.ContainsKey($sid)) {
                        $state[$sid] = [pscustomobject]@{
                            LastMaxOrder = 0
                            Deferred     = @()
                        }
                    }
                    return $state[$sid]
                }
                function Try-DrainDeferred([string]$sid, $ctxState) {
                    while ($ctxState.Deferred.Count -gt 0) {
                        $nextSeq = ($ctxState.Deferred | Sort-Object | Select-Object -First 1)
                        $ctxState.Deferred = $ctxState.Deferred | Where-Object { $_ -ne $nextSeq }
                        $deferred = Receive-SBDeferredMessage -Topic 'NO_SESSION' -Subscription 'NO_SESS_SUB' -ServiceBusConnectionString $script:connectionString -SequenceNumber $nextSeq
                        if (-not $deferred) { break }

                        $orderVal = [int]$deferred.ApplicationProperties['order']
                        $expected = $ctxState.LastMaxOrder + 1
                        if ($orderVal -eq $expected) {
                            $deferred | Set-SBMessage -Topic 'NO_SESSION' -Subscription 'NO_SESS_SUB' -ServiceBusConnectionString $script:connectionString -Complete | Out-Null
                            $ctxState.LastMaxOrder = $orderVal
                            $out = New-SBMessage -SessionId $sid -Body $deferred.Body.ToString() -CustomProperties @{
                                order     = $orderVal
                                sessionId = $sid
                            }
                            Write-Output $out
                        } else {
                            # still out of order; keep deferred
                            $ctxState.Deferred += $deferred.SequenceNumber
                            break
                        }
                    }
                }
            }
            process {
                $m = $Message
                $sidFromProp = $m.ApplicationProperties['sessionId']
                $ctxState = Get-State $sidFromProp

                $orderVal = [int]$m.ApplicationProperties['order']
                $expected = $ctxState.LastMaxOrder + 1

                if ($orderVal -eq $expected) {
                    $m | Set-SBMessage -Topic 'NO_SESSION' -Subscription 'NO_SESS_SUB' -ServiceBusConnectionString $script:connectionString -Complete | Out-Null
                    $ctxState.LastMaxOrder = $orderVal
                    $out = New-SBMessage -SessionId $sidFromProp -Body $m.Body.ToString() -CustomProperties @{
                        order     = $orderVal
                        sessionId = $sidFromProp
                    }
                    Write-Output $out
                    Try-DrainDeferred -sid $sidFromProp -ctxState $ctxState
                } else {
                    $m | Set-SBMessage -Topic 'NO_SESSION' -Subscription 'NO_SESS_SUB' -ServiceBusConnectionString $script:connectionString -Defer | Out-Null
                    $ctxState.Deferred += $m.SequenceNumber
                }
            }
        }

        Wait-Job $job -Timeout 30 | Out-Null
        Receive-Job $job | Out-Null
        Remove-Job $job | Out-Null

        Receive-SBMessage -Topic 'NO_SESSION' -Subscription 'NO_SESS_SUB' -ServiceBusConnectionString $script:connectionString -MaxMessages $total -WaitSeconds 5 -NoComplete |
            Reorder-And-Forward | Send-SBMessage -Topic 'ORDERED_TOPIC' -ServiceBusConnectionString $script:connectionString

        $received = @(Receive-SBMessage -ServiceBusConnectionString $script:connectionString -Topic 'ORDERED_TOPIC' -Subscription 'SESS_SUB' -MaxMessages $total -WaitSeconds 5)
        ($received | ForEach-Object { $_.Body.ToString() }) | Should -Be @('first','second','third')
        $received | ForEach-Object { $_.SessionId | Should -Be $sid }
    }

    It "pipes received messages into Send-SBMessage" {
        Clear-SBQueue -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString | Out-Null
        Clear-SBSubscription -Topic 'test-topic' -Subscription 'test-sub' -ServiceBusConnectionString $script:connectionString | Out-Null

        $messages = New-SBMessage -Body 'pipe-one'
        Send-SBMessage -Queue 'test-queue' -Message $messages -ServiceBusConnectionString $script:connectionString

        Receive-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 1 |
            Send-SBMessage -Topic 'test-topic' -ServiceBusConnectionString $script:connectionString

        $received = @(Receive-SBMessage -ServiceBusConnectionString $script:connectionString -Topic 'test-topic' -Subscription 'test-sub' -MaxMessages 1 -WaitSeconds 1)
        $received.Count | Should -Be 1
        $received[0].Body.ToString() | Should -Be 'pipe-one'
    }

    It "peeks messages without removing them" {
        Clear-SBQueue -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString | Out-Null

        $messages = New-SBMessage -Body 'peek-one', 'peek-two'
        Send-SBMessage -Queue 'test-queue' -Message $messages -ServiceBusConnectionString $script:connectionString

        $peeked = @(Receive-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 2 -Peek -WaitSeconds 1)
        $peeked.Count | Should -Be 2
        ($peeked | ForEach-Object { $_.Body.ToString() } | Sort-Object) | Should -Be @('peek-one','peek-two')

        $received = @(Receive-SBMessage -Queue 'test-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 2 -WaitSeconds 1)
        $received.Count | Should -Be 2
        ($received | ForEach-Object { $_.Body.ToString() } | Sort-Object) | Should -Be @('peek-one','peek-two')
    }

    It "sends multiple sessions in parallel when PerSessionThreadAuto is set" {
        Clear-SBQueue -Queue 'session-queue' -ServiceBusConnectionString $script:connectionString | Out-Null

        $sessionA = 'auto-sess-a'
        $sessionB = 'auto-sess-b'
        $msgA = New-SBMessage -SessionId $sessionA -Body @('a1','a2','a3','a4','a5')
        $msgB = New-SBMessage -SessionId $sessionB -Body @('b1','b2','b3','b4','b5')

        Send-SBMessage -Queue 'session-queue' -Message ($msgA + $msgB) -ServiceBusConnectionString $script:connectionString -PerSessionThreadAuto

        $received = @(Receive-SBMessage -Queue 'session-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 10 -BatchSize 10 -WaitSeconds 2)
        $received.Count | Should -Be 10
        ($received | Group-Object SessionId).Count | Should -Be 2
        ($received | Where-Object { $_.SessionId -eq $sessionA }).Count | Should -Be 5
        ($received | Where-Object { $_.SessionId -eq $sessionB }).Count | Should -Be 5
    }

    It "uses multiple sender threads when PerSessionThread is specified" {
        Clear-SBQueue -Queue 'session-queue' -ServiceBusConnectionString $script:connectionString | Out-Null

        $sessionId = 'parallel-sess'
        $payloads = 1..16 | ForEach-Object { "p$_" }
        $messages = New-SBMessage -SessionId $sessionId -Body $payloads

        Send-SBMessage -Queue 'session-queue' -Message $messages -ServiceBusConnectionString $script:connectionString -PerSessionThread 4

        $received = @(Receive-SBMessage -Queue 'session-queue' -ServiceBusConnectionString $script:connectionString -MaxMessages 16 -BatchSize 8 -WaitSeconds 2)
        $received.Count | Should -Be 16
        $received | ForEach-Object { $_.SessionId | Should -Be $sessionId }
        ($received | ForEach-Object { $_.Body.ToString() } | Sort-Object) | Should -Be (@($payloads) | Sort-Object)
    }
}
