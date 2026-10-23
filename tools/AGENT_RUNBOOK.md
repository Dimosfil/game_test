# Agent Runbook

Verification-tooling update: 2026-10-22. Gameplay/performance dates remain scoped
to their individual reports below.

## Project State

`game_test` is a Unity 6000.3.24f1 project with a first experimental Terran RTS
slice under Assets/Game and Frontier.unity in build scenes. Built-in rendering
is retained. Pipeline remains 0.8.0-exp.1; no second Editor bridge was installed.
See [gameplay/controls](../docs/sc2/first-slice.md),
[test contract](../docs/sc2/testing.md), and
[implementation contract](project-memory/specs/rts-first-slice.md).

## Current Local Workflow

Only one integrator controls the Editor. Preserve unrelated dirty scenes. All
commands below select this project; inspect current CLI help before new flags.
Builds/Evidence and Builds/Players are ignored rebuildable outputs.

Unity CLI is now 1.0.0-beta.14 (self-update/check verified 2026-10-22); Editor and
Pipeline remain 6000.3.24f1 / 0.8.0-exp.1. No second bridge or setup was installed.
The historical beta.13 note below describes the earlier connection setup.

```powershell
# Reuse a ready Editor, or start the project's installed version headlessly.
.\tools\start-unity.ps1

# Recompile, wait for Pipeline reload, run EditMode contracts, save JSON results.
.\tools\test-rts.ps1
# Optional bounded selection; each run gets its own ignored evidence/checkpoint.
.\tools\test-rts.ps1 -Filter ProductionRestoreTests

# Author scene only when no scene has unsaved changes.
unity command eval --project-path C:\work\game_test -- --code 'ColonyRTS.Editor.GameProjectTools.CreatePlayableScene();'

# Long operations are detached; inspect returned jobId with unity job status.
unity command eval --project-path C:\work\game_test --detach --timeout 120 -- --code 'ColonyRTS.Editor.GameProjectTools.HeadlessSmoke();' --timeout 110000
unity command eval --project-path C:\work\game_test --detach --timeout 240 -- --code 'ColonyRTS.Editor.TechnologySmoke.Run();' --timeout 220000
unity command eval --project-path C:\work\game_test --detach --timeout 360 -- --code 'ColonyRTS.Editor.GameProjectTools.BuildWindows();' --timeout 330000
# Separate instrumentation player; never mix its timing with the Release series.
unity command eval --project-path C:\work\game_test --detach --timeout 360 -- --code 'ColonyRTS.Editor.GameProjectTools.BuildWindowsDevelopmentProfile();' --timeout 330000

# Synthetic navigation measurements, independent of the open scene.
unity command eval --project-path C:\work\game_test --detach --timeout 240 -- --code 'ColonyRTS.Editor.NavigationBenchmark.Run();' --timeout 220000
unity command eval --project-path C:\work\game_test --detach --timeout 240 -- --code 'ColonyRTS.Editor.NavigationBenchmark.RunExtended();' --timeout 220000
```

`HeadlessSmoke` checks actual runtime rules, movement/economy/build/train,
1200 ticks, JSON save/load with working AI and accepted-command replay hashes.
It writes Builds/Evidence/smoke.json. BuildWindows writes
Builds/Players/Windows/KopruluFront.exe and verifies BuildReport; generated small
Resources/BuildShaders materials are build inputs, not downloaded media.

NavigationBenchmark.Run measures 200/400/800 movers for 600 ticks; RunExtended
uses the same fixtures for 1800 ticks. Their separate JSON files record actual
arrival/stuck, sampled geometry, tick percentiles and a cooperative watchdog.
Completed means measured, not that all movers arrived or a frame budget passed.

`tools/unity-mcp.mjs probe` starts the official `unity mcp` stdio process,
initializes it, lists tools and calls Editor status/open scenes. Run with a
Node.js executable; UNITY_CLI and UNITY_PROJECT_PATH optionally override path
discovery. `list pattern` prints exact schemas; `call name arguments.json`
executes an advertised tool. Do not read or print connection tokens.

## Connection Recovery on 2026-10-20

