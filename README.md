# pubs ![CI](https://github.com/eosfor/pubs/actions/workflows/ci.yml/badge.svg)

[English version](README.EN.md)

PowerShell‑модуль для работы с Azure Service Bus и локальным Service Bus Emulator.

## Что умеет
- `New-SBMessage` — создать шаблон(ы) сообщений с SessionId и application properties.
- `Send-SBMessage` — отправка в очередь или топик, поддерживает параллельную отправку по сессиям (`-PerSessionThreadAuto` или `-PerSessionThread`).
- `Receive-SBMessage` — чтение из очереди или подписки; поддерживает peek (`-Peek`) без удаления и `-NoComplete` для ручного подтверждения; автоматически переключается на session receiver, если сущность требует сессии, и умеет принимать `-SessionContext` для повторного использования открытой сессии.
- `Receive-SBDLQMessage` — чтение dead-letter очереди/подписки; те же ключи `-Peek`, `-NoComplete`, `-MaxMessages`, автоматически подключается к session DLQ при необходимости.
- `Receive-SBDeferredMessage` — получение отложенных (deferred) сообщений по SequenceNumber (сессии поддерживаются).
- `Set-SBMessage` — вручную завершить/abandon/defer/dead-letter полученные сообщения.
- `New-SBSessionState` — создаёт типобезопасный объект состояния (DSO) из примитивов.
- `Get-SBSessionState`, `Set-SBSessionState` — читают/пишут состояние сессии как DSO (BinaryData внутри, JSON под капотом), без PowerShell‑JSON «расплющивания».
- `New-SBSessionContext`, `Close-SBSessionContext` — открыть и переиспользовать session receiver, чтобы выполнять receive/settle/state в одном lock.
- `Clear-SBQueue`, `Clear-SBSubscription` — очистка очереди или подписки пакетами.
- `Get-SBTopic` — список топиков с метаданными из SDK (`TopicProperties`) и runtime-информацией.
- `Get-SBSubscription` — список подписок указанного топика с метаданными (`SubscriptionProperties`) и runtime-данными, включая количество сообщений.

## Требования
- .NET 8/9 SDK для сборки.
- PowerShell 7+.
- Для локального запуска — Service Bus Emulator + SQL Edge (`docker-compose.sbus.yml`) и `.env` с `SAS_KEY_VALUE`, `SQL_PASSWORD`, `ACCEPT_EULA` (опционально `EMULATOR_HOST`, `EMULATOR_HTTP_PORT`, `EMULATOR_AMQP_PORT`, `SQL_WAIT_INTERVAL`).

Пример `.env`:
```
SAS_KEY_VALUE=LocalEmulatorKey123!
SQL_PASSWORD=Pa55w0rd1!
ACCEPT_EULA=Y
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

### Поведение WaitSeconds
- `-MaxMessages` и `-WaitSeconds` — взаимоисключающие режимы (разные parameter set); используйте только один из них в одном вызове.
- `WaitSeconds` задаёт верхнюю границу ожидания для **одного** вызова получения: если за это окно нет сообщений, команда возвращает пустой список.
- Если не задан ни `-MaxMessages`, ни `-WaitSeconds`, команда выполняет непрерывный polling до отмены (например, `Ctrl+C`).
- При длительном стриме вызывайте получение в цикле с нужными паузами/условиями:
  ```pwsh
  while ($true) {
      $batch = @(Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -BatchSize 50 -WaitSeconds 2)
      if ($batch.Count -eq 0) { break } # или своя логика выхода/сна
      # обработка $batch
  }
  ```
- Для строгой работы с конкретной сессией используйте `-SessionContext`, тогда ожидание сессии не блокирует другие.

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

Создание сообщений в цикле с явным приведением типов:
```pwsh
# При использовании 1..10 в CustomProperties важно явно указывать тип [int],
# т.к. 1..10 создаёт PSObject'ы, которые SDK Service Bus не может корректно сериализовать
1..10 | ForEach-Object {
    New-SBMessage -Body "hello world $_" -CustomProperties @{ prop="v1"; order=[int]$_ } -SessionId "myLovelySession"
} | ForEach-Object { 
    Send-SBMessage -ServiceBusConnectionString $conn -Topic "NO_SESSION" -Message $_
    Start-Sleep -Milliseconds 1500
}
```

Просмотр (peek) без удаления:
```pwsh
$peeked = Receive-SBMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 5 -Peek
# сообщения остаются в очереди и могут быть получены позже обычным вызовом

# Для топика:
# $peeked = Receive-SBMessage -Topic "test-topic" -Subscription "test-sub" -ServiceBusConnectionString $conn -MaxMessages 5 -Peek

