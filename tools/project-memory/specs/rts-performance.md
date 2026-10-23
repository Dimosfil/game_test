# Simulation CPU and allocation contract

Owner: Simulation/Match; diagnostic workload owner: GraphicsSimulationFixture.
Iteration04 source optimization preserves gameplay, public DTO schema, content,
fixed 20 Hz advancement and the existing 200/400/800 fixture populations.

## Visibility and knowledge

Step's RefreshKnowledge and public Observe previously rasterized the same sight
discs independently. Visibility remains a radial test at each cell centre, with
inclusive integer squared distance; public terrain does not occlude sight.
Entity visibility remains the separate exact current entity-centre sight test.
Do not replace entity visibility with the containing cell's fog value.

Derived per-player fog masks are keyed by authoritative state reference and
tick. Step invalidates them before mutations and disables stable cache reads
while executing commands, construction, production, movement and combat.
After advancing the tick, RefreshKnowledge publishes the stable masks. Restore
replaces the state reference and cannot reuse constructor masks at the same tick.
Observe always clones Visible and Explored; changing a previously returned mask
cannot influence placement, later observations or authoritative exploration.

Rasterization computes exact inclusive horizontal spans for each circle row.
Integer correction around floating-point square root preserves boundary cells;
the old clamped/truncated integer bounding boxes, including very large Sight,
remain part of equivalence. IsVisible/knowledge loops avoid capturing closures
without changing iteration order or remembered-building refresh/removal rules.

## Detached copies

The existing public-field metadata drives both full DTO copies and filtered
observation projections. Cached typed expressions copy value/string fields
directly and deeply clone reference fields. For List<T> fields, only the newly
constructed unpublished destination's list is reused and filled; source lists,
elements, arrays and nested DTOs never become shared mutable state. This avoids
constructor-list replacement garbage and per-scalar reflection boxing. Reflection
list fallback uses indices instead of allocating boxed enumerators.

IL2CPP explicitly selects reflection object/projector copying. Other runtimes
fall back only for unsupported expression compilation; model/programming errors
remain visible. Semantics and detachment apply equally to both paths, while AOT
performance must be measured separately. Snapshot/replay/hash fields and rules
hash are unchanged; caches and compiled delegates are derived, never serialized.

Observe stages live, exactly visible entity references in a private reusable
per-Match list, then sizes the fresh observation's entity list to that visible
count and deeply projects each entry in original authoritative order. Clear the
staging list in finally, including projection failure. It is exclusively owned
with Match and never published, saved or hashed. Never size a public list from
the authoritative population: its public Capacity would reveal hidden entities.
Alternating players, later steps and Restore must preserve every previously
returned observation and cannot carry a previous player's staging/capacity into
the next result. MatchContractTests covers hidden population and retained DTOs;
paired allocation evidence is separate from Unity frame/native GC measurements.

## Combat and navigation broadphases

Automatic targeting keeps derived per-owner lists of live entity references,
keyed by state reference, tick and allocator. Step invalidates the index before
mutations; lazy rebuilding after production includes newly spawned units, and
each candidate's current health is checked again after earlier attackers fire.
Current gameplay has no ownership-changing ability; one must invalidate or
extend this index before adding such a mechanic. Target distance/ID ordering and
exact visibility remain authoritative, including after Restore.

Iteration05 automatic targeting first rejects candidates worse than the currently
selected squared-distance/ID pair, before reading target radius or calculating
its acquisition range. No target-dependent range can make a worse valid candidate
replace an already eligible nearer/tie-winning target. Its actor is alive and completed by TickOrder's
caller contract: a candidate inside that actor's inclusive exact Sight already
has the actor itself as a visibility witness. Only candidates beyond self-sight
(for example Hold firing with allied vision) need the army-wide IsVisible query.
This proof changes no acquisition range, target ordering or cooldown behavior;
searches must still run while cooling down because chasing/path changes remain
part of the authoritative tick. No additional spatial index is introduced here.

MatchCombatDiagnostics is a detached value capture for opt-in profiling. Shots
and actual health damage belong to the attacker owner; damage excludes overkill.
Deaths belong to the victim owner and include every owned combat definition.
Live marine counts/health sums cover alive definitionId="marine" entities only.
Private totals are derived per Match and reset on new/Restore; they never enter
MatchState, save/replay schemas or gameplay hashes. Capture is nonmutating and
must follow the same exclusive Match ownership as other authoritative reads.

