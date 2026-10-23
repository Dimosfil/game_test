# Standalone rendering measurement contract

Owner: RenderingProfile (Unity presentation diagnostics). CPU fixture owner:
Diagnostics/GraphicsSimulationFixture; gameplay remains Simulation/Match.
Checked source: 2026-10-21. Executed results belong in docs/sc2/iteration-03.md
and ignored Builds/Evidence/Iteration03, not in this memory file.

- Opt-in -rtsProfile is intercepted by GameHost before normal scene bootstrap.
  Normal gameplay never creates the diagnostic scene, extra camera or CPU army.
- Compare render-only separately from combined. Combined keeps rendered geometry
  stationary and fully framed while an independent authoritative army moves;
  fixed 20Hz Match.Step and detached observation consume CPU on the main thread.
  This is a workload fixture, not a full gameplay rendering/fog/input test.
- Same 200/400/800 models, animation, two team palettes, lighting, camera,
  shadows, MSAA, resolution and native counters must be used across variants.
  Asset construction, capture, JSON, cleanup, forced GC and sorting stay outside
  measured intervals. Never improve metrics by removing visible models.
- Wall-frame percentiles include waits; separate native CPU/GPU metrics must
  carry actual producing counter category/unit and availability. Missing GPU or
  allocation samples are unavailable, never measured zero. Keep raw samples.
- Sampling requires real graphical frames and a rendered overlay. Update count
  alone is insufficient: player startup or a hidden window can advance Update
  without IMGUI Repaint. Reject that run rather than report false fast frames.
- CPU fixture first group-command step is recorded separately from steady ticks.
  Report delta clamps, focus and actual tick advancement to expose slow clocks.
- Watchdog and coroutine guards must emit failed evidence and exit1. Passed
  means measurement completed, not that a frame budget or SC2 parity passed.
- Store generated evidence only below this checkout's ignored Builds directory.
  Normal player save/profile data is unrelated and must remain untouched.

Source: Assets/Game/Runtime/RenderingProfile.cs,
Assets/Game/Runtime/Diagnostics/GraphicsSimulationFixture.cs.
Reproduction and official API references: docs/sc2/render-profiling.md.

## Procedural shared mesh ownership

- ArtBuilder.Finish owns derived geometry reuse. Normal ProceduralArt.Create
  uses Shared; diagnostic CreateForProfile takes immutable per-instance
  ProceduralMeshMode.Shared/Unique, inherited by every child builder and flash.
  There is no mutable global mode or Clear operation affecting live instances.
- Cache equality compares the complete ordered source mesh/submesh/local TRS
  sequence after hashing. Owner/material is excluded from geometry identity;
  each renderer still receives its original palette material. Shared meshes
  remain immutable; per-instance animation modifies rig transforms only.
- Each renderer's ArtMeshLifetime owns one lease. Last release removes the
  cache entry before resource destruction. Use Destroy in Play Mode and
  DestroyImmediate in EditMode; never destroy a mesh while another lease lives.
  Register lifetime on an active child before parenting so inactive-parent
  creation/destruction also receives Unity lifecycle cleanup. ExecuteAlways
  and explicit Awake are required for native-resource ownership in EditMode
  PreviewScene as well; a runtime-only MonoBehaviour silently missed those
  destruction callbacks in the actual 14-case lifecycle verification.
- Zero-reference derived entries are removed immediately; only finite source
  shape/palette caches persist for the process. LiveSharedMeshCount and
  LiveSharedMeshReferences are diagnostics; compare scoped deltas when other
  world roots exist. Profile asserts cleanup returns to each case's start.
- Keep final baseline Unique and optimized Shared available in the same player
  for strict paired runs, preserving geometry counts, palettes, animation,
  shadows/camera and measuring actual native draw calls. Initial measured
  bottleneck/evidence and cache details belong in docs/sc2/render-profiling.md.

Owner sources: Assets/Game/Runtime/Presentation/ProceduralArt.cs and
Assets/Game/Runtime/RenderingProfile.cs. Independent contract tests:
Assets/Game/Tests/EditMode/RenderCacheTests.cs (execution status is reported
with the current Unity verification, not assumed from source presence).

