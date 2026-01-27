# pubs ![CI](https://github.com/eosfor/pubs/actions/workflows/ci.yml/badge.svg)

PowerShell‑модуль для работы с Azure Service Bus и локальным Service Bus Emulator.

## Что умеет
- `New-SBMessage` — создать шаблон(ы) сообщений с SessionId и application properties.
- `Send-SBMessage` — отправка в очередь или топик, поддерживает параллельную отправку по сессиям (`-PerSessionThreadAuto` или `-PerSessionThread`).
- `Receive-SBMessage` — чтение из очереди или подписки; поддерживает peek (`-Peek`) без удаления и `-NoComplete` для ручного подтверждения; автоматически переключается на session receiver, если сущность требует сессии, и умеет принимать `-SessionContext` для повторного использования открытой сессии.
- `Receive-SBDeferredMessage` — получение отложенных (deferred) сообщений по SequenceNumber (сессии поддерживаются).
- `Set-SBMessage` — вручную завершить/abandon/defer/dead-letter полученные сообщения.
- `Get-SBSessionState`, `Set-SBSessionState` — чтение/запись состояния сессии.
- `New-SBSessionContext`, `Close-SBSessionContext` — открыть и переиспользовать session receiver, чтобы выполнять receive/settle/state в одном lock.
- `Clear-SBQueue`, `Clear-SBSubscription` — очистка очереди или подписки пакетами.
- `Get-SBTopic` — список топиков с метаданными из SDK (`TopicProperties`) и runtime-информацией.
- `Get-SBSubscription` — список подписок указанного топика с метаданными (`SubscriptionProperties`) и runtime-данными, включая количество сообщений.

## Требования
- .NET 8/9 SDK для сборки.
- PowerShell 7+.
- Для локального запуска — Service Bus Emulator + SQL Edge (`docker-compose.sbus.yml`) и `.env` с `SAS_KEY_VALUE` (опционально `EMULATOR_HOST`, `EMULATOR_HTTP_PORT`, `EMULATOR_AMQP_PORT`).

Пример `.env`:
```
SAS_KEY_VALUE=LocalEmulatorKey123!
EMULATOR_HOST=localhost
EMULATOR_HTTP_PORT=5300
EMULATOR_AMQP_PORT=5672
```

## Быстрый старт с эмулятором
```bash
docker compose -f docker-compose.sbus.yml up -d
dotnet build          # соберёт модуль в src/SBPowerShell/bin/Debug/net8.0
pwsh -NoLogo          # далее команды из PowerShell
```

Импорт модуля из bin:
```pwsh
$module = "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
Import-Module $module -Force
```

Отправка и приём:
```pwsh
$conn = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=LocalEmulatorKey123!;UseDevelopmentEmulator=true;"
$messages = New-SBMessage -Body "hello","world" -CustomProperties @{ prop="v1" }
Send-SBMessage -Queue "test-queue" -Message $messages -ServiceBusConnectionString $conn
Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 2

# Топик/подписка
$topicMsgs = New-SBMessage -Body "t1","t2"
Send-SBMessage -Topic "test-topic" -Message $topicMsgs -ServiceBusConnectionString $conn
Receive-SBMessage -Topic "test-topic" -Subscription "test-sub" -ServiceBusConnectionString $conn -MaxMessages 2
```

- Для топика указывайте `-Topic` при отправке и пару `-Topic` + `-Subscription` при получении. Для очереди достаточно `-Queue`.
- `-Subscription` требуется только при чтении из топика; для очереди этот параметр не используется.

Просмотр метаданных топиков и подписок:
```pwsh
# все топики (TopicProperties + RuntimeProperties)
Get-SBTopic -ServiceBusConnectionString $conn

# все подписки топика, с runtime-данными (ActiveMessageCount и др.)
Get-SBSubscription -ServiceBusConnectionString $conn -Topic "test-topic"

# фильтр по подписке
Get-SBSubscription -ServiceBusConnectionString $conn -Topic "test-topic" -Subscription "test-sub"

# пайплайн: передать объекты TopicProperties дальше
Get-SBTopic -ServiceBusConnectionString $conn | Get-SBSubscription -ServiceBusConnectionString $conn
```

Отправка по сессиям с автопараллелизмом:
```pwsh
$s1 = New-SBMessage -SessionId "sess-1" -Body "a1","a2","a3"
$s2 = New-SBMessage -SessionId "sess-2" -Body "b1","b2","b3"
Send-SBMessage -Queue "session-queue" -Message ($s1 + $s2) -ServiceBusConnectionString $conn -PerSessionThreadAuto
Receive-SBMessage -Queue "session-queue" -ServiceBusConnectionString $conn -MaxMessages 6

# Аналог для топика/подписки с сессионной сущностью:
# Send-SBMessage -Topic "session-topic" -Message ($s1 + $s2) -ServiceBusConnectionString $conn -PerSessionThreadAuto
# Receive-SBMessage -Topic "session-topic" -Subscription "session-sub" -ServiceBusConnectionString $conn -MaxMessages 6
```

