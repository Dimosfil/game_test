# Host simulation frames

Owner: GameHost frontend orchestration; SimulationFrameRunner owns one pure
fixed-step batch and detached publication. Match remains single-threaded and
authoritative. Worker scheduling changes neither rules nor save/replay schema.

Measured motivation: Iteration04 SessionCycle2 had 800 patrols at real-time 20Hz,
GPU about5ms, but main-thread Step+Observe p95 about23ms and frame p95 about35ms.
GameHost owns the coupling between the render clock and pure simulation.

GameHost accumulates bounded unscaled frame time at the selected speed even
while work is pending. At most one Task owns Match; a batch advances <=16 ticks
and cannot cross the exact replay endpoint. The main thread polls completion,
then atomically publishes the detached observation, copied command results,
authoritative combat stats and research gates for that same state. UI/view
consume published data and never borrow live state while rendering. LiveTick
means the published tick. Worker compute time is reported separately from
main-thread scheduling/publication; throughput must remain real-time.

GameHost.Match is a main-thread diagnostic borrow: its getter drains/publishes
pending work before returning. Never retain the borrow across a later schedule.
Issue drains first and rechecks Playing/IsReplay before assigning/queueing an
authoritative command. SetSpeed also drains before changing the clock boundary.
Pause/menu/reset/save/load/replay transitions synchronize
pending work. Replacement waits for the old owner; Dispose waits for its task.
All drain faults pause the host, clear its accumulator and prevent continuation
of the failed action. Update faults use the same handler. Runner clears pending
in finally on failure and rejects overlap until the previous result is consumed.

Runner invokes no Unity API and owns no clock. Root Rules and catalogs are
immutable inputs after startup. A completed publication is read-only by caller
contract; its DTO/list/array objects never alias authoritative mutable state.
Pure runner tests compare sync/worker commands, JSON continuation, replay exact
end, result detachment, overlap, completion/fault/disposal and research states.
Standalone smoke verifies the actual GameHost pause/save/load/replay workflow.

Remaining synchronous user-action gate: RtsInteraction build preview currently
borrows Match.CanPlace, so blueprint hover can wait for a pending batch. It is
outside the measured patrol scenario and remains a separate performance task;
normal rendering/HUD/tech panels use cached read models.

Opt-in combat profiling can request a detached MatchCombatDiagnostics value in
the same publication. Ordinary scheduling leaves capture disabled. DiagnosticTickLimit
fences the configured60/240-tick experiment only when combat profiling is enabled;
it neither ends the match nor changes normal gameplay or replay endpoints.
Evidence collection reads published data during timing and borrows Match only
after synchronizing outside the timed interval. See [combat](rts-combat-performance.md).

Selection is still the public mutable List owned by RtsInteraction. Its observed
ID membership cache is rebuilt only when the detached observation reference
changes; pruning scans backwards against that set. Hidden/dead IDs are removed
by the same policy without a per-selected-ID scan/closure over all entities.