Verified 2026-10-21: final Unity suite 104/104 includes all 14 cache cases.
Iteration03/*Verified profiles passed with 180 actual camera/GUI/focused frames
per case, equal geometry/hardware and cache cleanup. At 800, Release render-only
frame p95 Unique/Shared = 72.2643/24.7756 ms; combined = 106.6294/51.2135 ms.
Development native GC p95 = 915776 bytes/frame at 800; calibrated System.GC
fallback is unavailable, not zero. Next owners: animation/Renderer, Step/Observe
and allocations; these fixtures do not establish full-session 60 FPS.
Evidence and reproducibility: docs/sc2/iteration-03.md, render-profiling.md;
Builds/Evidence/Iteration03/verified-comparison.json and source-manifest.json.

## Compact marine renderer (Iteration04)

Normal marine Create uses CreateInstanced; other definitions and unsupported
instancing/Standard shaders retain Shared hierarchy. CreateForProfile preserves
explicit Shared/Unique hierarchy as a reference. Geometry authoring remains in
ProceduralArt: per-owner prototypes capture exact 16 material parts/13 meshes,
retain each mesh lease before discarding the temporary hierarchy, and expire
with their last compact root (including inactive roots).

InstancedArtInstance owns independent matrix poses matching legacy body bob,
leg stride, rifle recoil and flash. Enable/disable controls submission; world
position/rotation/scale remains on one entity anchor. InstancedMarineRenderer
runs after BattleView, groups exact mesh/material/layer pairs, reuses prepared
matrix buffers, and submits chunks of at most 511 with cast/receive shadows,
BlendProbes and conservative full-model bounds. Last-owner release evicts only
unused pairs before freeing meshes; surviving owners retain warmed buffers.
Last family releases the driver. BattleView deactivates removed/reset roots
before retirement to prevent stale visible instances during fog updates.
Observed compact marine roots are retained by owner for later observed reuse;
inactive roots are unregistered but retain prototype leases until View teardown.
Noncompact/fallback roots retain deferred destruction. Reset and fresh authored
pose/selection/shot/layer/owner lifetimes are specified in rts-moving-ai-performance.md.

ArtRenderInventory/AccumulateRenderInventory reports authored logical parts and
summed geometry, separately from native Renderer/Transform counts. Public
TryGetInstancedPart supports exact mesh/material/world-pose parity checks.
InstancedArtTests isolate fixtures in PreviewScenes and validate geometry,
palettes, independent poses, flash and inactive/neighbor resource lifetimes.

Profile -rtsProfileBackend legacy|instanced is independent of mesh mode;
instanced requires optimized (Shared), supportsInstancing, and actual rendering.
Compare legacy/shared with instanced using one executable. Optional
-rtsProfileSeconds 0..30 requires the minimum duration AND >=180 real samples,
preallocates measured buffers, records 16.67ms p95/over-budget count and real-time
tick/clamp evidence. Root owns SessionPerformanceProfile: real host/view/fog/
HUD/input paths with +800 synthetic initial patrols, cleared terrain and enemy
AI disabled only for that opt-in workload, not a complete combat match.
Verified bounded session acceptance: SessionFinal (Iteration04) passed two15s
phases with all800patrolling/visible at20Hz. Release wall-frame p95 is11.4067ms
(one selected) and14.2755ms (802selected); all-selection p99 is17.3361ms.
The goal is the p95 budget, not a guarantee that every frame fits16.67ms.
World health bars preserve their black/fill rectangles in one dynamic mesh draw;
opt-in host profiling exposes source-frame upload/immediate-draw wall scopes and
logical geometry/shrink/empty counts plus native capacities/reserve events. These
durations are parts of GUI wall and do not prove deferred GPU buffer work.
The current C28 CPU-margin candidate retains C17's raw-word comparison,
C16's exact submitted-geometry cache, C13's initialized native indices and
C12's geometrically grown native vertex/index capacity
from actual submitted prefixes only. Begin's observed potential-bar count reserves
managed preparation lists; it cannot force native growth. A new actual high-water
may grow buffers during timing and must remain visible in evidence. Physical mesh
vertexCount is storage; submesh indices/vertexCount/bounds remain actual geometry.
Float32 position3/color4 stride28,UInt32 indices,clipping/pixel space stay exact.
C17 represents HealthVertex with explicit layout, Size=28 and Pack=4. Position
and Color retain offsets0/12; seven integer aliases at offsets0,4,8,12,16,20,24
read their raw Float32 words. Cache comparison checks these seven integers
directly instead of invoking BitConverter for each float. This distinguishes
signed zero and quiet/signalling NaN payloads without conversion or float
comparison. The GPU vertex layout, payload bytes, materials and draw cadence
remain unchanged; it introduces no shader, lighting or quality change.
SetVertexBufferParams/SetIndexBufferParams run only on capacity growth;
SetVertexBufferData uploads the actual vertex prefix when any of its seven
Float32 channel bit patterns or actual counts change. A private copied prefix
is certified only after vertex/submesh/bounds updates complete; invalidate before
every native mutation. Exact repeats reuse native geometry and still draw every
nonempty Repaint. VertexUploadCount counts successful native vertex calls;
ExactGeometryReuseCount counts unchanged payload reuse. UploadCalls remains
preparation attempts including comparison/reuse. Empty retains the certified
prefix; dispose invalidates it. Every accepted clipped
rectangle has the same contiguous quad index pattern, so C13 retains initialized
UInt32 index data: SetIndexBufferData runs only for a new actual index high-water
or recreated index storage, before vertex data. Growth within spare capacity must
initialize its new prefix; vertex-only reserve does not invalidate existing indices.
C28 additionally retains the managed UInt32 index prefix instead of clearing and
rewriting six indices for each clipped rectangle on every Repaint. AddRectangle
prepares vertices only. ActiveIndexCount is checked(vertices.Count/4*6), independent
of retained list.Count; native capacity/upload length, empty checks, exact-reuse
counts, submesh geometry and LatestIndices diagnostics all use this actual count.
Managed index generation runs only inside a needed native index upload and extends
only a new actual prefix. A recreated native index buffer resets initializedIndices
to zero and uploads the actual prefix from retained managed data; vertex-only
growth leaves initialized indices valid. Failed native mutations still invalidate
the exact-geometry certificate and retry; successful native-call counters retain
their earlier meaning. Begin may reserve managed capacity, never native potential
geometry. Native sizing/count policy, drawn content, shader, color bits, clipping,
bounds, camera HDR, MSAA, lighting and draw cadence are unchanged. This removes
redundant managed writes; it does not attribute presentation waits or prove an FPS
tail improvement.
SetSubMesh plus mesh.bounds describe only actual clipped geometry, excluding the
retained index tail. Logical uploaded counts remain separate from initialized
index high-water and storage capacity, including shrink diagnostics.
IndexUploadCount increments after a successful native SetIndexBufferData return;
it measures neither GPU completion nor FrameEvidence.UploadCalls, which counts
geometry-upload attempts. Frame/cumulative counters preserve these distinctions.
Never lower subMeshCount or Clear on empty/shrink; empty submits no draw/upload.
Empty retains prior native storage/metadata without submitting that stale payload;
disposal releases the mesh and material. HudHealthBarBufferTests independently
checks clipped positions/colors/indices/bounds, actual grow→shrink→empty→grow,
unused potential-count independence, new-prefix initialization within spare
capacity, cached index-prefix reuse and owner cleanup.
The C28 actual-Mesh case additionally checks a cached larger prefix followed by
one surviving clipped quad, empty/fully clipped passes, regrowth within the prior
prefix, new prefix within spare native capacity and native growth. Real indices,
colors, positions, submesh ranges and bounds must match the active geometry; the
cached tail must never become submitted triangles. C28 passed272/272 EditMode
tests in40.47s, zero reported compile errors/warnings and guarded dual builds;
214source inputs and788combined control/candidate binary files were verified.
Performance status and the still-open strict frame-tail goal are maintained in
rts-moving-ai-performance.md; fewer managed writes alone are not acceptance.
The additional raw-word test constructs Color through an explicit integer union
and reads native CPU vertex storage with Mesh.AcquireReadOnlyMeshData and
GetVertexData<int>(0). With finite positions it checks all56words of eight vertices,
including signed-zero and distinct positive/negative quiet/signalling NaN color
payloads, successful upload changes and exact-repeat reuse. It does not use
float GetColors or BitConverter as the NaN oracle; each read lease is disposed
before the next native mutation.
C17 verification passed230/230 EditMode tests; dual builds completed in15.293s.
Compile evidence remains ambiguous: compile.json reports compilationFailed=true
alongside zero reported errors/warnings, and postcompile-live-status.json returns
JobNotFound. Neither proves compilationFailed=false or a clean live compile state;
the successful tests and two builds are independent evidence.
Frozen manifests contain189source inputs/8binaries. Compact evidence is under
ignored Builds/Evidence/Iteration06/Cycle17Verification, with
source-manifest-cycle17.json and binary-manifest-cycle17.json in Iteration06.
C17A ran with background contamination and is retained as non-clean evidence;
its FPS result is not accepted. At that checkpoint C17 remained provisional
pending isolated series; this historical result is not C28 acceptance evidence.
Less comparison work or fewer API calls does not establish an FPS improvement.
C11's earlier potential-count reserve retained8192/16384 elements but failed its
FPS series; it is not the active sizing contract. The normal C12A/B series passed
all declared gates across20battles/32781frames but retained two>25ms tails and
maximum32.8942ms. C13's195tests/dual builds and30clean Release battles/50545frames
passed p95/p99/20Hz/full800/near720 gates;11frames>16.67ms,0>25ms,max20.9734ms.
Health/index proof validated all50545samples with6successfulindexuploads.
Fresh native Release/Development, Patrol and standalone regressions passed.
Retained capacity or fewer index uploads alone is no FPS-tail guarantee; the
12.1227msGfxWait in C13 native Release and earlier failed runs remain recorded.
See rts-moving-ai-performance.md for contracts and docs/sc2/iteration-06.md for
measured native/candidate evidence and causality limits. Selection rings remain
native48-point LineRenderers. Earlier patrol acceptance is bounded separately in
docs/sc2/iteration-04.md; complete-match and blueprint-hover acceptance remain open.