Iteration06 adds DetailedDiagnosticsEnabled (default false). Its lazy movement/AI
storage is prepared when enabled, reserving four sets for 400 IDs each before
timed movement (larger workloads grow normally). Ordinary gameplay performs guard
checks without per-movement sets/counter work. MarineTravel sums integer-root
millimetres of actual MoveEntity segments, MovedMarines counts lifetime distinct
IDs (including later casualties), and TickMovingMarines counts distinct IDs in
the latest executed Step. Disabling freezes lifetime totals and captures zero
latest-step movement. BotThinkCount counts enabled Think invocations. Accepted
player-1 commands count only after successful execution while a player-1 bot is
enabled, with separate AttackMove/Build/Gather/Train counts and AttackMove actor
totals; rejected attempts and player-0 driver commands do not count. This is an
opt-in harness attribution contract: its scripted driver issues player 0 only,
not a general command-origin tag. Restore/new Match resets the switch and all
derived totals; capture adds no authoritative state, fog disclosure or save data.

Static navigation circles cache immutable catalog radius plus live entity
references in centre buckets under the existing navigation invalidation owner.
Spawn, foundation cancellation and static death rebuild that derived geometry;
queries always recheck health and ignore the mover's ID. Segment bounds expand
by mover radius plus largest static radius to conservatively select buckets.
Large query grids fall back to the full static list. An open circle-AABB reject
precedes the original exact integer/decimal circle primitive, preserving strict
tangency and squared negative-radius behavior. Buckets only reject impossible
candidates; exact swept collision still decides every accepted step. Avoiding a
second identical static check before the unit sweep does not skip waypoint/path
mutations or change steering options.

## Verification and evidence

Automatic combat targeting tests local range before querying army-wide exact
visibility. It selects a single minimum by squared distance then entity ID,
preserving the former sorted result for Hold, Patrol and AttackMove. Patrol's
acquisition radius remains the attacker's own Sight; stationary firing retains
AttackRange plus target radius even when an ally supplies visibility. A closer
hidden target is ineligible. CombatTargetingTests covers these boundaries and
equal-distance IDs with reversed authoritative iteration order. The real-catalog
800-patrol workload exposed this cost independently of the isolated moving-army
fixture; both measurements remain required rather than substituting workloads.

VisibilityCacheTests independently compares the old per-cell raster at fractional
centres, exact boundaries, map edges and large sight; exercises observation-array
mutation, movement, actual combat death, public production/construction, same-tick
restore and 600 ticks of live AI/JSON continuation for both players.

Ignored Builds/Evidence/Iteration04/simulation-{baseline,fog-batch1,
typed-clone-batch2}.json retain pure CLR phase timing and current-thread allocated
bytes for the unchanged exact fixture. Compare all checkpoint/final gameplay
hashes and fixture source hashes as well as measurements. Pure values do not
establish Unity frame budgets, native GC or full-session 60 FPS. Root integration
owns actual Unity tests and standalone render/combined/full-session measurement.

Iteration04 session-stage artifacts retain exact 800 independent Patrols plus
the original catalog units; AI is disabled only in that diagnostic session.
`session-navigation-comparison.json` links before/after 100-tick hashes and
stage timings. `circle-oracle.json` compares 100000 primitive cases and 30000
static queries against the original full scan, including bucket-edge centres.
CombatTargetingTests additionally covers same-tick target death and production,
and continued target acquisition after Restore. These are not a substitute for
the real GameHost HUD/fog/camera/input frame measurement.

Iteration05 combat-cycle1-equivalence.json retains the exact 60-tick400-vs400
checkerboard comparison with frozen baseline sources: every full-state JSON,
diagnostic capture, gameplay hash and both-player observation agrees. Two short
live-AI scenarios additionally compare JSON continuation and accepted-command
replay. CombatTargetingTests covers inclusive self-sight boundaries and allied
visibility outside them; MatchCombatDiagnosticsTests covers cooldown, real HP,
overkill, deaths, capture detachment and reset/replay semantics. Actual standalone
combat throughput and allocations remain the integrator's measured gate.

combat-cycle2-equivalence.json repeats the same frozen-baseline comparison after
moving that pruning before catalog lookup. Its allocation counter surrounds Step
inside compiled C# and excludes PowerShell, JSON, Observe and hash/capture calls;
initial lazy caches remain included. These current-thread CLR bytes are a
separate diagnostic from standalone Unity native GC/frame measurements.

