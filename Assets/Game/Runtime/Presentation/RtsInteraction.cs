using System.Collections.Generic;
using System.Linq;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Presentation
{
    public sealed class RtsInteraction : MonoBehaviour
    {
        private enum TargetMode { None, Build, Attack, Patrol, Repair }
        public readonly List<int> Selection = new List<int>();
        public string BuildId { get; private set; }
        public bool AttackTargeting => targeting == TargetMode.Attack;
        public bool PatrolTargeting => targeting == TargetMode.Patrol;
        public bool RepairTargeting => targeting == TargetMode.Repair;
        public bool HasTargeting => targeting != TargetMode.None;
        public string TargetingPromptKey => targeting == TargetMode.Build ? "build.prompt" :
            targeting == TargetMode.Attack ? "attack.prompt" : targeting == TargetMode.Patrol ? "patrol.prompt" :
            targeting == TargetMode.Repair ? "repair.prompt" : null;
        public bool Dragging { get; private set; }
        public Vector2 DragStart { get; private set; }
        public Vector2 DragCurrent { get; private set; }
        public Vector3 CameraFocus { get; private set; }
        public SimPoint CursorWorld { get; private set; }
        public bool PlacementValid { get; private set; }
        public bool TechnologyOpen { get; private set; }
        private readonly Dictionary<int, int[]> groups = new Dictionary<int, int[]>();
        private readonly HashSet<int> observedSelectionIds = new HashSet<int>();
        private PlayerObservation selectionObservation;
        private GameHost host;
        private Vector2 down;
        private float cameraHeight = 27;
        private GameObject preview;
        private Vector3 lastMouse;
        private TargetMode targeting;
        private FrontendMode previousMode;
        public static float HudHeight => Mathf.Clamp(Screen.height * .235f, 180, 260);
        public static Rect MapRect => new Rect(16, Screen.height - HudHeight + 15, HudHeight - 30, HudHeight - 30);
        public static Rect TechnologyButtonRect
        {
            get
            {
                float scale = Mathf.Clamp(Screen.height / 900f, .65f, 1.5f);
                return new Rect(Screen.width-200*scale,112*scale,180*scale,30*scale);
            }
        }
        public static bool IsWorldArea(Vector2 mouse) => mouse.x>=0&&mouse.x<Screen.width&&mouse.y>62&&
            mouse.y<Screen.height-HudHeight&&!TechnologyButtonRect.Contains(mouse);
        public bool IsSelected(int id) => Selection.Contains(id);
        public void Initialize(GameHost owner) { host = owner; previousMode = host.Mode; lastMouse = Input.mousePosition; }

        public void ResetSelection() { Selection.Clear(); groups.Clear(); CancelTargeting(); TechnologyOpen = false; }
        public void ToggleTechnology() { CancelTargeting(); TechnologyOpen = !TechnologyOpen; }
        public void Research(int producerId, string upgradeId)
        {
            if (!CanIssueCommands) return;
            CancelTargeting();
            host.Issue(CommandKind.Research, new[] { producerId }, definitionId: upgradeId);
        }
        public void FocusBase(bool immediate = false)
        {
            var home = host.Observation.Entities.Find(e => e.Owner == 0 && host.Definition(e.DefinitionId).IsBase);
            if (home != null) Focus(BattleView.Position(home.Position), immediate);
        }
        public void SetZoom(float height)
        {
            cameraHeight = Mathf.Clamp(height, 16, 48);
            ApplyCamera(true);
        }
        public void Focus(Vector3 point, bool immediate = false)
        {
            CameraFocus = new Vector3(Mathf.Clamp(point.x, 5, host.Rules.MapWidth - 5), 0, Mathf.Clamp(point.z, 5, host.Rules.MapHeight - 5));
            if (immediate) ApplyCamera(true);
        }
        public void SelectAllArmy()
        {
            CancelTargeting();
            Selection.Clear();
            foreach (var e in host.Observation.Entities)
            {
                if (e.Owner != 0) continue;
                var definition = host.Definition(e.DefinitionId);
                if (!definition.IsWorker && !definition.IsBuilding && !definition.IsResource && definition.Damage > 0) Selection.Add(e.Id);
            }
        }
        public void BeginBuild(string definitionId)
        {
            if (!CanIssueCommands) return;
            CancelTargeting();
            if (!HasSelectedWorker()) return;
            BuildId = definitionId;
            targeting = TargetMode.Build;
            preview = ProceduralArt.Create(definitionId, 0, transform);
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            host.Notify(host.Definition(definitionId).DisplayName + "  ·  " + RuntimeText.Get("build.prompt"));
        }
        public void BeginAttack() => BeginPointTargeting(TargetMode.Attack);
        public void BeginPatrol() => BeginPointTargeting(TargetMode.Patrol);
        public void BeginRepair()
        {
            if (!CanIssueCommands) return;
            CancelTargeting();
            if (!HasSelectedWorker()) return;
            targeting = TargetMode.Repair;
            host.Notify(RuntimeText.Get(TargetingPromptKey));
        }
        private void BeginPointTargeting(TargetMode mode)
        {
            if (!CanIssueCommands) return;
            CancelTargeting();
            if (!HasSelectedMobile()) return;
            targeting = mode;
            host.Notify(RuntimeText.Get(TargetingPromptKey));
        }
        public void CancelTargeting()
        {
            BuildId = null; targeting = TargetMode.None; PlacementValid = false;
            CancelDrag();
            if (preview != null) Destroy(preview);
            preview = null;
        }
        public void Order(CommandKind kind)
        {
            if (!CanIssueCommands) return;
            CancelTargeting();
            host.Issue(kind, OwnSelection());
        }
        public void Train(string definitionId = null)
        {
            if (!CanIssueCommands) return;
            CancelTargeting();
            var choice = host.UiCommands.ChooseTraining(host.Observation, Selection, definitionId);
            if (choice.Available) host.Issue(CommandKind.Train, new[] { choice.ProducerId }, definitionId: choice.DefinitionId);
        }
        private bool CanIssueCommands => host != null && host.Mode == FrontendMode.Playing && !host.IsReplay;
        private bool HasSelectedWorker() => host.Observation.Entities.Any(e => e.Owner == 0 && e.Completed &&
            Selection.Contains(e.Id) && host.Definition(e.DefinitionId).IsWorker);
        private bool HasSelectedMobile()
        {
            foreach (var entity in host.Observation.Entities) if (IsSelectedMobile(entity)) return true;
            return false;
        }
        private bool IsSelectedMobile(EntityState entity) => entity.Owner == 0 && entity.Completed && Selection.Contains(entity.Id) &&
            !host.Definition(entity.DefinitionId).IsBuilding && host.Definition(entity.DefinitionId).Speed > 0;
        private int[] OwnSelection() => host.Observation.Entities.Where(e => e.Owner == 0 && Selection.Contains(e.Id)).Select(e => e.Id).ToArray();
        private int[] OwnMobileSelection() => host.Observation.Entities.Where(IsSelectedMobile).Select(e => e.Id).ToArray();
        private int[] OwnWorkers(int exceptId = 0) => host.Observation.Entities.Where(e => e.Owner == 0 && e.Completed && e.Id != exceptId &&
            Selection.Contains(e.Id) && host.Definition(e.DefinitionId).IsWorker).Select(e => e.Id).ToArray();
        private void CancelDrag() { Dragging = false; down = DragStart = DragCurrent = Vector2.zero; }

        private void Update()
        {
            if (host == null || host.Observation == null) return;
            if (host.Mode != previousMode) { CancelTargeting(); previousMode = host.Mode; }
            if(!ReferenceEquals(selectionObservation,host.Observation))
            {
                selectionObservation=host.Observation;
                observedSelectionIds.Clear();
                foreach(var entity in selectionObservation.Entities)observedSelectionIds.Add(entity.Id);
            }
            // Published observations are immutable. Membership can be indexed once
            // per publication; the public Selection list remains the command owner.
            for(int i=Selection.Count-1;i>=0;i--)
                if(!observedSelectionIds.Contains(Selection[i]))Selection.RemoveAt(i);
            if (HasTargeting && ((targeting == TargetMode.Build || RepairTargeting) ? !HasSelectedWorker() : !HasSelectedMobile()))
                CancelTargeting();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (TechnologyOpen) ToggleTechnology();
                else if (HasTargeting || Dragging) CancelTargeting();
                else { host.TogglePause(); CancelTargeting(); }
                return;
            }
            if (host.Mode != FrontendMode.MainMenu)
            {
                if (Input.GetKeyDown(KeyCode.F2)) ToggleTechnology();
                if (Input.GetKeyDown(KeyCode.F5)) host.SaveMatch();
                if (Input.GetKeyDown(KeyCode.F9)) { CancelTargeting(); host.LoadMatch(); return; }
            }
            if (TechnologyOpen) { lastMouse = Input.mousePosition; return; }
            bool interactive = host.Mode == FrontendMode.Playing || host.Mode == FrontendMode.Replay;
            if (!interactive) { CancelTargeting(); lastMouse = Input.mousePosition; ApplyCamera(false); return; }
            if (Input.GetKeyDown(KeyCode.Space)) FocusBase();
            var screenMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool worldArea = IsWorldArea(screenMouse);
            Vector3 pan = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) pan.z += 1;
            if (Input.GetKey(KeyCode.S) && Selection.Count == 0 || Input.GetKey(KeyCode.DownArrow)) pan.z -= 1;
            if (Input.GetKey(KeyCode.A) && Selection.Count == 0 || Input.GetKey(KeyCode.LeftArrow)) pan.x -= 1;
            if (Input.GetKey(KeyCode.D) && Selection.Count == 0 || Input.GetKey(KeyCode.RightArrow)) pan.x += 1;
            if (Input.GetMouseButton(2))
            {
                Vector3 mouseDelta = Input.mousePosition - lastMouse;
                pan += new Vector3(-mouseDelta.x, 0, -mouseDelta.y) * .12f;
            }
            lastMouse = Input.mousePosition;
            Focus(CameraFocus + pan * (cameraHeight * .65f * Time.unscaledDeltaTime));
            if (worldArea) cameraHeight = Mathf.Clamp(cameraHeight - Input.mouseScrollDelta.y * 2.5f, 16, 48);
            ApplyCamera(false);
            bool hasGround = GroundPoint(Input.mousePosition, out var point);
            if (hasGround) CursorWorld = SimPoint.World(point.x, point.z);
            if (preview != null)
            {
                preview.transform.position = BattleView.Position(CursorWorld);
                PlacementValid = hasGround && host.Match.CanPlace(BuildId, CursorWorld, 0);
                preview.transform.localScale = new Vector3(1, PlacementValid ? 1 : .35f, 1);
            }
            if (CanIssueCommands)
            {
                if (Input.GetKeyDown(host.UiCommands.TrainKey)) Train();
                if (Selection.Count > 0)
                    foreach (var shortcut in host.UiCommands.BuildShortcuts)
                        if (Input.GetKeyDown(shortcut.Value)) BeginBuild(shortcut.Key);
                if (Input.GetKeyDown(KeyCode.A) && Selection.Count > 0) BeginAttack();
                if (Input.GetKeyDown(KeyCode.P)) BeginPatrol();
                if (Input.GetKeyDown(KeyCode.Y)) BeginRepair();
                if (Input.GetKeyDown(KeyCode.S) && Selection.Count > 0) Order(CommandKind.Stop);
                if (Input.GetKeyDown(KeyCode.H)) Order(CommandKind.Hold);
                if (Input.GetKeyDown(KeyCode.T)) Order(CommandKind.Stim);
                if (Input.GetKeyDown(KeyCode.X)) Order(CommandKind.Cancel);
            }
            for (int i = 1; i <= 9; i++)
            {
                if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i))) continue;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    groups[i] = host.IsReplay ? Selection.ToArray() : OwnSelection();
                else if (groups.TryGetValue(i, out var members))
                {
                    CancelTargeting(); Selection.Clear();
                    Selection.AddRange(members.Where(id => host.Observation.Entities.Exists(e => e.Id == id)));
                }
            }
            if (Input.GetMouseButtonDown(0) && MapRect.Contains(screenMouse))
            {
                CancelDrag();
                float x = (screenMouse.x - MapRect.x) / MapRect.width * host.Rules.MapWidth;
                float z = (1 - (screenMouse.y - MapRect.y) / MapRect.height) * host.Rules.MapHeight;
                Focus(new Vector3(x, 0, z)); return;
            }
            if (Input.GetMouseButtonDown(1) && (HasTargeting || Dragging)) { CancelTargeting(); return; }
            if (Input.GetMouseButtonDown(0) && worldArea)
            {
                if (HasTargeting)
                {
                    if (CanIssueCommands && hasGround) ConfirmTarget(screenMouse);
                    return;
                }
                down = DragStart = DragCurrent = screenMouse; Dragging = true;
            }
            if (Dragging) DragCurrent = screenMouse;
            if (Input.GetMouseButtonUp(0) && Dragging)
            {
                Dragging = false;
                if (!worldArea) { CancelDrag(); return; }
                if (!Shift()) Selection.Clear();
                if (Vector2.Distance(down, screenMouse) > 8)
                {
                    var box = DragRectangle();
                    foreach (var entity in host.Observation.Entities)
                    {
                        var definition = host.Definition(entity.DefinitionId);
                        if ((!host.IsReplay && entity.Owner != 0) || definition.IsBuilding || definition.IsResource) continue;
                        var p = host.GameCamera.WorldToScreenPoint(BattleView.Position(entity.Position));
                        if (p.z > 0 && box.Contains(new Vector2(p.x, Screen.height - p.y)) && !Selection.Contains(entity.Id)) Selection.Add(entity.Id);
                    }
                }
                else
                {
                    var hit = PickEntity(screenMouse);
                    if (hit != null)
                    {
                        if (Selection.Contains(hit.Id) && Shift()) Selection.Remove(hit.Id);
                        else if (!Selection.Contains(hit.Id)) Selection.Add(hit.Id);
                    }
                }
                CancelDrag();
            }
            if (CanIssueCommands && Input.GetMouseButtonDown(1) && worldArea && hasGround)
                ContextOrder(PickEntity(screenMouse));
        }

        private void ConfirmTarget(Vector2 screenMouse)
        {
            if (targeting == TargetMode.Build)
            {
                var workers = OwnWorkers();
                if (workers.Length == 0 || !PlacementValid) return;
                host.Issue(CommandKind.Build, new[] { workers[0] }, CursorWorld, definitionId: BuildId);
            }
            else if (RepairTargeting)
            {
                var target = PickEntity(screenMouse);
                if (target == null) return;
                var workers = OwnWorkers(target.Id);
                if (workers.Length == 0) return;
                // Selection supplies intent; Match owns repair eligibility, range and costs.
                host.Issue(CommandKind.Repair, workers, targetId: target.Id, append: Shift());
            }
            else host.Issue(PatrolTargeting ? CommandKind.Patrol : CommandKind.AttackMove, OwnMobileSelection(), CursorWorld, append: Shift());
            CancelTargeting();
        }

        private void ContextOrder(EntityState target)
        {
            if (target != null && target.Owner == 0)
            {
                var definition = host.Definition(target.DefinitionId);
                var workers = OwnWorkers(target.Id);
                if (workers.Length > 0)
                {
                    if (!target.Completed && definition.IsBuilding)
                    {
                        host.Issue(CommandKind.Build, new[] { workers[0] }, targetId: target.Id); return;
                    }
                    if (target.Completed && definition.Repairable && target.Health < definition.MaxHealth)
                    {
                        host.Issue(CommandKind.Repair, workers, targetId: target.Id, append: Shift()); return;
                    }
                }
            }
            var actors = OwnSelection();
            if (target != null && (host.Definition(target.DefinitionId).IsResource || target.DefinitionId == "refinery") &&
                actors.Length > 0 && actors.All(id => host.Observation.Entities.Exists(e => e.Id == id && e.Completed && host.Definition(e.DefinitionId).IsBuilding)))
            {
                host.Issue(CommandKind.Move, actors, CursorWorld, target.Id); return;
            }
            var kind = target == null ? CommandKind.Move : target.Owner >= 0 && target.Owner != 0 ? CommandKind.Attack :
                host.Definition(target.DefinitionId).IsResource || target.DefinitionId == "refinery" ? CommandKind.Gather : CommandKind.Move;
            host.Issue(kind, actors, CursorWorld, target?.Id ?? 0, append: Shift());
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelTargeting(); lastMouse = Input.mousePosition; }
        private void OnDisable() { CancelTargeting(); }

        public Rect DragRectangle() => Rect.MinMaxRect(Mathf.Min(DragStart.x, DragCurrent.x), Mathf.Min(DragStart.y, DragCurrent.y), Mathf.Max(DragStart.x, DragCurrent.x), Mathf.Max(DragStart.y, DragCurrent.y));
        private static bool Shift() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        private bool GroundPoint(Vector3 mouse, out Vector3 point)
        {
            var ray = host.GameCamera.ScreenPointToRay(mouse);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
            point = default; return false;
        }
        private EntityState PickEntity(Vector2 mouse)
        {
            EntityState best = null; float score = float.MaxValue;
            foreach (var entity in host.Observation.Entities)
            {
                var definition = host.Definition(entity.DefinitionId);
                var p = host.GameCamera.WorldToScreenPoint(BattleView.Position(entity.Position) + Vector3.up * (definition.IsBuilding ? 1 : .5f));
                if (p.z < 0) continue;
                float distance = Vector2.Distance(mouse, new Vector2(p.x, Screen.height - p.y));
                float radius = Mathf.Clamp(definition.Radius / 1000f * Screen.height / (cameraHeight * .8f), 12, 65);
                if (distance < radius && distance / radius < score) { score = distance / radius; best = entity; }
            }
            return best;
        }
        private void ApplyCamera(bool immediate)
        {
            var offset = new Vector3(0, cameraHeight, -cameraHeight * .65f);
            var desired = CameraFocus + offset;
            host.GameCamera.transform.position = immediate ? desired : Vector3.Lerp(host.GameCamera.transform.position, desired, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12));
        }
    }
}
