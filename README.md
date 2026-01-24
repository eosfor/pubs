# pubs

PowerShell модуль для работы с Azure Service Bus / Emulator.

## Сборка

```bash
dotnet build src/SBPowerShell/SBPowerShell.csproj
```

## Упаковка модуля

Скрипт собирает и складывает модуль в `out/SBPowerShell/<version>` вместе с зависимостями:

```pwsh
./scripts/pack-module.ps1            # Release, net8.0, версия из psd1
./scripts/pack-module.ps1 -Configuration Debug
./scripts/pack-module.ps1 -Version 0.1.0 -Framework net8.0
```

Импорт модуля после упаковки:

```pwsh
Import-Module ./out/SBPowerShell/0.1.0/SBPowerShell.psd1
```