The CLI opened the project, but a GUI Administrator Privileges Detected dialog
blocked startup. Computer Use could read it but input failed with GetCursorPos
access denied. The integrator closed only its own newly launched modal process,
then started the same installed Editor in documented persistent batchmode
without -quit and without -nographics. Editor/Pipeline became ready.

Verified now: CLI status/editor_status/open scenes; MCP initialize 2025-03-26,
160 tools and successful read-only calls via the project client. Native Unity
MCP tools are absent from this chat's catalog; the working official CLI/client
does not prove they were dynamically restored there.

Batchmode has no Game View render target even if a GameView window object is
created. Camera-only screenshots omit IMGUI, so verify the real HUD in a normal
graphical standalone. `-rtsSmoke -rtsEvidencePath ABSOLUTE_DIRECTORY` enables the
opt-in player smoke: actual frames/UI/screenshot, 600 ticks, isolated save/load,
and a frame-driven replay. It writes player-smoke.json/gameplay.png and exits.
This is a test seam through real product entry points, not hardware input.

Verified 2026-10-20: 30/30 EditMode contracts; real-rules 1200-tick smoke;
Windows BuildReport success; final graphical standalone exit 0 with actual HUD
capture, isolated save/load and viewer replay to its exact end/hash. Detailed
results and gaps are in docs/sc2/testing.md. Run the graphical smoke in a normal
window; a hidden player skips IMGUI and is deliberately rejected:

```powershell
.\tools\run-rts-player.ps1 -Kind smoke
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode baseline
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode baseline -Simulation
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode optimized -Simulation -DevelopmentProfile
```

Iteration04 explicit backend/session commands (same Release executable for the
paired rendering comparison; Development allocations are a separate build):

```powershell
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode optimized -RenderBackend legacy -Simulation -ProfileSeconds 15
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode optimized -RenderBackend instanced -Simulation -ProfileSeconds 15
.\tools\run-rts-player.ps1 -Kind session
.\tools\run-rts-player.ps1 -Kind session -DevelopmentProfile
```

Iteration05 actual autonomous400+400 combat (AI disabled only in this fixture):

```powershell
.\tools\run-rts-player.ps1 -Kind combat -CombatTrials 5 -CombatTicks 60
.\tools\run-rts-player.ps1 -Kind combat -CombatTrials 1 -CombatTicks 240
.\tools\run-rts-player.ps1 -Kind combat -CombatTrials 1 -DevelopmentProfile
```

CombatTrials1..5 creates independent fresh battles per selection1/all400;
CombatTicks60(default) or240 fences the real host at the exact ending tick.
Warmup is paused, definitions/HP/damage remain ordinary and deaths are natural.
Initial800, exact800 and>=720 presentation population bands are explicit; the
240-tick attrition run ends with fewer survivors. No reinforcement/HP resets
occur inside a battle. `passed` validates evidence; separately require
Meets60FpsP95/P99 and RealTime20Hz. GC counters require the separate Development
player and retain raw samples/totals; never infer a GC improvement from the
stalled baseline's smaller publication frequency. See [iteration05](../docs/sc2/iteration-05.md).

Iteration06 adds genuine moving armies and ordinary bot decisions:

```powershell
.\tools\run-rts-player.ps1 -Kind combat -MovingAI -CombatTicks 240 -CombatTrials 5
.\tools\run-rts-player.ps1 -Kind combat -MovingAI -CombatTicks 240 -CombatTrials 1 -DevelopmentProfile
# Optional diagnostic boundaries; keep separately labelled from default Release evidence.
.\tools\run-rts-player.ps1 -Kind combat -MovingAI -CombatTicks 240 -CombatTrials 5 -MainLoopBoundaryProbe
# Optional five native wait markers; discover availability in this player.
.\tools\run-rts-player.ps1 -Kind combat -MovingAI -CombatTicks 240 -CombatTrials 5 -MainLoopBoundaryProbe -MainLoopNativeWaitProbe
```

