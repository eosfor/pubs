# Session State Overflow Incident Report (RU)

## Verdict
- Статус метода ручного восстановления из текущего runbook: **частично работает**.
- Подтверждено: `Emergency compact` восстанавливает возможность записи session state.
- Не подтверждено: полное восстановление порядка и данных через replay из `Deferred[]` (`MessageNotFound` в sample 100%).
- Вывод: для надежного ручного восстановления нужен обновленный процесс (`Recovery v2`) с внешним durable spillover/replay.

## 1. Контекст
- Сессия: `stress-session-224041`
- Вход: `NO_SESSION/STATE_SUB`
- Выход: `ORDERED_TOPIC/SESS_SUB`
- Основной инцидент: переполнение/неуспешная запись session state при большом `Deferred[]`

## 2. Прогон генерации и падения (11 Feb 2026)

### 2.1 Нагрузка
- Файл: `out/stress-runs/20260211-224041-renew-check/producer.log`
- Продюсер отправлял сообщения до `Sent 11851 / 20000`
- Процесс продюсера завершен с `exit=143`: `out/stress-runs/20260211-224041-renew-check/producer.exit`

### 2.2 Поведение consumer до отказа
- Файл: `out/stress-runs/20260211-224041-renew-check/consumer.log`
- Старт: `LastSeenOrder=0 DeferredCount=0`
- Затем: `LastSeenOrder` оставался `1`, `DeferredCount` рос (`301 -> 1261 -> 1501 -> 1621 -> 2581 -> 3181 -> 3781`)
- Процесс consumer завершен с `exit=143`: `out/stress-runs/20260211-224041-renew-check/consumer.exit`

### 2.3 Подход к лимиту размера state
- `inspect.step5.232951.log`: `DeferredCount=7722`, `Utf8Bytes=249457`
- `inspect.step5.233040.log`: `DeferredCount=7786`, `Utf8Bytes=251530`
- `inspect.step5.233128.log`: `DeferredCount=7818`, `Utf8Bytes=252565`
- `inspect.step5.233217.log`: `DeferredCount=7881`, `Utf8Bytes=254623`
- `inspect.step5.233317.log`: `DeferredCount=7912`, `Utf8Bytes=255626`

### 2.4 Фиксация отказа
- `inspect.limit-check.log`: `LastSeen=1 DeferredCount=8098 Utf8Bytes=261668`
- `consumer.limit-check.log`: ошибка на `Set-SBSessionState`:
  - `The operation is canceled because the owner is being closed. (ServiceTimeout)`
- `consumer.limit-check.exit`: `1`

### 2.5 Первые recovery-попытки в том же прогоне
- `recovery.after-261k.log`: `SessionLockLost` на `Set-SBMessage -Complete`
- `recovery.v2.log`: `MessageNotFound` на `Receive-SBDeferredMessage`
- `consumer.post-recovery.log`: снова `Set-SBSessionState ... (ServiceTimeout)`
- `inspect.after-recovery*.log`: state не изменился (`DeferredCount=8098`, `Utf8Bytes=261668`)

### 2.6 Анализ логов и поведения (producer/consumer/state)

#### Поведение producer
- `producer.log` содержит 237 строк прогресса с равномерным шагом `+50` сообщений (от `Sent 51` до `Sent 11851`).
- Ошибок отправки в логе нет.
- `producer.exit=143` означает внешнюю остановку процесса, а не бизнес-ошибку отправки.

#### Поведение consumer
- `consumer.log`: после инициализации `LastSeenOrder` фиксируется на `1`, при этом `DeferredCount` монотонно растет.
- Серия `consumer.step*.log` подтверждает тот же паттерн: `LastSeenOrder=1` при росте `DeferredCount` от ~`4450` до ~`7881`.
- Серия `consumer.step.2300*.log` и `inspect.step.2300*.log` содержит `SessionCannotBeLocked`: параллельные/повторные старты упираются в lock активного receiver.