Просмотр (peek) без удаления:
```pwsh
$peeked = Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 5 -Peek -WaitSeconds 1
# сообщения остаются в очереди и могут быть получены позже обычным вызовом

# Для топика:
# $peeked = Receive-SBMessage -Topic "test-topic" -Subscription "test-sub" -ServiceBusConnectionString $conn -MaxMessages 5 -Peek -WaitSeconds 1
```

Ручное подтверждение/отложка (settlements):
```pwsh
# Получаем без Complete
$msg = Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 1 -NoComplete

# Отложить (defer) сообщение и потом забрать его по SequenceNumber
$deferredSeq = ($msg | Set-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -Defer).SequenceNumber
$again = Receive-SBDeferredMessage -Queue "test-queue" -ServiceBusConnectionString $conn -SequenceNumber $deferredSeq

# Или завершить вручную
$msg | Set-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -Complete
```

Потоковая сортировка через defer (для несессионных подписок):
```pwsh
# Сообщения содержат ApplicationProperties.order и sessionId (псевдо-сессия)
Receive-SBMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -MaxMessages 50 -NoComplete |
    ForEach-Object {
        $sid   = $_.ApplicationProperties['sessionId']
        $order = [int]$_.ApplicationProperties['order']
        $state = $global:orderState[$sid] ??= [pscustomobject]@{ Last = 0; Deferred = @() }
        $expected = $state.Last + 1

        if ($order -eq $expected) {
            $_ | Set-SBMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -Complete | Out-Null
            $state.Last = $order
            New-SBMessage -SessionId $sid -Body $_.Body.ToString() -CustomProperties @{ order = $order; sessionId = $sid }
        } else {
            $_ | Set-SBMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -Defer | Out-Null
            $state.Deferred += $_.SequenceNumber
            return
        }

        # пробуем подтянуть отложенные в правильном порядке
        while ($state.Deferred.Count) {
            $nextSeq = ($state.Deferred | Sort-Object | Select-Object -First 1)
            $state.Deferred = $state.Deferred | Where-Object { $_ -ne $nextSeq }
            $deferred = Receive-SBDeferredMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -SequenceNumber $nextSeq
            if (-not $deferred) { break }
            $dOrder = [int]$deferred.ApplicationProperties['order']
            if ($dOrder -eq $state.Last + 1) {
                $deferred | Set-SBMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -Complete | Out-Null
                $state.Last = $dOrder
                New-SBMessage -SessionId $sid -Body $deferred.Body.ToString() -CustomProperties @{ order = $dOrder; sessionId = $sid }
            } else {
                $state.Deferred += $deferred.SequenceNumber
                break
            }
        }
    } |
    Send-SBMessage -Topic "ORDERED_TOPIC" -ServiceBusConnectionString $conn

# SESS_SUB (требует сессий) получит упорядоченный поток благодаря SessionId=sessionId
```

Повторное использование session receiver (SessionContext):
```pwsh
# Открываем сессионный ресивер один раз
$ctx = New-SBSessionContext -Queue "session-queue" -SessionId "sess-1" -ServiceBusConnectionString $conn

Receive-SBMessage -SessionContext $ctx -MaxMessages 5 -NoComplete |
    Set-SBMessage -SessionContext $ctx -Complete

# Работаем с состоянием в рамках того же lock
Get-SBSessionState -SessionContext $ctx
Set-SBSessionState -SessionContext $ctx -State @{ Progress = 42 }

Close-SBSessionContext -Context $ctx
```

## Тесты
- Pester: `pwsh -NoLogo -File tests/SBPowerShell.Tests.ps1` (использует эмулятор).
- C# xUnit интеграционные: `dotnet test tests/SBPowerShell.IntegrationTests/SBPowerShell.IntegrationTests.csproj` (тоже требует работающего эмулятора и .env).

## Ручные проверки
- `scripts/manual/send-100.ps1` — отправляет 100 сообщений `msg1..msg100` в топик `test-topic`.
- `scripts/manual/receive-100.ps1` — принимает до 100 сообщений из подписки `test-topic` / `test-sub` и выводит краткую сводку объектов.

Запуск:
```pwsh
pwsh scripts/manual/send-100.ps1
pwsh scripts/manual/receive-100.ps1
```

## Упаковка модуля
Скрипт складывает модуль в `out/SBPowerShell/<version>` вместе с зависимостями:
```pwsh
./scripts/pack-module.ps1            # Release, net8.0, версия из psd1
./scripts/pack-module.ps1 -Configuration Debug
./scripts/pack-module.ps1 -Version 0.1.0 -Framework net8.0
```

Импорт после упаковки:
```pwsh
Import-Module ./out/SBPowerShell/0.1.0/SBPowerShell.psd1
```
