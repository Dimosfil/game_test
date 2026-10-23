# Technology Stack

Development tooling reviewed: 2026-10-22. Historical gameplay/performance evidence
retains its own dates; current verification is linked from the runbook.

Canonical source: this file
Linked from: project README

This is project documentation. Keep business rules, feature algorithms, workflow
contracts, state machines, and verification guarantees in project memory; keep
stack facts, commands, runtime assumptions, and operational notes here.

## Summary

- Unity 6000.3.24f1, revision 4e7b9b5b6244; first RTS slice under Assets/Game,
  Frontier.unity enabled for builds, Windows x64 standalone built successfully.
- Pure C# simulation (integer X/Z, 20 Hz), single-owner Task worker with detached
  frame publication, and separate Unity views are implemented.
  Full target scope remains in [the plan](../../../docs/sc2/implementation-plan.md).
- Facts below are verified from files; live connection has a separate status.

## Components

| Layer | Technology | Evidence | Notes |
| --- | --- | --- | --- |
| Engine | Unity 6000.3.24f1 | ProjectSettings/ProjectVersion.txt | C# runtime under Assets/Game |
| Rendering | Built-in, no global/quality SRP | GraphicsSettings.asset, QualitySettings.asset | URP/HDRP absent |
| Input/color | Input Manager, Gamma | ProjectSettings.asset | activeInputHandler=0; m_ActiveColorSpace=0 |
| Backend/storage | Local versioned JSON snapshot/replay | GameHost, Simulation/Match.Persistence | No external product service or SC2 format compatibility |
| Editor integration | Pipeline 0.8.0-exp.1 | manifest/lockfile, live CLI/MCP probe | Persistent project Editor ready in batchmode |
| Tests | Test Framework 1.6.0, transitive | Resolved packages-lock entry, scoped test source manifests | C26 preparation:271/271,36.49s,compile0/0; source214/binary394 and guarded dual builds verified. Captured GAME oracle is separate. Current status: docs/sc2/current-iteration.md; GI remediation231/231 is historical. |
| Multiplayer tooling | Multiplayer Center 1.0.1 | manifest/lockfile | Not a gameplay transport/server |
| CLI | 1.0.0-beta.14 | self-update/--check, 2026-10-22 | Current/latest matched; Editor/Pipeline unchanged |
| UI command data | Catalog-derived production and required rts-ui.json layout | UiCommandCatalog, ui-ru.json, UiCommandCatalogTests | Shared input/caption shortcuts and resource localization; no independent gameplay prices |
| Agent memory | Local SQLite FTS5, Python stdlib | rag-system.json, run-memory.ps1, build_project_memory_index.py, rag_check.py | Hash freshness, heading chunks, bounded evidence retrieval; generated DB/JSONL ignored, vector/services disabled |
| Build/runtime | Windows x64 Mono, Built-in procedural graphics | BuildReport, Frontier.unity, player smoke | Actual rendered HUD/PNG, save/load and replay verified |
| Render profiling | Release + separate Development player, native ProfilerRecorder | Runtime/RenderingProfile.cs, docs/sc2/render-profiling.md | 200/400/800 models; CPU/GPU/geometry/focus/clock evidence; availability is explicit |
| Runtime geometry | Refcounted procedural meshes, RenderMeshInstanced marine batches, prototype-local calculation cache | Presentation/ProceduralArt.cs, InstancedMarineRenderer.cs, InstancedArtTests | Exact legacy parts/poses and independently copied animation; legacy Shared/Unique diagnostics and GPU fallback retained |
| UI graphics | Generated 24-cell PNG atlas, cached IMGUI mapping; dynamic mesh health bars | Resources/UI/command-icons.png, HudIconAtlas, HudHealthBarBatch | Icon provenance/prompt in docs/sc2/ui-icons.md; no image editing or media commit |
| Combat feedback | Observed target lookup and reusable LineRenderer shot pool | Presentation/BattleView.cs, ObservedShotPoolTests | Original beam geometry/palette/lifetime/audio; retained high-water capacity, no timed combat resource creation |
| Combat profiling | Opt-in actual GameHost 400+400 battle, exact60/240-tick fences | Diagnostics/SessionPerformanceProfile.Combat.cs | Separate population bands, real20Hz throughput, Release frame percentiles and Development GC samples |
| Navigation scratch | Per-Match reusable A* arrays/heap; exact allowed nearest-centre shortcut | Simulation/Match.Navigation.cs, NavigationWorkspaceTests | Exclusive synchronous workspace, independent saved routes, blocked/outside fallback and old row-major ties |
| Moving AI profiling | Ordinary Bot1, two400 formations, real attack-move/combat | Same combat harness with -MovingAI/-CombatTicks240 | Accepted AI commands, distinct movers, shot overlap, raw frame stages and rare-tail metrics; original supply/costs retained |
| GUI cycle diagnostics | Opt-in Windows QueryThreadCycleTime | Diagnostics/WindowsCpuProbe.cs, BattleHud.cs, SessionPerformanceProfile.Combat.cs | C21: native-wait flag only; ordinary additional read attempts zero; unavailable and partial intervals explicit; raw cycles are not CPU milliseconds. Contract: rts-moving-ai-performance.md. |
| Own-game tail diagnostics | Windows native QPC/seqlock mapping, external owned-process minidump, untimed Mono public JIT accessors | Diagnostics/WindowsGameStallProbe.cs, WindowsGameStallSymbols.cs, GameStallSymbolRequests.cs | C26 query runs only after all timed counters/volley PNG/final pause. Own-run max2/16KB requests; no remote invocation/full heap/system trace. Post-run PC mapping is not method CPU time or FPS acceptance. Focused passport: rts-moving-ai-performance.md. |

