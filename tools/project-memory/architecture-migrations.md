# Architecture Migrations

Use this file for durable history of major architecture rewrites, platform
moves, framework replacements, storage changes, service splits, routing changes,
and other changes that alter how the project is organized.

Do not use this file for ordinary feature tasks or chat handoffs. Keep current
feature behavior in feature specs and current chat state in `tools/summary/`.

## Iteration 04 worker and detached frame publication

Status: implemented in iteration 04, dated 2026-10-21 in the iteration record;
this retrospective entry was written 2026-10-22. The exact original edit time
and any undocumented alternatives are not reconstructed.

Previously GameHost advanced fixed-step Match and built observations on the main
thread. Iteration04 SessionCycle2 measured main-thread Step/Observe p95 about
23 ms with 800 patrols, frame p95 about 35 ms and GPU about 5 ms. The simulation
cost crossed the rendering frame budget even when drawing itself was affordable.

GameHost now owns the clock and schedules at most one pure SimulationFrameRunner
batch. A task exclusively owns Match until the main thread consumes its result.
The batch publishes detached observation, command results, own combat stats and
research gates from one authoritative state. Presentation consumes that frame.
Normal render/HUD paths do not borrow live Match; synchronous CanPlace build
preview remains an explicitly documented exception and performance follow-up.

Unchanged contracts: 20 Hz rules, integer simulation, costs/damage/fog, accepted
commands, exact replay endpoint, save format/rule version and AI continuation.
Pause/menu/reset/save/load/replay and user actions drain the worker before they
touch authoritative state. Faults release pending ownership, pause the host and
prevent the failed action from continuing. No persistent-state migration or
external service was introduced.

Owners and implementation: GameHost owns frontend orchestration/time;
SimulationFrameRunner owns batch execution/publication; Simulation/Match owns
gameplay. Responsibility, exceptions and public-boundary guarantees are in
[host frames](specs/rts-host-frames.md), [first slice](specs/rts-first-slice.md)
and [simulation performance](specs/rts-performance.md). These are the current
contracts rather than copies of this historical description.

Verification: SimulationFrameRunnerTests exercises sync/worker equivalence,
detachment, all-step results, overlap, fault/disposal, research projection,
JSON continuation and replay endpoint. The real GameHost standalone smoke covers
pause/save/load/replay. Historical measurements and evidence references are in
[iteration 04](../../docs/sc2/iteration-04.md); later performance series do not
retroactively prove a causal gain for this architecture change.

Compatibility/rollback: snapshots have no worker/task/cache state. A coordinated
return to synchronous host scheduling can retain the Match/observation contracts;
it must keep draining ownership and reverify the assembled clock/save/replay
workflow. There is no claimed runtime rollback toggle. Jobs/Burst/Entities remain
separate measured decisions, not implied installed alternatives.
