using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ColonyRTS.Simulation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColonyRTS.Editor
{
    /// <summary>Project-owned, reproducible authoring and verification entry points.</summary>
    public static class GameProjectTools
    {
        public const string PlayableScene = "Assets/Game/Scenes/Frontier.unity";
        const string ShaderResources = "Assets/Game/Resources/BuildShaders";

        [MenuItem("Colony RTS/Create Playable Scene")]
        public static void CreatePlayableScene()
        {
            RequireIdleEditor();
            RequireCleanScenes();
            EnsureShaderResources();
            EnsureAssetFolder("Assets/Game/Scenes");

            // Dirty scenes were rejected above. Use a single playable scene so unrelated
            // default cameras/lights cannot silently participate in the Editor playtest.
            var scene = SceneManager.GetSceneByPath(PlayableScene);
            if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount != 1)
            {
                scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayableScene) == null
                    ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                    : EditorSceneManager.OpenScene(PlayableScene, OpenSceneMode.Single);
            }
            var hosts = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<GameHost>(true)).ToArray();
            if (hosts.Length > 1) throw new InvalidOperationException("Frontier contains multiple GameHost components; resolve scene ownership before regenerating it.");
            if (hosts.Length == 0)
            {
                var runtime = new GameObject("RTS Runtime");
                SceneManager.MoveGameObjectToScene(runtime, scene);
                runtime.AddComponent<GameHost>();
            }
            if (!EditorSceneManager.SaveScene(scene, PlayableScene))
                throw new IOException("Unity could not save the playable scene: " + PlayableScene);

            var scenes = EditorBuildSettings.scenes.ToList();
            int existing = scenes.FindIndex(item => item.path == PlayableScene);
            if (existing < 0) scenes.Add(new EditorBuildSettingsScene(PlayableScene, true));
            else scenes[existing] = new EditorBuildSettingsScene(PlayableScene, true);
            EditorBuildSettings.scenes = scenes.ToArray();
            SceneManager.SetActiveScene(scene);
            Debug.Log("Playable scene saved: " + PlayableScene);
        }

        [MenuItem("Colony RTS/Build Windows")]
        public static void BuildWindows()
            => BuildWindowsCore("Windows", BuildOptions.None);

        [MenuItem("Colony RTS/Build Windows Development Profile")]
        public static void BuildWindowsDevelopmentProfile()
            => BuildWindowsCore("WindowsProfile", BuildOptions.Development);

        // A separate verification build preserves another iteration's frozen players.
        public static void BuildWindowsTo(string directory, bool development = false)
            => BuildWindowsCore(directory, development ? BuildOptions.Development : BuildOptions.None);

        [MenuItem("Colony RTS/Configure UI Icon Import")]
        public static void ConfigureUiIconImport()
        {
            const string path = "Assets/Game/Resources/UI/command-icons.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Required generated UI icon atlas is missing.");
            importer.textureType=TextureImporterType.Default;
            importer.sRGBTexture=true;
            importer.maxTextureSize=2048;
            importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static void BuildWindowsCore(string directory, BuildOptions options)
        {
            RequireIdleEditor();
            RequireCleanScenes();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PlayableScene) == null)
                throw new InvalidOperationException("CreatePlayableScene must create Frontier before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("This Unity Editor does not have Windows Standalone build support installed.");
            EnsureShaderResources();
            ConfigureUiIconImport();
            string output = ProjectPath("Builds", "Players", directory, "KopruluFront.exe");
            if (!output.StartsWith(ProjectPath("Builds", "Players") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Player output must stay under Builds/Players.", nameof(directory));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PlayableScene },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            if (report == null || report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0)
            {
                string reason = report == null ? "No BuildReport returned" :
                    report.summary.result + "; errors=" + report.summary.totalErrors + "; warnings=" + report.summary.totalWarnings;
                throw new InvalidOperationException("Windows build failed: " + reason);
            }
            if (!File.Exists(output)) throw new IOException("Build reported success but the executable is missing: " + output);
            Debug.Log("Windows build succeeded: " + output + " (" + report.summary.totalSize + " bytes)");
        }

        [MenuItem("Colony RTS/Headless Simulation Smoke")]
        public static void HeadlessSmoke()
        {
            var evidence = new SmokeEvidence { Status = "running", StartedUtc = DateTime.UtcNow.ToString("O"), RequestedTicks = 1200 };
            string path = ProjectPath("Builds", "Evidence", "smoke.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            WriteEvidence(path, evidence);
            try
            {
                var asset = Resources.Load<TextAsset>("rts-rules");
                Require(asset != null, "Runtime Resources/rts-rules is missing.");
                var rules = JsonUtility.FromJson<RuleSet>(asset.text);
                var match = new Match(rules, true, 731);
                evidence.RuleVersion = rules.VersionId;
                evidence.RuleHash = match.CaptureState().RuleHash;
                evidence.Checks.Add("actual-runtime-rules-loaded");
                var initial = match.CaptureState();
                int initialWorkers = initial.Entities.Count(e => e.Owner == 0 && e.DefinitionId == "worker");
                int initialMarines = initial.Entities.Count(e => e.Owner == 0 && e.DefinitionId == "marine");
                long initialMinerals = initial.Entities.Where(e => e.DefinitionId == "mineral").Sum(e => (long)e.ResourceRemaining);
                Match restored = null;

                for (int tick = 0; tick < evidence.RequestedTicks; tick++)
                {
                    Require(match.Tick == tick, "Simulation stopped advancing before tick " + tick + ".");
                    DriveScenario(match, tick);
                    if (restored != null) DriveScenario(restored, tick);
                    match.Step();
                    if (restored != null) restored.Step();
                    evidence.CompletedTicks = match.Tick;
                    Require(!match.LastResults.Any(result => result.Player == 0 && !result.Accepted),
                        "Synthetic player command was rejected at tick " + tick + ": " +
                        string.Join(", ", match.LastResults.Where(result => result.Player == 0 && !result.Accepted).Select(result => result.Reason)));
                    ValidateObservation(match.Observe(0), rules, match.Tick);
                    ValidateObservation(match.Observe(1), rules, match.Tick);

                    if (match.Tick == 600)
                    {
                        // Exercise the same JSON boundary used by a disk save, with the live AI state intact.
                        string json = JsonUtility.ToJson(match.CaptureState());
                        restored = Match.Restore(rules, JsonUtility.FromJson<MatchState>(json));
                        evidence.SnapshotTick = restored.Tick;
                        Require(restored.StateHash() == match.StateHash(), "JSON snapshot changed the gameplay state.");
                        evidence.Checks.Add("json-save-load-at-tick-600");
                    }
                    if (restored != null && match.Tick % 100 == 0)
                        Require(restored.StateHash() == match.StateHash(), "AI continuation diverged after save/load at tick " + match.Tick + ".");
                }

                var final = match.CaptureState();
                Require(final.Tick == evidence.RequestedTicks, "The requested smoke timeline did not complete.");
                Require(final.Entities.Count(e => e.Owner == 0 && e.DefinitionId == "worker") > initialWorkers, "Worker training did not complete.");
                Require(final.Entities.Any(e => e.Owner == 0 && e.DefinitionId == "depot" && e.Completed), "Supply depot construction did not complete.");
                Require(final.Entities.Any(e => e.Owner == 0 && e.DefinitionId == "barracks" && e.Completed), "Barracks construction did not complete.");
                Require(final.Entities.Count(e => e.Owner == 0 && e.DefinitionId == "marine") > initialMarines, "Marine training did not complete.");
                Require(final.Entities.Where(e => e.DefinitionId == "mineral").Sum(e => (long)e.ResourceRemaining) < initialMinerals, "Workers did not extract minerals.");
                Require(initial.Entities.Where(e => e.Owner == 0 && e.DefinitionId == "marine").Any(start =>
                {
                    var end = final.Entities.FirstOrDefault(e => e.Id == start.Id);
                    if (end == null) return false;
                    long dx = (long)end.Position.X - start.Position.X, dz = (long)end.Position.Z - start.Position.Z;
                    return dx * dx + dz * dz > 1000000;
                }), "Move commands did not move an initial marine by at least one metre.");
                int spent = final.CommandLog.Where(command => command.Player == 0 &&
                    (command.Kind == CommandKind.Build || command.Kind == CommandKind.Train))
                    .Sum(command => match.Definition(command.DefinitionId).MineralCost);
                Require(final.Players[0].Minerals + spent > rules.StartingMinerals, "Extracted minerals were not delivered to the player.");
                evidence.Checks.Add("movement-economy-construction-production");
                evidence.Checks.Add("both-player-observations-through-1200-ticks");
                evidence.FinalHash = match.StateHash();
                evidence.RestoredHash = restored.StateHash();
                Require(evidence.RestoredHash == evidence.FinalHash, "Final restored continuation hash mismatch.");
                evidence.Checks.Add("live-ai-save-load-continuation");

                // Serialize accepted inputs, then reconstruct from them without rerunning the AI.
                var replayData = JsonUtility.FromJson<ReplayData>(JsonUtility.ToJson(match.ExportReplay()));
                var replay = Match.FromReplay(rules, replayData);
                evidence.ReplayHash = replay.StateHash();
                Require(evidence.ReplayHash == evidence.FinalHash, "Accepted-command replay hash mismatch.");
                evidence.AcceptedCommands = replayData.Commands.Count;
                evidence.EntityCount = final.Entities.Count;
                evidence.Checks.Add("json-replay-reconstruction");
                evidence.Status = "passed";
                evidence.FinishedUtc = DateTime.UtcNow.ToString("O");
                WriteEvidence(path, evidence);
                Debug.Log("Simulation smoke passed; evidence: " + path);
            }
            catch (Exception exception)
            {
                evidence.Status = "failed";
                evidence.FinishedUtc = DateTime.UtcNow.ToString("O");
                evidence.Error = exception.ToString();
                WriteEvidence(path, evidence);
                throw;
            }
        }

        static void DriveScenario(Match match, int tick)
        {
            if (tick != 0 && tick != 20 && tick != 400 && tick != 800) return;
            var own = match.Observe(0).Entities.Where(entity => entity.Owner == 0).ToArray();
            var home = own.First(entity => match.Definition(entity.DefinitionId).IsBase);
            if (tick == 0)
            {
                match.QueueCommand(match.MakeCommand(0, CommandKind.Train, new[] { home.Id }, definitionId: "worker"));
                var marines = own.Where(entity => entity.DefinitionId == "marine").Select(entity => entity.Id).ToArray();
                Require(marines.Length > 0, "Runtime rules do not spawn a marine for the movement smoke.");
                var target = new SimPoint(Math.Min(match.Rules.MapWidth * 1000 - 2000, home.Position.X + 7000),
                    Math.Min(match.Rules.MapHeight * 1000 - 2000, home.Position.Z + 6000));
                match.QueueCommand(match.MakeCommand(0, CommandKind.Move, marines, target));
            }
            else if (tick == 20 || tick == 400)
            {
                string definition = tick == 20 ? "depot" : "barracks";
                var worker = own.First(entity => match.Definition(entity.DefinitionId).IsWorker && entity.Completed);
                var position = FindPlacement(match, definition, home.Position);
                match.QueueCommand(match.MakeCommand(0, CommandKind.Build, new[] { worker.Id }, position, definitionId: definition));
            }
            else
            {
                var barracks = own.FirstOrDefault(entity => entity.DefinitionId == "barracks" && entity.Completed);
                Require(barracks != null, "Synthetic barracks must be ready before tick 800 marine production.");
                match.QueueCommand(match.MakeCommand(0, CommandKind.Train, new[] { barracks.Id }, definitionId: "marine"));
            }
        }

        static SimPoint FindPlacement(Match match, string definition, SimPoint home)
        {
            for (int radius = 6000; radius <= 14000; radius += 2000)
                for (int direction = 0; direction < 16; direction++)
                {
                    double angle = direction * Math.PI * 2 / 16;
                    var target = new SimPoint(home.X + (int)Math.Round(Math.Cos(angle) * radius),
                        home.Z + (int)Math.Round(Math.Sin(angle) * radius));
                    if (match.CanPlace(definition, target, 0)) return target;
                }
            throw new InvalidOperationException("No legal " + definition + " position found for the smoke scenario.");
        }

        static void ValidateObservation(PlayerObservation observation, RuleSet rules, int tick)
        {
            Require(observation.Tick == tick, "Observation is from the wrong tick.");
            int cells = rules.MapWidth * rules.MapHeight;
            Require(observation.Visible != null && observation.Visible.Length == cells &&
                observation.Explored != null && observation.Explored.Length == cells, "Observation fog dimensions do not match runtime map dimensions.");
            Require(observation.Minerals >= 0 && observation.Gas >= 0 && observation.SupplyUsed >= 0 && observation.SupplyCap >= 0,
                "Observation exposes a negative economy value.");
            for (int i = 0; i < cells; i++)
                Require(!observation.Visible[i] || observation.Explored[i], "Visible terrain must be explored.");
            foreach (var entity in observation.Entities)
                Require(entity.Health > 0 && entity.Position.X >= 0 && entity.Position.Z >= 0 &&
                    entity.Position.X < rules.MapWidth * 1000 && entity.Position.Z < rules.MapHeight * 1000, "Observation contains an invalid entity.");
        }

        static void EnsureShaderResources()
        {
            EnsureAssetFolder(ShaderResources);
            PreserveShader("Standard", "Keep_Standard", false);
            PreserveShader("Standard", "Keep_Standard_Emission", true);
            PreserveShader("Sprites/Default", "Keep_Sprites_Default", false);
            PreserveShader("ColonyRTS/HudHealthBars", "Keep_Hud_HealthBars", false);
            PreserveShader("Unlit/Color", "Keep_Unlit_Color", false);
        }

        static void PreserveShader(string shaderName, string assetName, bool emission)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Required built-in shader is unavailable: " + shaderName);
            string path = ShaderResources + "/" + assetName + ".mat";
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && !(asset is Material)) throw new InvalidOperationException("Shader preservation path contains an unrelated asset: " + path);
            var material = asset as Material;
            if (material == null)
            {
                material = new Material(shader) { name = assetName, enableInstancing = true };
                if (emission)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.white);
                }
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
                throw new InvalidOperationException("Shader preservation material was modified to a different shader: " + path);
            if (emission && !material.IsKeywordEnabled("_EMISSION"))
                throw new InvalidOperationException("Emission preservation material must keep its _EMISSION keyword: " + path);
        }

        static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) throw new ArgumentException("Asset folder must be below Assets.", nameof(path));
            string parent = path.Substring(0, slash);
            EnsureAssetFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(slash + 1))))
                throw new IOException("Could not create Unity asset folder: " + path);
        }

        static string ProjectPath(params string[] parts)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string result = root;
            foreach (string part in parts) result = Path.Combine(result, part);
            result = Path.GetFullPath(result);
            if (!result.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Output path must stay inside the current Unity project.");
            return result;
        }

        static void RequireIdleEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait until the Editor is out of Play Mode and finishes importing/compiling before authoring or building.");
        }

        static void RequireCleanScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty) throw new InvalidOperationException("Unsaved scene changes are preserved. Save them before this operation: " +
                    (string.IsNullOrEmpty(scene.path) ? scene.name + " (unsaved)" : scene.path));
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void WriteEvidence(string path, SmokeEvidence evidence)
            => File.WriteAllText(path, JsonUtility.ToJson(evidence, true), new UTF8Encoding(false));

        [Serializable]
        sealed class SmokeEvidence
        {
            public string Status;
            public string StartedUtc;
            public string FinishedUtc;
            public string RuleVersion;
            public string RuleHash;
            public int RequestedTicks;
            public int CompletedTicks;
            public int SnapshotTick;
            public int AcceptedCommands;
            public int EntityCount;
            public string FinalHash;
            public string RestoredHash;
            public string ReplayHash;
            public string Error;
            public List<string> Checks = new List<string>();
        }
    }
}