## Commands

| Purpose | Command | Evidence |
| --- | --- | --- |
| Unity connection | See [runbook](../../AGENT_RUNBOOK.md) | Recheck current CLI help and project identity |
| Product run/test/build | See runbook; tools/test-rts.ps1 | Test results and player/evidence under ignored Builds/ |

## External Services

| Service | Role | Evidence | Boundary |
| --- | --- | --- | --- |
| None configured for product | Documentation research only | [Reference register](integration-contracts/connected-projects.md) | No external service launched |

## Gaps

- Initial STATUS_NO_INSTANCES was resolved through the documented local recovery.
  Native Unity tools are callable in this chat on 2026-10-22. The official
  stdio probe also completed initialize, listed 160 tools and read this Editor's
  status/scenes. No second bridge or package replacement was needed.
- Entities/Burst, separate AI Navigation, Netcode/Transport and URP/HDRP are not
  in the current package graph. Their selection remains a planning decision.
- Full reference hardware and final network/render pipeline remain open. The
  first graphical player passed on D3D11, AMD Radeon HD 7800 Series at 1600×900;
  standalone frame measurements exist in iteration03/04. The iteration04 actual
  800-patrol session meets the 16.67ms p95 budget (11.41ms one selected,
  14.28ms all selected) at real-time 20Hz with HUD/fog/minimap. This does not
  establish full-game performance. Iteration05 separately verifies ten 400+400
  Release battles: worst trial p99 15.54ms at real-time20Hz, natural losses to760
  at tick60. Exact800 population bands and longer attrition are reported separately;
  this does not establish a full moving/bot-controlled match. Initial Windows input access denied was
  followed on 2026-10-21 by successful launch/UIA but window-capture timeout;
  hardware gameplay input remains unverified. See [testing](../../../docs/sc2/testing.md).
- Synthetic navigation measurements now include 600 and 1800 ticks for
  200/400/800 movers. All arrive by 1800; stress800 tick spikes remain. These
  are Editor CPU measurements, not a graphical 60 FPS claim.
