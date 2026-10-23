# Known Issues

## GI audit remediation 2026-10-22

Status: GI-A01..GI-A08 resolved for the current implementation. Original
[audit findings](../../docs/sc2/gi-audit-2026-10-22.md) remain historical;
[remediation and verification](../../docs/sc2/gi-remediation-2026-10-22.md) is the
current result. Raw evidence: ignored Builds/Evidence/GiRemediation/2026-10-22/.

| ID | Resolution | Evidence |
| --- | --- | --- |
| GI-A01 | Restore shares producer/unit rules with Train and validates paid queue invariants; zero head is legal. | ProductionRestoreTests 16 cases; legitimate JSON continuation/replay and genuine research fixture load pass. |
| GI-A02 | rts-manifest requires every Runtime assembly and exact whole-player/source coverage. | Three negative fixture cases; fresh 394-file binary manifest includes both Simulation DLLs; 200 source files verify unchanged. |
| GI-A03 | Accepted consumer files recovered, local INDEX and transitive startup validation added. | 15 pinned-source/hash records; 62 instruction files / 43 routes pass; version 2026.10.17.1, pending 0. |
| GI-A04 | UI resolves catalog producers/options and shared resource ordering/keys. | New producer/unit through Match, mixed selection, actual layout and key-caption parity tests. |
| GI-A05 | RuntimeText/resource table owns technology/status/reason/cost/time/combat copy. | Research reason cases and actual HUD/technology progress captures; no Russian literals in Presentation C#. |
| GI-A06 | Working stdlib SQLite/FTS5 helpers replace nonexistent configured backends. | Current-source/count health, bounded retrieval evals and chunk export; optional invalid Python is isolated; generated files ignored. |
| GI-A07 | Retrospective worker/frame architecture migration is recorded. | Source/spec/test/iteration links, invariants, compatibility and honest historical scope. |
| GI-A08 | Test runner checkpoints/restores scenes in finally, rejects unsafe/busy starts and stale coverage. | Success, zero-test failure, watchdog cancellation/timeout and dirty-scene preservation verified; Frontier active/clean. |

Remediation-snapshot assembled verification: compile 0 errors/warnings, 231/231 tests in 37.16s,
both isolated Windows builds, standalone 291 frames / 0 runtime errors / replay
end tick 626 and identical snapshot/save/load/replay hashes. Scene setup restored.

Historical limitation: old C13–C16 eight-file binary manifests do not contain
their Simulation DLL hashes. New complete records do not supply those historical
hashes. No new FPS acceptance, hardware-input claim, system trace, commit or push.
Current performance work and other product gaps remain in docs/sc2/next-iterations.md.

## C21–C22 GI follow-up — 2026-10-22

Scoped source review found no new violation in the opt-in GUI cycle diagnostics
or callback-local screen-height change. C22 source204 and binary394 manifests
were freshly reverified; saved Retry1 results are246/246,35.91s, compile0/0,
Frontier restored clean. See the focused contracts in
[moving AI performance](specs/rts-moving-ai-performance.md) and
[follow-up verification](../../docs/sc2/gi-remediation-2026-10-22.md#проверка-после-c21c22).
Overview/test/stack pointers were stale and have been reconciled. Local FTS was
stale for four updated sources; SQL/chunk export refreshed and four retrieval
evals passed. Strict FPS acceptance remains open; C23 trace
preparation is not an executed recording or a completed optimization.
