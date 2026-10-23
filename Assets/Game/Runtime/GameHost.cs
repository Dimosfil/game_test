using System;
using System.Collections.Generic;
using System.IO;
using ColonyRTS.Presentation;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS
{
    public enum FrontendMode { MainMenu, Playing, Paused, Replay, Results }

    public sealed class GameHost : MonoBehaviour
    {
        public static GameHost Instance { get; private set; }
        private Match ownedMatch;
        private readonly SimulationFrameRunner simulationRunner = new SimulationFrameRunner();
        private Dictionary<string, ObservedCombatStats> observedCombat = new Dictionary<string, ObservedCombatStats>(StringComparer.Ordinal);
        private Dictionary<int, Dictionary<string, CommandResult>> observedResearch = new Dictionary<int, Dictionary<string, CommandResult>>();
        private Dictionary<string, CommandResult> missingProducerResearch = new Dictionary<string, CommandResult>(StringComparer.Ordinal);
        // Main-thread diagnostic access drains the worker. Frontend drawing consumes
        // immutable read models instead; never retain this borrow across frames.
        public Match Match
        {
            get { CompleteSimulation(); return ownedMatch; }
            private set { CompleteSimulation(); ownedMatch = value; }
        }
        public RuleSet Rules { get; private set; }
        public UiCommandCatalog UiCommands { get; private set; }
        public PlayerObservation Observation { get; private set; }
        public FrontendMode Mode { get; private set; }
        public float Speed { get; private set; } = 1;
        public string Notice { get; private set; }
        public float NoticeUntil { get; private set; }
        public RtsInteraction Interaction { get; private set; }
        public Camera GameCamera { get; private set; }
        public int ReplayEndTick { get; private set; }
        public bool IsReplay { get; private set; }
        public int LiveTick => Observation == null ? 0 : Observation.Tick;
        public string SaveDirectoryOverride { get; set; }
        public double DiagnosticAdvanceMs { get; private set; }
        public double DiagnosticWorkerMs { get; private set; }
        public bool SimulationPending => simulationRunner.HasPending;
        private bool sessionProfile;
        private bool combatProfile;
        private bool movingAiProfile;
        public bool DiagnosticProfilingEnabled => sessionProfile;
        public MatchCombatDiagnostics DiagnosticCombat { get; private set; }
        // Only the opt-in combat harness may fence advancement at a reproducible tick.
        public int DiagnosticTickLimit { get; set; } = -1;
        public string SavePath => Path.Combine(string.IsNullOrEmpty(SaveDirectoryOverride) ? Application.persistentDataPath : Path.GetFullPath(SaveDirectoryOverride), "frontier-save.json");
        private double accumulator;
        private BattleView view;
        private readonly Dictionary<string, EntityDefinition> displayCatalog = new Dictionary<string, EntityDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, UpgradeDefinition> displayUpgrades = new Dictionary<string, UpgradeDefinition>(StringComparer.Ordinal);
        public EntityDefinition Definition(string id) => displayCatalog[id];
        public UpgradeDefinition Upgrade(string id) => displayUpgrades[id];
        public int ObservedDamage(string id) => observedCombat.TryGetValue(id, out var value) ? value.Damage : Definition(id).Damage;
        public int ObservedArmor(string id) => observedCombat.TryGetValue(id, out var value) ? value.Armor : Definition(id).Armor;
        public CommandResult ResearchAvailability(int producerId, string upgradeId) =>
            observedResearch.TryGetValue(producerId, out var gates) && gates.TryGetValue(upgradeId, out var gate) ? gate : missingProducerResearch[upgradeId];

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.runInBackground = true;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-rtsProfile") >= 0)
            {
                RenderingProfile.StartProfile(gameObject);
                return;
            }
            Application.targetFrameRate = 60;
            combatProfile = Array.IndexOf(Environment.GetCommandLineArgs(), "-rtsCombatProfile") >= 0;
            movingAiProfile = combatProfile && Array.IndexOf(Environment.GetCommandLineArgs(), "-rtsCombatMovingAI") >= 0;
            sessionProfile = combatProfile || Array.IndexOf(Environment.GetCommandLineArgs(), "-rtsSessionProfile") >= 0;
            var rulesAsset = Resources.Load<TextAsset>("rts-rules");
            if (rulesAsset == null) throw new InvalidOperationException("Required rts-rules content is missing.");
            Rules = JsonUtility.FromJson<RuleSet>(rulesAsset.text);
            if(combatProfile)
                Rules = ColonyRTS.Diagnostics.SessionPerformanceProfile.CreateCombatFixtureRules(Rules,movingAiProfile);
            else if (sessionProfile)
                Rules = ColonyRTS.Diagnostics.SessionPerformanceProfile.CreateFixtureRules(Rules);
            foreach (var definition in Rules.Definitions) displayCatalog.Add(definition.Id, definition);
            foreach (var upgrade in Rules.Upgrades) displayUpgrades.Add(upgrade.Id, upgrade);
            UiCommands = UiCommandCatalog.Load(Rules);
            Mode = FrontendMode.MainMenu;
            SetupCameraAndLighting();
            view = gameObject.AddComponent<BattleView>();
            view.Initialize(this);
            Interaction = gameObject.AddComponent<RtsInteraction>();
            Interaction.Initialize(this);
            gameObject.AddComponent<BattleHud>().Initialize(this);
            ResetMatch();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-rtsSmoke") >= 0)
                gameObject.AddComponent<ColonyRTS.Diagnostics.PlayerSmoke>();
            if (sessionProfile)
                gameObject.AddComponent<ColonyRTS.Diagnostics.SessionPerformanceProfile>();
        }

        private void SetupCameraAndLighting()
        {
            GameCamera = Camera.main;
            if (GameCamera == null) GameCamera = new GameObject("Tactical Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            GameCamera.tag = "MainCamera";
            GameCamera.clearFlags = CameraClearFlags.SolidColor;
            GameCamera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            GameCamera.fieldOfView = 43;
            GameCamera.nearClipPlane = 0.2f;
            GameCamera.farClipPlane = 180;
            GameCamera.allowHDR = true;
            GameCamera.transform.rotation = Quaternion.Euler(57, 0, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.35f, 0.45f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.29f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.08f);
            RenderSettings.fog = false;
            var sun = FindFirstObjectByType<Light>();
            if (sun == null) sun = new GameObject("Colony Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1, 0.86f, 0.67f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 100;
            QualitySettings.antiAliasing = 4;
        }

        private void ResetMatch()
        {
            Match = new Match(Rules, !sessionProfile || movingAiProfile, 42);
            ownedMatch.DetailedDiagnosticsEnabled = movingAiProfile;
            accumulator = 0;
            Speed = 1;
            IsReplay = false;
            RefreshReadModel();
            view.ResetViews();
            Interaction.ResetSelection();
            Interaction.FocusBase(true);
        }

        public void StartMatch()
        {
            ResetMatch();
            Mode = FrontendMode.Playing;
            Notify(RuntimeText.Get("objective"));
        }

        public void ReturnToMenu() { CompleteSimulation(); Mode = FrontendMode.MainMenu; }
        public void TogglePause()
        {
            CompleteSimulation();
            if (Mode == FrontendMode.Playing || Mode == FrontendMode.Replay) Mode = FrontendMode.Paused;
            else if (Mode == FrontendMode.Paused) Mode = IsReplay ? FrontendMode.Replay : FrontendMode.Playing;
        }
        public void SetSpeed(float speed) { CompleteSimulation(); Speed = Mathf.Clamp(speed, 1, 8); }
        public void Notify(string text) { Notice = text; NoticeUntil = Time.unscaledTime + 4; }

        public void Issue(CommandKind kind, int[] ids, SimPoint target = default, int targetId = 0, string definitionId = null, bool append = false)
        {
            CompleteSimulation();
            if (Mode != FrontendMode.Playing || IsReplay) return;
            ownedMatch.QueueCommand(ownedMatch.MakeCommand(0, kind, ids, target, targetId, definitionId, append));
        }

        private void Update()
        {
            DiagnosticAdvanceMs = 0;
            if (ownedMatch == null) return;
            if (Mode != FrontendMode.Playing && Mode != FrontendMode.Replay) return;
            long diagnosticStart = sessionProfile ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            try
            {
                accumulator += Math.Min(Time.unscaledDeltaTime, 0.25f) * Speed;
                if (simulationRunner.HasPending)
                {
                    if (!simulationRunner.TryComplete(out var completed)) return;
                    PublishFrame(completed);
                    if (Mode != FrontendMode.Playing && Mode != FrontendMode.Replay) return;
                }
                if(IsReplay && LiveTick >= ReplayEndTick) { Mode=FrontendMode.Results; return; }
                double step=1.0/Rules.TickRate;
                int steps=Math.Min(16,(int)(accumulator/step));
                if(combatProfile&&DiagnosticTickLimit>=0)steps=Math.Min(steps,Math.Max(0,DiagnosticTickLimit-LiveTick));
                if(steps>0)
                {
                    if(IsReplay)steps=Math.Min(steps,ReplayEndTick-LiveTick);
                    accumulator-=steps*step;
                    simulationRunner.Schedule(ownedMatch,Rules,steps,IsReplay?ReplayEndTick:-1,combatProfile);
                }
            }
            catch(Exception exception)
            {
                HandleSimulationFailure(exception);
            }
            if(sessionProfile) DiagnosticAdvanceMs = (System.Diagnostics.Stopwatch.GetTimestamp()-diagnosticStart)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }

        void PublishFrame(SimulationFrame frame)
        {
            Observation=frame.Observation;
            observedCombat=frame.OwnCombatStats;
            observedResearch=frame.ResearchAvailability;
            missingProducerResearch=frame.MissingProducerAvailability;
            DiagnosticWorkerMs=frame.ComputeMilliseconds;
            if(combatProfile)DiagnosticCombat=frame.CombatDiagnostics;
            foreach(var result in frame.Results)
                if(result.Player==0&&!result.Accepted)Notify(RuntimeText.Get("rejected")+": "+result.Reason);
            if(Observation.Phase==MatchPhase.Finished||frame.ReplayEnded)Mode=FrontendMode.Results;
        }
        void CompleteSimulation()
        {
            try { if(simulationRunner.HasPending)PublishFrame(simulationRunner.Complete()); }
            catch(Exception exception) { HandleSimulationFailure(exception); throw; }
        }
        void HandleSimulationFailure(Exception exception)
        {
            Mode=FrontendMode.Paused; accumulator=0;
            Debug.LogException(exception); Notify(RuntimeText.Get("simulation.error"));
        }
        void RefreshReadModel()
        {
            CompleteSimulation();
            simulationRunner.Schedule(ownedMatch,Rules,0,-1,combatProfile);
            PublishFrame(simulationRunner.Complete());
        }

        public void SaveMatch()
        {
            if (IsReplay || Match == null) return;
            try
            {
                var path = SavePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var json = JsonUtility.ToJson(Match.CaptureState());
                File.WriteAllText(path + ".tmp", json, new System.Text.UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
                Notify(RuntimeText.Get("save.ok"));
            }
            catch (Exception exception) { Debug.LogException(exception); Notify(RuntimeText.Get("saving.error")); }
        }

        public void LoadMatch()
        {
            if (!File.Exists(SavePath)) { Notify(RuntimeText.Get("save.missing")); return; }
            try
            {
                var snapshot = JsonUtility.FromJson<MatchState>(File.ReadAllText(SavePath, System.Text.Encoding.UTF8));
                var restored = Match.Restore(Rules, snapshot);
                Match = restored;
                RefreshReadModel();
                accumulator = 0;
                IsReplay = false;
                Speed = 1;
                Mode = Observation.Phase == MatchPhase.Finished ? FrontendMode.Results : FrontendMode.Playing;
                view.ResetViews();
                Interaction.ResetSelection();
                Interaction.FocusBase(true);
                Notify(RuntimeText.Get("load.ok"));
            }
            catch (Exception exception) { Debug.LogException(exception); Notify(RuntimeText.Get("saving.error")); }
        }

        public void WatchReplay()
        {
            if (IsReplay || Match == null || Match.Tick < 2) return;
            var replay = Match.ExportReplay();
            var playback = Match.BeginReplay(Rules, replay);
            Match = playback;
            ReplayEndTick = replay.FinalTick;
            RefreshReadModel();
            accumulator = 0;
            IsReplay = true;
            Speed = 2;
            Mode = FrontendMode.Replay;
            view.ResetViews();
            Interaction.ResetSelection();
            Interaction.FocusBase(true);
        }

        private void OnDestroy()
        {
            try { simulationRunner.Dispose(); }
            catch(Exception exception) { Debug.LogException(exception); }
            if (Instance == this) Instance = null;
        }
    }
}
