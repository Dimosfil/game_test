using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ColonyRTS.Presentation;
using ColonyRTS.Simulation;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace ColonyRTS.Diagnostics
{
    // Opt-in workload through the real host, observation, fog, view, HUD and input.
    // Only initial spawns and terrain differ; definitions/economy/research rules do not.
    public sealed partial class SessionPerformanceProfile : MonoBehaviour
    {
        const int MarineCount = 800;
        readonly Evidence evidence = new Evidence();
        readonly List<double> frames = new List<double>(30000);
        readonly List<double> allocations = new List<double>(30000);
        readonly List<double> advanceTimes = new List<double>(30000);
        readonly List<double> cpuTimes = new List<double>(30000);
        readonly List<double> gpuTimes = new List<double>(30000);
        readonly List<double> draws = new List<double>(30000);
        readonly List<double> workerTimes = new List<double>(1024);
        GameHost host;
        RenderingProfileCameraProbe cameraProbe;
        ProfilerRecorder allocationRecorder;
        ProfilerRecorder cpuRecorder,gpuRecorder,drawRecorder;
        ProfilerRecorder gcCollectRecorder,gcIncrementalRecorder;
        string directory;
        double started;
        bool finished;
        int repaints, lastRepaint = -1;

        public static RuleSet CreateFixtureRules(RuleSet original)
        {
            // The loaded catalog is a private startup copy. No production asset is mutated.
            var spawns = new List<SpawnDefinition>(original.Spawns);
            for (int i = 0; i < MarineCount; i++)
                spawns.Add(new SpawnDefinition { DefinitionId="marine", Owner=0,
                    Position=SimPoint.World(32.4f+i%40*.8f,28.4f+i/40*.8f) });
            original.Spawns = spawns.ToArray();
            original.Blocks = new MapBlock[0];
            return original;
        }

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable()
        {
            StopMainLoopBoundaryProbe();
            Application.logMessageReceived -= OnLog;
            if(allocationRecorder.Valid)allocationRecorder.Dispose();
            if(cpuRecorder.Valid)cpuRecorder.Dispose();
            if(gpuRecorder.Valid)gpuRecorder.Dispose();
            if(drawRecorder.Valid)drawRecorder.Dispose();
            if(gcCollectRecorder.Valid)gcCollectRecorder.Dispose();
            if(gcIncrementalRecorder.Valid)gcIncrementalRecorder.Dispose();
        }
        IEnumerator Start()
        {
            var run = Run();
            while(!finished)
            {
                bool more=false; object next=null; Exception failure=null;
                try { more=run.MoveNext(); if(more)next=run.Current; }
                catch(Exception exception) { failure=exception; }
                if(failure!=null) { evidence.Error=failure.ToString(); Finish(false); yield break; }
                if(!more)yield break;
                yield return next;
            }
        }

        IEnumerator Run()
        {
            started=Time.realtimeSinceStartupAsDouble;
            evidence.StartedUtc=DateTime.UtcNow.ToString("O");
            evidence.Status="running";
            host=GetComponent<GameHost>();
            Require(host!=null&&!Application.isEditor&&!Application.isBatchMode,"Session profile requires a normal standalone GameHost.");
            var args=Environment.GetCommandLineArgs();
            string candidateDirectory=null;
            for(int i=0;i<args.Length;i++)if(args[i]=="-rtsEvidencePath")
            { Require(i+1<args.Length&&Path.IsPathRooted(args[i+1]),"Evidence path must be absolute."); candidateDirectory=Path.GetFullPath(args[++i]); }
            var ancestor=new DirectoryInfo(Path.GetFullPath(Path.Combine(Application.dataPath,"..")));
            while(ancestor!=null&&!string.Equals(ancestor.Name,"Builds",StringComparison.OrdinalIgnoreCase))ancestor=ancestor.Parent;
            Require(ancestor!=null&&candidateDirectory!=null&&candidateDirectory.StartsWith(ancestor.FullName+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Evidence must stay within this checkout Builds directory.");
            directory=candidateDirectory;
            Directory.CreateDirectory(directory);
            Screen.SetResolution(1600,900,FullScreenMode.Windowed);
            QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
            evidence.UnityVersion=Application.unityVersion; evidence.Cpu=SystemInfo.processorType;
            evidence.Gpu=SystemInfo.graphicsDeviceName; evidence.GraphicsApi=SystemInfo.graphicsDeviceType.ToString();
            evidence.DevelopmentBuild=Debug.isDebugBuild;
            evidence.LogicalProcessors=SystemInfo.processorCount;
            evidence.JobWorkers=Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount;
            evidence.MaximumJobWorkers=Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerMaximumCount;
            for(int i=0;i<args.Length;i++)if(args[i]=="-job-worker-count")
            { Require(i+1<args.Length&&int.TryParse(args[++i],out evidence.RequestedJobWorkers),"Invalid job-worker-count request."); }
            Require(evidence.RequestedJobWorkers==0||evidence.RequestedJobWorkers==evidence.JobWorkers,"Unity did not honor requested job worker count.");
            cameraProbe=host.GameCamera.gameObject.AddComponent<RenderingProfileCameraProbe>();
            host.StartMatch();
            if(Array.IndexOf(args,"-rtsCombatProfile")>=0)
            {
                var combat=RunCombat(args);
                while(combat.MoveNext())yield return combat.Current;
                yield break;
            }
            host.Interaction.SetZoom(48);
            host.Interaction.Focus(new Vector3(49,0,39),true);
            var marineIds=new List<int>(MarineCount);
            foreach(var entity in host.Observation.Entities)
                if(entity.Owner==0&&entity.DefinitionId=="marine"&&entity.Position.X>=32000&&entity.Position.Z>=28000)
                    marineIds.Add(entity.Id);
            Require(marineIds.Count==MarineCount,"Session stress fixture did not produce exactly 800 added marines.");
            // Independent patrols preserve the visible coverage and run the normal navigation loop.
            foreach(int id in marineIds)
            {
                var entity=host.Observation.Entities.Find(item=>item.Id==id);
                host.Issue(CommandKind.Patrol,new[]{id},new SimPoint(entity.Position.X+3000,entity.Position.Z));
            }
            host.Interaction.Selection.Add(marineIds[0]);
            DiscoverAllocations();
            double warmupDeadline=Time.realtimeSinceStartupAsDouble+30;
            double warmupStarted=Time.realtimeSinceStartupAsDouble;
            int renderStart=cameraProbe.RenderedFrames,guiStart=repaints;
            while(Time.realtimeSinceStartupAsDouble-warmupStarted<5||cameraProbe.RenderedFrames-renderStart<90||repaints-guiStart<90)
            {
                evidence.WarmupCameraFrames=cameraProbe.RenderedFrames-renderStart;
                evidence.WarmupGuiRepaints=repaints-guiStart;
                evidence.WarmupTick=host.LiveTick;
                evidence.WarmupMaxAdvanceMs=Math.Max(evidence.WarmupMaxAdvanceMs,host.DiagnosticAdvanceMs);
                Require(Time.realtimeSinceStartupAsDouble<warmupDeadline,"Session graphical warmup timed out."); yield return null;
            }
            evidence.WarmupSeconds=Time.realtimeSinceStartupAsDouble-warmupStarted;
            Require(HudIconAtlas.IsReady,"Generated UI icon atlas is missing in player.");
            foreach(bool selectAll in new[]{false,true})
            {
                if(selectAll)host.Interaction.SelectAllArmy();
                yield return null;
                frames.Clear(); allocations.Clear(); advanceTimes.Clear(); cpuTimes.Clear(); gpuTimes.Clear(); draws.Clear(); workerTimes.Clear();
                var phase=new PhaseEvidence { SelectionCount=host.Interaction.Selection.Count, StartTick=host.LiveTick };
                evidence.Phases.Add(phase);
                int renders=cameraProbe.RenderedFrames,gui=repaints;
                int lastPublishedTick=host.LiveTick;
                double sampleStarted=Time.realtimeSinceStartupAsDouble, previous=sampleStarted;
                while(Time.realtimeSinceStartupAsDouble-sampleStarted<15||frames.Count<180)
                {
                    Require(frames.Count<frames.Capacity,"Session sample preallocation exceeded.");
                    yield return null;
                    double now=Time.realtimeSinceStartupAsDouble;
                    double delta=now-previous;
                    frames.Add(delta*1000); previous=now;
                    advanceTimes.Add(host.DiagnosticAdvanceMs);
                    if(host.LiveTick!=lastPublishedTick)
                    {
                        lastPublishedTick=host.LiveTick;
                        Require(workerTimes.Count<workerTimes.Capacity,"Worker timing preallocation exceeded.");
                        workerTimes.Add(host.DiagnosticWorkerMs);
                    }
                    if(host.SimulationPending)phase.PendingSimulationFrames++;
                    if(delta>.25)phase.ClampedFrames++;
                    if(Application.isFocused)phase.FocusedFrames++;else phase.UnfocusedFrames++;
                    if(allocationRecorder.Valid&&allocationRecorder.Count>0)allocations.Add(allocationRecorder.LastValue);
                    if(cpuRecorder.Valid&&cpuRecorder.Count>0)cpuTimes.Add(cpuRecorder.LastValue*1e-6);
                    if(gpuRecorder.Valid&&gpuRecorder.Count>0&&gpuRecorder.LastValue>0)gpuTimes.Add(gpuRecorder.LastValue*1e-6);
                    if(drawRecorder.Valid&&drawRecorder.Count>0)draws.Add(drawRecorder.LastValue);
                    if(host.Mode!=FrontendMode.Playing)throw new InvalidOperationException("Session ceased playing during sample.");
                }
                phase.SampleSeconds=Time.realtimeSinceStartupAsDouble-sampleStarted;
                phase.EndTick=host.LiveTick; phase.SampleFrames=frames.Count;
                phase.CameraFrames=cameraProbe.RenderedFrames-renders; phase.GuiRepaints=repaints-gui;
                phase.FrameP50Ms=Percentile(frames,.5); phase.FrameP95Ms=Percentile(frames,.95); phase.FrameP99Ms=Percentile(frames,.99); phase.FrameMaxMs=Percentile(frames,1);
                phase.WallFrameSamples=frames.ToArray();
                phase.AdvanceP95Ms=Percentile(advanceTimes,.95); phase.AdvanceMaxMs=Percentile(advanceTimes,1);
                if(workerTimes.Count>0) { phase.WorkerComputeP95Ms=Percentile(workerTimes,.95); phase.WorkerComputeMaxMs=Percentile(workerTimes,1);phase.WorkerBatches=workerTimes.Count; }
                phase.NativeCpuAvailable=cpuTimes.Count>0; phase.NativeGpuAvailable=gpuTimes.Count>0; phase.NativeDrawCallsAvailable=draws.Count>0;
                if(phase.NativeCpuAvailable)phase.CpuP95Ms=Percentile(cpuTimes,.95);
                if(phase.NativeGpuAvailable)phase.GpuP95Ms=Percentile(gpuTimes,.95);
                if(phase.NativeDrawCallsAvailable)phase.DrawCallsP50=Percentile(draws,.5);
                phase.NativeGcAvailable=allocations.Count>0;
                if(phase.NativeGcAvailable) { phase.GcP50Bytes=Percentile(allocations,.5); phase.GcP95Bytes=Percentile(allocations,.95); }
                phase.Meets60FpsP95=phase.FrameP95Ms<=1000.0/60;
                foreach(var entity in host.Observation.Entities)
                    if(marineIds.Contains(entity.Id))
                    {
                        phase.ObservedMarines++;
                        if(entity.Orders.Count>0&&entity.Orders[0].Kind==OrderKind.Patrol)phase.PatrollingMarines++;
                        if(entity.LastAttackTick>=phase.StartTick)phase.AttackingMarines++;
                        var point=host.GameCamera.WorldToViewportPoint(BattleView.Position(entity.Position)+Vector3.up*.6f);
                        if(point.z>0&&point.x>0&&point.x<1&&point.y>RtsInteraction.HudHeight/Screen.height&&point.y<1-62f/Screen.height)phase.VisibleMarineCenters++;
                    }
                Require(phase.CameraFrames>=phase.SampleFrames&&phase.GuiRepaints>=phase.SampleFrames,"Session samples lack actual camera/GUI frames.");
                Require(phase.ObservedMarines==MarineCount&&phase.PatrollingMarines==MarineCount,"Session lost army or active patrol workload.");
                Require(phase.AttackingMarines==0,"Patrol-only fixture drifted into combat.");
                Require(phase.VisibleMarineCenters==MarineCount,"Not all 800 marine centers were within the unobscured world viewport.");
                Require(phase.EndTick-phase.StartTick>=phase.SampleSeconds*host.Rules.TickRate-3,"Simulation did not sustain its real-time tick rate.");
                var hud=host.GetComponent<BattleHud>();
                phase.HealthBars=hud.WorldHealthBarCount; phase.HealthBarDrawCalls=hud.WorldHealthBarDrawCalls;
                Require(phase.HealthBars>0&&phase.HealthBarDrawCalls==1,"Observed world health bars were not drawn by their actual batched path.");
                string screenshot=Path.Combine(directory,selectAll?"session-selected800.png":"session-selected1.png");
                if(File.Exists(screenshot))File.Delete(screenshot);
                yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(screenshot);
                double captureDeadline=Time.realtimeSinceStartupAsDouble+10;
                long previousLength=-1; int stable=0;
                while(stable<2)
                {
                    Require(Time.realtimeSinceStartupAsDouble<captureDeadline,"Session screenshot did not finish writing.");
                    long length=File.Exists(screenshot)?new FileInfo(screenshot).Length:0;
                    stable=length>1024&&length==previousLength?stable+1:0; previousLength=length;
                    yield return null;
                }
                var captured=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try { Require(ImageConversion.LoadImage(captured,File.ReadAllBytes(screenshot),true)&&captured.width==1600&&captured.height==900,"Session screenshot is not a complete 1600x900 image."); }
                finally { Destroy(captured); }
                WriteEvidence();
            }
            host.Interaction.Selection.Clear(); host.Interaction.Selection.Add(marineIds[0]);
            yield return null; yield return new WaitForEndOfFrame();
            var finalHud=host.GetComponent<BattleHud>();
            Require(finalHud.WorldHealthBarCount==1&&finalHud.WorldHealthBarDrawCalls==1,"Health mesh did not shrink from the full army to one bar.");
            evidence.SelectionShrinkValidated=true;
            host.Interaction.Selection.Clear();
            yield return null; yield return new WaitForEndOfFrame();
            Require(finalHud.WorldHealthBarCount==0&&finalHud.WorldHealthBarDrawCalls==0,"Empty selection retained stale health geometry.");
            evidence.EmptySelectionValidated=true;
            host.TogglePause();
            Require(host.Mode==FrontendMode.Paused&&!host.SimulationPending,"Pause did not drain the simulation worker.");
            evidence.WorkerPauseValidated=true;
            Require(evidence.RuntimeErrors.Count==0,"Runtime errors occurred during session profile.");
            Finish(true);
        }

        void DiscoverAllocations()
        {
            var handles=new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            foreach(var handle in handles)
            {
                var description=ProfilerRecorderHandle.GetDescription(handle);
                if(description.Name=="GC Allocated In Frame")allocationRecorder=StartedRecorder(handle);
                if(description.Name=="CPU Main Thread Frame Time"&&description.UnitType.ToString()=="TimeNanoseconds")cpuRecorder=StartedRecorder(handle);
                if(description.Name=="GPU Frame Time"&&description.UnitType.ToString()=="TimeNanoseconds")gpuRecorder=StartedRecorder(handle);
                if(description.Name=="Draw Calls Count")drawRecorder=StartedRecorder(handle);
                if(description.UnitType.ToString()=="TimeNanoseconds"&&description.Name=="GC.Collect")gcCollectRecorder=StartedRecorder(handle);
                if(description.UnitType.ToString()=="TimeNanoseconds"&&description.Name=="GarbageCollector.CollectIncremental")gcIncrementalRecorder=StartedRecorder(handle);
            }
        }
        static ProfilerRecorder StartedRecorder(ProfilerRecorderHandle handle)
        { var recorder=new ProfilerRecorder(handle,1,ProfilerRecorderOptions.Default); if(recorder.Valid)recorder.Start();return recorder; }
        static double Percentile(List<double> source,double fraction)
        { var sorted=source.ToArray(); Array.Sort(sorted); return sorted[Math.Max(0,(int)Math.Ceiling(sorted.Length*fraction)-1)]; }
        void OnGUI() { if(Event.current.type==EventType.Repaint&&lastRepaint!=Time.frameCount) { lastRepaint=Time.frameCount;repaints++; } }
        void Update() { if(!finished&&started>0&&Time.realtimeSinceStartupAsDouble-started>200) { evidence.Error="Session profile watchdog."; Finish(false); } }
        void OnLog(string message,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)evidence.RuntimeErrors.Add(message+"\n"+stack); }
        void Finish(bool passed)
        {
            if(finished)return; finished=true; evidence.Status=passed?"passed":"failed";
            evidence.FinishedUtc=DateTime.UtcNow.ToString("O");
            int exitCode=passed?0:1;
            try { if(directory!=null) { Directory.CreateDirectory(directory); WriteEvidence(); } }
            catch(Exception exception) { Debug.LogError("Could not write session profile evidence: "+exception.Message); exitCode=1; }
            finally { Application.Quit(exitCode); }
        }
        void WriteEvidence() => File.WriteAllText(Path.Combine(directory,"session-profile.json"),JsonUtility.ToJson(evidence,true),new UTF8Encoding(false));
        static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        [Serializable] sealed class Evidence
        {
            public string Status,StartedUtc,FinishedUtc,Error,UnityVersion,Cpu,Gpu,GraphicsApi;
            public bool DevelopmentBuild;
            public double WarmupSeconds;
            public int WarmupCameraFrames,WarmupGuiRepaints,WarmupTick;
            public double WarmupMaxAdvanceMs;
            public string SimulationThreading="Single-owner worker with detached frame publication at20Hz; main-thread time excludes worker compute, reported separately. Tick throughput must remain real-time.";
            public bool SelectionShrinkValidated,EmptySelectionValidated,WorkerPauseValidated;
            public string Scope="Actual GameHost 20Hz, BattleView, full fog/HUD/minimap/input; original unit definitions, synthetic initial +800 marines, cleared terrain, 48m product zoom, 1600x900 4xMSAA soft shadows. Enemy AI disabled only in this opt-in fixture to preserve 800 patrols; combat and a complete game are not measured.";
            public List<PhaseEvidence> Phases=new List<PhaseEvidence>();
            public bool CombatRequested;
            public bool MovingAiRequested;
            public bool NativeCpuProbeAvailable,IncrementalGcEnabled;
            public string GcMode;
            public long IncrementalGcSliceNanoseconds;
            public int LogicalProcessors,JobWorkers,MaximumJobWorkers,RequestedJobWorkers;
            public string NativeCpuProbeScope="Windows main-thread cycle deltas between coroutine resumes (frequency dependent, not converted to milliseconds); process CPU sums all threads and can exceed wall time. Corroboration only, not proof of OS preemption. Native GC markers are separate previous-completed-frame durations and must not be summed if nested.";
            public int CombatEndTick;
            public int VolleyTick,VolleyLiveMarines;
            public bool VolleyCaptured;
            public int Width,Height,Msaa;
            public float ShadowDistance;
            public string QualityLevel;
            public bool InstancingSupported;
            public List<CombatTrialEvidence> CombatTrials=new List<CombatTrialEvidence>();
            public List<string> RuntimeErrors=new List<string>();
        }
        [Serializable] sealed class PhaseEvidence
        {
            public int SelectionCount,StartTick,EndTick,SampleFrames,CameraFrames,GuiRepaints,FocusedFrames,UnfocusedFrames,ClampedFrames,ObservedMarines,PatrollingMarines,VisibleMarineCenters,AttackingMarines;
            public double SampleSeconds,FrameP50Ms,FrameP95Ms,FrameP99Ms,FrameMaxMs,GcP50Bytes,GcP95Bytes;
            public double AdvanceP95Ms,AdvanceMaxMs;
            public double WorkerComputeP95Ms,WorkerComputeMaxMs;
            public int WorkerBatches,PendingSimulationFrames,HealthBars,HealthBarDrawCalls;
            public double CpuP95Ms,GpuP95Ms,DrawCallsP50;
            public bool NativeCpuAvailable,NativeGpuAvailable,NativeDrawCallsAvailable;
            public bool Meets60FpsP95,NativeGcAvailable;
            public double[] WallFrameSamples;
        }
    }
}
