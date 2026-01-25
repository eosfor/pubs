# pubs ![CI](https://github.com/eosfor/pubs/actions/workflows/ci.yml/badge.svg)

PowerShell‑модуль для работы с Azure Service Bus и локальным Service Bus Emulator.

## Что умеет
- `New-SBMessage` — создать шаблон(ы) сообщений с SessionId и application properties.
- `Send-SBMessage` — отправка в очередь или топик, поддерживает параллельную отправку по сессиям (`-PerSessionThreadAuto` или `-PerSessionThread`).
- `Receive-SBMessage` — чтение из очереди или подписки; поддерживает peek (`-Peek`) без удаления; автоматически переключается на session receiver, если сущность требует сессии.
- `Clear-SBQueue`, `Clear-SBSubscription` — очистка очереди или подписки пакетами.

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
