# Mass-combat performance contract

Owners: Match for authoritative combat/visibility; BattleView for observed shot
feedback; GameHost/SimulationFrameRunner for exclusive worker publication;
SessionPerformanceProfile.Combat for opt-in measurement. Rules/save/replay
remain prototype-terran-0.3. The measured fixture is not a balance reference.

## Mechanism mapping

- Idle/Hold/AttackMove/Patrol -> Engage -> Match.Combat chooses the exact nearest
  eligible target by squared distance then ID. Acceleration must preserve allied
  vision outside the attacker's own sight, dead-target filtering and same-tick
  death/production. Verify the frozen old implementation and every tick/hash,
  observations and cumulative combat outcomes, plus focused public tests.
- Published LastAttackTick/Target -> BattleView resolves only current observed
  or previously visible nonremembered targets, then displays the existing beam
  and audio. Reusable shot resources must preserve every eligible beam, widths,
  endpoints, palettes, material and .07 scaled Unity-time lifetime. Population
  preparation belongs to normal observed-view setup, never fake benchmark shots.
- Commands/resource/selection UI -> BattleHud owns display strings and dispatch;
  remove repeated temporary lists/delegates and unchanged text formatting while
  keeping controls, hotkeys, authoritative eligibility and input behavior.
- Frame publication -> SimulationFrameRunner owns detached read models. Private
  immutable catalog preparation may be cached; published mutable DTOs/maps must
  not be overwritten/recycled while consumers can still retain them.

## Diagnostic boundary

MatchCombatDiagnostics is a detached value with cumulative shots and actual HP
damage per attacker owner, deaths per victim owner, current marine live counts
and summed health. It is derived, never saved/hashed, and starts at zero on a new
Match/Restore. Capture only while owning Match. Worker publication carries it
only for the opt-in combat diagnostic; product UI remains fog-filtered.

The combat fixture keeps original nonmarine spawns and real bases, replaces
marine spawns with exactly400+400 interleaved ordinary marines, and clears terrain.
Only the fixture disables the bot. Autonomous normal Engage performs real shots,
damage and death; no HP reset/resurrection/changed stats occurs within a battle.
Each independent battle warms rendering while paused, then runs exactly ticks0..60
or0..240 (explicit launcher choice)
through the actual host. DiagnosticTickLimit fences only opt-in combat advancement;
normal gameplay/replay clocks are unchanged. One/all selections reset the same
fixture. Multiple trials are independent battles, not one continuous800-survivor fight.

## Acceptance and evidence

Require actual camera/IMGUI work and foreground focus, the same geometry,
animation, shadows/MSAA/resolution, and all living marines still observed/in the
unobscured viewport while population is at least720. At lower populations record
natural fog losses explicitly; do not call that band an800-unit workload.
Record authoritative counters and publication/coverage traces
without synchronously borrowing Match during timing. Record final tick60 hashes
outside timing; selection/trial changes must not change the result. The240-tick
attrition horizon ends at289 live marines,511 deaths in the frozen fixture.

Render budget and real-time20Hz advancement are independent gates. Original
baseline had frame p95 around11..12ms but60ticks took13..15seconds, so FPS alone
was not success. Report worker compute per batch with completed ticks, not as
single-tick CPU. Population frame bands describe the last published presentation,
not hidden in-flight worker state. PostFirstShotFrames means after the first shot;
NewAttackPublications records actual positive cumulative shot deltas.

No dead/hidden units may silently turn an800 combat claim into a smaller workload.
Report initial800 and natural casualties; separate exact800 and >=720 published
population bands and all measured frames. First volleys/death frames belong in
timing; screenshots, JSON and sorting stay outside it. The separate untimed volley
capture proves VFX presence without PNG encoding stalling timing samples.

Native GC requires a producing Development recorder. Record raw bytes/frame and
totals/bytes per completed tick as well as quantiles. A stalled baseline publishes
less often, so its small GC p95 cannot be compared directly to a real-time build.
Use consecutive real-time cycles for allocation claims; pure .NET bytes/tick are
separate evidence. Fresh source/binary hashes, reports and samples live under
ignored Builds/Evidence/Iteration05; docs/sc2/iteration-05.md owns measured results.

ObservedShotPool retains its highest prepared population capacity across resets
until BattleView destruction. This trades retained native resources for absence
of per-shot GameObject/LineRenderer churn; no visible shot is dropped at capacity.
Final05:160 EditMode tests; ten Release60-tick trials pass p95/p99/20Hz with
zero timed tracer creation. Pure Step-only C2→C3 evidence reduces allocation
22,260.8→13,843.33bytes/tick on the240-tick fixture (37.8%); that excludes
observations, native allocations and rendering. This is not a zero-GC claim.

Next scope06 is moving armies plus genuine bot decisions. Do not reuse05 AI-off
results as evidence for that combined scenario. Keep its own frozen baseline,
frame-to-publication annotations, movement/AI counters and raw tail samples.