Iteration06 A* scratch belongs to one exclusively owned Match. FindPath is
synchronous, nonrecursive and invokes no external callbacks; parent/cost/closed
arrays, neighbour offsets and heap backing storage can therefore be reused by
successive searches. Each A* resets every numeric array and clears the heap,
including after unreachable searches or prior early success with a nonempty
frontier. Returned paths remain newly owned lists, never scratch aliases. This
changes neither goal scanning, neighbour order, score/node ties, exact geometry,
replanning policy nor serialized routes. Restore constructs its own scratch
lazily; none is saved or hashed. NavigationWorkspaceTests protects simultaneous
route independence, unreachable-to-reachable reset and JSON continuation.

Ignored Iteration06/moving-ai-cycle1-equivalence.json retains source/catalog/
fixture/probe hashes and the exact 240-tick comparison against frozen C0. Both
players' full observations, full states, accepted replay and diagnostics agree;
JSON Restore at tick 120 continues to the same uninterrupted state, and every
replay tick matches the live gameplay hash. The real moving-AI fixture ends at
hash 57aa79fa7f18f8e3f46f42c40386b51ca3891b0b2d9d7cc83d6ab56fdb544a46.
Pure current-thread Step allocations fall from 3136951.73 to 26555.83 bytes/tick;
total measured Step time falls from 5877.47 to 4640.13 ms. These are pure CLR
diagnostics, not standalone Unity frame/GC or real-time guarantees. The small
opt-in diagnostic-set reservation also moved before timing in this batch.

Iteration06 Cycle2 first chooses the unconstrained closest cell centre using
clamped long arithmetic, with midpoint ties choosing the lower X/Z indices.
When that cell is allowed (or the existing start-cell exception), it is exactly
the minimum the old row-major strict-distance scan would choose. A blocked
nearest cell still uses the unchanged exhaustive scan; nearest alternatives are
not guessed. Valid authoritative destinations are inside maps of at most
512 by 512 cells, so squared-distance sums retain their existing numeric bounds.
Out-of-map destinations explicitly retain the old full scan, including its
historical overflow behavior for arbitrary internal/restored coordinates.
Neither the A* traversal nor stopping-distance termination changes. Independent
old-scan arithmetic checks cover every midpoint/centre +/-1mm for several widths
and one-dimensional int extremes (not overflowing two-axis distance sums).
Public chase tests preserve the old detour
side at a midpoint, exercise the blocked-building fallback, and reach targets
near both map edges.

Ignored moving-ai-cycle2-equivalence.json compares final guarded source against
frozen Cycle1Sources/Runtime/Simulation (only Match.Navigation.cs differs).
All 240 live states/observations/diagnostics, tick-120 JSON continuation and every
accepted-command replay tick remain exact, as do two short AI/Restore scenarios.
Its independent old one-axis scan checks 4810 cases. Pure Step totals are
4768.30 ms before and 2509.30 ms after; current-thread allocations are 28188.47
and 26865.27 bytes/tick, including cold lazy initialization. These measurements
are separate from standalone Unity CPU/frame/allocation verification and do not
establish a frame budget by themselves.

## Phase iteration allocation contract

Construction/production and order processing capture their own phase-entry
entity count and traverse that prefix by index. This preserves the former
shallow-array snapshot because Spawn only appends and physical removal/reordering
occurs after orders at Step end. Production captures before births, so a newborn
does not receive a second production turn; orders capture after production, so
that same newborn receives its existing same-tick action. Health and other live
reference changes remain visible to later actors. Restore replaces state only
outside Step under the exclusive Match owner. Any future mid-phase removal,
reordering or ownership transfer must revisit this invariant before shipping.
Private Entity(id) scans directly for the first matching live entity, avoiding
the capturing predicate previously allocated by List.Find without changing
lookup order or dead-entity behavior.

Iteration05 combat-cycle3-equivalence.json compares frozen Cycle2 sources with
this change for every one of 240 exact fixture ticks: full state, both player
observations, counters and hashes agree, including the known 60/240-tick hashes.
Two AI scenarios include JSON Restore and accepted-command replay; six public
proof cases per variant include newborn firing on its birth tick while the next
production item remains unadvanced. Step-only current-thread CLR allocation
falls from 22260.8 to 13843.33 bytes/tick over the unchanged attrition run; this
does not establish a CPU/frame-time improvement. The probe and frozen reference
source manifests are retained under ignored Builds/Scratch and
Builds/Evidence/Iteration05/Cycle2Sources.
