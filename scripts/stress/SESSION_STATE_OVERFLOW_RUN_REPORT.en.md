# Session State Overflow Incident Report (EN)

## Verdict
- Current manual-recovery method status: **partially working**.
- Confirmed: `Emergency compact` restores the ability to persist session state.
- Not confirmed: full order/data restoration via replay from `Deferred[]` (`MessageNotFound` in 100% sampled probes).
- Conclusion: reliable manual recovery requires an updated process (`Recovery v2`) with external durable spillover/replay.

## 1. Context
- Session: `stress-session-224041`
- Input: `NO_SESSION/STATE_SUB`
- Output: `ORDERED_TOPIC/SESS_SUB`
- Main incident: session-state persistence failure once `Deferred[]` grows too large

## 2. Load generation and failure run (Feb 11, 2026)

### 2.1 Load
- File: `out/stress-runs/20260211-224041-renew-check/producer.log`
- Producer sent messages up to `Sent 11851 / 20000`
- Producer process ended with `exit=143`: `out/stress-runs/20260211-224041-renew-check/producer.exit`

### 2.2 Consumer behavior before failure
- File: `out/stress-runs/20260211-224041-renew-check/consumer.log`
- Start: `LastSeenOrder=0 DeferredCount=0`
- Then `LastSeenOrder` stayed at `1` while `DeferredCount` kept growing (`301 -> 1261 -> 1501 -> 1621 -> 2581 -> 3181 -> 3781`)
- Consumer process ended with `exit=143`: `out/stress-runs/20260211-224041-renew-check/consumer.exit`

### 2.3 Session-state size approaching limit
- `inspect.step5.232951.log`: `DeferredCount=7722`, `Utf8Bytes=249457`
- `inspect.step5.233040.log`: `DeferredCount=7786`, `Utf8Bytes=251530`
- `inspect.step5.233128.log`: `DeferredCount=7818`, `Utf8Bytes=252565`
- `inspect.step5.233217.log`: `DeferredCount=7881`, `Utf8Bytes=254623`
- `inspect.step5.233317.log`: `DeferredCount=7912`, `Utf8Bytes=255626`

### 2.4 Failure point
- `inspect.limit-check.log`: `LastSeen=1 DeferredCount=8098 Utf8Bytes=261668`
- `consumer.limit-check.log`: `Set-SBSessionState` failed with `The operation is canceled because the owner is being closed. (ServiceTimeout)`
- `consumer.limit-check.exit`: `1`

### 2.5 Initial recovery attempts in the same run
- `recovery.after-261k.log`: `SessionLockLost` on `Set-SBMessage -Complete`
- `recovery.v2.log`: `MessageNotFound` on `Receive-SBDeferredMessage`
- `consumer.post-recovery.log`: same `Set-SBSessionState ... (ServiceTimeout)` failure
- `inspect.after-recovery*.log`: state remained unchanged (`DeferredCount=8098`, `Utf8Bytes=261668`)

### 2.6 Log and behavior analysis (producer/consumer/state)

#### Producer behavior
- `producer.log` has 237 progress lines with consistent `+50` increments (from `Sent 51` to `Sent 11851`).
- No send errors are present in the producer log.
- `producer.exit=143` indicates external stop/termination, not a business send failure.

#### Consumer behavior
- In `consumer.log`, after initialization, `LastSeenOrder` remains at `1` while `DeferredCount` grows monotonically.
- `consumer.step*.log` confirms the same pattern: `LastSeenOrder=1` while `DeferredCount` increases from ~`4450` to ~`7881`.
- `consumer.step.2300*.log` and `inspect.step.2300*.log` show `SessionCannotBeLocked`, indicating lock contention across parallel/restart attempts.

#### State growth dynamics
- From checkpoint logs `inspect.step3* -> inspect.limit-check.log`:
  - `DeferredCount`: `5604 -> 8098` (`+2494`)
  - `Utf8Bytes`: `180768 -> 261668` (`+80900`)
  - Average growth: `74.82 deferred/min`
  - Average footprint: `32.44 bytes/deferred`
- In the final window before failure (`23:34:06 -> 23:36:03`):
  - `DeferredCount`: `7912 -> 8098` (`+186`)
  - Rate: `95.38 deferred/min`
- Practical conclusion: state growth was near-linear with out-of-order intake while `LastSeenOrder` did not progress.

## 3. Recovery run (Feb 12, 2026)

### 3.1 Recovery script and snapshot
- Script: `scripts/stress/recover-session-state-overflow.ps1`
- Log: `out/recovery-runs/recover-op.20260211-235200.log`
- Snapshot: `out/recovery-runs/stress-session-224041-20260211-235200/state.stress-session-224041.before.json`
- Snapshot size: `261669` bytes

### 3.2 Observed recovery progress
- `Deferred sequence count: 8098`
- `Chunk 1..5`: `recovered=0`, `missing=1000`
- Symptom: deferred entries from state were not recoverable via `Receive-SBDeferredMessage`

### 3.3 Recoverability probe
- First-500 seq probe: `ok=0`, `miss=500`
- 120-spread seq probe: `ok=0`, `miss=120`
- Interpretation: `Deferred[]` in state behaves as a stale index in this incident

### 3.4 Emergency compact and canary
- After compact: `LastSeen=1 DeferredCount=0 rawlen=33`
- 35s canary: `LastSeen=1 DeferredCount=461 rawlen=15456`
- 2m canary: `LastSeen=1 DeferredCount=1461 rawlen=48916`
- After injecting `order 2..9`: `LastSeen=1 DeferredCount=2040 rawlen=68265`
- Cutover test (`LastSeen=9`, `Deferred=[]`) + 60s: `LastSeen=9 DeferredCount=783 rawlen=26217`

### 3.5 Current state (at report time)
- `LastSeen=9 DeferredCount=783 rawlen=26217`

### 3.6 Recovery behavior analysis
- Replay by snapshot `SeqNumber` did not validate (`MessageNotFound` in 100% sampled probe), i.e. `Deferred[]` acted as a stale index for this recovery path.
- Emergency compact restored state write capability but did not restore order progression:
  - after `Deferred=0`, state started growing again (`0 -> 1461` in ~2 minutes, then up to `2040`).
- Cutover test (`LastSeen=9`, `Deferred=[]`) reduced state size but did not eliminate deferred re-accumulation (`0 -> 783` in ~60 seconds).
- Practical conclusion: current recovery commands mitigate overflow symptoms but do not remove the ordering-stall root cause in this topology.

## 4. Operational conclusions
- The failure around ~`261k` session state is reproducible.
- Emergency compact restores state write capability, but does not automatically restore order progress.
- Deferred replay by state seq did not work in this incident (`MessageNotFound` 100% in sampled probes).
- Without changing deferred storage strategy, `Deferred[]` tends to grow again.

## 5. Recommended next steps
1. Add strict state-size guardrails (soft/hard thresholds, prevent unbounded `Deferred[]` growth in state).
2. Move deferred index to external storage; keep only compact pointer/metadata in session state.
3. Add a dedicated operational command for controlled cutover (`set LastSeen + clear Deferred`) with mandatory audit trail.
