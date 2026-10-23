# Moving-army combat and AI performance

## C23 completed evidence; C24 diagnostic in progress

C23 reused frozen C22 source204/binary394 without changing quality or gameplay.
Ordinary A/B/C retain30 battles/52025 frames:10 >1000/60ms,7 >20ms,6 >21ms,
4 >25ms,max196.3688ms. Whole p95/p99/real-time20Hz and full800/near720 gates
pass30/30, but strict every-frame acceptance FAILS. Five B all-selection trials
have6958 unfocused frames and remain in the full report. Focus/source-eligible
25/44232 still have8 >1000/60ms,6 >20ms,5 >21ms,3 >25ms,max196.3688ms;
all non-strict gates pass25/25. Clamps/source events0, deterministic hash and
1600x900/4MSAA/Ultra/shadow100/D3D11 unchanged. GUI-cycle proofs pass with0
additional ordinary native queries; health proofs retain7096 native uploads/
44929 exact reuses. Preserve focus exclusions separately; never hide focused tails.

Worst B selection1 trial5 frame10091/tick165 is fully focused1757/1757.
GCCollect is available0ms/0calls; previous source10090 View.8209/GUI1.0263/
Prepare2.2541/Draw.9111ms are short. Health exact reuse has no reserve/index/
vertex-upload delta. MainLoop/native-wait/GUI-cycle measurements are disabled;
these records do not localize the196.3688ms loop remainder or establish CPU,
GC, GPU, scheduler or product-owner causality.
[Ordinary summary](../../../Builds/Evidence/Iteration06/Cycle23SeriesAnalysis/cycle23-ordinary-summary.json)
and [neighbour rows](../../../Builds/Evidence/Iteration06/Cycle23SeriesAnalysis/cycle23-worst-neighborhood.json)
retain raw/helper/proof provenance and all unfavorable rows.

The one newly consented C23 CPU/GPU capture is completed/stopped, not reusable
authorization. Diagnostic4/6425 frames have2 >1000/60ms,0 >20ms,max17.5083ms:
the target20–40ms tail was NOT reproduced. Actual CSwitch/Ready coverage4928;
1497 later frames are post-stop. Only five covered targets+five matched controls
were analyzed, using freshPID26580/main20700/Present-calling17084 and eight
clock anchors; six structural proofs pass. Source watcher is unavailable/events
null despite complete pre/post204/394 identity. Focus/clamps0. The two slowest
covered loops have explicit main Waiting10.3191/11.5448ms and Ready.0205/.0133ms;
this is neither a specific fence diagnosis nor permission to tune worker policy.
Native Present differs from Unity's marker; queue lifetime is not exclusive GPU
execution. Never transfer this earlier process's ETL to later ordinary196ms.
[Trace proofs](../../../Builds/Evidence/Iteration06/NativeTraceCycle23/c23-proof-fps-summary.json),
[scheduler limits](../../../Builds/Evidence/Iteration06/NativeTraceCycle23/native-analysis/scheduler-verdict.md),
[GPU limits](../../../Builds/Evidence/Iteration06/NativeTraceCycle23/native-analysis/gpu-review.md).

C24 is an in-progress opt-in watchdog/minidump diagnostic of the owned GAME
process only. No external app data, new system trace or quality reduction is
part of that scope. Do not claim a fix or passed verification before evidence.
Dump-induced suspension/overhead must be flagged separately from natural tails.
The optional minimized-UI control remains unanswered and unperformed.

## C22 callback-local screen-height cleanup — completed, no tail-fix acceptance

Baseline owner BattleHud.DrawWorldLabels read Screen.height twice before its entity loop
and once for each entity passing the resource/selection/health filter, even when
subsequently behind the camera or outside the viewport. For N such entities,
the cleanup replaces2+N getter reads with one fresh local int after the Repaint guard. It reuses
that int only within this callback for viewportBottom, healthBars.Begin and the
screen-Y conversion. Width already has one read; camera projection calls and
their order remain unchanged. No cross-frame/event cache, observation mutation,
projection cache, GUI suppression or quality/cadence change is authorized.
Unity6000.3 [SetResolution](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen.SetResolution.html)
applies requested resolution changes at frame end; this loop has no resolution
mutator, yield or user callback. Verify exact mesh Float32 positions/colors,
clipping/bounds/indices and different heights/resize between callbacks. The
source-proven reduction is N+1 getter calls per Repaint, not an established
millisecond gain or rare-tail cause. Fresh verification passed246/246 tests in
35.91 seconds with0 compile errors/warnings and successful dual builds. Frozen
manifests contain204 source inputs/394 binaries; only BattleHud differs from the
control. The first test pipeline's400 response occurred before EditMode tests
and is retained as infrastructure evidence; Retry1 passed.

Completed ABAB control:4 trials/5920 frames,11 >16.667ms,4 >20ms,0 >25ms,
max22.1452ms. Candidate:4 trials/5895 frames,6 >16.667ms,3 >20ms,0 >25ms,
max23.5397ms. Both cohorts pass whole p95/p99/20Hz4/4; combined full800/near720
bands pass2/4 control and4/4 candidate. CandidateD selection1 has one unfocused
frame (1622/1623 focused), so that trial is focus-qualified; the other seven
trials are clean and all clamps/source events are zero. Quality, actual graphics
mode, hashes and health proofs match; every ordinary run has zero new GUI cycle
reads. Retain all tails and do not treat the focus-qualified trial as clean.

WorldLabels source-frame aggregate median changes are not repeatable across
both pairs: selection1 A/B0.2625→0.2569ms and C/D0.2625→0.2545ms;
all-selection A/B0.6586→0.6537ms but C/D0.6337→0.6949ms (+9.66%). These are
deduplicated all-callback scope aggregates, not callback-level or exclusive CPU
timings; frame/population mixes differ. Retain the bounded cleanup, but do not
accept it as a rare-tail fix or expand to30 battles on this evidence.
[C22 ABAB summary](../../../Builds/Evidence/Iteration06/Cycle22SeriesAnalysis/cycle22-abab-summary.json)
retains all four sources, individual comparisons and unfavorable results.

## C21 scoped main-thread cycle diagnostics — completed, not a tail fix

