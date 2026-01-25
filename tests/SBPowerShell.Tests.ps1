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

function script:Wait-ForEmulator {
    param(
        [string]$EmulatorHost,
        [int]$HttpPort = 5300,
        [int]$AmqpPort = 5672,
        [int]$TimeoutSeconds = 60
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    Write-Host ("Waiting for emulator health on http://{0}:{1}/health and AMQP {2} ..." -f $EmulatorHost, $HttpPort, $AmqpPort)

    while ([DateTime]::UtcNow -lt $deadline) {
        $httpOk = $false
        try {
            $resp = Invoke-RestMethod -UseBasicParsing -Method Get -TimeoutSec 5 -Uri ("http://{0}:{1}/health" -f $EmulatorHost, $HttpPort)
            $httpOk = $true
        } catch {}

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

    throw "Emulator not ready after $TimeoutSeconds seconds."
}

function script:Ensure-TopicSubscription {
    param(
        [Parameter(Mandatory)][string]$Topic,
        [Parameter(Mandatory)][string]$Subscription,
        [bool]$RequiresSession = $false
    )

    try {
        $admin = [Azure.Messaging.ServiceBus.Administration.ServiceBusAdministrationClient]::new($script:connectionString)

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
