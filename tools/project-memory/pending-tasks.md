# Pending Tasks

## GI Bootstrap — 2026-10-17

Goal: install a project-owned instruction kit from the requested source.

- [x] Verify target identity and preserve the existing README and Git remote.
- [x] Install GI instructions, routing, preferences, and project memory.
- [x] Adapt the project entrypoint and runbook to the empty repository.
- [x] Verify installed paths, metadata, PowerShell syntax, and whitespace.
- [ ] Ask whether to enable WorkNest plan sync or use none.

No product implementation or technology selection was requested.

## Unity Pipeline Connection — 2026-10-17

Goal: connect Pipeline to the current Unity project and verify MCP access.

- [x] Verify project identity, installed CLI help, latest CLI version, and missing Pipeline.
- [x] Install the latest Pipeline package using official project-scoped CLI (0.8.0-exp.1).
- [x] Verify live Editor identity and scenes through MCP in this chat.
- [x] Record package version, commands, and verification evidence.

Mechanism: official CLI updates Packages/manifest.json; Unity Package Manager
resolves/imports the package; Pipeline serves Editor commands to Unity MCP.
Preserve scenes, Play Mode, user focus, and unrelated changes.

## GI Project Audit — 2026-10-22

Goal: assess current project compliance with GI, with source-backed findings and
separate fresh verification from historical evidence. Audit only; preserve
existing implementation changes, scenes, settings and prior evidence.

- [x] Check instruction-kit versions: installed/source 2026.10.17.1; pending 0.
- [x] Load applicable rules and establish project/worktree/Editor identity.
- [x] Inspect simulation/host/presentation boundaries and focused contracts.
- [x] Verify C13 source/binary manifest entries against current files.
- [x] Run current EditMode contracts: 195/195, 38.44 seconds, no skips/failures.
- [x] Reproduce suspected boundary defects using isolated in-memory matches.
- [x] Check GI routing, source/evidence coverage, resources and memory hygiene.
- [x] Prepare prioritized findings, rule references, gaps and remediation order.

Historical audit result: eight findings (seven P2, one P3); at that checkpoint
fixes had not started. All eight were subsequently resolved below.
Report: `docs/sc2/gi-audit-2026-10-22.md`; focused follow-up:
`tools/project-memory/known-issues.md`. First fix is invalid production Restore,
then complete binary provenance and instruction/retrieval configuration.
Scene restoration was verified. External runtime/test edits appeared during the
audit; the 195-test result must not be attributed to that later source state.

Verification scope: static inspection, read-only inventory/hash checks, isolated
in-memory probes and existing EditMode suite. No graphical benchmarks, system
traces, rebuilds, dependency changes, commit or push. Raw audit evidence belongs
under ignored Builds/Evidence/GiAudit/2026-10-22/; this checklist is compact memory.

## GI audit remediation — 2026-10-22

Goal: fix GI-A01..GI-A08, preserve existing gameplay rules/schema and unrelated
C14–C16 performance work, verify the assembled product and update durable docs.

- [x] A01 Restore -> ValidateState/production owner -> reject invalid producer,
  costs and timers; retain paid legal queues, blocked output, JSON/replay.
- [x] A08 test lifecycle -> test-rts.ps1 -> capture clean
  scene setup and restore on success/failure/timeout; preserve dirty work.
- [x] A02 build provenance -> reusable source/binary manifest tooling -> cover
  every product assembly and required launch inputs; keep old manifests intact.
- [x] A03 kit installation -> accepted-source recovery + transitive validation
  -> restore missing consumer rules and prevent silent incomplete installation.
- [x] A06 retrieval config -> working local SQL/FTS, disabled optional backends, valid
  project identity and no commands pointing to missing helpers.
- [x] A04/A05 UI -> catalog-derived production/build bindings + UI resources ->
  preserve existing commands/hotkeys and move all localized copy to RuntimeText.
- [x] A07 architecture history -> record implemented worker/frame transition.
- [x] Recompile, focused and assembled tests, smoke/build checks, resource/link/
  instruction validation, final provenance and docs/memory reconciliation.

Mechanism owners and verification are mapped above before implementation.
Existing worktree is dirty; do not replace it or rewrite C13/C16 evidence.
Raw new evidence: ignored Builds/Evidence/GiRemediation/2026-10-22/.
No commit/push or new system trace is requested. Coordinate Editor checks around
any existing performance player; do not start a competing performance job.

Result: all eight remediations complete. Final suite 231/231 (37.16s), compile
0/0, 200 source files unchanged, isolated Release/Development builds and 394-file
binary provenance verified. Standalone 291 frames, 0 errors, exact replay end626,
genuine research fixture workflow and HUD captures passed. Safety runner failure/
timeout/dirty-scene cases preserve work; Frontier restored clean. Fresh FTS
readiness/evals are recorded in the remediation evidence after final docs.

## C21–C22 GI follow-up — 2026-10-22

Scope: latest GUI diagnostics and callback-local screen-height cleanup, not a
new whole-project audit or FPS experiment. Mapping: current-state lookup ->
overview/test/stack pointers and local FTS -> docs and retrieval tooling owners
-> scoped documentation updates and SQL/chunk refresh -> links, source/binary
hashes, retrieval health/evals. No gameplay source change or bypass required.

- [x] Instruction-kit integrity62 files/43 routes; accepted version unchanged, pending0.
- [x] Inspect C21 native opt-in/failure validity, tests and C22 Repaint/resize boundary.
- [x] Reverify C22 exact source204 and both player trees394; saved suite246/246,
  compile0/0 and clean scene restoration match this source.
- [x] Reconcile README, testing and stack; preserve historical results and failed FPS gates.
- [x] Refresh stale local SQL/chunk retrieval and verify four evals, links and whitespace.

Performance continuation and unexecuted C23 trace remain in current-iteration;
they are not unresolved GI-A01..GI-A08 fixes. No Editor/tests/build/player/trace
operation is needed for this documentation-only follow-up.
