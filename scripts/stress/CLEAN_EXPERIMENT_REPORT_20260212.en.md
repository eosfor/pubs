# Clean Experiment 2026-02-12 (PT1H, freeze -> sample -> recovery)

## Quick result
- The experiment completed end-to-end via the automated orchestrator.
- Rule "freeze producer+consumer before recovery" was respected.
- Rule "run deferred sample before any state change" was respected.
- Branch selection was correct: `sample ok=120`, so `Replay` path was used.
- Replay finished without `MessageNotFound`: `Recovered=1798`, `Missing=0`.
- After recovery `Deferred=0`, but deferred started growing again during canary (`0 -> 1400`).

## Run artifacts
- Run folder:
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100`
- Main log:
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100/orchestrator.log`
- Auto-generated reports:
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100/EXPERIMENT_REPORT.ru.md`
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100/EXPERIMENT_REPORT.en.md`
- Summary JSON:
  - `out/clean-experiment/20260212-222100-clean-session-20260212-222100/summary.json`

## What was executed
1. Emulator reset (`docker down -v`, then `docker up -d`).
2. Producer and consumer started.
3. Stop-point reached and both processes frozen.
4. Session-state snapshot captured.
5. Deferred sample probe executed before any state mutation.
6. Recovery branch selected from sample result.
7. Recovery executed.
8. Canary executed and post-canary state captured.

## Facts from logs
- Freeze trigger: `consumer-exited`.
- Consumer failure reason: `Set-SBSessionState ... A task was canceled.`
  - File: `out/clean-experiment/20260212-222100-clean-session-20260212-222100/consumer.log.err`
- State before recovery:
  - `LastSeen=1`, `Deferred=1798`, `Utf8Bytes=56268`.
- Deferred sample:
  - `sample=120`, `ok=120`, `miss=0`, `err=0`.
- Decision:
  - `Decision=Replay`.
- Replay outcome:
  - `DeferredTotal=1798`, `Recovered=1798`, `NotFound=0`.
- State after recovery:
  - `LastSeen=1`, `Deferred=0`, `Utf8Bytes=33`.
- State after canary:
  - `LastSeen=1`, `Deferred=1400`, `Utf8Bytes=44657`.

## Producer/consumer behavior (plain language)
- Producer sent steadily in batches and reached `7201` of `9000` before freeze.
- Consumer did not fail on the 261k overflow point in this run; it failed earlier on state-persist cancellation.
- Deferred replay worked well in this run (no sampled loss indicators).
- After consumer restart, deferred started accumulating again.

## Operational meaning
- The recovery flow is usable as an emergency "unblock and re-activate deferred" step for this case.
- It does not remove the ordering-stall root cause: deferred can start growing again.
- So recovery is a mitigation, not a final architecture fix.

## Next actions
1. Add state-size guardrails in consumer (soft/hard thresholds) before hard failure.
2. Add controlled cutover operation (`set LastSeen` + clear deferred) with audit.
3. For strict 261k reproduction, run a dedicated longer scenario with lock-renew and/or a more resilient consumer loop.

## Orchestrator
- Working automated orchestrator is saved in a separate file:
  - `scripts/stress/run-clean-recovery-experiment.ps1`