MovingAI requires240ticks. Two separated400 formations keep original stats,
bases/workers/resources/supply; only player0 receives a scripted attack-move.
Bot1 must actually think, build and issue accepted army commands. Evidence checks
distinct movers/commanded IDs, cumulative travel and movement/shot overlap,
without claiming every unit moves/fires simultaneously or survives throughout.
Tail evidence includes p99.9/max, counts over16.667/25/33.333/50ms, bursts and raw
stage/frame indices. Native GC preparation precedes actual-frame warmup; screenshots
are untimed. Require the independent throughput/frame/coverage gates, not just
the measurement status. See [iteration06](../docs/sc2/iteration-06.md).

MainLoopBoundaryProbe is opt-in for MovingAI only. It records early Update,
coroutine resume and WaitForEndOfFrame timestamps/cycles with frame IDs, validity
flags and three telescoping intervals. EndOfFrame is not a Present boundary;
use raw timestamps and availability when localizing delays. C7 CPU cycle/process
samples are resume-adjacent before coverage, unlike C6's after-coverage samples.
MainLoopNativeWaitProbe additionally records five individually discovered native
wait/Present markers with raw nanoseconds, invocation counts and availability.
They aggregate all threads and may overlap/nest; never add them as independent
main-thread durations. CaptureFrame is the read frame, not a native producer ID.

`session` runs the actual GameHost at20Hz with an opt-in initial +800-marines
fixture, individual patrols, full HUD/fog/minimap/input, then selections of1/all.
Both phases require15seconds and180actual camera/GUIframes, all800visible and
patrolling, and real-time ticks. Frame p95 <=16.667ms is reported separately
from workload validity. It checks health mesh shrink/empty and worker pause.
Only this fixture disables enemy AI and clears terrain; ordinary gameplay
keeps its original AI/rules. It does not measure combat or a complete game.

The reusable launcher explicitly requests a normal visible window, waits for its
owned process and verifies exit code, fresh evidence, status and configuration.
An ordinary child process can inherit SW_HIDE from a hidden shell, advance Update
and never render IMGUI. The render harness requires actual camera and GUI frames.
Native CPU/GPU timing, counters and paired profile commands are documented in
[render-profiling](../docs/sc2/render-profiling.md). Normal gameplay omits test flags.

TechnologySmoke uses unchanged runtime rules and legitimate resource income to
build the full technology chain, complete six upgrades, train an upgraded marine,
restore an in-flight research and reconstruct the accepted-command replay. It
writes Builds/Evidence/technology-smoke.json. The economic fixture disables enemy
AI; live AI continuation is covered separately by HeadlessSmoke and NUnit tests.
An optional `-TechnologyFixture ABSOLUTE_SNAPSHOT` on the smoke launcher loads a
legitimate command-generated research snapshot through the isolated product save,
then tests cancel/research entry points and captures producer/tree progress HUD.
Development profiling builds go to Builds/Players/WindowsProfile. Their native
allocation counters supplement Release measurements; their frame times are a
separate build configuration and cannot be substituted into Release comparisons.

## Verification lifecycle and provenance

The test runner refuses a running project player, another test run, Play Mode,
dirty scenes and untitled scenes. It captures clean named scene setup before
recompile, cancels/awaits its own timed-out request, restores only if setup changed,
and verifies restoration in finally. New unsaved work is preserved and reported
as restoration-required. Default evidence is Builds/Evidence/Tests/<unique-run>/:
compile.json, source-manifest.json, editmode-results.json, scene-setup.json and
scene-restoration.json. All selected tests must pass with no skips/inconclusives.
The source manifest is frozen after compile and checked again after the suite.

For isolated verification players, call
`GameProjectTools.BuildWindowsTo("GiVerification/Windows")` and
`BuildWindowsTo("GiVerification/WindowsProfile", true)` through the same
project-scoped eval flow. The launcher accepts
`-PlayerDirectory GiVerification/Windows` (or the profile counterpart with
`-DevelopmentProfile`). This preserves another iteration's player paths.