No causal GUI fix is established after C18/C19/C20. Owners: WindowsCpuProbe
provides fault-contained current-thread cycle reads; BattleHud pairs raw cycle
boundaries for total GUI, WorldLabels and Bottom; SessionPerformanceProfile
publishes aligned value evidence. Enable only with the native-wait diagnostic
flag. Ordinary play must make zero additional native calls. Preserve every GUI
event/control/draw, projection, published DTO and quality setting. Aggregate
same-frame callback attempts explicitly, retain unavailable/partial records and
sticky fault validity, and never convert cycles to milliseconds or physical CPU
occupancy. Nested Bottom/WorldLabels cycles are not additive exclusive work.
Fresh verification passed246/246 tests in38.1 seconds with0 compile errors and
warnings; frozen manifests contain204 source inputs and394 binary files. Both
native diagnostic battles passed GUI-cycle proof and all health/exact/mainloop/
native structural proofs:2472 samples,29664 thread-only read attempts. Validity,
partial sums and exact callback/source IDs remain explicit. Several large wall
scopes lack proportional cycle increases; another GC-bearing case has both.
This distinguishes observed scope behavior, not CPU milliseconds, scheduler
state or a proven OS/driver cause. Diagnostic observer overhead remains present.

The separate ordinary series has10 battles/14691 frames:21 >16.667ms,7 >20ms,
0 >25ms, max23.0099ms. Whole p95/p99/20Hz pass10/10, combined full800/near720
bands7/10; focus/clamps/source observer events are zero. Ordinary mode adds zero
new native cycle reads, so these frames are not pooled with instrumented battles.
Strict acceptance still fails. Retained compact evidence:
[ordinary summary](../../../Builds/Evidence/Iteration06/Cycle21SeriesAnalysis/cycle21-ordinary-summary.json),
[cycle-owner summary](../../../Builds/Evidence/Iteration06/Cycle21SeriesAnalysis/cycle21-cycle-owner-summary.json).

## C20 Graphics Jobs-off experiment — completed, rejected

Separate build configuration, restored through try/finally and retained final
restoration artifacts, compared default A/C `kGfxThreadingModeClientWorkerJobs`
against B/D `kGfxThreadingModeThreaded`; actual modes were verified in startup
logs. No `-force-gfx-direct` was added. Quality/hardware and game hashes matched,
20Hz passed all eight trials, clamps and source observer events were zero.
Default: 4 trials/5788 frames, 4 >16.667ms, 2 >20ms, 0 >25ms, max22.4065ms;
whole p99 and full800/near720 bands passed4/4. Jobs-off: 4 trials/5332 frames,
30 >16.667ms, 8 >20ms, 6 >25ms, max31.5107ms; p99 passed3/4 and bands2/4.
JobsOffD selection1 includes one unfocused frame and is focus-qualified, not
clean acceptance. Retain it rather than removing unfavorable data. The fully
focused A/B pair also has worse jobs-off medians for both selections; both
jobs-off processes have higher medians. Reject expansion to30 battles/default
adoption. This bounded comparison does not prove a causal platform defect.
[C20 ABAB summary](../../../Builds/Evidence/Iteration06/Cycle20SeriesAnalysis/cycle20-abab-summary.json)
retains source hashes, actual modes, observer evidence and all adverse tails.

## C19 direct graphics-thread experiment — completed, rejected

