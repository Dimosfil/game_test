using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ColonyRTS.Presentation;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Diagnostics
{
    public sealed partial class SessionPerformanceProfile
    {
        int combatEndTick = 60;
        readonly List<double> fullArmyFrames = new List<double>(10000);
        readonly List<double> nearFullArmyFrames = new List<double>(10000);
        readonly List<int> liveSamples = new List<int>(10000);
        // C7: unresolved wall gaps -> diagnostic main-loop boundaries -> this opt-in
        // evidence owner. Preserve gameplay scheduling; validate frame alignment and
        // telescoping interval sums from the actual player trace, not CPU attribution.
        MainLoopBoundaryProbe mainLoopBoundaryProbe;
        WindowsGameStallProbe gameStallProbe;

        void StopMainLoopBoundaryProbe()
        {
            if(gameStallProbe)
            {
                gameStallProbe.EndSample();gameStallProbe.enabled=false;
                Destroy(gameStallProbe);gameStallProbe=null;
            }
            if (!mainLoopBoundaryProbe) return;
            mainLoopBoundaryProbe.enabled=false;
            Destroy(mainLoopBoundaryProbe);
            mainLoopBoundaryProbe=null;
        }

        public static RuleSet CreateCombatFixtureRules(RuleSet original,bool movingAI=false)
        {
            var spawns=new List<SpawnDefinition>();
            foreach(var spawn in original.Spawns)
                if(spawn.DefinitionId!="marine")spawns.Add(spawn);
            // Exactly400 per owner, distinct nonoverlapping positions, ordinary catalog HP/damage.
            // Interleaving makes every rifleman acquire a nearby real opponent at the opening volley.
            for(int i=0;i<800;i++)
                spawns.Add(new SpawnDefinition { DefinitionId="marine",Owner=movingAI?(i<400?0:1):(i%40+i/40)%2,
                    Position=SimPoint.World(32.4f+i%40*.8f,28.4f+i/40*.8f+(movingAI&&i>=400?2.4f:0)) });
            original.Spawns=spawns.ToArray(); original.Blocks=new MapBlock[0];
            return original;
        }

        IEnumerator RunCombat(string[] args)
        {
            evidence.CombatRequested=true;
            bool movingAI=Array.IndexOf(args,"-rtsCombatMovingAI")>=0;
            bool mainLoopProbeRequested=Array.IndexOf(args,"-rtsMainLoopBoundaryProbe")>=0;
            bool nativeWaitProbeRequested=Array.IndexOf(args,"-rtsMainLoopNativeWaitProbe")>=0;
            bool gameStallProbeRequested=Array.IndexOf(args,"-rtsGameStallProbe")>=0;
            string gameStallMap=null;
            int selfTestStallDelay=0;
            for(int i=0;i<args.Length;i++)
            {
                if(args[i]=="-rtsGameStallMap")
                { Require(i+1<args.Length,"Game stall map name missing.");gameStallMap=args[++i]; }
                else if(args[i]=="-rtsGameStallSelfTestDelayMs")
                { Require(i+1<args.Length&&int.TryParse(args[++i],out selfTestStallDelay)&&selfTestStallDelay>=20&&selfTestStallDelay<=200,"Game stall self-test delay must be20..200ms."); }
            }
            Require(!gameStallProbeRequested||mainLoopProbeRequested,"Game stall probe requires main-loop boundaries.");
            Require(gameStallProbeRequested||(gameStallMap==null&&selfTestStallDelay==0),"Stall map/self-test flags require the game stall probe.");
            Require(!nativeWaitProbeRequested||mainLoopProbeRequested,"Native wait probe requires main-loop boundary probe.");
            Require(!mainLoopProbeRequested||movingAI,"Main-loop boundary probe requires moving AI combat.");
            if(mainLoopProbeRequested)mainLoopBoundaryProbe=gameObject.AddComponent<MainLoopBoundaryProbe>();
            if(nativeWaitProbeRequested)mainLoopBoundaryProbe.EnableNativeWaitProbe();
            if(gameStallProbeRequested)
            {
                Require(!string.IsNullOrEmpty(gameStallMap),"Game stall probe requires an explicit private map name.");
                gameStallProbe=gameObject.AddComponent<WindowsGameStallProbe>();
                Require(gameStallProbe.Initialize(gameStallMap),"Game stall private mapping could not be initialized.");
                mainLoopBoundaryProbe.AttachGameStallProbe(gameStallProbe);
            }
            // Native diagnostic only: ordinary combat profiles never add per-GUI
            // native reads. Raw cycles are a workload proxy, not elapsed CPU time.
            host.GetComponent<BattleHud>().DiagnosticGuiThreadCyclesEnabled=nativeWaitProbeRequested;
            evidence.MovingAiRequested=movingAI;
            evidence.Scope="Actual GameHost 20Hz worker, BattleView, fog/HUD/minimap/input. Exactly400+400 catalog marines in interleaved initial positions, original nonmarine spawns/bases retained, terrain cleared, fixture AI off. Ordinary autonomous targeting, HP/damage/death, no resurrection/reinforcement/HP reset. Independent battles to the explicit CombatEndTick for one/all selection. Population bands are reported separately; this is not steady800 survivors or a complete game.";
            int trialCount=5;
            for(int i=0;i<args.Length;i++)if(args[i]=="-rtsCombatTrials")
            { Require(i+1<args.Length&&int.TryParse(args[++i],out trialCount)&&trialCount>=1&&trialCount<=5,"Combat trial count must be1..5."); }
            for(int i=0;i<args.Length;i++)if(args[i]=="-rtsCombatTicks")
            { Require(i+1<args.Length&&int.TryParse(args[++i],out combatEndTick)&&(combatEndTick==60||combatEndTick==240),"Combat tick horizon must be60 or240."); }
            evidence.CombatEndTick=combatEndTick;
            Require(!movingAI||combatEndTick==240,"Moving AI requires240ticks to exercise multiple real bot decisions.");
            if(movingAI)evidence.Scope="Actual GameHost20Hz,800 initial catalog marines in two separated400 formations, original bases/workers/resources/costs/supply, full HUD/fog/minimap/input,1600x900 4xMSAA full shadows. Player0 queues one attack-move at tick0; player1 is controlled solely by ordinary enabled bot. AI economy and autonomous movement/combat/deaths run during exact240ticks. Free initial stress army exceeds normal supply cap; rejected production is not a production proof. Natural losses/fog recorded, no health reset/reinforcement or hidden visibility override. Per-frame stage markers are last completed callbacks, paired by explicit frame indices; native counters retain their own previous-completed-frame semantics.";
            host.DiagnosticTickLimit=combatEndTick;
            host.Interaction.SetZoom(48); host.Interaction.Focus(new Vector3(49,0,39),true);
            DiscoverAllocations();
            evidence.IncrementalGcEnabled=UnityEngine.Scripting.GarbageCollector.isIncremental;
            evidence.GcMode=UnityEngine.Scripting.GarbageCollector.GCMode.ToString();
            evidence.IncrementalGcSliceNanoseconds=(long)UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds;
            bool cpuProbe=WindowsCpuProbe.TrySample(out _,out _);
            evidence.NativeCpuProbeAvailable=cpuProbe;
            string expectedFinalHash=null;
            bool selfTestStallInjected=false;
            foreach(bool selectAll in new[]{false,true})
            for(int trial=1;trial<=trialCount;trial++)
            {
                host.StartMatch(); host.TogglePause();
                host.Interaction.SetZoom(48); host.Interaction.Focus(new Vector3(49,0,39),true);
                if(selectAll)host.Interaction.SelectAllArmy();
                else foreach(var entity in host.Observation.Entities)
                    if(entity.Owner==0&&entity.DefinitionId=="marine") { host.Interaction.Selection.Add(entity.Id);break; }
                var initial=host.DiagnosticCombat;
                Require(initial.Tick==0&&initial.LiveMarines0==400&&initial.LiveMarines1==400&&initial.Shots0+initial.Shots1==0,"Combat must start with400+400 fresh ordinary marines.");
                int visible=0;
                foreach(var entity in host.Observation.Entities)
                {
                    if(entity.DefinitionId!="marine")continue;
                    var point=host.GameCamera.WorldToViewportPoint(BattleView.Position(entity.Position)+Vector3.up*.6f);
                    if(point.z>0&&point.x>0&&point.x<1&&point.y>RtsInteraction.HudHeight/Screen.height&&point.y<1-62f/Screen.height)visible++;
                }
                Require(visible==800,"All800 initial combatant centres must be observed inside the unobscured world viewport.");
                var result=new CombatTrialEvidence { SelectAll=selectAll,Trial=trial,SelectionStart=host.Interaction.Selection.Count,Start=initial,InitialVisibleCenters=visible };
                result.MainLoopBoundaryProbeEnabled=mainLoopProbeRequested;
                result.MainLoopNativeWaitProbeEnabled=nativeWaitProbeRequested;
                result.GameStallProbeRequested=gameStallProbeRequested;
                result.GameStallProbeAvailable=gameStallProbe&&gameStallProbe.Available;
                result.GameStallMapName=gameStallMap;
                result.GameStallSelfTestDelayMs=selfTestStallDelay;
                result.GameStallDiagnosticContaminated=gameStallProbeRequested;
                result.MainLoopNativeWaitRecorderScope=nativeWaitProbeRequested?"ProfilerRecorder Default aggregation, all threads; previous-completed-frame samples read at CaptureFrame; markers may overlap/nest and are not summed.":"disabled";
                result.MainLoopStopwatchFrequency=System.Diagnostics.Stopwatch.Frequency;
                result.TickTrace.Capacity=combatEndTick+1;result.Coverage.Capacity=combatEndTick+1;
                if(movingAI)result.FrameTrace.Capacity=10000;
                evidence.CombatTrials.Add(result);
                // All evidence backing storage precedes the90 real-frame warmup,
                // so native last-completed-frame GC cannot include its allocation.
                double warmStarted=Time.realtimeSinceStartupAsDouble;
                int warmRender=cameraProbe.RenderedFrames,warmGui=repaints;
                while(Time.realtimeSinceStartupAsDouble-warmStarted<2||cameraProbe.RenderedFrames-warmRender<90||repaints-warmGui<90)
                { Require(Time.realtimeSinceStartupAsDouble-warmStarted<20,"Paused combat graphics warmup timed out.");yield return null; }
                Require(host.LiveTick==0&&!host.SimulationPending,"Warmup must not consume combat HP/ticks.");
                Require(Screen.width==1600&&Screen.height==900,"Combat viewport must remain1600x900.");
                evidence.Width=Screen.width;evidence.Height=Screen.height;evidence.Msaa=QualitySettings.antiAliasing;
                evidence.ShadowDistance=QualitySettings.shadowDistance;evidence.QualityLevel=QualitySettings.names[QualitySettings.GetQualityLevel()];
                evidence.InstancingSupported=SystemInfo.supportsInstancing;
                Require(evidence.Msaa==4&&QualitySettings.shadows==ShadowQuality.All,"Combat quality must retain4xMSAA and full shadows.");
                var battleView=host.GetComponent<BattleView>();
                var battleHud=host.GetComponent<BattleHud>();
                result.HealthBarsInstrumentationEnabled=host.DiagnosticProfilingEnabled;
                result.GuiSectionsInstrumentationEnabled=host.DiagnosticProfilingEnabled;
                result.GuiThreadCyclesInstrumentationEnabled=battleHud.DiagnosticGuiThreadCyclesEnabled;
                result.GuiThreadCyclesRecorderScope=nativeWaitProbeRequested?
                    "QueryThreadCycleTime on the GUI/main thread; paired raw cycles for total GUI, WorldLabels and Bottom, aggregated by GuiSections.Frame. Native diagnostic only. Nested scopes are not additive; cycles are not milliseconds and do not identify scheduler state. Unavailable or incomplete pairs must not be interpreted as measured zero.":"disabled";
                result.GuiThreadCycleReadAttemptsStart=battleHud.DiagnosticGuiThreadCycleReadAttempts;
                result.GuiThreadCycleSuccessfulReadsStart=battleHud.DiagnosticGuiThreadCycleSuccessfulReads;
                result.HealthBarsRecorderScope="HUD Repaint health geometry preparation (including exact payload comparison/reuse) and immediate draw wall intervals aggregate by source frame; UploadCalls counts preparation attempts, VertexUploadCount counts successful native vertex uploads. Nested within GUI wall and never additive exclusive stages. Raw source frame retained; SampleAvailable requires a completed repaint aligned with GuiFrame in the capture/current-or-previous frame. Native deferred work may fall outside these intervals.";
                result.HealthBarsCapacityCountersAvailable=true;
                result.HealthBarsBufferReserveCountStart=battleHud.HealthBarBufferReserveCount;
                result.HealthBarsIndexUploadCountStart=battleHud.HealthBarIndexUploadCount;
                result.HealthBarsVertexUploadCountStart=battleHud.HealthBarVertexUploadCount;
                result.HealthBarsExactGeometryReuseCountStart=battleHud.HealthBarExactGeometryReuseCount;
                result.HealthBarsIndexUploadCountersAvailable=true;
                result.HealthBarsExactGeometryCountersAvailable=true;
                result.HealthBarsVertexCapacityStart=battleHud.HealthBarVertexCapacity;
                result.HealthBarsIndexCapacityStart=battleHud.HealthBarIndexCapacity;
                // C9 lifetime counters are presentation-owned: true creations and
                // cumulative reuse are distinct from the current inactive-pool gauge.
                result.VisualLifetimeCountersAvailable=true;
                result.CreatedVisualsStart=battleView.CreatedVisualCount;
                result.ReusedVisualsStart=battleView.ReusedVisualCount;
                result.PooledCompactVisualsStart=battleView.PooledCompactVisualCount;
                if(mainLoopProbeRequested)result.NativeClockStart=MainLoopBoundaryProbe.CaptureNativeClockAnchor();
                int createdTracers=battleView.CreatedTracerCount;
                long shownTracers=battleView.DisplayedShotCount;
                long matrixGrowths=InstancedMarineRenderer.MatrixBufferGrowthCount;
                frames.Clear();allocations.Clear();advanceTimes.Clear();cpuTimes.Clear();gpuTimes.Clear();draws.Clear();workerTimes.Clear();
                fullArmyFrames.Clear();nearFullArmyFrames.Clear();liveSamples.Clear();
                int renders=cameraProbe.RenderedFrames,gui=repaints,lastTick=0;
                int observedMarines=visible,visibleMarines=visible;
                int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                ulong previousThreadCycles=0,previousProcessCpu=0;
                long previousBoundarySampleTimestamp=mainLoopProbeRequested?System.Diagnostics.Stopwatch.GetTimestamp():0;
                cpuProbe=cpuProbe&&WindowsCpuProbe.TrySample(out previousThreadCycles,out previousProcessCpu);
                bool previousBoundaryCpuAvailable=cpuProbe;
                int previousBoundarySampleFrame=Time.frameCount;
                double sampleStart=Time.realtimeSinceStartupAsDouble,previous=sampleStart;
                if(gameStallProbe)gameStallProbe.BeginSample();
                try
                {
                host.TogglePause();
                if(movingAI)IssuePlayerAdvance();
                while(host.LiveTick<combatEndTick)
                {
                    Require(Time.realtimeSinceStartupAsDouble-sampleStart<40,"Combat did not reach its tick horizon within40seconds.");
                    Require(frames.Count<10000,"Combat sample buffer exceeded preallocation.");
                    yield return null;
                    if(gameStallProbe&&selfTestStallDelay>0&&!selfTestStallInjected&&host.LiveTick>=80)
                    {
                        // Positive diagnostic oracle only. It is never ordinary
                        // performance evidence and is explicitly marked below.
                        selfTestStallInjected=true;result.GameStallSelfTestInjected=true;
                        result.GameStallSelfTestFrame=Time.frameCount;
                        System.Threading.Thread.Sleep(selfTestStallDelay);
                    }
                    double now=Time.realtimeSinceStartupAsDouble,delta=(now-previous)*1000;previous=now;
                    // Probe intervals use these resume-adjacent native observations, before coverage work.
                    long sampleTimestamp=mainLoopProbeRequested?System.Diagnostics.Stopwatch.GetTimestamp():0;
                    long threadCycles=0;double processCpuMs=0;
                    ulong currentCycles=0,currentProcessCpu=0;
                    bool cpuSample=false;
                    MainLoopBoundaryEvidence boundaryEvidence=default;
                    if(movingAI)
                    {
                        long probeStarted=System.Diagnostics.Stopwatch.GetTimestamp();
                        cpuSample=cpuProbe&&WindowsCpuProbe.TrySample(out currentCycles,out currentProcessCpu);
                        if(cpuSample)cpuSample=currentCycles>=previousThreadCycles&&currentProcessCpu>=previousProcessCpu;
                        if(cpuSample){threadCycles=(long)(currentCycles-previousThreadCycles);processCpuMs=(currentProcessCpu-previousProcessCpu)/10000.0;}
                        if(mainLoopBoundaryProbe)boundaryEvidence=mainLoopBoundaryProbe.Read(previousBoundarySampleFrame,previousBoundarySampleTimestamp,
                            previousBoundaryCpuAvailable,previousThreadCycles,sampleTimestamp,cpuSample,currentCycles);
                        previousThreadCycles=currentCycles;previousProcessCpu=currentProcessCpu;
                        previousBoundaryCpuAvailable=cpuSample;previousBoundarySampleFrame=Time.frameCount;previousBoundarySampleTimestamp=sampleTimestamp;
                        if(!cpuSample){cpuProbe=false;evidence.NativeCpuProbeAvailable=false;}
                        result.NativeProbeMaxMs=Math.Max(result.NativeProbeMaxMs,(System.Diagnostics.Stopwatch.GetTimestamp()-probeStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                    }
                    frames.Add(delta);advanceTimes.Add(host.DiagnosticAdvanceMs);
                    var counters=host.DiagnosticCombat;
                    int population=counters.LiveMarines0+counters.LiveMarines1;
                    liveSamples.Add(population);
                    if(host.LiveTick!=lastTick)
                    {
                        Require(workerTimes.Count<workerTimes.Capacity,"Combat worker sample buffer exceeded.");
                        workerTimes.Add(host.DiagnosticWorkerMs);
                        result.TickTrace.Add(counters);
                        long priorShots=result.TickTrace.Count==1?0:result.TickTrace[result.TickTrace.Count-2].Shots0+result.TickTrace[result.TickTrace.Count-2].Shots1;
                        bool newShots=counters.Shots0+counters.Shots1>priorShots;
                        if(newShots)result.NewAttackPublications++;
                        if(counters.TickMovingMarines0>=20&&counters.TickMovingMarines1>=20&&newShots)result.MovingAttackPublications++;
                        long captureStarted=System.Diagnostics.Stopwatch.GetTimestamp();
                        var coverage=new CombatCoverage { Tick=host.LiveTick,Selected=host.Interaction.Selection.Count };
                        foreach(var entity in host.Observation.Entities)
                        {
                            if(entity.DefinitionId!="marine")continue;
                            coverage.Observed++;
                            var point=host.GameCamera.WorldToViewportPoint(BattleView.Position(entity.Position)+Vector3.up*.6f);
                            if(point.z>0&&point.x>0&&point.x<1&&point.y>RtsInteraction.HudHeight/Screen.height&&point.y<1-62f/Screen.height)coverage.VisibleCenters++;
                        }
                        result.Coverage.Add(coverage);
                        observedMarines=coverage.Observed;visibleMarines=coverage.VisibleCenters;
                        result.HarnessCoverageMaxMs=Math.Max(result.HarnessCoverageMaxMs,(System.Diagnostics.Stopwatch.GetTimestamp()-captureStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                        if(!movingAI&&population>=720)Require(coverage.Observed==population&&coverage.VisibleCenters==population,"Fog/camera hid live combatants in the near-full-army measurement band.");
                        lastTick=host.LiveTick;
                    }
                    if(counters.Shots0+counters.Shots1>0)
                    {
                        result.PostFirstShotFrames++;
                        if(population==800&&observedMarines==800&&visibleMarines==800)fullArmyFrames.Add(delta);
                        if(population>=720&&observedMarines==population&&visibleMarines==population)nearFullArmyFrames.Add(delta);
                    }
                    if(movingAI)
                    {
                        int captureFrame=Time.frameCount,guiFrame=battleHud.DiagnosticGuiFrame;
                        var healthBars=battleHud.DiagnosticHealthBars;
                        var guiSections=battleHud.DiagnosticGuiSections;
                        bool guiSectionsAligned=guiSections.Recorded&&guiSections.Frame==guiFrame;
                        bool healthBarsGuiAligned=healthBars.Recorded&&healthBars.Frame==guiFrame;
                        result.FrameTrace.Add(new CombatFrameEvidence {
                            Frame=captureFrame,Tick=host.LiveTick,Population=population,Observed=observedMarines,Visible=visibleMarines,WallMs=delta,AdvanceMs=host.DiagnosticAdvanceMs,
                            WorkerMs=host.DiagnosticWorkerMs,Pending=host.SimulationPending,
                            ViewMs=battleView.DiagnosticLateUpdateMs,ViewFrame=battleView.DiagnosticLateUpdateFrame,
                            GuiMs=battleHud.DiagnosticGuiMs,GuiFrame=guiFrame,
                            GuiSections=guiSections,GuiSectionsFrameAligned=guiSectionsAligned,
                            GuiSectionsSampleAvailable=result.GuiSectionsInstrumentationEnabled&&guiSectionsAligned&&guiSections.Completed&&guiSections.Frame<=captureFrame&&guiSections.Frame>=captureFrame-1,
                            HealthBars=healthBars,HealthBarsGuiFrameAligned=healthBarsGuiAligned,
                            HealthBarsSampleAvailable=result.HealthBarsInstrumentationEnabled&&healthBarsGuiAligned&&healthBars.Completed&&healthBars.RepaintCalls>0&&
                                healthBars.Frame<=captureFrame&&healthBars.Frame>=captureFrame-1,
                            SubmitMs=InstancedMarineRenderer.DiagnosticSubmitMs,SubmitFrame=InstancedMarineRenderer.DiagnosticSubmitFrame,
                            PrepareMs=InstancedMarineRenderer.DiagnosticPrepareMs,DrawMs=InstancedMarineRenderer.DiagnosticDrawMs,
                            CpuProbeAvailable=cpuSample,MainThreadCycles=threadCycles,ProcessCpuMs=processCpuMs,
                            MainLoop=boundaryEvidence,
                            GcCollectMs=gcCollectRecorder.Valid&&gcCollectRecorder.Count>0?gcCollectRecorder.LastValue*1e-6:-1,
                            GcIncrementalMs=gcIncrementalRecorder.Valid&&gcIncrementalRecorder.Count>0?gcIncrementalRecorder.LastValue*1e-6:-1,
                            GcCollectInvocations=gcCollectRecorder.Valid&&gcCollectRecorder.Count>0?gcCollectRecorder.GetSample(0).Count:-1,
                            CreatedVisuals=battleView.CreatedVisualCount,MatrixGrowths=InstancedMarineRenderer.MatrixBufferGrowthCount,
                            VisualLifetimeCountersAvailable=true,VisualCountersFrame=battleView.DiagnosticLateUpdateFrame,
                            ReusedVisuals=battleView.ReusedVisualCount,PooledCompactVisuals=battleView.PooledCompactVisualCount,
                            Collections0=GC.CollectionCount(0)-gc0,Collections1=GC.CollectionCount(1)-gc1,Collections2=GC.CollectionCount(2)-gc2,
                            GcBytes=allocationRecorder.Valid&&allocationRecorder.Count>0?allocationRecorder.LastValue:-1,
                            NativeCpuMs=cpuRecorder.Valid&&cpuRecorder.Count>0?cpuRecorder.LastValue*1e-6:-1,
                            NativeGpuMs=gpuRecorder.Valid&&gpuRecorder.Count>0?gpuRecorder.LastValue*1e-6:-1 });
                    }
                    if(Application.isFocused)result.FocusedFrames++;else result.UnfocusedFrames++;
                    if(delta>250)result.ClampedFrames++;
                    if(allocationRecorder.Valid&&allocationRecorder.Count>0)allocations.Add(allocationRecorder.LastValue);
                    if(cpuRecorder.Valid&&cpuRecorder.Count>0)cpuTimes.Add(cpuRecorder.LastValue*1e-6);
                    if(gpuRecorder.Valid&&gpuRecorder.Count>0&&gpuRecorder.LastValue>0)gpuTimes.Add(gpuRecorder.LastValue*1e-6);
                    if(drawRecorder.Valid&&drawRecorder.Count>0)draws.Add(drawRecorder.LastValue);
                    Require(host.Mode==FrontendMode.Playing,"Combat left the real gameplay loop.");
                }
                }
                finally { if(gameStallProbe)gameStallProbe.EndSample(); }
                int collectionEnd0=GC.CollectionCount(0),collectionEnd1=GC.CollectionCount(1),collectionEnd2=GC.CollectionCount(2);
                result.GuiThreadCycleReadAttemptsEnd=battleHud.DiagnosticGuiThreadCycleReadAttempts;
                result.GuiThreadCycleSuccessfulReadsEnd=battleHud.DiagnosticGuiThreadCycleSuccessfulReads;
                result.HealthBarsBufferReserveCountEnd=battleHud.HealthBarBufferReserveCount;
                result.HealthBarsBufferReserveCountDuringSample=result.HealthBarsBufferReserveCountEnd-result.HealthBarsBufferReserveCountStart;
                result.HealthBarsIndexUploadCountEnd=battleHud.HealthBarIndexUploadCount;
                result.HealthBarsIndexUploadCountDuringSample=result.HealthBarsIndexUploadCountEnd-result.HealthBarsIndexUploadCountStart;
                result.HealthBarsVertexUploadCountEnd=battleHud.HealthBarVertexUploadCount;
                result.HealthBarsVertexUploadCountDuringSample=result.HealthBarsVertexUploadCountEnd-result.HealthBarsVertexUploadCountStart;
                result.HealthBarsExactGeometryReuseCountEnd=battleHud.HealthBarExactGeometryReuseCount;
                result.HealthBarsExactGeometryReuseCountDuringSample=result.HealthBarsExactGeometryReuseCountEnd-result.HealthBarsExactGeometryReuseCountStart;
                result.HealthBarsVertexCapacityEnd=battleHud.HealthBarVertexCapacity;
                result.HealthBarsIndexCapacityEnd=battleHud.HealthBarIndexCapacity;
                result.CreatedVisualsEnd=battleView.CreatedVisualCount;
                result.ReusedVisualsEnd=battleView.ReusedVisualCount;
                result.PooledCompactVisualsEnd=battleView.PooledCompactVisualCount;
                result.CreatedVisualsDuringSample=result.CreatedVisualsEnd-result.CreatedVisualsStart;
                result.ReusedVisualsDuringSample=result.ReusedVisualsEnd-result.ReusedVisualsStart;
                result.PooledCompactVisualsDelta=result.PooledCompactVisualsEnd-result.PooledCompactVisualsStart;
                result.Seconds=Time.realtimeSinceStartupAsDouble-sampleStart;
                if(mainLoopProbeRequested)result.NativeClockEnd=MainLoopBoundaryProbe.CaptureNativeClockAnchor();
                result.Frames=frames.Count;result.CameraFrames=cameraProbe.RenderedFrames-renders;result.GuiRepaints=repaints-gui;
                result.End=host.DiagnosticCombat;
                result.EndLiveMarines=result.End.LiveMarines0+result.End.LiveMarines1;
                result.RealTime20Hz=result.End.Tick>=result.Seconds*host.Rules.TickRate-3;
                result.FrameP50Ms=Percentile(frames,.5);result.FrameP95Ms=Percentile(frames,.95);result.FrameP99Ms=Percentile(frames,.99);result.FrameMaxMs=Percentile(frames,1);
                result.FrameP999Ms=Percentile(frames,.999);
                int consecutive=0;
                foreach(double frame in frames)
                {
                    if(frame>1000.0/60) { result.FramesOver16Ms++;consecutive++;result.LongestOverBudgetBurst=Math.Max(result.LongestOverBudgetBurst,consecutive); }else consecutive=0;
                    if(frame>25)result.FramesOver25Ms++;if(frame>1000.0/30)result.FramesOver33Ms++;if(frame>50)result.FramesOver50Ms++;
                }
                result.Meets60FpsP95=result.FrameP95Ms<=1000.0/60;result.Meets60FpsP99=result.FrameP99Ms<=1000.0/60;
                result.MainAdvanceP95Ms=Percentile(advanceTimes,.95);result.WorkerP95Ms=Percentile(workerTimes,.95);result.WorkerMaxMs=Percentile(workerTimes,1);result.WorkerBatches=workerTimes.Count;
                result.Full800CombatFrames=fullArmyFrames.Count;result.NearFull720CombatFrames=nearFullArmyFrames.Count;
                if(fullArmyFrames.Count>0) { result.Full800P95Ms=Percentile(fullArmyFrames,.95);result.Full800P99Ms=Percentile(fullArmyFrames,.99); }
                if(nearFullArmyFrames.Count>0) { result.NearFull720P95Ms=Percentile(nearFullArmyFrames,.95);result.NearFull720P99Ms=Percentile(nearFullArmyFrames,.99); }
                result.NativeGcAvailable=allocations.Count>0;
                if(result.NativeGcAvailable)
                {
                    result.GcP50Bytes=Percentile(allocations,.5);result.GcP95Bytes=Percentile(allocations,.95);result.GcMaxBytes=Percentile(allocations,1);
                    foreach(double bytes in allocations)result.TotalGcBytes+=bytes;
                    result.GcBytesPerCompletedTick=result.TotalGcBytes/result.End.Tick;
                    result.NativeGcSamples=allocations.ToArray();
                }
                result.NativeCpuAvailable=cpuTimes.Count>0;result.NativeGpuAvailable=gpuTimes.Count>0;result.NativeDrawsAvailable=draws.Count>0;
                if(cpuTimes.Count>0)result.CpuP95Ms=Percentile(cpuTimes,.95);
                if(gpuTimes.Count>0)result.GpuP95Ms=Percentile(gpuTimes,.95);
                if(draws.Count>0)result.DrawsP50=Percentile(draws,.5);
                result.WallFrameSamples=frames.ToArray();result.LiveMarineSamples=liveSamples.ToArray();
                result.TracerCapacity=battleView.TracerCapacity;
                result.TracersCreatedDuringSample=battleView.CreatedTracerCount-createdTracers;
                result.DisplayedTracers=battleView.DisplayedShotCount-shownTracers;
                result.MatrixGrowthsDuringSample=InstancedMarineRenderer.MatrixBufferGrowthCount-matrixGrowths;
                result.Collections0=collectionEnd0-gc0;result.Collections1=collectionEnd1-gc1;result.Collections2=collectionEnd2-gc2;
                result.NativeGcCollectAvailable=gcCollectRecorder.Valid&&gcCollectRecorder.Count>0;
                result.NativeGcIncrementalAvailable=gcIncrementalRecorder.Valid&&gcIncrementalRecorder.Count>0;
                Require(result.End.Tick==combatEndTick&&result.End.Shots0>0&&result.End.Shots1>0&&result.End.HealthDamage0>0&&result.End.HealthDamage1>0&&result.End.Deaths0+result.End.Deaths1>0,"No actual two-sided combat/deaths or exact tick fence.");
                Require(result.CameraFrames>=result.Frames&&result.GuiRepaints>=result.Frames,"Combat samples lack actual camera/GUI work.");
                result.FinalHash=host.Match.StateHash();
                if(movingAI)
                {
                    Require(result.End.MovedMarines0>=100&&result.End.MovedMarines1>=100&&result.End.MarineTravel0>100000&&result.End.MarineTravel1>100000,"Insufficient actual two-sided movement.");
                    Require(result.End.BotThinkCount>=10&&result.End.BotAttackMoveCommands>0&&result.End.BotAttackMoveUnits>=100&&result.End.BotBuildCommands>0,"Enabled AI did not actually think, build and command an army.");
                    var snapshot=host.Match.CaptureState();
                    foreach(var command in snapshot.CommandLog)if(command.Player==1)result.AiCommands.Add(command);
                    var commanded=new HashSet<int>();
                    foreach(var command in result.AiCommands)if(command.Kind==CommandKind.AttackMove)foreach(int id in command.EntityIds)commanded.Add(id);
                    result.UniqueBotAttackMoveUnits=commanded.Count;
                    Require(commanded.Count>=100&&result.MovingAttackPublications>=10,"Insufficient distinct AI-controlled army or concurrent two-sided movement/shot publications.");
                }
                if(expectedFinalHash==null)expectedFinalHash=result.FinalHash;
                Require(result.FinalHash==expectedFinalHash,"Selection/trial changed the authoritative battle outcome.");
                if(trial==1)
                {
                    string screenshot=Path.Combine(directory,selectAll?"combat-selected400.png":"combat-selected1.png");
                    if(File.Exists(screenshot))File.Delete(screenshot);
                    yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(screenshot);
                    double deadline=Time.realtimeSinceStartupAsDouble+10;long previousLength=-1;int stable=0;
                    while(stable<2)
                    {
                        Require(Time.realtimeSinceStartupAsDouble<deadline,"Combat screenshot did not finish writing.");
                        long length=File.Exists(screenshot)?new FileInfo(screenshot).Length:0;
                        stable=length>1024&&length==previousLength?stable+1:0;previousLength=length;yield return null;
                    }
                    var captured=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    try { Require(ImageConversion.LoadImage(captured,File.ReadAllBytes(screenshot),true)&&captured.width==1600&&captured.height==900,"Incomplete combat screenshot."); }
                    finally { Destroy(captured); }
                }
                host.TogglePause();
                Require(!host.SimulationPending&&host.Mode==FrontendMode.Paused,"Battle pause did not drain the worker.");
                WriteEvidence();
            }
            // Separate untimed first-volley capture: captures/PNG encoding cannot pollute frame samples.
            host.DiagnosticTickLimit=1;host.StartMatch();host.TogglePause();
            host.Interaction.SetZoom(48);host.Interaction.Focus(new Vector3(49,0,39),true);host.Interaction.SelectAllArmy();
            int captureWarm=cameraProbe.RenderedFrames;
            while(cameraProbe.RenderedFrames-captureWarm<90)yield return null;
            host.TogglePause();
            if(movingAI)IssuePlayerAdvance();
            double volleyDeadline=Time.realtimeSinceStartupAsDouble+20;
            while(host.LiveTick<1) { Require(Time.realtimeSinceStartupAsDouble<volleyDeadline,"First volley capture timed out.");yield return null; }
            Require(host.LiveTick==1&&host.DiagnosticCombat.LiveMarines0+host.DiagnosticCombat.LiveMarines1==800&&
                host.DiagnosticCombat.Shots0>0&&host.DiagnosticCombat.Shots1>0,"First-volley capture must contain800 living actual combatants.");
            evidence.VolleyTick=host.LiveTick;evidence.VolleyLiveMarines=800;
            yield return new WaitForEndOfFrame();
            string volleyPath=Path.Combine(directory,"combat-volley800.png");
            if(File.Exists(volleyPath))File.Delete(volleyPath);
            ScreenCapture.CaptureScreenshot(volleyPath);
            long priorLength=-1;int captureStable=0;
            while(captureStable<2)
            {
                Require(Time.realtimeSinceStartupAsDouble<volleyDeadline,"First volley PNG timed out.");
                long length=File.Exists(volleyPath)?new FileInfo(volleyPath).Length:0;
                captureStable=length>1024&&length==priorLength?captureStable+1:0;priorLength=length;yield return null;
            }
            var volleyTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try { Require(ImageConversion.LoadImage(volleyTexture,File.ReadAllBytes(volleyPath),true)&&volleyTexture.width==1600&&volleyTexture.height==900,"Incomplete first-volley PNG."); }
            finally { Destroy(volleyTexture); }
            evidence.VolleyCaptured=true;
            host.TogglePause();
            Require(HudIconAtlas.IsReady&&evidence.RuntimeErrors.Count==0,"Combat UI atlas missing or runtime errors present.");
            evidence.WorkerPauseValidated=!host.SimulationPending&&host.Mode==FrontendMode.Paused;
            Require(evidence.WorkerPauseValidated,"Combat final pause did not drain the worker.");
            StopMainLoopBoundaryProbe();
            Finish(true);
        }

        void IssuePlayerAdvance()
        {
            var army=new List<int>(400);
            foreach(var entity in host.Observation.Entities)if(entity.Owner==0&&entity.DefinitionId=="marine")army.Add(entity.Id);
            host.Issue(CommandKind.AttackMove,army.ToArray(),SimPoint.World(49,49));
        }

        [Serializable] sealed class CombatTrialEvidence
        {
            public bool SelectAll,RealTime20Hz,Meets60FpsP95,Meets60FpsP99,NativeGcAvailable,NativeCpuAvailable,NativeGpuAvailable,NativeDrawsAvailable;
            public int Trial,SelectionStart,InitialVisibleCenters,Frames,CameraFrames,GuiRepaints,FocusedFrames,UnfocusedFrames,ClampedFrames,PostFirstShotFrames,NewAttackPublications,EndLiveMarines,WorkerBatches,Full800CombatFrames,NearFull720CombatFrames;
            public int TracerCapacity,TracersCreatedDuringSample;
            public long DisplayedTracers;
            public long MatrixGrowthsDuringSample;
            public bool VisualLifetimeCountersAvailable;
            public bool HealthBarsInstrumentationEnabled;
            public string HealthBarsRecorderScope;
            public bool HealthBarsCapacityCountersAvailable;
            public bool GuiSectionsInstrumentationEnabled;
            public bool GuiThreadCyclesInstrumentationEnabled;
            public string GuiThreadCyclesRecorderScope;
            public long GuiThreadCycleReadAttemptsStart,GuiThreadCycleReadAttemptsEnd;
            public long GuiThreadCycleSuccessfulReadsStart,GuiThreadCycleSuccessfulReadsEnd;
            public long HealthBarsBufferReserveCountStart,HealthBarsBufferReserveCountEnd,HealthBarsBufferReserveCountDuringSample;
            public long HealthBarsIndexUploadCountStart,HealthBarsIndexUploadCountEnd,HealthBarsIndexUploadCountDuringSample;
            public long HealthBarsVertexUploadCountStart,HealthBarsVertexUploadCountEnd,HealthBarsVertexUploadCountDuringSample;
            public long HealthBarsExactGeometryReuseCountStart,HealthBarsExactGeometryReuseCountEnd,HealthBarsExactGeometryReuseCountDuringSample;
            public bool HealthBarsIndexUploadCountersAvailable;
            public bool HealthBarsExactGeometryCountersAvailable;
            public int HealthBarsVertexCapacityStart,HealthBarsVertexCapacityEnd,HealthBarsIndexCapacityStart,HealthBarsIndexCapacityEnd;
            public long CreatedVisualsStart,CreatedVisualsEnd,CreatedVisualsDuringSample;
            public long ReusedVisualsStart,ReusedVisualsEnd,ReusedVisualsDuringSample;
            public long PooledCompactVisualsStart,PooledCompactVisualsEnd,PooledCompactVisualsDelta;
            public int Collections0,Collections1,Collections2,FramesOver16Ms,FramesOver25Ms,FramesOver33Ms,FramesOver50Ms,LongestOverBudgetBurst;
            public int MovingAttackPublications,UniqueBotAttackMoveUnits;
            public bool NativeGcCollectAvailable,NativeGcIncrementalAvailable;
            public bool MainLoopBoundaryProbeEnabled;
            public bool MainLoopNativeWaitProbeEnabled;
            public string MainLoopNativeWaitRecorderScope;
            public bool GameStallProbeRequested,GameStallProbeAvailable,GameStallDiagnosticContaminated,GameStallSelfTestInjected;
            public string GameStallMapName;
            public int GameStallSelfTestDelayMs,GameStallSelfTestFrame;
            public long MainLoopStopwatchFrequency;
            public NativeClockAnchorEvidence NativeClockStart,NativeClockEnd;
            public double NativeProbeMaxMs;
            public double FrameP999Ms;
            public double Seconds,FrameP50Ms,FrameP95Ms,FrameP99Ms,FrameMaxMs,Full800P95Ms,Full800P99Ms,NearFull720P95Ms,NearFull720P99Ms,MainAdvanceP95Ms,WorkerP95Ms,WorkerMaxMs,GcP50Bytes,GcP95Bytes,GcMaxBytes,CpuP95Ms,GpuP95Ms,DrawsP50;
            public MatchCombatDiagnostics Start,End;
            public double HarnessCoverageMaxMs;
            public double TotalGcBytes,GcBytesPerCompletedTick;
            public string FinalHash;
            public List<MatchCombatDiagnostics> TickTrace=new List<MatchCombatDiagnostics>(64);
            public List<CombatCoverage> Coverage=new List<CombatCoverage>(64);
            public double[] WallFrameSamples;
            public double[] NativeGcSamples;
            public int[] LiveMarineSamples;
            public List<CombatFrameEvidence> FrameTrace=new List<CombatFrameEvidence>();
            public List<GameCommand> AiCommands=new List<GameCommand>();
        }
        [Serializable] struct CombatFrameEvidence
        {
            public int Frame,Tick,Population,Observed,Visible,ViewFrame,GuiFrame,SubmitFrame,Collections0,Collections1,Collections2;
            public bool Pending;
            public bool CpuProbeAvailable;
            public MainLoopBoundaryEvidence MainLoop;
            public HudHealthBarBatch.FrameEvidence HealthBars;
            public bool HealthBarsGuiFrameAligned,HealthBarsSampleAvailable;
            public long MainThreadCycles,GcCollectInvocations;
            public double ProcessCpuMs,GcCollectMs,GcIncrementalMs;
            public double WallMs,AdvanceMs,WorkerMs,ViewMs,GuiMs,SubmitMs,PrepareMs,DrawMs,NativeCpuMs,NativeGpuMs;
            public long CreatedVisuals,MatrixGrowths,GcBytes;
            public BattleHud.GuiSectionEvidence GuiSections;
            public bool GuiSectionsFrameAligned,GuiSectionsSampleAvailable;
            public bool VisualLifetimeCountersAvailable;
            public int VisualCountersFrame;
            public long ReusedVisuals,PooledCompactVisuals;
        }
        [Serializable] struct CombatCoverage { public int Tick,Observed,VisibleCenters,Selected; }
    }
}
