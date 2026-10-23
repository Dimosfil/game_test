using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using ColonyRTS.Diagnostics;
using ColonyRTS.Presentation;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ColonyRTS
{
    public sealed class RenderingProfile : MonoBehaviour
    {
        const int WarmupFrames = 90;
        const int SampleFrames = 180;
        readonly ProfileEvidence evidence = new ProfileEvidence();
        readonly List<Counter> counters = new List<Counter>();
        readonly GUIContent overlay = new GUIContent();
        GameObject[] marines;
        GraphicsSimulationFixture simulation;
        Transform caseRoot;
        Camera profileCamera;
        RenderingProfileCameraProbe cameraProbe;
        Scene profileScene;
        GUIStyle overlayStyle;
        string outputDirectory;
        double startedAt;
        bool initialized, finished;
        int repaintCount;
        int lastRepaintFrame = -1;

        public static void StartProfile(GameObject host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (host.GetComponent<RenderingProfile>() == null) host.AddComponent<RenderingProfile>();
        }

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; DisposeCounters(); }

        IEnumerator Start()
        {
            var workflow = Run();
            while (!finished)
            {
                bool more = false; object wait = null; Exception failure = null;
                try { more = workflow.MoveNext(); if (more) wait = workflow.Current; }
                catch (Exception exception) { failure = exception; }
                if (failure != null) { Fail(failure); yield break; }
                if (!more) yield break;
                yield return wait;
            }
        }

        IEnumerator Run()
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            evidence.StartedUtc = DateTime.UtcNow.ToString("O");
            evidence.Status = "running";
            ParseArguments();
            Directory.CreateDirectory(outputDirectory);
            initialized = true;
            WriteEvidence();
            Require(!Application.isEditor && !Application.isBatchMode, "Rendering profile requires the ordinary graphical standalone player.");
            if (evidence.SimulationRequested)
                evidence.Scope = "combined: identical static rendered marine coverage plus independent traveling 20Hz Match/Observe fixture; not a full product session";
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            for (int i = 0; i < 4; i++) yield return null;
            Require(Screen.width == 1600 && Screen.height == 900, "The requested 1600x900 viewport was not established.");
            SetupScene();
            overlay.text = "RENDER PROFILE | waiting for the graphical window\nRequired: camera render + IMGUI repaint + two-second initial settle";
            double settleStart = Time.realtimeSinceStartupAsDouble;
            double settleDeadline = settleStart + 15;
            int settleRepaints = repaintCount, settleRenders = cameraProbe.RenderedFrames;
            while (Time.realtimeSinceStartupAsDouble - settleStart < 2 || repaintCount <= settleRepaints || cameraProbe.RenderedFrames <= settleRenders)
            {
                Require(Time.realtimeSinceStartupAsDouble < settleDeadline,
                    "Graphical window did not establish camera rendering and IMGUI repaint within 15 seconds.");
                yield return null;
            }
            evidence.InitialSettleSeconds = Time.realtimeSinceStartupAsDouble - settleStart;
            evidence.InitialCameraFrames = cameraProbe.RenderedFrames - settleRenders;
            evidence.InitialGuiRepaints = repaintCount - settleRepaints;
            DiscoverCounters();
            evidence.Hardware = ReadHardware();
            WriteEvidence();

            foreach (int count in new[] { 200, 400, 800 })
            {
                var result = new CaseEvidence { MarineCount = count, WarmupFrames = WarmupFrames, RequestedSamples = SampleFrames };
                evidence.Cases.Add(result);
                string scope = (evidence.SimulationRequested ? "RENDER + SIM 20HZ " : "RENDER-ONLY ") + evidence.Mode.ToUpperInvariant();
                overlay.text = scope + " | " + count + " animated marines\nPreparing assets; independent fixed workload";
                double constructionStart = Time.realtimeSinceStartupAsDouble;
                result.MeshMode = evidence.Mode == "baseline" ? "Unique" : "Shared";
                result.SharedMeshesBefore = ProceduralArt.LiveSharedMeshCount;
                result.SharedReferencesBefore = ProceduralArt.LiveSharedMeshReferences;
                CreateMarines(count);
                if (evidence.SimulationRequested) simulation = new GraphicsSimulationFixture(count);
                result.CreationMilliseconds = (Time.realtimeSinceStartupAsDouble - constructionStart) * 1000;
                CountGeometry(result);
                result.SharedMeshesDuring = ProceduralArt.LiveSharedMeshCount;
                result.SharedReferencesDuring = ProceduralArt.LiveSharedMeshReferences;
                overlay.text = scope + " | " + count + " animated marines | " + result.RendererCount + " renderers\nWarmup: 90 frames | 1600x900 | VSync OFF | no frame cap";
                int warmupRenderStart = cameraProbe.RenderedFrames, warmupGuiStart = repaintCount;
                double warmupDeadline = Time.realtimeSinceStartupAsDouble + 45;
                while (cameraProbe.RenderedFrames - warmupRenderStart < WarmupFrames || repaintCount - warmupGuiStart < WarmupFrames)
                {
                    Require(Time.realtimeSinceStartupAsDouble < warmupDeadline,
                        "Warmup did not complete 90 actual camera/IMGUI frames within 45 seconds.");
                    yield return null;
                }
                result.WarmupRenderedFrames = cameraProbe.RenderedFrames - warmupRenderStart;
                result.WarmupGuiRepaints = repaintCount - warmupGuiStart;
                Require(Camera.allCamerasCount == 1 && profileCamera.isActiveAndEnabled, "Benchmark has an unexpected camera workload.");
                ValidateFraming(result);
                int sampleCapacity = SampleFrames + (int)Math.Ceiling(evidence.MinimumSampleSeconds * 2000);
                foreach (var counter in counters) counter.ResetSamples(sampleCapacity);
                var wallFrames = new List<double>(sampleCapacity);
                var mainThreadAllocatedBytes = new List<double>(sampleCapacity);
                bool allocationAvailable = true;
                string allocationUnavailable = "";
                long previousAllocated = 0;
                // Some player runtimes expose this API but return a non-producing
                // zero counter. Validate a known allocation outside the timed interval.
                try
                {
                    long beforeProbe = GC.GetAllocatedBytesForCurrentThread();
                    var allocationProbe = new byte[4096];
                    long afterProbe = GC.GetAllocatedBytesForCurrentThread();
                    GC.KeepAlive(allocationProbe);
                    if (afterProbe < beforeProbe || afterProbe - beforeProbe < 4096)
                    {
                        allocationAvailable = false;
                        allocationUnavailable = "Controlled allocation probe did not produce bytes: current-thread counter is non-producing or nonmonotonic on this runtime.";
                    }
                }
                catch (Exception exception) { allocationAvailable = false; allocationUnavailable = "System.GC current-thread counter unavailable: " + exception.Message; }
                result.GcCollectionsBefore = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                result.ManagedHeapBeforeBytes = GC.GetTotalMemory(false);
                overlay.text = scope + " | " + count + " animated marines | " + result.RendererCount + " renderers\nSampling: 180 frames | wall-frame + available native counters | fixed pixel coverage";
                // Discard the overlay text replacement and transition frame from the timed region.
                yield return null;
                if (simulation != null) { simulation.BeginSamples(); result.SimulationStartTick = simulation.TickCount; }
                int sampleRenderStart = cameraProbe.RenderedFrames, sampleGuiStart = repaintCount;
                double previous = Time.realtimeSinceStartupAsDouble;
                double sampleStarted = previous;
                try { if (allocationAvailable) previousAllocated = GC.GetAllocatedBytesForCurrentThread(); }
                catch (Exception exception) { allocationAvailable = false; allocationUnavailable = "System.GC current-thread counter unavailable: " + exception.Message; }
                for (int frame = 0; frame < SampleFrames || Time.realtimeSinceStartupAsDouble - sampleStarted < evidence.MinimumSampleSeconds; frame++)
                {
                    Require(frame < sampleCapacity, "Sample preallocation exceeded; no timed buffer growth is permitted.");
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    wallFrames.Add((now - previous) * 1000);
                    previous = now;
                    if (allocationAvailable)
                    {
                        try
                        {
                            long allocated = GC.GetAllocatedBytesForCurrentThread();
                            if (allocated < previousAllocated)
                            {
                                allocationAvailable = false;
                                allocationUnavailable = "System.GC current-thread allocation counter was not monotonic.";
                                mainThreadAllocatedBytes.Clear();
                            }
                            else mainThreadAllocatedBytes.Add(allocated - previousAllocated);
                            previousAllocated = allocated;
                        }
                        catch (Exception exception)
                        {
                            allocationAvailable = false;
                            allocationUnavailable = "System.GC current-thread counter unavailable: " + exception.Message;
                            mainThreadAllocatedBytes.Clear();
                        }
                    }
                    foreach (var counter in counters) counter.Sample();
                    if (Application.isFocused) result.FocusedFrames++; else result.UnfocusedFrames++;
                }
                result.SampleRenderedFrames = cameraProbe.RenderedFrames - sampleRenderStart;
                result.SampleGuiRepaints = repaintCount - sampleGuiStart;
                result.SampleSeconds = Time.realtimeSinceStartupAsDouble - sampleStarted;
                Require(result.SampleRenderedFrames >= wallFrames.Count && result.SampleGuiRepaints >= wallFrames.Count,
                    "Timed updates were not backed by 180 actual camera renders and IMGUI repaints; the window may be hidden or minimized.");
                result.WallFrameMs = Summarize("Update-to-Update wall frame", "milliseconds", wallFrames, "");
                foreach (double milliseconds in wallFrames) if (milliseconds > 1000.0 / 60) result.Over60FpsBudgetFrames++;
                result.Meets60FpsP95 = result.WallFrameMs.P95 <= 1000.0 / 60;
                result.ManagedHeapAfterBytes = GC.GetTotalMemory(false);
                result.GcCollectionsAfter = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                foreach (var counter in counters) result.Counters.Add(counter.Summary());
                result.Counters.Add(Summarize("Main-thread managed allocated bytes/frame (System.GC current-thread delta)",
                    "bytes/frame (main thread only)",mainThreadAllocatedBytes,allocationUnavailable));
                if (simulation != null)
                {
                    result.SimulationEnabled = true;
                    result.SimulationEndTick = simulation.TickCount;
                    result.FirstGroupCommandStepMs = simulation.FirstGroupCommandStepMs;
                    result.GroupCommandCount = simulation.GroupCommandCount;
                    result.DiscardedCatchupFrames = simulation.DiscardedCatchupFrames;
                    result.SimulationStepMs = Summarize("20Hz Match.Step + detached observation","milliseconds",simulation.SampleStepTimes,
                        "No simulation step completed in the sampling interval.");
                    Require(result.GroupCommandCount > 0 && result.SimulationStepMs.Available,
                        "Combined fixture did not reach its group command and sampled simulation steps.");
                    if (result.DiscardedCatchupFrames > 0) result.Warnings.Add("Simulation frame deltas were clamped; real-time throughput was not sustained on every frame.");
                    simulation = null;
                }
                if (result.UnfocusedFrames > 0) result.Warnings.Add("Some timed frames were unfocused; repeat in foreground before comparing performance.");
                overlay.text = scope + " | " + count + " animated marines | " + result.RendererCount + " renderers\nWall frame p50 " + result.WallFrameMs.P50.ToString("F2") + " ms | p95 " + result.WallFrameMs.P95.ToString("F2") + " ms | p99 " + result.WallFrameMs.P99.ToString("F2") + " ms";
                string screenshot = Path.Combine(outputDirectory, "marines-" + count + ".png");
                if (File.Exists(screenshot)) File.Delete(screenshot);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(screenshot);
                double screenshotDeadline = Time.realtimeSinceStartupAsDouble + 12;
                long previousLength = -1; int stableFrames = 0;
                while (stableFrames < 2)
                {
                    Require(Time.realtimeSinceStartupAsDouble < screenshotDeadline, "Screenshot was not written: " + screenshot);
                    long length = ScreenshotLength(screenshot);
                    stableFrames = length > 1024 && length == previousLength ? stableFrames + 1 : 0;
                    previousLength = length;
                    yield return null;
                }
                ValidateScreenshot(screenshot);
                result.Screenshot = screenshot;
                result.CompletedSamples = wallFrames.Count;
                result.Status = "measured";
                WriteEvidence();
                marines = null;
                Destroy(caseRoot.gameObject);
                caseRoot = null;
                yield return null;
                yield return null;
                result.SharedMeshesAfterCleanup = ProceduralArt.LiveSharedMeshCount;
                result.SharedReferencesAfterCleanup = ProceduralArt.LiveSharedMeshReferences;
                Require(result.SharedMeshesAfterCleanup == result.SharedMeshesBefore && result.SharedReferencesAfterCleanup == result.SharedReferencesBefore,
                    "Shared procedural mesh leases were not released after case destruction.");
                WriteEvidence();
                // All cleanup is outside the measured interval and before the next 90-frame warmup.
                GC.Collect();
                yield return null;
            }
            Require(evidence.RuntimeErrors.Count == 0, "Unity logged runtime errors during profiling.");
            evidence.Status = "passed";
            Finish(0);
        }

        void ParseArguments()
        {
            var directory = new DirectoryInfo(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            DirectoryInfo builds = directory;
            while (builds != null && !string.Equals(builds.Name, "Builds", StringComparison.OrdinalIgnoreCase)) builds = builds.Parent;
            string allowedRoot = builds != null ? builds.FullName : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
            outputDirectory = Path.Combine(allowedRoot, "Evidence", "Rendering", "baseline");
            bool explicitOutput = false;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-rtsProfileSimulation") evidence.SimulationRequested = true;
                if (args[i] != "-rtsEvidencePath" && args[i] != "-rtsProfileMode" && args[i] != "-rtsProfileBackend" && args[i] != "-rtsProfileSeconds") continue;
                string option = args[i];
                Require(i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal), option + " requires a value.");
                string value = args[++i];
                if (option == "-rtsProfileMode")
                {
                    Require(value == "baseline" || value == "optimized", "Profile mode must be baseline or optimized.");
                    evidence.Mode = value;
                }
                else if (option == "-rtsProfileBackend")
                {
                    Require(value == "legacy" || value == "instanced", "Profile backend must be legacy or instanced.");
                    evidence.Backend = value;
                }
                else if (option == "-rtsProfileSeconds")
                {
                    Require(double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds >= 0 && seconds <= 30,
                        "Profile seconds must be in [0,30].");
                    evidence.MinimumSampleSeconds = seconds;
                }
                else
                {
                    Require(Path.IsPathRooted(value), "Profile evidence path must be absolute.");
                    string normalized = Path.GetFullPath(value);
                    Require(normalized.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                        "Profile output must stay below this checkout's ignored Builds directory.");
                    outputDirectory = normalized;
                    explicitOutput = true;
                }
            }
            if (!explicitOutput)
                outputDirectory = Path.Combine(allowedRoot,"Evidence","Rendering",evidence.Mode+(evidence.SimulationRequested?"-simulation":""));
            Require(evidence.Backend == "legacy" || evidence.Mode == "optimized", "Instanced backend requires shared optimized mesh mode.");
            if (evidence.Backend == "instanced")
                Require(SystemInfo.supportsInstancing, "This GPU cannot run the requested instanced backend; fallback is not a successful paired profile.");
        }

        void SetupScene()
        {
            Require(Camera.allCamerasCount == 0, "Normal gameplay cameras must not be created before StartProfile.");
            profileScene = SceneManager.CreateScene("RTS Render Profile", new CreateSceneParameters(LocalPhysicsMode.None));
            SceneManager.SetActiveScene(profileScene);
            profileCamera = new GameObject("Profile Camera", typeof(Camera)).GetComponent<Camera>();
            cameraProbe = profileCamera.gameObject.AddComponent<RenderingProfileCameraProbe>();
            profileCamera.tag = "MainCamera";
            profileCamera.transform.position = new Vector3(0, 55, -35.75f);
            profileCamera.transform.LookAt(Vector3.zero);
            profileCamera.fieldOfView = 43;
            profileCamera.nearClipPlane = .2f;
            profileCamera.farClipPlane = 180;
            profileCamera.backgroundColor = new Color(.025f,.035f,.055f);
            profileCamera.clearFlags = CameraClearFlags.SolidColor;
            profileCamera.allowHDR = true;
            profileCamera.allowMSAA = true;
            profileCamera.allowDynamicResolution = false;
            var sun = new GameObject("Profile Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.45f; sun.color = new Color(1,.86f,.67f);
            sun.transform.rotation = Quaternion.Euler(48,-32,0);
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .72f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.35f,.45f,.55f);
            RenderSettings.ambientEquatorColor = new Color(.22f,.29f,.35f);
            RenderSettings.ambientGroundColor = new Color(.12f,.1f,.08f);
            RenderSettings.fog = false;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 100;
            QualitySettings.antiAliasing = 4;
            var ground = new GameObject("Profile Ground");
            var builder = new ArtBuilder(ground.transform,"Floor",evidence.Mode=="baseline"?ProceduralMeshMode.Unique:ProceduralMeshMode.Shared);
            builder.Box(ArtPalette.Get("profile-floor",new Color(.22f,.235f,.23f)),new Vector3(0,-.12f,0),new Vector3(90,.24f,60));
            builder.Finish();
        }

        void CreateMarines(int count)
        {
            caseRoot = new GameObject("Case " + count).transform;
            marines = new GameObject[count];
            const int columns = 40;
            int rows = Mathf.CeilToInt(count / (float)columns);
            for (int i = 0; i < count; i++)
            {
                var marine = evidence.Backend == "instanced"
                    ? ProceduralArt.CreateInstanced("marine",i % 2,caseRoot)
                    : ProceduralArt.CreateForProfile("marine",i % 2,caseRoot,
                        evidence.Mode=="baseline"?ProceduralMeshMode.Unique:ProceduralMeshMode.Shared);
                marine.transform.position = new Vector3((i % columns - (columns - 1) * .5f) * 1.5f,0,(i / columns - (rows - 1) * .5f) * 1.5f);
                marines[i] = marine;
            }
        }

        void CountGeometry(CaseEvidence result)
        {
            var inventory = new ArtRenderInventory();
            foreach(var marine in marines) ProceduralArt.AccumulateRenderInventory(marine, inventory);
            result.LogicalRendererCount = inventory.LogicalRendererCount;
            result.ActiveLogicalRendererCount = inventory.ActiveLogicalRendererCount;
            result.SummedVertices = inventory.SummedVertices;
            result.SummedTriangles = inventory.SummedTriangles;
            result.UniqueMeshCount = inventory.UniqueMeshCount;
            result.SharedMaterialCount = inventory.SharedMaterialCount;
            foreach (var renderer in caseRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                result.RendererCount++;
                if (renderer.gameObject.activeInHierarchy) result.ActiveRenderers++;
            }
            result.TransformCount = caseRoot.GetComponentsInChildren<Transform>(true).Length;
        }

        void ValidateFraming(CaseEvidence result)
        {
            foreach (var marine in marines)
            {
                var point = profileCamera.WorldToViewportPoint(marine.transform.position + Vector3.up * .6f);
                if (point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1) result.VisibleMarineCenters++;
            }
            Require(result.VisibleMarineCenters == result.MarineCount, "The fixed benchmark camera does not frame all marine centers.");
        }

        void DiscoverCounters()
        {
            var available = new Dictionary<string,ProfilerRecorderHandle>(StringComparer.Ordinal);
            var handles = new List<ProfilerRecorderHandle>();
            try
            {
                ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (!available.ContainsKey(description.Name)) available.Add(description.Name,handle);
                }
            }
            catch (Exception exception) { evidence.CounterDiscoveryNote = exception.Message; }
            counters.Add(new Counter("Total Used Memory","bytes",available));
            counters.Add(new Counter("GC Used Memory","bytes",available));
            counters.Add(new Counter("GC Reserved Memory","bytes",available));
            counters.Add(new Counter("GC Allocated In Frame","bytes/frame",available));
            counters.Add(new Counter("Draw Calls Count","count/frame",available));
            counters.Add(new Counter("Batches Count","count/frame",available));
            counters.Add(new Counter("SetPass Calls Count","count/frame",available));
            counters.Add(new Counter("Triangles Count","count/frame",available));
            counters.Add(new Counter("Main Thread","milliseconds",available,1e-6));
            counters.Add(new Counter("CPU Main Thread Frame Time","milliseconds",available,1e-6));
            counters.Add(new Counter("CPU Render Thread Frame Time","milliseconds",available,1e-6));
            counters.Add(new Counter("GPU Frame Time","milliseconds",available,1e-6,true));
        }

        void Update()
        {
            if (finished) return;
            if (initialized && Time.realtimeSinceStartupAsDouble - startedAt > 180)
            { Fail(new TimeoutException("Rendering profile exceeded the 180-second watchdog.")); return; }
            if (marines == null) return;
            float time = Time.unscaledTime;
            try
            {
                if (simulation != null) simulation.Advance(Time.unscaledDeltaTime);
                for (int i = 0; i < marines.Length; i++) ProceduralArt.Animate(marines[i],"marine",true,false,time+i*.17f);
            }
            catch (Exception exception) { Fail(exception); }
        }

        void OnGUI()
        {
            if (overlayStyle == null) overlayStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
            GUI.Box(new Rect(12,12,1040,66),GUIContent.none);
            GUI.Label(new Rect(24,20,1010,55),overlay,overlayStyle);
            if (Event.current.type == EventType.Repaint && Time.frameCount != lastRepaintFrame)
            { lastRepaintFrame = Time.frameCount; repaintCount++; }
        }

        HardwareEvidence ReadHardware() => new HardwareEvidence
        {
            UnityVersion = Application.unityVersion, ProductVersion = Application.version, DevelopmentBuild = Debug.isDebugBuild,
            OperatingSystem = SystemInfo.operatingSystem, Cpu = SystemInfo.processorType, CpuCores = SystemInfo.processorCount,
            SystemMemoryMb = SystemInfo.systemMemorySize, Gpu = SystemInfo.graphicsDeviceName, GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            GraphicsVersion = SystemInfo.graphicsDeviceVersion, GraphicsMemoryMb = SystemInfo.graphicsMemorySize, SupportsInstancing = SystemInfo.supportsInstancing,
            Width = Screen.width, Height = Screen.height, VSyncCount = QualitySettings.vSyncCount, TargetFrameRate = Application.targetFrameRate,
            QualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()], Msaa = QualitySettings.antiAliasing,
            ShadowDistance = QualitySettings.shadowDistance, ShadowResolution = QualitySettings.shadowResolution.ToString(),
            RenderPipeline = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.name
        };

        static MetricEvidence Summarize(string name,string unit,List<double> values,string unavailable)
        {
            var result = new MetricEvidence { Name = name, Unit = unit, Available = values.Count > 0, Samples = values.Count,
                UnavailableReason = values.Count > 0 ? "" : unavailable, Values = values.ToArray() };
            if (values.Count == 0) return result;
            var sorted = values.ToArray(); Array.Sort(sorted);
            double sum = 0; foreach (double value in sorted) sum += value;
            result.Min = sorted[0]; result.Max = sorted[sorted.Length-1]; result.Mean = sum / sorted.Length;
            result.P50 = sorted[Math.Max(0,(int)Math.Ceiling(sorted.Length*.5)-1)];
            result.P95 = sorted[Math.Max(0,(int)Math.Ceiling(sorted.Length*.95)-1)];
            result.P99 = sorted[Math.Max(0,(int)Math.Ceiling(sorted.Length*.99)-1)];
            return result;
        }

        static long ScreenshotLength(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : -1; }
            catch (IOException) { return -1; }
        }
        static void ValidateScreenshot(string path)
        {
            var image = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try { Require(image.LoadImage(File.ReadAllBytes(path)) && image.width == 1600 && image.height == 900,"Profile screenshot is not a valid 1600x900 PNG."); }
            finally { Destroy(image); }
        }
        void OnLog(string condition,string trace,LogType type)
        {
            if (!finished && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && evidence.RuntimeErrors.Count < 16)
                evidence.RuntimeErrors.Add(condition);
        }
        void Fail(Exception exception) { if (finished) return; evidence.Status = "failed"; evidence.Error = exception.ToString(); Finish(1); }
        void Finish(int exitCode)
        {
            if (finished) return;
            finished = true;
            evidence.ExitCode = exitCode; evidence.FinishedUtc = DateTime.UtcNow.ToString("O");
            evidence.ElapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
            DisposeCounters();
            try { if (!string.IsNullOrEmpty(outputDirectory)) { Directory.CreateDirectory(outputDirectory); WriteEvidence(); } }
            catch (Exception exception) { Debug.LogError("Unable to write render profile: " + exception.Message); exitCode = 1; }
            Application.Quit(exitCode);
        }
        void DisposeCounters() { foreach (var counter in counters) counter.Dispose(); }
        void WriteEvidence() => File.WriteAllText(Path.Combine(outputDirectory,"profile.json"),JsonUtility.ToJson(evidence,true),new UTF8Encoding(false));
        static void Require(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }

        sealed class Counter : IDisposable
        {
            readonly string name,unit;
            readonly double scale;
            readonly bool requirePositive;
            readonly List<double> samples = new List<double>(SampleFrames);
            ProfilerRecorder recorder;
            string unavailable;
            string nativeCategory,nativeUnit;
            bool disposed;
            internal Counter(string name,string unit,Dictionary<string,ProfilerRecorderHandle> available,double scale=1,bool requirePositive=false)
            {
                this.name=name;this.unit=unit;this.scale=scale;this.requirePositive=requirePositive;
                if (!available.TryGetValue(name,out var handle)) { unavailable="Counter is not registered in this player/build; unavailable is not zero."; return; }
                try
                {
                    var description=ProfilerRecorderHandle.GetDescription(handle);
                    nativeCategory=description.Category.Name; nativeUnit=description.UnitType.ToString();
                    if(unit=="milliseconds"&&nativeUnit!="TimeNanoseconds")
                    { unavailable="Unexpected timing counter unit: "+nativeUnit+"; nanosecond conversion was not applied.";return; }
                    recorder=new ProfilerRecorder(handle,1,ProfilerRecorderOptions.Default);
                    if(recorder.Valid)recorder.Start();
                    else unavailable="ProfilerRecorder is invalid on this runtime.";
                }
                catch(Exception exception) { unavailable=exception.Message; }
            }
            internal void ResetSamples(int capacity) { samples.Clear(); if(samples.Capacity<capacity) samples.Capacity=capacity; }
            internal void Sample()
            {
                if(disposed||!recorder.Valid||recorder.Count==0)return;
                long value=recorder.LastValue;
                if(requirePositive&&value<=0)return;
                samples.Add(value*scale);
            }
            internal MetricEvidence Summary()
            {
                var result=Summarize(name,unit,samples,unavailable??
                    (requirePositive?"No positive GPU timing samples; runtime GPU timestamp support may be absent.":"No completed counter samples were returned."));
                result.NativeCategory=nativeCategory;result.NativeUnit=nativeUnit;return result;
            }
            public void Dispose() { if(disposed)return;disposed=true;if(recorder.Valid)recorder.Dispose(); }
        }

        [Serializable] sealed class ProfileEvidence
        {
            public string Status,StartedUtc,FinishedUtc,Error;
            public int ExitCode;
            public double ElapsedSeconds;
            public double InitialSettleSeconds;
            public int InitialCameraFrames,InitialGuiRepaints;
            public string Mode="baseline";
            public string Backend="legacy";
            public double MinimumSampleSeconds;
            public string BaselineComparison="baseline retains unique per-renderer combined meshes; optimized leases exact shared combined geometry. Camera, materials, animations, lighting, shadows and sample policy stay identical.";
            public string Scope="render-only: procedural marine hierarchy, walking animation, camera, lighting, shadows, and small diagnostics overlay; no Match, simulation, AI, fog, input, or gameplay HUD";
            public bool SimulationRequested;
            public string CounterDiscoveryNote;
            public string Interpretation="WallFrameMs includes render-thread/GPU/OS waits and animation/harness work; it is not exclusive CPU execution time. Main Thread and GPU counters are separate and optional. Native GC bytes/frame require a producing ProfilerRecorder. The separately named System.GC allocation delta counts managed bytes allocated on this main thread only, excluding other threads and native allocations; it is not a native GC total. No target-FPS or full-game acceptance is implied.";
            public HardwareEvidence Hardware;
            public List<CaseEvidence> Cases=new List<CaseEvidence>();
            public List<string> RuntimeErrors=new List<string>();
        }
        [Serializable] sealed class HardwareEvidence
        {
            public string UnityVersion,ProductVersion,OperatingSystem,Cpu,Gpu,GraphicsApi,GraphicsVersion,QualityLevel,ShadowResolution,RenderPipeline;
            public bool DevelopmentBuild,SupportsInstancing;
            public int CpuCores,SystemMemoryMb,GraphicsMemoryMb,Width,Height,VSyncCount,TargetFrameRate,Msaa;
            public float ShadowDistance;
        }
        [Serializable] sealed class CaseEvidence
        {
            public string Status="running",Screenshot,MeshMode;
            public int MarineCount,WarmupFrames,RequestedSamples,CompletedSamples,FocusedFrames,UnfocusedFrames;
            public int Over60FpsBudgetFrames;
            public bool Meets60FpsP95;
            public double SampleSeconds;
            public int WarmupRenderedFrames,WarmupGuiRepaints,SampleRenderedFrames,SampleGuiRepaints;
            public int SharedMeshesBefore,SharedReferencesBefore,SharedMeshesDuring,SharedReferencesDuring,SharedMeshesAfterCleanup,SharedReferencesAfterCleanup;
            public int RendererCount,ActiveRenderers,UniqueMeshCount,SharedMaterialCount,TransformCount,VisibleMarineCenters;
            public int LogicalRendererCount,ActiveLogicalRendererCount;
            public long SummedVertices,SummedTriangles,ManagedHeapBeforeBytes,ManagedHeapAfterBytes;
            public int[] GcCollectionsBefore,GcCollectionsAfter;
            public double CreationMilliseconds;
            public bool SimulationEnabled;
            public int SimulationStartTick,SimulationEndTick,GroupCommandCount,DiscardedCatchupFrames;
            public double FirstGroupCommandStepMs;
            public MetricEvidence SimulationStepMs;
            public MetricEvidence WallFrameMs;
            public List<MetricEvidence> Counters=new List<MetricEvidence>();
            public List<string> Warnings=new List<string>();
        }
        [Serializable] sealed class MetricEvidence
        {
            public string Name,Unit,UnavailableReason,NativeCategory,NativeUnit;
            public bool Available;
            public int Samples;
            public double Min,P50,P95,P99,Max,Mean;
            public double[] Values;
        }
    }

    // Attached to the benchmark's Built-in camera; unlike Update, this callback
    // proves that the camera submitted a rendered frame. Count each engine frame once.
    public sealed class RenderingProfileCameraProbe : MonoBehaviour
    {
        public int RenderedFrames { get; private set; }
        int lastFrame = -1;
        void OnPostRender()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount; RenderedFrames++;
        }
    }
}