Bounded ABAB used the same frozen C18 product: 202 source inputs and 394 verified
binary files. Default A/C startup logs confirm `kGfxThreadingModeClientWorkerJobs`;
B/D confirm `kGfxThreadingModeDirect` with only `-force-gfx-direct` added. Unity
6000.3 documents this argument as direct rendering without multithreading in the
[Player command-line reference](https://docs.unity3d.com/6000.3/Documentation/Manual/PlayerCommandLineArguments.html).
No product or quality default was changed. All runs retained 1600x900, 4xMSAA,
shadow distance 100, Ultra, Direct3D11 and Radeon HD7800. All eight trials kept
20Hz, the same game hash, full focus and zero clamps; source observer events
were zero and the post-run binary manifests matched.

Default: 4 trials/6492 frames, 9 >16.667ms, 3 >20ms, 0 >25ms, max 23.0686ms;
whole-trial p99 passed 4/4 and combined full800/near720 bands 2/4. Direct: 4 trials/
4945 frames, 29 >16.667ms, 7 >20ms, 2 >25ms, max 35.4769ms; whole-trial p99
passed 3/4 and bands 2/4. Both modes fail the every-frame goal. Direct whole-frame
medians are worse for both selections in both A/B and C/D pairs, despite lower
Prepare/Draw/GUI scopes. Moving work between paths does not make those scopes
exclusive or prove a reduced whole-frame cost. The bounded comparison is not
statistical causal proof, but provides no useful lead for 30-battle expansion.
Reject direct mode for expansion and default adoption; retain all adverse tails.

Evidence: [C19 ABAB summary](../../../Builds/Evidence/Iteration06/Cycle19SeriesAnalysis/cycle19-abab-summary.json).
C20's separate completed Graphics Jobs-off experiment is rejected above. C18
source and the strict FPS goal remain unchanged.

## C18 GUI selection membership cache — verified mechanism, strict FPS failed

Mechanism owner: `Assets/Game/Runtime/Presentation/BattleHud.cs`,
`CaptureGuiSelection`. The baseline rebuilds its private selected-ID HashSet and
rescans every observed entity for each GUI callback, including repeated callbacks
over the same detached publication and unchanged selection. The candidate may
retain only this derived membership/list while the observation reference and
exact ordered selection IDs remain identical. On every callback compare reference,
count and every copied ID; neither tick/frame equality, list identity nor a hash
alone is a valid key. Keep selected entities in observation order, preserving
duplicate/missing-ID behavior. The read-only completed-publication contract in
`rts-host-frames.md` remains required; published DTOs are not pooled or refilled.

Invalidate the private key before any rebuild mutation. Commit its copied IDs,
observation reference and valid flag only after membership and entity capture
succeed. Initialize invalidates the key. A new publication, even with the same
tick/IDs, must refresh entity references; same-frame equal-count ID replacement,
reorder, death/withdrawal, empty selection and reset must be reflected by the next
callback. Preserve every GUI event, control/draw order, projection, health work,
pixels, animation and quality setting. No GUI suppression, projection cache,
mutable published-frame reuse or DTO pool belongs to this candidate.

The targeted runtime diff against
`Builds/Scratch/C18SelectionCacheExperiment/BattleHud.baseline.cs` contains only
private key fields, Initialize invalidation and CaptureGuiSelection. Independent
tests exercise actual captured entity references/order and world-label
membership, same-callback-frame ID edits, new publications/death/restore/reset,
empty/missing/duplicate IDs and retry after incomplete rebuild. Existing host
ownership/deep-detachment tests remain required. Seven component cases have been
promoted into actual EditMode `BattleHudSelectionCacheTests`; source review passed.
Fresh verification passed 238/238 tests in 35.14 seconds, with 0 compile errors,
0 warnings and compilationFailed=false. The exact 202-source-input snapshot
matched before and after tests and both builds. Dual builds succeeded in
12.423 seconds under `Builds/Players/WindowsCycle18` and
`Builds/Players/WindowsProfileCycle18`, with a complete 394-file binary manifest.
The control/candidate source comparison proves only BattleHud changed, with the
test file and its .meta added:
[source changes](../../../Builds/Evidence/Iteration06/Cycle18Verification/control-candidate-source-changes.json).
These checks verify the implementation contract, not an FPS gain.

The verified current-source baseline has 200 source inputs; the ordinary Release
control is frozen separately under `Builds/Players/WindowsCycle18Control` with
its 146-file manifest. Compare candidate/control ordinary moving-AI battles under
the same full-source/quality/selection workload, alternating launch order and
retaining all tails, focus/clamps, hashes, bands and throughput. Diagnostic section
timings may verify reduced repeated capture work but are not ordinary FPS
acceptance or proof of a CPU/GPU/OS stall cause. The completed ordinary candidate
series has30 battles/50384 frames:68 >16.667ms,22 >20ms,10 >25ms, max37.8034ms,
full focus/zero clamps, matching final hash, p95/20Hz30/30, p99 29/30 and combined
full800/near720 bands27/30. Strict acceptance failed. Keep the paired cohorts
separate: control4/6838 has9 >16.667ms and6 >25ms/max34.9216ms; candidate4/6954
has4 >16.667ms and0 >20ms/max19.7982ms. Both paired cohorts pass p95/p99/20Hz
and bands4/4 but fail the every-frame goal; these few processes are not causal
FPS proof.

The mechanism has a measured scope gain. Paired selection-capture mean per GUI
callback changes0.06439→0.00779ms selection1 and0.09355→0.01565ms all selected.
Deduplicated source-frame all-callback aggregate medians change0.1233→0.0027ms
and0.1757→0.0105ms respectively; these are not callback-level medians. Ordinary
candidate aggregate medians are0.0025/0.0102ms. Observations remain detached and
selection is checked exactly every callback. [C18 series summary](../../../Builds/Evidence/Iteration06/Cycle18SeriesAnalysis/cycle18-summary.json)
retains all seven source runs, all tails and unavailable/partial rules. Reduced
repeated capture work does not establish a fix for the remaining long frames.

### C18 separate native-wait diagnostic — completed, performance failed

`Builds/Evidence/Iteration06/NativeWaitReleaseCycle18/session-profile.json`
completed on 2026-10-22 at 10:09:47 UTC. Keep this diagnostic separate from the
ordinary C18 30-battle/50384-frame series (68 frames >16.667ms, max37.8034ms)
and its selection-capture mechanism measurements. Launcher Status=passed means
completion, not FPS acceptance. The diagnostic contains1715 frames:974 selection1
and741 all-selection. There are127 frames >25ms (52/75), four clamps (2/2),
no unfocused frames, and matching final hashes. Both trials fail20Hz and whole-trial
p95/p99. Selection1 p95/p99/max are26.1398/76.0374/11549.3511ms;
all-selection29.8995/58.9333/1628.1213ms. These pauses remain unfavorable evidence.

Mainloop, native-wait, health-bars and exact-health proofs all passed against the
same source SHA256 `e2efa3495a3da363565b3f8f4288b75f393709312919b3d71bc0f7d71db434d9`.
They validate structure, source alignment and counters, not the FPS gates.
Selection1 capture f724/t111 (GUI/submit/view source f723) has11549.3511ms wall:
previous sample-to-EOF10216.0821ms, EOF-to-EarlyUpdate1333.0735ms, then0.1957ms.
Its previous-completed all-thread Gfx.WaitForPresent marker is10205.8192ms,
main-thread cycle delta34966722, GC.Collect0ms and GUI2.1138ms. All-selection
capture f1737/t115 (GUI/submit/view f1736) instead has1628.1213ms wall with
12.8842/1610.7169/4.5212ms boundary segments, Gfx.WaitForPresent0.0041ms,
main-thread cycles109038882 and GC.Collect0ms. Different elapsed-wall owners
are visible; they do not establish the same engine/driver/scheduler cause.

Native allocation-byte recorder is unavailable in both trials: reported byte
zeros do not mean zero allocation or provide bytes/tick. GC.Collect is available;
incremental GC marker is unavailable. Collection counters0/1/2 are34/34/34 and
29/29/29; maximum GC markers4.1263ms at f1024 and7.5323ms at f1850 occur on
different frames from the worst pauses. Exact-health counters show334 actual
vertex uploads and1381 reuses (80.5248% API calls avoided), not a causal FPS gain.
Process PID12500 metadata records affinityFFF, Normal/base8; this is not thread
affinity or proof of scheduling eligibility. Cycles cannot be converted to wall
milliseconds or physical CPU occupancy here. This analysis uses no C18 ETL;
C16 scheduler/competing-process conclusions must not be transferred to C18.

Compact retained verdict and raw-source-index summary:
[native verdict](../../../Builds/Evidence/Iteration06/NativeWaitReleaseCycle18/native-owner-verdict.md),
[native owner summary](../../../Builds/Evidence/Iteration06/NativeWaitReleaseCycle18/native-owner-summary.json).
All127 frames >25ms, top10 per trial, GC maxima and raw MainLoop timestamps/cycles
remain available. C18 strict FPS acceptance remains unproven.

## C17 exact comparison representation candidate

C16 avoids85.6% of vertex API calls but increases all-selection health preparation
median from0.0229ms to0.1911ms in the bounded C15/C16 alternating comparison.
Owner HudHealthBarBatch compares the committed private28byte vertex payload.
A bounded Editor Mono source-equivalent experiment measures6400vertices:
BitConverter median0.1467ms versus explicit integer overlays0.0506ms, not player
FPS evidence. C17 overlays seven integers onto the same Position3/Color4 Float32
bytes (explicit28byte layout, Pack4), comparing raw words without float-return
conversions. Preserve every cache commit/invalidation, counter, native prefix,
bounds, draw, animation and quality setting. Existing real Mesh oracles plus raw
native mesh-data words must verify signed-zero and distinct NaN color payloads;
position/bounds remain finite. This also avoids signaling-NaN quieting observed
in14/1008 BitConverter raw-input oracle cases under Editor Mono. No GPU/OS wait
cause or strict FPS success is implied. C17 passed230 EditMode tests and dual
builds in15.293s; frozen manifests contain189 inputs/8 key binaries. The initial
C17A series remains separate because six concurrent Runtime write events were
observed. After coordinating a pause, B/C/D completed30 battles/50126 frames:
42 >16.667ms,9 >20ms,1 >25ms, max32.3311ms. Focus/clamps were0 throughout and
whole-trial p95/p99/20Hz passed30/30, but combined full800/near720 bands passed
only28/30; retain both unfavorable B trials. Exact mesh/counter proofs passed
all50126 samples, with7069 native uploads and43057 exact reuses. Pooled health
preparation medians are0.0127ms selection1 and0.0525ms all selected. These are
mechanism measurements, not a causal FPS gain versus the older UI binaries.
Strict acceptance failed;20–21ms frames remain. Editor used about1.2 CPU cores
over these windows despite no observed Runtime/Resources writes; aggregate CPU
context does not attribute individual tails. Compact evidence is ignored
`Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-bcd-summary.json`.
## C16 exact submitted health-geometry candidate (provisional, not accepted)

Scenario: DrawWorldLabels projects current observation positions directly, rebuilding
health geometry every Repaint; observations publish at20Hz. HudHealthBarBatch owns
native mesh contents. Compare all seven Float32 channel bit patterns of the actual
clipped prefix and actual index count with a private copy of the last completely
committed mesh payload. Reuse only identical final geometry, retaining every
nonempty Repaint draw, original colors, pixels, bounds and quality. Camera,
viewport, health and ordering changes are covered by actual output comparison.
Invalidate before any native mesh mutation and commit the copied cache only after
vertex data, submesh and bounds succeed. Empty passes retain native/cache state.
Native capacities still follow actual high-water, never potential observed count.
VertexUploadCount counts successful SetVertexBufferData calls; ExactGeometryReuseCount
counts exact payload reuse. Existing UploadCalls remains preparation attempts,
including reuse, and its wall scope includes comparison. No FPS improvement is
claimed until fresh ordinary long series and retained outliers verify the candidate.
Independent real Mesh readback tests cover exact repeats, tiny changes, clipping,
empty/reappearance, prefix growth, bounds and storage growth. Strict frame goal
remains open.

C16 compile reported 0 errors/warnings; 197 tests passed in 38.74s and dual
builds succeeded in 14.826s. Frozen manifests contain 179 inputs/8 binaries.
Three ordinary Release processes completed 30 trials/49315 frames with full
focus and 0 clamps: 52 frames >16.667ms, 8 >25ms, max 34.8935ms. Strict
acceptance failed. C16A selection1 trial1 additionally failed full800 p99;
C16C all trial5 failed whole-trial p99. Retain all unfavorable results.
Successful vertex calls were 2367+2367+2364=7098; exact reuse counts were
14088+14186+13943=42217. The approximately 85.6% reduction in repeated vertex
API calls proves the mechanism operated, not an FPS improvement or causal tail
fix. Quality and every nonempty Repaint draw remain unchanged. The separate C16
Retry1 native capture and offline analysis are complete as documented below;
they do not establish C16 acceptance or a causal FPS gain against C15. Ordinary
series evidence: ignored `Builds/Evidence/Iteration06/cycle16-release-series.json`.

### C16 Retry1 completed native analysis

The separately consented system CPU/GPU capture succeeded in the same approved
scope after the first failed save. Retry1 requested stop after 45.08465 seconds
while the game remained alive; WPR start/stop and owned game exit returned 0.
The retained ETL is 1,151,336,448 bytes under ignored
`Builds/Evidence/Iteration06/NativeTraceCycle16/Retry1/rts-cpu-gpu.etl`.
It contains 13,493,012 events, documented eventsLost 0/buffersLost 0, QPC clockType 1
and 10 MHz matching the same-player native clock anchors. DxgKrnl is present with
1,108,686 events, unlike the earlier C10 capture. This is an instrumented diagnostic
run, not ordinary FPS acceptance. Of 5338 profile frames, 4004 are fully inside the
trace; 1334 outside frames are excluded from native attribution. Twenty-four
selected slow/control frames were analyzed; main 41192 and DXGI-calling 39304
scheduler transitions have 0 coherence anomalies. Direct sandwich-anchor bounds
replace the older scheduler epoch fit; outward-rounded conservative intervals
span 4–5 QPC ticks, without claiming physical sub-tick clock precision.

Worst captured frame 2781/tick 153 is 99.9014 ms, with renderer Draw 85.0834 ms wall.
Its largest main-thread off-CPU interval is 84.1019 ms with explicit CSwitch
OldThreadState 1 (Ready), reason 30 (WrQuantumEnd), not Waiting. The whole pre-EOF
interval contains 6.6747 ms on-CPU, 87.165 ms off-CPU entered Ready, 0.1713 ms blocked
until explicit ReadyThread and 5.0405 ms ready after that event. The graphics
calling thread concurrently has 91.0358 ms explicit-Ready-bounded blocking.
Recorded game device/context ownership follows payload
Device.hProcessId→Device.hDevice→Context.hDevice/hContext lifetimes. Game queue
packets 8711–8713 complete in about 4 ms near the beginning of the 84.1019 ms interval;
subsequent recorded game submissions appear after main resumes. Queue/DMA
Start→Stop wall intervals are not exclusive GPU execution, and this sequence
does not establish a renderer/health-buffer/GPU ultimate cause. No exact
switch-out stack was found for the largest interval; nearby module samples
remain explicitly contextual.

The authorized extra read-only pass retained 39166 CSwitch records in the existing
windows and 6319 TID/PID mappings, with 0 core-continuity anomalies. Main migrated
from CPU9 to CPU8. Both cores have full 84.1019 ms coverage and 0 gaps: entry CPU9
ran game 0.9126 ms, owned Editor 2.2031 ms, WPR process 0 ms, other 80.7467 ms,
idle 0.0064 ms and unknown 0.2331 ms; resume CPU8 ran game 0.5499 ms, owned
Editor 4.0499 ms, WPR process 0 ms and other 79.5021 ms. This occupancy is context,
not causal attribution: affinity/eligible migration CPUs are unknown, and
unidentified kernel ETW work can be in other rather than WPR process. Do not
publish other processes' names or add all-core occupancy as main-thread delay.

Compact evidence and input/helper/output hashes are retained under Retry1's
`native-analysis/`: `game-specific-summary.json`, `cpu-occupancy-summary.json`,
`captured-frame-analysis.json`, `anchors/anchor-bounds.json` and
`analysis-hashes.json`. This completes C16 native investigation for this capture;
the strict frame goal and C17 candidate acceptance remain open.

### Own Editor activity and completed Editor-closed control

A fresh isolated five-second observation found own Editor PID 18668/TID 18248
using 5000 CPU ms despite batchmode, a clean Frontier scene, no play/compile/update
and Profiler recording disabled. The existing authorized C16 Retry1 ETL confirms
that TID's ownership and contains 38384 CPU samples over 45.895 seconds. A bounded
lookup resolved 20 selected addresses from matching local PDBs only. Sampled
stacks include MainMessageLoop/UnityMain, application/GUI polling, shader-variant
updates, memory statistics and profiler/counter bookkeeping. Engine profiler
symbols do not prove Profiler recording was enabled. No single managed callback,
plugin or exclusive cause of the busy Editor or game stalls is established.
Evidence: [own-Editor verdict](../../../Builds/Evidence/Iteration06/NativeTraceCycle16/Retry1/native-analysis/EditorIdle18668/editor-idle-verdict.json).

The Editor subsequently exited cleanly after preserving
`Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/editor-pre-shutdown-checkpoint.json`.
C17E/F/G completed 30 battles/41391 frames with the Editor closed:
474 frames >16.667 ms, 144 >20 ms, 48 >25 ms and maximum 96.3815 ms. Focus/clamps were 0,
the gameplay hash stayed identical and exact health/counter validation passed,
but strict acceptance failed. Whole-trial p95/p99 passed 29/30 and 16/30;
20 Hz passed 30/30 and combined full800/near720 bands passed 20/30. Editor CPU
observations are null because the process was absent, not numeric zero samples.
The worse tails after closing the Editor do not establish its exclusive cause
or a causal C17 regression. Evidence:
`Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-efg-editor-closed-summary.json`.

The short Editor-closed native-marker run is separate: 2 trials/2808 frames,
28 >16.667 ms, 10 >20 ms, 4 >25 ms and maximum 39.9139 ms. Do not pool these
diagnostic samples into the ordinary E/F/G series. Evidence:
`Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-editor-closed-native-summary.json`.

Recorded PID occupancy in the earlier C16 84.1019 ms Ready interval identifies
two dominant contexts from the same ETL's process metadata and matching
ProcessId/UniqueProcessKey lifetime brackets: desktop host ChatGPT.exe/PID12348
occupied CPU9/CPU8 for 68.9518/45.9950 ms; node.exe/PID40936 occupied them for
9.5197/23.1909 ms. The JavaScript process's task/owner remains unproven. Both
cores have full interval coverage, 0 gaps and 0 switch-continuity anomalies.
These are separate core occupancies, not additive main-thread delay or an
exclusive cause: affinity, eligible migrations and priorities remain unknown.
No live PID lookup, command lines, process memory or application files were read.
Evidence: [PID occupancy verdict](../../../Builds/Evidence/Iteration06/NativeTraceCycle16/Retry1/native-analysis/PidOccupancyA/pid-occupancy-verdict.json).

The Editor was subsequently restored cleanly as PID23464. A fresh isolated idle
thread observation again used 5000 CPU ms over five seconds, so restart did not
remove the busy idle behavior. The optional minimized-UI control remains pending;
no new system trace was recorded. No priority, graphics quality or system setting
was changed, and C17 acceptance remains failed/open.

## C15 diagnostic owner selection

C14 exact dirty fog comparison passed200tests but avoided zero uploads in the
actual workload and failed the new strict frame gate. Its View change was
restored to frozen C13; the five experiment tests are archived under ignored
Cycle14ExperimentTests, not active product tests. A pure240tick oracle preserves
the exact final gameplay hash and proves both fog and final ordered minimap
RGBA change on every tick; minimap dirty skipping is not an FPS fix here.
The contemporaneous isolated C13 player matched all four recorded binaries and
also produced37.5949ms with full focus, so C14 is not established as the exclusive
cause of long frames. All unfavorable and unfocused C14 trials remain retained.

C15 adds only profiling evidence to BattleHud: per-source-frame aggregates of
Header,SelectionCapture,WorldLabels,Bottom and nested Minimap/SelectionPanel/
Commands scopes. Preserve GUI calls, controls, pixels and existing signatures;
no font prewarming, cache/backend switch, cadence or graphics reduction. Normal
nonprofiling calls issue no extra clock reads or allocations. Availability requires
Recorded/Completed and exact GuiFrame/current-or-previous capture alignment.
Bottom contains its three child scopes, WorldLabels contains health work, and all
sections are parts of existing GUI wall time; never add overlapping durations.
Preserve partial/exception evidence. Clock anchors use raw WindowsQPC sandwiched
by MonoStopwatch at diagnostic boundaries only, with PID/TID/frequencies and
explicit unavailable sentinels. Equal frequencies do not imply a shared epoch.
These probes must select a measured owner before another performance change.

C15 diagnostic Release completed10trials/16334frames:24frames exceed16.667ms,
3exceed25ms, max27.5312ms. The new every-measured-frame limit remains failed;
earlier p99/20Hz acceptance does not close it. HUD/fog/minimap,1600x900,4xMSAA,
Ultra and shadow distance100 remain unchanged. This is diagnostic evidence,
not an accepted gameplay/render optimization or a scheduler/driver cause claim.
Offline frame joining of the game's Unity binary Profiler C15 is now validated
on normal and holdout samples: raw frameIndex = profile capture Frame - 1.
This offset is separate from existing source Frame-1 callback semantics. There
are 2000 loaded frames, 50/55 slow hierarchies and 0 measured truncations;
this does not establish final CPU/GPU causality. Evidence:
`Builds/Evidence/Iteration06/UnityCpuCycle15/profiler-marker-join.json`.
This C15 record did not authorize another system capture. The separately consented
C16 Retry1 capture and its completed analysis are documented above.

NativeClock proof passed all20start/end anchors across10trials: PID34228,
thread39764, both raw frequencies10000000. Frame/sample enclosure and ordering
passed; bracket widths0–91.1microseconds. Exact BigInt intersection is the
degenerate raw-tick interval50622629164200000000/10000000 Stopwatch ticks for
both bounds. No non-overlap was observed; zero width in quantized ticks does not
claim physical sub-tick precision. ETW header/clock matching remains unverified:
the proof read only player JSON and does not transfer a process-specific offset
to another run. Evidence under ignored `Builds/Evidence/Iteration06/`:
`NativeWaitReleaseCycle15/tail-summary.json` and
`NativeWaitReleaseCycle15/native-clock-proof.json`.
Source profile SHA256:
`6af5d3a8792e4bb50bdb9212789060b65dd28202107910537c477e000c9208d8`.

## C14 exact unchanged-fog upload candidate (rejected experiment)

The following preserves the experiment's original mechanism contract; the
candidate was rejected and restored to C13 as recorded above.
The continued user target includes every measured20–21ms frame, not only p99.
Scenario: each newly published observation recalculates fog RGBA, then BattleView
unconditionally calls Texture2D.SetPixels32/Apply even when every final byte is
unchanged. BattleView owns the pixel cache and upload decision. C14 may skip only
byte-identical native fog uploads, retaining the exact visible/explored/unseen
pixels, first upload, current observation cadence and same material/texture/filter.
Reset/new-match transitions must compare against actual retained texture contents.
No minimap, quality, visibility, simulation or render cadence reduction is allowed.
Successful upload and unchanged-tick counters plus last upload frame/tick expose
the mechanism separately from frame timing. Independent pixel/texture tests and
fresh long ordinary/native series must verify this candidate; redundant work is
source-proven, its contribution to native waiting is not yet established.

## C13 retained index prefix and bounded acceptance

Scenario: health rectangle repaint repeatedly submits the same contiguous quad
index prefix while positions/colors and actual quad count change. Owner:
HudHealthBarBatch. Each clipped rectangle contributes either one complete quad
or no geometry; its topology is independent of bar identity, position and color.
C13 retains initialized native index data and uploads only when a new actual
index high-water is reached or index storage is recreated. Actual submesh count,
vertex prefix, Float32 colors, clipping and bounds remain C12. Empty passes do
not upload or draw. IndexUploadCount increases only after SetIndexBufferData
returns successfully, not on attempted geometry uploads or GPU completion. It is
distinct from BufferReserveCount and FrameEvidence.UploadCalls: UploadCalls counts
nonempty geometry-upload attempts even when native index data is reused or a later
stage fails. Preserve raw frame/cumulative counters rather than treating these
different measures as interchangeable.

Growing within spare capacity must still initialize every newly referenced index.
UploadGeometry ensures capacity, conditionally uploads the full current index
prefix before vertex data, then updates actual submesh/bounds. initializedIndices
retains the largest successfully initialized prefix and resets only when index
storage is recreated; vertex-buffer growth alone does not invalidate it. Shrinking
or regrowing within that prefix changes only actual submitted geometry, never
draws the retained tail, and requires no new index upload. Logical uploadedIndices
still records current submitted count, not the initialized high-water.

HudHealthBarBufferTests independently verifies new-prefix growth within unchanged
capacity, a clipped single quad, cached-prefix shrink/regrow, empty passes and
actual buffer recreation against Mesh geometry/colors/indices/bounds. C13 compile
reported0errors/warnings,195/195tests passed in36.01s and dual builds succeeded
in12.375s;179inputs/8binaries are frozen and verified unchanged.
Three ordinary Release processes passed all30clean trials/50545frames, including
p95/p99/20Hz, movement/AI, full800/near720 bands and focus/clamp gates. Whole-trial
p99 ranges are9.3438–12.4454ms(selection1) and10.8571–14.0427ms(all); full800
band p99 maxima16.2196/14.6846ms. Eleven frames exceed16.67ms, none exceed25ms;
maximum20.9734ms and longest burst3 remain explicit residuals. All50545health/index
samples validated. Six successful index uploads replace repeating50545indexAPI
calls under the earlier policy; this is not physical upload-byte or FPS causality
proof. Fresh native Release/Development3423/3239samples passed all validators,
max20.9221/17.6425ms; a12.1227msGfxWait remains at the Release tick97 transition.
Dev allocation325925.01/320847.21bytes/completedtick remains nonzero. Patrol,
standalone research/save/load/replay623 and manual HUD/volley checks passed.
Evidence and hashes: ignoredBuilds/Evidence/Iteration06/cycle13-release-series.json,
NativeWaitReleaseCycle13,NativeWaitDevelopmentCycle13,regression-summary-cycle13.json.
These results accept the bounded implementation, not an every-frame16.67ms or
ultimate GPU/driver root-cause claim. No isolated C10-versus-C13 timing AB was run.
Neither less API traffic nor retained buffers alone establishes an FPS-tail fix.

Owners: Match for movement/navigation/combat/bot decisions; its exclusive worker
for scheduling; BattleView/InstancedMarineRenderer/HUD for observed presentation;
SessionPerformanceProfile.Combat for opt-in evidence. This extends
[combat05](rts-combat-performance.md), never substitutes its AI-off measurements.

## Workload and observable contracts

Use the private startup catalog copy, original nonmarine spawns, original costs,
HP/damage/speed/sight/supply, ordinary enabled player1 bot and seed42. Replace
marine spawns with two400 formations:40 columns,10 rows per owner, .8m spacing,
3.2m front gap. All800 start observed in the unobscured1600x900 viewport.
Only player0 receives one scripted tick0 AttackMove through the actual host;
player1 commands must originate from ordinary bot decisions. Clear terrain only
in this opt-in stress fixture; this is not a complete map/campaign balance test.

Warm graphics while paused, then run exactly240 authoritative ticks at20Hz.
Natural damage/death/fog remain active; initial800 is not800 permanent survivors.
The free stress army exceeds original supply cap, so production may correctly
reject. Accepted bot Build and AttackMove prove decisions, not successful Train.
End evidence retains the accepted player1 command log and hash.

Require at least100 distinct initial/actual marine movers per owner, cumulative
travel above100,000mm each,12s interval/at least10 real bot thinks, accepted Build,
and at least100 distinct bot-commanded AttackMove IDs. Require at least10
publications with increased shot totals and20+ last-step movers per owner.
This proves overlapping movement and fighting across armies; it does not mean
all800 individually move and fire simultaneously. Repeated AttackMoveUnits is
the summed command payload, not distinct IDs. Travel is cumulative accepted path
length, not net displacement. TickMoving describes the last step of a batch.

DetailedDiagnosticsEnabled defaults false and controls all movement/bot counters.
Its derived sets/totals are private per Match, excluded from state/hash/save,
and reset on New/Restore. Enable/preparation belongs outside timing. Ordinary
product fog/UI never receives these hidden authoritative diagnostics.

## Tail diagnosis and acceptance

Retain every wall-frame sample, p95/p99/p99.9/max, counts over16.667/25/33.333/50ms
and longest consecutive budget breach. Do not discard outliers. Annotate frame
indices, published tick/population/observed/visible counts, worker/advance time,
view/GUI/submission stages, native CPU/GPU/GC, collection counters, visual creation
and matrix growth. Stage callbacks and native recorders refer to their explicit
completed frame indices; correlation is evidence, not exclusive CPU attribution.
The optional MainLoopBoundaryProbe records previous resume -> previous
WaitForEndOfFrame -> current early Update -> current resume. Retain raw frame IDs,
Stopwatch ticks/frequency, thread cycles and independent validity flags. Only
aligned, monotonic samples yield segment durations; unavailable segments use -1.
Their wall sums telescope to the raw timestamp interval. EOF is a coroutine
boundary, never a proven Present endpoint. Capture only for explicitly requested
moving-AI diagnostics, reuse the yield object and prewarm evidence arrays; stop
the probe synchronously on profile disable/completion. Native samples in C7 move
before coverage, so their boundaries differ from C6; normal gameplay is unchanged.
An additional explicit native-wait flag requires the boundary probe. Discover the
five exact configured wait/Present names in the current player before warmup;
Editor discovery alone proves no player availability. Retain each recorder's
valid/sample flags, raw nanoseconds and invocation counts (-1 when unavailable),
the read frame and previous-completed-frame semantics. Default all-thread
aggregation may overlap/nest and exceed wall time; never sum these values or
identify a main-thread stall from one marker. Dispose recorders on disable.
Separate sustained20Hz throughput from individual batch compute time and render
percentiles. Check actual camera/GUI work, focus and clamp counts independently.

Population bands require authoritative population equal to observed and visible
centres: exact800 or>=720 after first shot. Lower/hidden populations remain in the
whole-battle trace and never support an800-visible-combat claim. No quality or
geometry reduction, invulnerability, suppressed bot decisions or padded Results
frames may manufacture a pass. Screenshots/serialization/sorting stay untimed.

## Mechanism constraints

Navigation acceleration must preserve row-major closest-goal ties, heap neighbor
and score/node ordering, radius/static invalidation, saved route samples and
same-tick movement. A private synchronous A* workspace can be reused under
exclusive Match ownership; every search resets it, and returned path lists remain
independent. Never change moving-target replan policy to claim exact equivalence.

Render capacity is prepared from actually registered observed authored parts,
including inactive muzzle flashes. Retain capacity, preserve all poses/materials/
shadows and every eligible draw. Hidden authoritative counts must not precreate
revealed actors. Registration removal must keep surviving anchors/layer bindings
valid and release all prototype leases exactly once.

Local marine animation calculations may be cached per prototype for the same
exact time/movement/attack state. Copy resulting matrix values immediately into
each instance's existing pose arrays; later cache writes, other instances and
backward/different time calls must never change an earlier instance. Preserve
the authored pose before first animation and the existing start/stop assignment
guards. Never share a mutable instance pose or reduce animation cadence.
Inactive flash world composition may be omitted only in actual submission that
does not read/draw that part; public geometry queries still compute all parts.
Affine preparation preserves the root/body/tool/flash/part composition graph
and arithmetic order. Use the reduced affine operator only when the root has
an exact affine bottom row and coefficients bounded by1e20, and prototype/local
poses are bounded by1024; otherwise retain Unity's full matrix operator.
This bound keeps the four-composition chain finite. Bounds use the same maximum
root-column length with one square root, retaining conservative full-model
shadow-caster coverage. MarinePreparationTests exercises actual preparation,
eligible batch matrices/materials/layers, bounds and legacy vertex containment;
InstancedArtTests covers the public inactive-part query and fallback geometry.
Diagnostic preparation and Graphics submission times are separate wall stages,
not exclusive CPU markers or proof that any particular long frame was GC.

Current C13 HUD health geometry uses C12 retained native storage sized from actual
submitted high-water counts; Begin's potential count reserves managed lists only.
Float32 position3/color4 vertices upload the current prefix. The invariant UInt32
quad index prefix uploads only on a new initialized high-water or index-buffer
recreation, before vertex data. Actual submesh counts/bounds and mesh.bounds define
the submitted geometry; initialized native index tails never define draw counts.
Shrink never changes native buffer layouts or submesh count. Keep logical uploaded
counts separate from mesh.vertexCount storage capacity in diagnostics. Empty
selection issues no upload/draw, retaining prior native metadata without drawing
it. Verify exact clipped geometry/colors/topology through actual grow→shrink→empty
→grow and player all→one→empty. The prior C2–C10 index-before-vertex shrink path
remains historical evidence; it did not establish physical GPU buffer retention.

Prepare evidence backing arrays before the90 real-frame paused warmup, not merely
before the stopwatch: native GC refers to the previous completed render frame.
Capture collection counts immediately at sample end, before percentiles/ToArray/
serialization. C0/C1 raw series retain their older preparation boundary; do not
attribute their first-sample diagnostic-array peak to normal product gameplay.
The separate untimed fresh Tick1 volley capture must assert800 live marines and
shots on both sides, keep the same player0 command, validate the PNG and pause.

Evidence belongs under ignored Builds/Evidence/Iteration06 with frozen source and
binary hashes. [Iteration06](../../../docs/sc2/iteration-06.md) owns measured
results; live AI restore, accepted replay, exact per-tick state/observation proofs
and Unity tests supplement graphical timings, without replacing them.

## C9 candidate: observed compact visual lifetime

Scenario: observed marine leaves fog/death publication -> BattleView defers native
root/ring destruction -> later reveal reconstructs the same compact model.
C8 records a creation/removal transition preceding four first-process tick97
spikes; it does not establish an exclusive cause of the long platform gaps.
BattleView owns the presentation-only candidate: reuse inactive compact-marine
visuals by owner from previously observed resources, never from hidden counts.
Do not pool noncompact/fallback actors in this batch. Preserve the old snapshot
update/removal order so killing-shot fallback still sees the previous target.

Before activation under a new observed ID, reset the root transform, selection,
shot history and authored compact pose; InstancedArtInstance owns pose reset.
Returned roots are inactive/unregistered, lose their snapshot reference and ID
binding, and receive no hidden updates. Capacity is bounded by actually observed
active/retired resources (including a publication transition's old/new union).
Reset may retain these native resources; view destruction releases all leases
exactly once. Tests must compare reused/fresh authored geometry and owner palette,
fog withdrawal, independent neighboring actors, reset and final resource cleanup.
Graphical evidence must retain hashes/quality/AI and distinguish actual root
creation from reuse. CreatedVisualCount counts true instantiations;
ReusedVisualCount is cumulative activations and PooledCompactVisualCount is an
inactive-resource gauge, not cumulative work. Trial start/end/deltas and callback
frame IDs make reset/warmup boundaries explicit. Pool retirement storage reserves
capacity alongside actual observed creation, avoiding list growth on casualties.
ObservedVisualReuseTests covers both owners, fresh authored/animated parts,
transform/layer/selection/shot reset, real fog retirement, killing-shot fallback,
noncompact destruction, neighbors, repeated reset and exact final lease cleanup.
Fresh191/191 tests passed. Actual first-trial creations278→14,264reuse proved
the lifetime change. C9/C10 raw traces still contain the recurring tick97/98
stall after pooled activation; eliminating true creation is not sufficient to
eliminate it. No exclusive FPS/GC causal gain is inferred from creation counters.

## C10 diagnostic boundary: HUD health mesh upload

C9 removes most true visual creations but its first-trial tick97 stall remains.
Managed instancing buffers do not shrink on retirement and prototypes retain
leases. Native health-mesh upload remains an unproven candidate: casualty/fog
changes shorten its actual vertex/index lists. Before changing geometry/buffers,
measure the existing upload and immediate draw separately in HudHealthBarBatch.
Keep exact existing SetTriangles/SetVertices/SetColors ordering and draw output.
Enable timing only through host diagnostic profiling. A value snapshot records
frame, repaint/upload/draw/shrink counts, first previous and latest actual
vertex/index/bar counts, total/last upload and immediate-draw wall durations.
Aggregate multiple repaints in one frame; empty geometry records no upload/draw
while retaining explicit previous uploaded counts. No per-frame arrays/objects.
BattleHud exposes this diagnostic value; Combat evidence copies it with its own
frame boundary. Stale/absent repaint snapshots are not current samples. Upload
and draw times are parts of GUI wall, never additional exclusive costs. Native
deferred work can remain outside these scopes; short timings do not rule it out.

C10A17589 actual samples passed frame/count/topology/timing validation with full
focus. Firsttick97 grew408→416 vertices, then slowframe had steady geometry and
.0375ms upload/.0278ms immediate draw. The next actual shrink416→392 was a
6.8966ms frame. All six >16.67ms samples had steady geometry. This does not support
a synchronous health-shrink bottleneck; do not change geometry/buffer policy on
this evidence or claim that deferred GPU work is excluded.

## Native trace clock and attribution contract

One separately authorized CPU-scheduling/D3D11/DXGI/DxgKrnl WPR capture is retained
in ignored Builds/Evidence/Iteration06/NativeTraceCycle10; WPR was stopped. It is
an instrumented workload, not a replacement for ordinary Release acceptance.
Do not upload it or treat this one-recording authorization as permission for
another system-wide capture. Compact results should identify the game and only
the system graphics/scheduling context needed to explain its observed waits.

Mono Stopwatch values in this player have a relative origin. ETL raw QPC uses a
different epoch despite both reporting10MHz; frequency equality permits no direct
frame join. EOF is not Present, and Unity Gfx.WaitForPresentOnGfxThread is not the
DXGI Present API duration. A cadence match alone includes pipeline phase error.
Exact-frame native attribution requires validated clock calibration or an
explicit bounded epoch interval independently checked against actual main-thread
on-CPU timestamps, with holdouts, scheduler coherence and clock/loss checks.
Report uncertainty and only conclusions robust over its whole range. Unverified
system/context graphics events are not automatically attributable to the game.

## C11 failed experiment: reserve from observed potential bars

The calibrated C10 trace shows main blocked15.6484ms and UnityGfxDeviceWorker
waiting14.9310ms in the25.4128ms sample. A preceding HUD payload shrank392→288
vertices/588→432indices; native named Observed health rectangles buffers changed
10976→8064 vertex bytes (28byte stride) and2352→1728 index bytes. New resources
were reported after the wait; causality/exclusive attribution is not established.
This is a measured deferred resource-lifetime scenario, distinct from the short
synchronous upload scopes. Constant-buffer activity is a separate unproven owner.

C11 reserved and grew vertex/index GPU storage geometrically from actually observed
maximum potential bars (Begin's maximumBars), then uploaded actual payload
prefixes. Its Float32 position3/color4 and UInt32 layout preserved28byte vertices,
clipping, quad topology, colors and pixel space. Actual submesh counts/bounds
excluded spare storage, empty geometry issued no upload/draw, and shrink did not
resize/redefine buffer layouts. This is the historical sizing experiment, not the
actual-payload C12 storage policy retained by C13 below.

The reserve stayed8192vertices/16384indices with no timed growth, but C11A passed
p99only5/10 and reached201.776ms. Correct capacity/geometry contracts did not
establish acceptable FPS. Preserve this failed series; neither its failures nor
the earlier deferred-resource sequence prove an exclusive cause. No additional
system trace is covered by the completed one-recording authorization.

## C12 candidate: actual drawn high-water capacity

C11A fulfills capacity stability but fails performance (p99only5/10,max201.776ms),
with broad View/Prepare/GUI inflation. Its potential-population reserve holds
8192vertices/16384indices =294912GPUbytes even when only49bars use13328bytes
(~22x storage). Exclusive cause is not established; C11 is not accepted.
The C12 storage policy, retained by current C13, grows native storage only from
actual vertices.Count and indices.Count at nonempty UploadGeometry, using
geometric headroom (minimum128
elements, doubled until the required prefix fits). There are no desired-potential
GPU-reserve fields. Begin maximumBars still reserves managed preparation lists;
it cannot force unused native storage. A new actual peak may legitimately grow
during the first battle; retain and report these reserve events rather than
requiring zero or hiding them in warmup. Later repeats reuse that actual high-water.

SetVertexBufferParams/SetIndexBufferParams define layouts only when capacity grows.
The interleaved sequential HealthVertex is position Float32x3/color Float32x4 in
stream0 (28bytes), with UInt32 index storage. SetVertexBufferData and
SetIndexBufferData updated actual prefixes on every C12 nonempty upload;
C13 retains the invariant initialized index prefix as specified above.
SetSubMesh(0) supplies actual index count/vertex range/bounds, and mesh.bounds uses
actual clipped vertex extrema.
Never reduce subMeshCount or call Clear on shrink/empty. No spare geometry is
submitted. Empty UploadGeometry returnsfalse before any native reserve/upload;
Draw likewise skips drawing, retaining the prior native payload/metadata until
a later nonempty upload. Dispose releases the owned mesh/material.

Logical uploaded vertex/index/bar counts remain separate from physical capacity;
shrink compares the current payload with uploadedVertices, not mesh.vertexCount.
DiagnosticHealthBars retains source-frame timing/count semantics and reports
VertexCapacity/IndexCapacity plus cumulative BufferReserveCount. BattleHud exposes
the same capacity/reserve properties for trial warmup/start/end boundaries.
HudHealthBarBufferTests independently checks exact clipped geometry, Float32
colors, triangle indices, actual submesh/bounds, unused potential-count independence,
grow→shrink→empty→grow capacity reuse and owner cleanup. Fresh195/195 tests and
both builds passed. The two normal C12A/B series retained20clean battles and32781
frames, all declared percentile/throughput gates passed, maximum32.8942ms and
two frames>25ms. Preserve those tails; p99 success is not an every-frame guarantee.
The separate short instrumented C12 run reached44.8992ms and remains independent
evidence. C12 supports the bounded storage change, not a claim that HUD replacement
caused every earlier wait or that rare FPS tails are eliminated. C13's separate
fresh graphical evidence and bounded acceptance are specified at the top.


