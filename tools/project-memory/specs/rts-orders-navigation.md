# Orders, repair, rally and group navigation

Status: implemented for prototype-terran-0.2; verification results live in
[iteration 02](../../../docs/sc2/iteration-02.md) and testing.md.
This extends [the first slice contract](rts-first-slice.md), not SC2 parity.

## Ownership and state

Match.Commands owns atomic selection/type/target validation and queues;
Match.Combat dispatches orders and interrupts patrol for visible combat;
Match.Repair owns paid healing; Match.Economy owns creation/resource rally;
Match.Navigation and Match.GroupMovement own collision/routes/destinations.
Input emits these commands and reads only Observation. It never heals,
charges costs or decides authoritative reachability itself.

CommandKind and OrderKind append Patrol/Repair, preserving old numeric members.
UnitOrder holds PatrolOrigin and PatrolReturning. EntityState records
PathTarget/PathStoppingDistance so changing destinations after chase clears a
stale path without a derived cache causing save/load divergence. These route
fields and Path/PathIndex are redacted even for the owner's Observation.
RallyTargetId is shown only to the owning player, alongside RallyPoint.
DTO fields participate in the shared deep copier/hash/JSON path automatically.
Catalog 0.2 and its hash reject older prototype saves; no migration is claimed.

Observation applies the same redaction policy before deep copying entity fields.
Cached reflected field selections omit authoritative route fields for everyone
and private queues/cargo/rally intent for opponents; all other current and future
DTO fields pass through the shared detached copier. This avoids allocating long
marching paths only to discard them. The same selected fields generate cached
typed expression projectors: scalar/value/string assignments avoid per-entity
reflection boxing, while reference fields retain the shared deep copier. Future
DTO fields still participate automatically. IL2CPP explicitly uses the filtered
reflection projector; other runtimes fall back only when expression compilation
throws NotSupportedException. That preserves detachment and values but does not
promise the compiled-path performance on AOT platforms. Full snapshots and
diagnostic Find continue to copy every field. Quantitative improvement requires
the paired combined render/simulation benchmark; this implementation fact alone
is not a timing pass.

AvoidanceSide is serialized steering state (-1/0/1), not an unsaved runtime
dictionary: stateless left/right choice caused a reproducible two-position
oscillation beside a settled ally. The side remains stable during the obstacle
pass, resets with successful direct movement/order completion/replacement and
is redacted from Observation. State restore validates its range and path bounds.

## Order semantics

- A lone Patrol repeats origin→target→origin and may chase visible enemies.
  After combat it resumes its patrol destination. A queued successor makes the
  current Patrol finite: reach outbound target once, then dequeue it. Appending
  during its return changes its destination back to outbound before advancing.
  Only the final Patrol repeats. Shift Patrol after a positional order derives
  its origin from that preceding endpoint, not the original issue position.
- Stop/Hold replace cyclic/repair intent. Invalid ownership, actor types,
  destination, target or unavailable group slots leave the prior orders intact;
  processed command sequence still advances as in the base command contract.
- Repair requires completed owned workers and a different, completed, owned,
  damaged Repairable target. Biological marine is not Repairable. Validate
  all actors first; never heal an enemy or self. Target loss/full HP ends the
  order without a new charge. Moving to a mobile target resets work until contact.
- At contact, each RepairTicks pays configured minerals/gas and heals up to
  RepairHealth, capped by missing HP. The partial last cycle pays one fixed cost.
  Lacking funds holds a ready cycle and the order without healing or payment;
  gaining funds allows continuation. Multiple workers execute in stable entity
  order and each rechecks current HP/funds before their own paid cycle.
- Building Move sets ground rally. With a visible resource/refinery TargetId,
  it stores resource intent; a newly produced worker uses Gather when that target
  is still harvestable. Invalid hidden IDs reject without changing rally.
  Ground rally clears resource intent. A non-worker uses the point normally.

## Navigation boundaries

Group endpoint slots are stable by ID, separated and constrained by map bounds
and public terrain. They do not consult hidden live occupancy, which would leak
information through owned order targets. One actor retains the exact requested
point. If the map cannot supply enough slots, the whole command rejects.
This is a loose arrival arrangement, not a rigid formation while moving.

Static obstacle cache and radius walkability grids are derived: building/resource
spawn/cancel/death invalidate them through InvalidateNavigation. Mobile broadphase
is derived spatial buckets, rebuilt on tick/allocator change and updated at every
position change; dead entities are filtered immediately. Snapshots restore
authoritative state, not bucket references. Actual swept-circle tests still
validate each step; broadphase never grants permission to cross geometry.

Static path smoothing happens once while constructing the route; intermediate
samples remain for diagnostics. An occupied intermediate waypoint may skip at
most four nodes after LOS and live-clear checks, while each actual movement step
still tests its swept segment. Exact segment/circle and rounded-rectangle tests
replace approximate terrain samples: a legal position 357mm below a corner
previously got a full LOS accepted but its next short step rejected forever.
Large radii/out-of-map restored paths are rejected to keep geometry arithmetic
inside the declared map bounds. No random escape or teleport bypass is used.

The same exact circle primitive also validates mobile swept segments through the
spatial broadphase. Integer resampling is checked after smoothing; rounding a
grazing safe line could otherwise create an unsafe subsegment. Invalid smoothing
falls back to the original corridor. Every A* edge, including the actual start
connector, checks exact static geometry. If the actual short movement step is
blocked after rounding, it uses the existing exact-checked steering fan, not an
endless identical replan. This escape also preserves foundation rerouting.

A path's terminal connector can still be suboptimal/invalid before local recovery;
independent public circle-obstacle Move→Shift-Move regression now covers its
actual completion and continuous swept safety. This remains a planner-quality
follow-up, not a reason to weaken collision or skip the requested endpoint.

Drop-off/harvest approach efficiency remains a separate navigation follow-up.
Stopping radii narrower than the available cell-centre ring can bias the current
global approach goal toward one side of a base; retargeting nearest mineral nodes
can amplify worker congestion. The observation-copy optimization changes neither
those goals nor economy rates. Measure region goals and saturation independently
before altering their owner mechanism.

## Presentation and verification

P targets Patrol, Y targets Repair, smart right-click continues construction or
repairs observed damaged own targets. Resource rally uses Move+TargetId rather
than issuing a Gather command from a building. A shared target mode cancels on
Esc/right-click, mode changes, lost focus and reset. Replay selection/groups and
minimap operate on the same visibility projection but cannot issue gameplay.

Contract tests cover cycles, queue successors, Stop, combat interruption,
ownership/self-target/funds/caps, snapshot/replay and hidden route redaction;
RallyTests cover resource intent and hidden IDs. GroupMovementTests verifies
every sampled tick for 16 units. NavigationBenchmark separately records 200,
400 and 800 units, arrival/stuck and tick percentiles. A completed measurement
is not a performance or full-arrival pass, and Editor timings are not graphical
standalone frame timings. Test watchdogs do not change gameplay outcomes.
