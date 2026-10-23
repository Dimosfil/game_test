# Project verification and local memory tooling

Owners: tools/test-rts.ps1 controls an Editor test lifecycle;
tools/rts-manifest.ps1 owns evidence coverage; instruction-kit helpers own local
consumer-kit completeness; project-memory Python helpers own generated retrieval.
All operate inside game_test. They do not own gameplay or deployment.

## Editor test lifecycle

test-rts validates project identity, stopped Play Mode, absence of a project
player/another test run, and clean named scenes before changing Editor state.
It persists scene paths/load/active flags in the unique run's scene-setup.json
before recompilation. Dirty/untitled scenes are rejected without saving or
discarding them. Selected tests must have nonzero count and all pass; skipped,
failed and inconclusive results are failures, not silently accepted coverage.

Success, failure and timeout all enter finally. A possibly active owned test
request is cancelled and awaited before restoring scenes. Matching scene setup
is left intact. A different setup is restored through EditorSceneManager only
if no new dirty scene would be discarded, then load/active/path equality is
verified. A cancellation or restoration failure leaves a durable
restoration-required result/checkpoint; it never deletes user work. Evidence is
confined to ignored Builds and previous runs are preserved by unique defaults.

## Source and binary provenance

rts-manifest captures source inputs (Assets/Game, ProjectSettings, package
manifest/lock and root development scripts) or complete selected player trees.
Binary capture discovers every Runtime asmdef and requires its corresponding
Managed DLL beside the unique player/Data pair. This includes Simulation as
well as Runtime, engine/plugin dependencies, boot config and content archives.
Verify recomputes both the expected input set and hashes: changed, missing,
duplicate and newly unrecorded files are rejected. Capture never overwrites an
existing manifest. Output and selected player roots stay under ignored Builds.
GameProjectTools.BuildWindowsTo and launcher PlayerDirectory support isolated
verification players without overwriting another iteration's binaries.

Historical eight-file C13–C16 manifests remain historical partial records. A new
complete manifest cannot establish the missing historical Simulation DLL hashes.
Correctness builds/smoke do not confer an older performance acceptance on new code.

## Instruction kit completeness

Version/migration readiness and file completeness are separate checks.
test-instruction-kit checks route context_files plus static patterns/templates
references in the installed root/runtime/pattern rules. The staged update check
invokes it even when versions match, without inspecting migrations or updates.
repair-instruction-kit accepts an explicit reviewed commit from the configured
public GitHub source, requires VERSION equality, creates missing library files
only, follows their static references within a bounded consumer kit, and records
paths/hashes/source in instruction-kit-recovery.json. Existing files are never
overwritten; explicit registration of a partial prior repair requires byte/hash
equality to that pinned source. INDEX maps the local installed instructions.

## Local retrieval

rag-system.json identifies game_test and approved source groups/exclusions.
Python standard-library SQLite/FTS5 indexes local Markdown/JSON only. Documents
retain path, group, trust and content hash; heading chunks retain line spans.
Duplicate source-group matches use the first group's authoritative classification.
Outside-root/symlink files, private/generated exclusions, eval inputs and the
mutable RAG configuration are excluded from the searchable corpus. There is no
network/embedding/LLM call and no credentials are needed.

SQL rebuild atomically replaces only its configured generated database. Stats
reports unavailable/current/stale using source hashes and stable configuration;
rebuild-state bookkeeping does not invalidate config. Bounded search returns
source evidence, not execution authority. Chunk export requires a current index.
Read-only rag_check checks current source/count consistency and a small retrieval
eval set. Generated SQLite/JSONL stays ignored; configs/scripts/specs stay reviewable.

run-memory resolves Python from GI_PYTHON, ignored agent-runtime.local.json or PATH.
Missing Python affects retrieval only and gives a clear configuration error.
Vector, service retrieval and code intelligence remain explicitly disabled;
working FTS satisfies the present exact lookup cases. A full GI rebuild still
has its separate confirmation contract; this implementation verifies individual
SQL/chunks/eval nodes only. Source changes require a new freshness check/rebuild.