#### Динамика роста state
- По checkpoint-логам `inspect.step3* -> inspect.limit-check.log`:
  - `DeferredCount`: `5604 -> 8098` (`+2494`)
  - `Utf8Bytes`: `180768 -> 261668` (`+80900`)
  - Средний рост: `74.82 deferred/min`
  - Средний удельный размер записи: `32.44 bytes/deferred`
- В финальном участке перед отказом (`23:34:06 -> 23:36:03`):
  - `DeferredCount`: `7912 -> 8098` (`+186`)
  - Темп: `95.38 deferred/min`
- Практический вывод: state рос почти линейно с количеством out-of-order сообщений, а `LastSeenOrder` не продвигался.

## 3. Прогон recovery (12 Feb 2026)

### 3.1 Recovery-скрипт и snapshot
- Скрипт: `scripts/stress/recover-session-state-overflow.ps1`
- Лог: `out/recovery-runs/recover-op.20260211-235200.log`
- Snapshot: `out/recovery-runs/stress-session-224041-20260211-235200/state.stress-session-224041.before.json`
- Размер snapshot: `261669` байт

### 3.2 Наблюдаемый прогресс recovery
- `Deferred sequence count: 8098`
- `Chunk 1..5`: `recovered=0`, `missing=1000`
- Симптом: deferred-индекс в state не подтверждается как recoverable через `Receive-SBDeferredMessage`

### 3.3 Recoverability probe
- Проверка 500 seq (первые): `ok=0`, `miss=500`
- Проверка 120 seq (равномерно): `ok=0`, `miss=120`
- Интерпретация: `Deferred[]` в state для этого инцидента — stale index

### 3.4 Emergency compact и canary
- После compact: `LastSeen=1 DeferredCount=0 rawlen=33`
- Canary 35s: `LastSeen=1 DeferredCount=461 rawlen=15456`
- Canary 2m: `LastSeen=1 DeferredCount=1461 rawlen=48916`
- После инъекции `order 2..9`: `LastSeen=1 DeferredCount=2040 rawlen=68265`
- Cutover-тест (`LastSeen=9`, `Deferred=[]`) + 60s: `LastSeen=9 DeferredCount=783 rawlen=26217`

### 3.5 Текущее состояние (на момент отчета)
- `LastSeen=9 DeferredCount=783 rawlen=26217`

### 3.6 Анализ поведения recovery
- Replay по `SeqNumber` из snapshot не подтвердился (`MessageNotFound` в sample 100%), то есть `Deferred[]` в state фактически stale для восстановления этим методом.
- Emergency compact стабилизировал запись state (можно снова вызывать `Set-SBSessionState`), но не восстановил progression:
  - после `Deferred=0` state снова растет (`0 -> 1461` за ~2 минуты, затем до `2040`).
- Cutover-тест (`LastSeen=9`, `Deferred=[]`) снизил размер state, но не устранил паттерн повторного накопления deferred (`0 -> 783` за ~60 секунд).
- Практический вывод: в текущей топологии recovery-команды устраняют симптом переполнения, но не первопричину стагнации порядка.

## 4. Операционные выводы
- Авария на ~`261k` state воспроизводится стабильно.
- Emergency compact снимает только отказ записи state, но не восстанавливает прогресс по order автоматически.
- Replay deferred по seq из state в данном инциденте не сработал (`MessageNotFound` 100% в sample).
- Без изменения алгоритма хранения deferred процесс склонен снова накапливать `Deferred[]`.

## 5. Рекомендованные следующие шаги
1. Внедрить hard-guardrail по размеру state (soft/hard thresholds, отказ от дальнейшего роста `Deferred[]` внутри state).
2. Вынести deferred-индекс во внешнее хранилище (state хранит только компактный pointer/метаданные).
3. Добавить отдельный operational command для controlled cutover (`set LastSeen + clear Deferred`) с обязательным audit trail.