Capture a fresh manifest with `tools/rts-manifest.ps1 -Action capture -Kind
source|binary -ManifestPath Builds/Evidence/<run>/<manifest>.json`. Binary capture
accepts `-PlayerRoots` for isolated builds and covers the whole player tree,
including every Runtime asmdef DLL. Verify with the same arguments and
`-Action verify`; roots come from the saved manifest. Keep the manifest outside
the player tree. Missing/changed/unrecorded files fail. Historical manifests are
never overwritten or supplemented with invented old hashes.

## Local instruction integrity and retrieval

The update checker treats missing policy keys, including an empty `update_check`
object, as enabled defaults under PowerShell StrictMode. Property lookup uses
the metadata property collection directly; preserve this local adaptation when
refreshing the shared checker. Only JSON booleans are accepted for policy keys.

`tools/test-instruction-kit.ps1` validates all route and transitive static library
references. Staged update checks invoke it separately from migration readiness.
Missing consumer rules can be recovered with `tools/repair-instruction-kit.ps1
-SourceCommit <reviewed-40-character-commit>` only when its VERSION matches the
installed accepted version. Recovery preserves existing local content and records
source/hash provenance in tools/project-memory/instruction-kit-recovery.json.

Memory retrieval uses Python 3.10+ and stdlib SQLite FTS5. Resolve Python through
GI_PYTHON, config/agent-runtime.local.json (ignored; copy the example), or PATH.
No dependency install, network, embeddings or vector service is needed.

```powershell
.\tools\project-memory\run-memory.ps1 -Node sql rebuild
.\tools\project-memory\run-memory.ps1 -Node sql stats
.\tools\project-memory\run-memory.ps1 -Node sql search "ResearchAvailability"
.\tools\project-memory\run-memory.ps1 -Node sql export-chunks
.\tools\project-memory\run-memory.ps1 -Node evals run
```

These are individual configured nodes, not the full `gi tools rebuild` flow.
Stats detects stale source/config hashes. DB and chunk JSONL stay ignored;
vector/service/code-intelligence capabilities remain disabled. Source groups,
exclusions, commands and stable project ID are in rag-system.json. UI production
bindings and localization are documented in [UI commands](../docs/sc2/ui-commands.md).

## Unity MCP Access

Historical setup, checked 2026-10-17. Unity CLI 1.0.0-beta.13 is current according to
`unity self-update --check --json`. Installed `com.unity.pipeline` 0.8.0-exp.1
through the official CLI; Unity resolved it in `Packages/packages-lock.json`.

```powershell
unity pipeline install --project-path "C:\work\game_test" --non-interactive --json
unity status --project-path "C:\work\game_test" --json
```

After installation, Unity initially returned `STATUS_PIPELINE_LOAD_PENDING`.
The user activated the existing Editor window, allowing Package Manager to
import the package. No restart, scene changes, or client reconfiguration was
needed.

Verified access:

- Project-scoped MCP stdio: initialize succeeded (protocol 2025-03-26),
  tools/list advertised 160 tools, and tools/call editor_status returned ready
  for `C:\work\game_test`, Unity 6000.3.24f1.
- Direct MCP tools in this chat: editor_status, list_open_scenes,
  get_scene_hierarchy, and console_status all succeeded.
- Editor: not compiling, no domain reload, Play Mode stopped.
- One active scene without a saved path; isDirty=false; roots Main Camera and
  Directional Light. No scene was saved or modified during verification.
- Console: no compilation failure, zero errors and warnings at verification.

Connection behavior and boundaries are documented in
`tools/project-memory/specs/integration-contracts/unity-pipeline.md`.

Git ignore rules were configured on 2026-10-18 for Unity caches, local settings,
build output, generated IDE files, and local .env values. Assets and their
.meta files, Packages manifests/lockfile, and ProjectSettings remain eligible.

## GI Context

Run from the project root in PowerShell:

```powershell
.\tools\get-gi-context.ps1 -CommandText "gi start"
```

See `COMMANDS.md` for the GI command index. Add product commands here when
implemented.

## Documentation Check

```powershell
git diff --check
```

Expected result: exit code 0 and no whitespace errors.

## Preferences

- Agent replies match the user's language by default.
- Commit messages default to English; no commit was requested during setup.
- Task-manager plan sync awaits the user's choice: WorkNest or none.
- Config-service integration is disabled by default.
