# Чистый эксперимент 2026-02-12 (PT1H, freeze -> sample -> recovery)

## Короткий итог
- Эксперимент полностью завершен автоматическим оркестратором.
- Условие "freeze producer+consumer перед recovery" выполнено.
- Условие "sample deferred до любых изменений state" выполнено.
- Ветка выбрана корректно: `sample ok=120`, поэтому пошли в `Replay`.
- Replay отработал без `MessageNotFound`: `Recovered=1798`, `Missing=0`.
- После recovery `Deferred=0`, но на canary deferred снова начал расти (`0 -> 1400`).

## Где лежит прогон
- Папка прогона:
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100`
- Главный лог:
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100/orchestrator.log`
- Итоговые авто-отчеты:
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100/EXPERIMENT_REPORT.ru.md`
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100/EXPERIMENT_REPORT.en.md`
- Сводка JSON:
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100/summary.json`

## Что было сделано по шагам
1. Обнулили эмулятор (`docker down -v`, затем `docker up -d`).
2. Запустили producer и consumer.
3. Дождались stop-point и сделали freeze обоих процессов.
4. Сняли snapshot session state.
5. До любых изменений state сделали sample-проверку deferred.
6. По результату sample выбрали recovery-ветку.
7. Выполнили recovery.
8. Запустили canary и сняли состояние после canary.

## Факты из логов
- Триггер freeze: `consumer-exited`.
- Причина падения consumer: `Set-SBSessionState ... A task was canceled.`
  - Файл: `out/clean-experiment/20260212-222100-clean-session-20260212-222100/consumer.log.err`
- Состояние перед recovery:
  - `LastSeen=1`, `Deferred=1798`, `Utf8Bytes=56268`.
- Sample deferred:
  - `sample=120`, `ok=120`, `miss=0`, `err=0`.
- Решение:
  - `Decision=Replay`.
- Replay результат:
  - `DeferredTotal=1798`, `Recovered=1798`, `NotFound=0`.
- Состояние после recovery:
  - `LastSeen=1`, `Deferred=0`, `Utf8Bytes=33`.
- Состояние после canary:
  - `LastSeen=1`, `Deferred=1400`, `Utf8Bytes=44657`.

## Поведение producer/consumer (простыми словами)
- Producer отправлял быстро и ровно батчами, дошел до `7201` из `9000` до момента freeze.
- Consumer на этом тесте упал не на "переполнении 261k", а раньше: на отмене операции сохранения state.
- Recovery deferred-сообщений сработал хорошо (без потерь по sampled-критерию).
- Но после запуска consumer снова пошло накопление deferred.

## Что это значит операционно
- Наш recovery-процесс рабочий как "разблокировка и возврат deferred в active" для этого кейса.
- Но он не лечит первопричину стагнации порядка: после перезапуска deferred снова растет.
- То есть recovery годится как аварийная процедура, но не как финальное исправление архитектуры.

## Что делать дальше
1. Добавить в consumer guardrail по размеру state (soft/hard пороги) и аварийный режим до падения.
2. Продумать controlled cutover (manual step): выставление `LastSeen` и очистка `Deferred` с аудитом.
3. Для воспроизведения именно 261k-сценария сделать отдельный длинный прогон с lock-renew и/или более устойчивым consumer.

## Оркестратор
- Рабочий автоматизированный оркестратор сохранен в отдельном файле:
  - `scripts/stress/run-clean-recovery-experiment.ps1`