# Dead-letter очереди/подписки
Receive-SBDLQMessage -Queue "test-queue" -ServiceBusConnectionString $conn -Peek -MaxMessages 10
Receive-SBDLQMessage -Queue "test-queue" -ServiceBusConnectionString $conn -MaxMessages 10     # читает и Complete

Receive-SBDLQMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -Peek -MaxMessages 10
Receive-SBDLQMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $conn -MaxMessages 10
# Поведение: по умолчанию сообщения из DLQ завершаются (Complete) и удаляются; для чтения без удаления используйте -Peek.
# Если нужно вручную подтвердить/переложить сообщения, задайте -NoComplete и прогоните через Set-SBMessage (как с обычной очередью).
# Без -MaxMessages и -WaitSeconds команда выполняет непрерывный polling до отмены (Ctrl+C).
# Для ограниченного вызова используйте либо -MaxMessages, либо -WaitSeconds. Сессионные DLQ обрабатываются автоматически.
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

## Потоковая сортировка несессионного топика (`reorderAndForward2.ps1`)
### Что делает
Скрипт `scripts/orderingTest/reorderAndForward2.ps1` переупорядочивает входной поток сообщений из несессионной подписки (`NO_SESSION/NO_SESS_SUB`) по `ApplicationProperties.order`, используя session state в `ORDERED_TOPIC/SESS_SUB`.
Состояние хранится как DSO `SessionOrderingState` (`LastSeenOrderNum:int`, `Deferred: List<OrderSeq{Order:int,Seq:long}>`), поэтому не требуется ручной `ConvertFrom/To-Json`.
Выходные сообщения скрипт сам отправляет в `ORDERED_TOPIC`.

### Зачем
Подходит для сценария, когда источник не гарантирует порядок, но downstream-потребителю нужен упорядоченный поток.
Скрипт держит отложенные сообщения в deferred до появления нужного `order`.
Сообщения с `order` меньше текущего ожидаемого считаются устаревшими и уходят в DLQ.

### Как попробовать
1. Убедитесь, что в эмуляторе есть сущности `NO_SESSION/NO_SESS_SUB` и `ORDERED_TOPIC/SESS_SUB` (по умолчанию они есть в `emulator/config.json`).
2. Откройте 2 окна PowerShell. В каждом загрузите модуль и подготовьте подключение. Во втором окне дополнительно загрузите скрипт:
```pwsh
$module = "src/SBPowerShell/bin/Debug/net8.0/SBPowerShell.psd1"
Import-Module $module -Force
$cs = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=LocalEmulatorKey123!;UseDevelopmentEmulator=true;"

# нужно в окне, где вызывается Process-Message
. ./scripts/orderingTest/reorderAndForward2.ps1
```
3. Во втором окне запустите обработчик потока:
```pwsh
Receive-SBMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $cs -NoComplete |
    Process-Message -ConnStr $cs
```
4. В первом окне отправьте перемешанные сообщения (обязательны `SessionId` и integer-поле `order`):
```pwsh
1..10 |
  ForEach-Object { New-SBMessage -Body "hello world $_" -CustomProperties @{ order = [int]$_; prop = "v1" } -SessionId "myLovelySession" } |
  Sort-Object { Get-Random } |
  ForEach-Object { $_; Send-SBMessage -ServiceBusConnectionString $cs -Topic "NO_SESSION" -Message $_; Start-Sleep -Milliseconds 1500 }
```
5. Остановите обработчик во втором окне через `Ctrl+C`, когда отправка завершена.
6. Проверьте упорядоченный выход:
```pwsh
$ordered = @(Receive-SBMessage -Topic "ORDERED_TOPIC" -Subscription "SESS_SUB" -ServiceBusConnectionString $cs -MaxMessages 20)
$ordered | Select-Object @{N='order';E={$_.ApplicationProperties['order']}}, SessionId, SequenceNumber, @{N='Body';E={$_.Body.ToString()}}
```
7. При необходимости посмотрите DLQ для устаревших `order`:
```pwsh
Receive-SBDLQMessage -Topic "NO_SESSION" -Subscription "NO_SESS_SUB" -ServiceBusConnectionString $cs -Peek -MaxMessages 20
```

## Полная перезагрузка эмулятора
```bash
docker compose -f docker-compose.sbus.yml down -v   # остановить и очистить данные
docker compose -f docker-compose.sbus.yml pull      # обновить образы
docker compose -f docker-compose.sbus.yml up -d     # запустить
docker compose -f docker-compose.sbus.yml ps        # проверить статус
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
