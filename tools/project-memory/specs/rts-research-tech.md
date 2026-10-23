# Research and owner-wide combat technology

Status: implemented experimental research mechanism; integration verification is
reported in the current iteration documents. This is not SC2 numerical parity.

## Ownership and API

`Simulation/Match.Research.cs` owns research availability, payment, completion and
effective combat values. `Match.ResearchValidation.cs` owns catalog and restored
technology invariants. Commands, economy ticks, combat and observation delegate
to those mechanisms. Presentation must not independently decide affordability,
prerequisites, reservation, damage or armor.

- Append `CommandKind.Research`, preserving all earlier enum numbers. The command
  uses exactly one distinct producer entity ID and an upgrade `DefinitionId`.
- `Upgrade(string)` returns a detached definition or null.
- `ResearchAvailability(int player, int producerId, string upgradeId)` takes an
  entity ID, returns detached `CommandResult`, and never pays or reserves. Its
  gate is the same one called when the command executes. Invalid or foreign
  guessed producers return a uniform unavailable result before private queues
  are inspected.
- `CombatDamage(int player, string definitionId)` and `CombatArmor(...)` return
  base values plus that owner's completed matching bonuses. Invalid players or
  definition IDs throw; these are authoritative diagnostic/own-UI queries.

## Rules and lifecycle

`RuleSet.Upgrades` is an explicit catalog of one-time `UpgradeDefinition` entries.
Missing/null upgrade arrays normalize to an empty owned array; callers' rule
objects are not mutated. Levels are separate definitions linked by
`PrerequisiteUpgradeId`, not a hard-coded weapon/armor level algorithm. IDs,
producers, entity/upgrade prerequisites, affected definitions, durations, costs
and bonuses come from the catalog. Cyclic prerequisites, malformed references,
negative values and aggregate combat integer overflow are rejected at startup.
Base armor defaults to zero.

Research requires an alive, completed, owned building of the exact producer
definition; optional completed owned entity and upgrade prerequisites are checked
at start. A building may hold one active research item. Production and research
are mutually exclusive, and an owner cannot reserve the same upgrade at two
buildings or research a completed upgrade. Opponents have independent technology.
All gates run before payment or queue mutation.

`EntityState.Research` stores `ProductionItem` with upgrade ID, configured total
and remaining ticks, and actual paid mineral/gas costs. Payment occurs once at
acceptance. Research advances in the existing economy phase before orders and
combat; a duration of one completes in its execution Step, matching production
timer semantics. Completion appends the owner's upgrade ID once and clears the
active item. Losing an entity prerequisite after acceptance does not cancel
research. Cancel refunds the full paid item and releases reservation. Destruction
loses progress and cost; reservation disappears with the dead producer.

## Combat, persistence and knowledge

Completed technology applies owner-wide to matching existing and subsequently
created entities. Damage bonuses accumulate before defender armor; each actual
weapon hit deals `max(1, attacker damage - defender armor)`. Technology does not
modify base catalog definitions or copy bonuses into individual unit state.

Shared DTO cloning, rule hashing, state hashing, JSON save/load and accepted-command
replay include research and completed technology automatically. Restore rejects
unknown/duplicate completed IDs, broken completed upgrade chains, unknown or
duplicate owner reservations, wrong/incomplete/neutral producers, simultaneous
production and research, completed-and-active duplication, invalid remaining or
total progress, and paid costs inconsistent with the rule catalog. Active research
does not require its start-time entity prerequisite to remain alive. No separate
unsaved reservation dictionary or combat cache is authoritative.

`PlayerObservation.CompletedUpgrades` is a detached list for that observation's
owner only. Visible and remembered opponents never expose active research queues;
ordinary own queues remain visible. Full `CaptureState`/`Find` remain diagnostic
APIs and must not be passed to player presentation or a future network client.

## Verification boundary

Independent tests cover same-owner reservation, opponent independence, queues and
refund, prerequisites, completed levels, minimum damage, existing/future units,
malformed catalogs/saves, observation redaction and save/replay continuation. Unity
operations belong to the integrator; a pure C# compile or probe does not establish
that the Editor, actual catalog, HUD or standalone build has passed.

Verified 2026-10-21: Unity 104/104 includes 33 research contracts. Actual-catalog
technology smoke completed all six upgrades with real income/build commands at
tick 31149; mid-research JSON continuation and replay agree. Final Release player
smoke passed with genuine saved research loaded through product save/load,
cancel/restart entry points and captured producer/tree progress, 0 runtime errors.
This does not verify physical keyboard/mouse or autonomous AI tech strategy.
Evidence: Builds/Evidence/Iteration03/technology-smoke.json,
StandaloneVerified/player-smoke.json; summary docs/sc2/iteration-03.md.
