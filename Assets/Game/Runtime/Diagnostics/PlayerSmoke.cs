using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ColonyRTS.Presentation;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Diagnostics
{
    public sealed class PlayerSmoke : MonoBehaviour
    {
        readonly PlayerEvidence evidence = new PlayerEvidence();
        string evidenceDirectory;
        double startedAt;
        bool initialized;
        bool finished;
        int firstFrame;
        int guiRepaints;

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        IEnumerator Start()
        {
            // Unity does not route coroutine exceptions to a caller. Advance our own
            // staged iterator so every failure writes evidence and returns a failing exit code.
            var workflow = Run();
            while (!finished)
            {
                bool more = false;
                object wait = null;
                Exception failure = null;
                try
                {
                    more = workflow.MoveNext();
                    if (more) wait = workflow.Current;
                }
                catch (Exception exception) { failure = exception; }
                if (failure != null) { Fail(failure); yield break; }
                if (!more) yield break;
                yield return wait;
            }
        }

        IEnumerator Run()
        {
            startedAt = Time.realtimeSinceStartupAsDouble;
            firstFrame = Time.frameCount;
            evidence.StartedUtc = DateTime.UtcNow.ToString("O");
            evidence.Status = "running";
            evidenceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence"));
            string technologyFixture = null;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
                if (arguments[i] == "-rtsEvidencePath")
                {
                    Require(i + 1 < arguments.Length && !arguments[i + 1].StartsWith("-", StringComparison.Ordinal),
                        "-rtsEvidencePath requires an absolute directory argument.");
                    Require(Path.IsPathRooted(arguments[i + 1]), "-rtsEvidencePath must be absolute.");
                    evidenceDirectory = Path.GetFullPath(arguments[++i]);
                }
                else if(arguments[i] == "-rtsTechnologyFixture")
                {
                    Require(i+1<arguments.Length&&Path.IsPathRooted(arguments[i+1]),"-rtsTechnologyFixture requires an absolute JSON snapshot path.");
                    technologyFixture=Path.GetFullPath(arguments[++i]);
                }
            Directory.CreateDirectory(evidenceDirectory);
            initialized = true;
            WriteEvidence();
            Require(!Application.isEditor, "PlayerSmoke must run in the built standalone player.");
            Require(!Application.isBatchMode, "Graphical smoke requires a normal player; batch mode cannot establish rendered-frame evidence.");
            yield return null;

            var host = GameHost.Instance;
            Require(host != null && host.Rules != null && host.Match != null, "GameHost did not initialize its runtime rules and match.");
            host.SaveDirectoryOverride = Path.Combine(evidenceDirectory, "Saves");
            Directory.CreateDirectory(host.SaveDirectoryOverride);
            string saveDirectory = Path.GetFullPath(host.SaveDirectoryOverride) + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(host.SavePath).StartsWith(saveDirectory, StringComparison.OrdinalIgnoreCase),
                "Smoke save isolation did not resolve inside the evidence directory.");
            evidence.SavePath = host.SavePath;
            host.StartMatch();
            Require(HudIconAtlas.IsReady,"Generated UI icon atlas failed to load in standalone HUD.");
            evidence.Checks.Add("generated-ui-atlas-loaded-with-all-24-action-resource-technology-cells");
            Require(host.Mode == FrontendMode.Playing && host.LiveTick == 0, "StartMatch did not enter a fresh playable match.");
            evidence.InitialHash = host.Match.StateHash();
            host.SetSpeed(8);
            Require(Mathf.Approximately(host.Speed, 8), "Requested simulation speed was not applied.");
            CountRuntimeComponents(host);
            evidence.Checks.Add("real-host-start-and-camera-ui-components");

            double advanceDeadline = Time.realtimeSinceStartupAsDouble + 30;
            while (host.LiveTick < 600)
            {
                Require(Time.realtimeSinceStartupAsDouble < advanceDeadline, "Real player did not reach tick 600 within 30 seconds.");
                Require(host.Mode == FrontendMode.Playing, "Gameplay left Playing before the smoke target tick.");
                yield return null;
            }
            evidence.GameplayTicks = host.LiveTick;
            Require(host.Observation != null && host.Observation.Tick == host.LiveTick, "Runtime observation is stale.");
            host.SetSpeed(1);
            host.Interaction.FocusBase(true);
            host.Interaction.SelectAllArmy();
            evidence.SelectedArmyCount = host.Interaction.Selection.Count;
            Require(evidence.SelectedArmyCount > 0, "Product army selection did not select a visible friendly marine.");
            evidence.Checks.Add("real-update-loop-600-ticks-and-product-army-selection");

            // Save an active cyclic order so the actual viewer must reconstruct
            // non-default order state through the same product command path.
            int patrolId = host.Interaction.Selection[0];
            var patrolUnit = host.Observation.Entities.Find(entity => entity.Id == patrolId);
            host.Issue(CommandKind.Patrol, new[] { patrolId },
                new SimPoint(patrolUnit.Position.X + 4000, patrolUnit.Position.Z + 4000));
            // Issue may publish an already-running batch before queueing input.
            // Await ticks after the actual enqueue boundary, not an older UI snapshot.
            int patrolTick = host.LiveTick;
            double patrolDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while (host.LiveTick < patrolTick + 3)
            {
                Require(Time.realtimeSinceStartupAsDouble < patrolDeadline, "Product patrol command did not advance.");
                yield return null;
            }
            patrolUnit = host.Observation.Entities.Find(entity => entity.Id == patrolId);
            Require(patrolUnit != null && patrolUnit.Orders.Count > 0 && patrolUnit.Orders[0].Kind == OrderKind.Patrol,
                "Product Patrol did not become an active observed order.");
            evidence.Checks.Add("product-patrol-command-and-nondefault-order-snapshot");

            string screenshot = Path.Combine(evidenceDirectory, "gameplay.png");
            if (File.Exists(screenshot)) File.Delete(screenshot);
            yield return null;
            yield return new WaitForEndOfFrame();
            Require(guiRepaints > 0, "No IMGUI repaint was observed before the gameplay screenshot.");
            evidence.GuiRepaints = guiRepaints;
            evidence.GameplayCaptureFrame = Time.frameCount;
            evidence.ScreenWidth = Screen.width;
            evidence.ScreenHeight = Screen.height;
            ScreenCapture.CaptureScreenshot(screenshot);
            double screenshotDeadline = Time.realtimeSinceStartupAsDouble + 10;
            long lastLength = -1;
            int stableFrames = 0;
            while (stableFrames < 2)
            {
                Require(Time.realtimeSinceStartupAsDouble < screenshotDeadline, "The graphical player did not finish writing gameplay.png within 10 seconds.");
                long length = ScreenshotLength(screenshot);
                stableFrames = length > 1024 && length == lastLength ? stableFrames + 1 : 0;
                lastLength = length;
                yield return null;
            }
            ValidateScreenshot(screenshot);
            evidence.ScreenshotPath = screenshot;
            evidence.ScreenshotExists = File.Exists(screenshot);
            evidence.Checks.Add("actual-rendered-frame-png-with-visual-variation");

            var worker = host.Observation.Entities.Find(entity => entity.Owner == 0 && host.Definition(entity.DefinitionId).IsWorker);
            Require(worker != null, "The product smoke needs an own worker for the repair/build command panel.");
            host.Interaction.Selection.Clear();
            host.Interaction.Selection.Add(worker.Id);
            string workerScreenshot = Path.Combine(evidenceDirectory, "worker-hud.png");
            if (File.Exists(workerScreenshot)) File.Delete(workerScreenshot);
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(workerScreenshot);
            screenshotDeadline = Time.realtimeSinceStartupAsDouble + 10;
            stableFrames = 0; lastLength = -1;
            while (stableFrames < 2)
            {
                Require(Time.realtimeSinceStartupAsDouble < screenshotDeadline, "Worker HUD capture did not complete.");
                long length = ScreenshotLength(workerScreenshot);
                stableFrames = length > 1024 && length == lastLength ? stableFrames + 1 : 0;
                lastLength = length;
                yield return null;
            }
            ValidateScreenshot(workerScreenshot);
            evidence.WorkerScreenshotPath = workerScreenshot;
            evidence.Checks.Add("actual-worker-build-repair-patrol-panel-frame");

            host.Interaction.ToggleTechnology();
            Require(host.Interaction.TechnologyOpen,"Technology entry point did not open the graph.");
            string technologyScreenshot=Path.Combine(evidenceDirectory,"technology-tree.png");
            if(File.Exists(technologyScreenshot)) File.Delete(technologyScreenshot);
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(technologyScreenshot);
            screenshotDeadline=Time.realtimeSinceStartupAsDouble+10;
            stableFrames=0; lastLength=-1;
            while(stableFrames<2)
            {
                Require(Time.realtimeSinceStartupAsDouble<screenshotDeadline,"Technology graph capture did not complete.");
                long length=ScreenshotLength(technologyScreenshot);
                stableFrames=length>1024&&length==lastLength?stableFrames+1:0;
                lastLength=length;
                yield return null;
            }
            ValidateScreenshot(technologyScreenshot);
            evidence.TechnologyScreenshotPath=technologyScreenshot;
            host.Interaction.ToggleTechnology();
            evidence.Checks.Add("actual-technology-graph-and-locked-upgrade-panel-frame");

            host.TogglePause();
            Require(host.Mode == FrontendMode.Paused, "Pause entry point did not pause the match.");
            evidence.SnapshotTick = host.LiveTick;
            evidence.SnapshotHash = host.Match.StateHash();
            var beforeLoad = host.Match;
            // This directory is smoke-owned; removing the old output prevents stale saves
            // from making a failed SaveMatch look successful on a repeated smoke run.
            if (File.Exists(host.SavePath)) File.Delete(host.SavePath);
            host.SaveMatch();
            Require(File.Exists(host.SavePath), "SaveMatch did not write the isolated save file.");
            var saved = JsonUtility.FromJson<MatchState>(File.ReadAllText(host.SavePath, Encoding.UTF8));
            Require(saved != null && saved.Tick == evidence.SnapshotTick, "SaveMatch wrote an unexpected snapshot tick.");
            evidence.SavedHash = Match.Restore(host.Rules, saved).StateHash();
            Require(evidence.SavedHash == evidence.SnapshotHash, "Saved JSON changed the paused gameplay state.");
            host.LoadMatch();
            Require(!ReferenceEquals(beforeLoad, host.Match), "LoadMatch did not replace the live match with a restored match.");
            evidence.LoadedHash = host.Match.StateHash();
            Require(host.LiveTick == evidence.SnapshotTick && evidence.LoadedHash == evidence.SnapshotHash,
                "Save/load public entry points changed the same-frame gameplay state.");
            Require(host.Mode == FrontendMode.Playing && !host.IsReplay, "LoadMatch did not restore normal gameplay mode.");
            evidence.Checks.Add("pause-save-json-load-same-frame-hash-and-isolated-profile");

            host.WatchReplay();
            Require(host.Mode == FrontendMode.Replay && host.IsReplay && host.LiveTick == 0,
                "WatchReplay did not open the actual viewer at tick zero.");
            Require(host.ReplayEndTick == evidence.SnapshotTick, "Replay viewer timeline does not match the saved match.");
            evidence.ReplayStartHash = host.Match.StateHash();
            Require(evidence.ReplayStartHash == evidence.InitialHash, "Replay viewer tick zero differs from the original match tick zero.");
            evidence.ReplayStartFrame = Time.frameCount;
            double replayDeadline = Time.realtimeSinceStartupAsDouble + 8;
            while (host.LiveTick < 3 || Time.frameCount <= evidence.ReplayStartFrame + 1)
            {
                Require(Time.realtimeSinceStartupAsDouble < replayDeadline, "Replay viewer did not advance across multiple rendered frames.");
                Require(host.Mode == FrontendMode.Replay && host.IsReplay, "Replay viewer exited before demonstrating playback.");
                yield return null;
            }
            evidence.ReplayTicks = host.LiveTick;
            evidence.ReplayFrames = Time.frameCount - evidence.ReplayStartFrame;
            evidence.ReplayAdvancedHash = host.Match.StateHash();
            Require(evidence.ReplayAdvancedHash != evidence.ReplayStartHash, "Replay tick advancement did not change gameplay state.");
            CountRuntimeComponents(host);
            evidence.Checks.Add("actual-replay-viewer-tick-zero-and-frame-driven-playback");
            host.SetSpeed(8);
            double replayEndDeadline = Time.realtimeSinceStartupAsDouble + 15;
            while (host.Mode == FrontendMode.Replay)
            {
                Require(Time.realtimeSinceStartupAsDouble < replayEndDeadline, "Replay viewer did not finish its recorded timeline.");
                yield return null;
            }
            Require(host.Mode == FrontendMode.Results && host.LiveTick == host.ReplayEndTick,
                "Replay viewer did not finish on its exact final recorded tick.");
            Require(host.Match.StateHash() == evidence.SavedHash, "Actual replay viewer ended on a different gameplay state.");
            evidence.ReplayFinalHash = host.Match.StateHash();
            evidence.ReplayFinalTick = host.LiveTick;
            yield return null;
            yield return new WaitForEndOfFrame();
            evidence.Checks.Add("actual-replay-viewer-exact-end-tick-and-final-save-hash");
            if(!string.IsNullOrEmpty(technologyFixture))
            {
                // Load a genuine command-generated economic snapshot through the
                // ordinary product save boundary. Only our isolated smoke save is replaced.
                Require(File.Exists(technologyFixture),"Technology fixture is missing.");
                var researchState=JsonUtility.FromJson<MatchState>(File.ReadAllText(technologyFixture,Encoding.UTF8));
                var validated=Match.Restore(host.Rules,researchState);
                Require(validated.Observe(0).CompletedUpgrades.Count>0&&validated.Observe(0).Entities.Exists(e=>e.Owner==0&&e.Research.Count>0),
                    "Technology fixture must contain completed and active legitimate research.");
                File.Copy(technologyFixture,host.SavePath,true);
                host.LoadMatch();
                Require(host.Mode==FrontendMode.Playing&&host.Match.StateHash()==validated.StateHash(),"Product did not load the validated technology fixture.");
                host.SetSpeed(1);
                var lab=host.Observation.Entities.Find(e=>e.Owner==0&&e.Research.Count>0);
                int labId=lab.Id;
                string upgradeId=lab.Research[0].DefinitionId;
                evidence.ResearchFixtureSource=technologyFixture;
                evidence.ResearchFixtureTick=host.LiveTick;
                evidence.ResearchUpgradeId=upgradeId;
                host.Interaction.Selection.Clear(); host.Interaction.Selection.Add(labId);
                host.Interaction.Focus(BattleView.Position(lab.Position),true);
                host.Interaction.Order(CommandKind.Cancel);
                double researchDeadline=Time.realtimeSinceStartupAsDouble+4;
                while(host.Observation.Entities.Find(e=>e.Id==labId).Research.Count>0)
                {
                    Require(Time.realtimeSinceStartupAsDouble<researchDeadline,"Product Cancel did not cancel research.");
                    yield return null;
                }
                host.Interaction.Research(labId,upgradeId);
                researchDeadline=Time.realtimeSinceStartupAsDouble+4;
                while(host.Observation.Entities.Find(e=>e.Id==labId).Research.Count==0)
                {
                    Require(Time.realtimeSinceStartupAsDouble<researchDeadline,"Product Research did not restart after a refunded cancel.");
                    yield return null;
                }
                Require(host.Observation.Entities.Find(e=>e.Id==labId).Research[0].DefinitionId==upgradeId,"Product queued a different technology.");
                evidence.Checks.Add("product-load-genuine-research-fixture-cancel-refund-and-research-entrypoints");
                for(int capture=0;capture<2;capture++)
                {
                    if(capture==1) host.Interaction.ToggleTechnology();
                    string researchScreenshot=Path.Combine(evidenceDirectory,capture==0?"research-hud.png":"technology-progress.png");
                    if(File.Exists(researchScreenshot)) File.Delete(researchScreenshot);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(researchScreenshot);
                    screenshotDeadline=Time.realtimeSinceStartupAsDouble+10;
                    stableFrames=0; lastLength=-1;
                    while(stableFrames<2)
                    {
                        Require(Time.realtimeSinceStartupAsDouble<screenshotDeadline,"Research UI screenshot did not complete.");
                        long length=ScreenshotLength(researchScreenshot);
                        stableFrames=length>1024&&length==lastLength?stableFrames+1:0;
                        lastLength=length;
                        yield return null;
                    }
                    ValidateScreenshot(researchScreenshot);
                }
                host.Interaction.ToggleTechnology();
                evidence.Checks.Add("actual-research-progress-producer-and-technology-tree-frames");
            }
            Require(evidence.RuntimeErrorCount == 0, "Unity logged runtime errors during standalone smoke.");
            evidence.Status = "passed";
            Finish(0);
        }

        void CountRuntimeComponents(GameHost host)
        {
            evidence.HudComponents = FindObjectsByType<BattleHud>(FindObjectsSortMode.None).Length;
            evidence.InteractionComponents = FindObjectsByType<RtsInteraction>(FindObjectsSortMode.None).Length;
            evidence.WorldViewComponents = FindObjectsByType<BattleView>(FindObjectsSortMode.None).Length;
            evidence.CameraComponents = Camera.allCamerasCount;
            evidence.RendererComponents = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            Require(evidence.HudComponents == 1 && host.GetComponent<BattleHud>().isActiveAndEnabled,
                "The actual IMGUI BattleHud is missing, duplicated, or disabled.");
            Require(evidence.InteractionComponents == 1 && host.Interaction != null && host.Interaction.isActiveAndEnabled,
                "The actual interaction component is missing, duplicated, or disabled.");
            Require(evidence.WorldViewComponents == 1, "The actual world view is missing or duplicated.");
            Require(host.GameCamera != null && host.GameCamera.isActiveAndEnabled && Camera.main == host.GameCamera &&
                evidence.CameraComponents == 1, "The player must have exactly one enabled tactical main camera.");
            Require(evidence.RendererComponents > 10, "Procedural world geometry did not create renderers.");
            Require(Screen.width >= 640 && Screen.height >= 360, "The graphical viewport is too small to verify the actual HUD.");
        }

        void ValidateScreenshot(string path)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(image.LoadImage(File.ReadAllBytes(path)), "Captured gameplay PNG could not be decoded.");
                evidence.ImageWidth = image.width;
                evidence.ImageHeight = image.height;
                Require(image.width == evidence.ScreenWidth && image.height == evidence.ScreenHeight,
                    "Gameplay PNG dimensions differ from the rendered viewport.");
                var pixels = image.GetPixels32();
                int min = 765, max = 0;
                int stride = Math.Max(1, pixels.Length / 4096);
                for (int i = 0; i < pixels.Length; i += stride)
                {
                    int light = pixels[i].r + pixels[i].g + pixels[i].b;
                    min = Math.Min(min, light); max = Math.Max(max, light);
                }
                evidence.ImageLuminanceRange = max - min;
                Require(evidence.ImageLuminanceRange > 60, "Captured gameplay frame is visually blank or nearly uniform.");
            }
            finally { Destroy(image); }
        }

        static long ScreenshotLength(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : -1; }
            catch (IOException) { return -1; }
        }

        void OnGUI()
        {
            if (Event.current != null && Event.current.type == EventType.Repaint) guiRepaints++;
        }

        void Update()
        {
            if (initialized && !finished && Time.realtimeSinceStartupAsDouble - startedAt > 60)
                Fail(new TimeoutException("Standalone smoke exceeded the overall 60-second watchdog."));
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (finished || type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            evidence.RuntimeErrorCount++;
            if (evidence.RuntimeErrors.Count < 16)
                evidence.RuntimeErrors.Add(condition.Length > 2000 ? condition.Substring(0, 2000) : condition);
        }

        void Fail(Exception exception)
        {
            if (finished) return;
            evidence.Status = "failed";
            evidence.Error = exception.ToString();
            Finish(1);
        }

        void Finish(int exitCode)
        {
            if (finished) return;
            finished = true;
            evidence.FinishedUtc = DateTime.UtcNow.ToString("O");
            evidence.ElapsedSeconds = Time.realtimeSinceStartupAsDouble - startedAt;
            evidence.Frames = Time.frameCount - firstFrame;
            evidence.ExitCode = exitCode;
            try
            {
                if (string.IsNullOrEmpty(evidenceDirectory))
                    evidenceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Evidence"));
                Directory.CreateDirectory(evidenceDirectory);
                WriteEvidence();
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not write standalone smoke evidence: " + exception.Message);
                exitCode = 1;
            }
            Application.Quit(exitCode);
        }

        void WriteEvidence()
            => File.WriteAllText(Path.Combine(evidenceDirectory, "player-smoke.json"), JsonUtility.ToJson(evidence, true), new UTF8Encoding(false));

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [Serializable]
        sealed class PlayerEvidence
        {
            public string Status;
            public int ExitCode;
            public string StartedUtc;
            public string FinishedUtc;
            public double ElapsedSeconds;
            public int Frames;
            public int GameplayTicks;
            public int SnapshotTick;
            public int SelectedArmyCount;
            public int HudComponents;
            public int InteractionComponents;
            public int WorldViewComponents;
            public int CameraComponents;
            public int RendererComponents;
            public int GuiRepaints;
            public int GameplayCaptureFrame;
            public int ScreenWidth;
            public int ScreenHeight;
            public int ImageWidth;
            public int ImageHeight;
            public int ImageLuminanceRange;
            public string ScreenshotPath;
            public string WorkerScreenshotPath;
            public string TechnologyScreenshotPath;
            public string ResearchFixtureSource;
            public string ResearchUpgradeId;
            public int ResearchFixtureTick;
            public bool ScreenshotExists;
            public string SavePath;
            public string InitialHash;
            public string SnapshotHash;
            public string SavedHash;
            public string LoadedHash;
            public string ReplayStartHash;
            public string ReplayAdvancedHash;
            public string ReplayFinalHash;
            public int ReplayFinalTick;
            public int ReplayStartFrame;
            public int ReplayFrames;
            public int ReplayTicks;
            public int RuntimeErrorCount;
            public string Error;
            public List<string> Checks = new List<string>();
            public List<string> RuntimeErrors = new List<string>();
            public string Unverified = "Physical mouse/keyboard input and subjective visual quality require a separate interactive playtest.";
        }
    }
}
