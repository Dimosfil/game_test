using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColonyRTS.Diagnostics;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Presentation
{
    public sealed class BattleHud : MonoBehaviour
    {
        private GameHost host;
        private readonly Color ink = new Color(.025f,.043f,.063f,.98f);
        private readonly Color panel = new Color(.045f,.073f,.099f,.97f);
        private readonly Color cyan = new Color(.25f,.88f,1);
        private readonly Color muted = new Color(.47f,.61f,.69f);
        private readonly Dictionary<int, GUIStyle> labels = new Dictionary<int, GUIStyle>();
        private readonly HashSet<int> selectedIds = new HashSet<int>();
        private readonly List<EntityState> selectedEntities = new List<EntityState>();
        private readonly List<int> capturedSelection = new List<int>();
        private PlayerObservation capturedSelectionObservation;
        private bool capturedSelectionValid;
        private enum HudActionKind { Build, Repair, Cancel, Research, Technology, Train, Attack, Patrol, Stop, Hold, Stim }
        private struct HudAction
        {
            public string Label,Id;
            public HudActionKind Kind;
            public bool Active;
            public HudIcon Icon;
            public int Producer;
        }
        private readonly List<HudAction> commandActions=new List<HudAction>(8);
        private readonly Dictionary<string,string> buildCaptions=new Dictionary<string,string>();
        private readonly Dictionary<string,string> trainCaptions=new Dictionary<string,string>();
        private readonly HashSet<string> trainingShown=new HashSet<string>();
        private readonly Dictionary<string,string> researchCaptions=new Dictionary<string,string>();
        private readonly Dictionary<string,string> completedResearchCaptions=new Dictionary<string,string>();
        private readonly Dictionary<string,UpgradeDefinition[]> researchGroups=new Dictionary<string,UpgradeDefinition[]>();
        private string mapCaption,mineralsCaption,gasCaption,supplyCaption,clockCaption,selectionCaption,selectionStats;
        private int cachedMinerals=int.MinValue,cachedGas=int.MinValue,cachedSupply=int.MinValue,cachedSupplyCap=int.MinValue,cachedSeconds=int.MinValue;
        private int cachedSelectionCount=-1,cachedHealth=int.MinValue,cachedMaxHealth=int.MinValue,cachedDamage=int.MinValue,cachedArmor=int.MinValue;
        private string cachedSelectionDefinition;
        private GUIStyle button;
        private Texture2D minimap;
        private Color32[] miniPixels;
        private int mapTick = -1;
        private int skinHeight;
        private TechnologyPanel technology;
        private HudHealthBarBatch healthBars;
        public int WorldHealthBarCount => healthBars == null ? 0 : healthBars.BarCount;
        public int WorldHealthBarDrawCalls => healthBars == null ? 0 : healthBars.DrawCalls;
        public HudHealthBarBatch.FrameEvidence DiagnosticHealthBars=>healthBars==null?default:healthBars.DiagnosticFrame;
        public int HealthBarVertexCapacity=>healthBars==null?0:healthBars.VertexCapacity;
        public int HealthBarIndexCapacity=>healthBars==null?0:healthBars.IndexCapacity;
        public long HealthBarBufferReserveCount=>healthBars==null?0:healthBars.BufferReserveCount;
        public long HealthBarIndexUploadCount=>healthBars==null?0:healthBars.IndexUploadCount;
        public long HealthBarVertexUploadCount=>healthBars==null?0:healthBars.VertexUploadCount;
        public long HealthBarExactGeometryReuseCount=>healthBars==null?0:healthBars.ExactGeometryReuseCount;
        public double DiagnosticGuiMs { get; private set; }
        public int DiagnosticGuiFrame { get; private set; } = -1;
        public bool DiagnosticGuiThreadCyclesEnabled { get; set; }
        // Cumulative helper attempts/successes, including failed/unsupported reads.
        public long DiagnosticGuiThreadCycleReadAttempts { get; private set; }
        public long DiagnosticGuiThreadCycleSuccessfulReads { get; private set; }
        [System.Serializable]
        public struct GuiThreadCycleEvidence
        {
            public int Calls, StartReadAttempts, EndReadAttempts, SuccessfulPairs, CompletedCalls;
            // Raw frequency-dependent cycles. Only successful monotonic pairs are
            // summed; Available distinguishes an unavailable sum from measured zero.
            public ulong Cycles;
            public bool Available, Completed;
        }
        [System.Serializable]
        public struct GuiSectionEvidence
        {
            public bool Recorded, Completed;
            public bool ThreadCyclesRequested;
            public int Frame, Calls, RepaintCalls;
            public GuiThreadCycleEvidence TotalThreadCycles, WorldLabelsThreadCycles, BottomThreadCycles;
            // Bottom contains Minimap, SelectionPanel and Commands; WorldLabels
            // contains health-bar upload/draw. These nested times must not be summed.
            public double HeaderMs, SelectionCaptureMs, WorldLabelsMs, BottomMs;
            public double MinimapMs, SelectionPanelMs, CommandsMs;
        }
        private GuiSectionEvidence guiSections;
        private bool recordGuiSections;
        private bool recordGuiThreadCycles;
        private struct GuiThreadCycleStart
        {
            public bool Requested, Available;
            public ulong Cycles;
        }
        public GuiSectionEvidence DiagnosticGuiSections => guiSections;
        private float Scale => Mathf.Clamp(Screen.height / 900f, .65f, 1.5f);
        public void Initialize(GameHost owner)
        {
            capturedSelectionValid=false;capturedSelectionObservation=null;capturedSelection.Clear();
            host=owner;HudIconAtlas.Initialize();healthBars=new HudHealthBarBatch();
            mapCaption=host.Rules.MapName.ToUpperInvariant();
            foreach(var definition in host.Rules.Definitions)
            {
                buildCaptions.Add(definition.Id,host.UiCommands.BuildCaption(definition)+"\n"+RuntimeText.Cost(definition.MineralCost,definition.GasCost));
                trainCaptions.Add(definition.Id,host.UiCommands.TrainCaption(definition)+"\n"+RuntimeText.Cost(definition.MineralCost,definition.GasCost));
            }
            foreach(var group in (host.Rules.Upgrades??new UpgradeDefinition[0]).GroupBy(u=>u.ProducerId))
                researchGroups.Add(group.Key,group.ToArray());
            foreach(var upgrade in host.Rules.Upgrades??new UpgradeDefinition[0])
            {
                researchCaptions.Add(upgrade.Id,upgrade.DisplayName+"\n"+RuntimeText.Cost(upgrade.MineralCost,upgrade.GasCost));
                completedResearchCaptions.Add(upgrade.Id,upgrade.DisplayName+"\n"+L("tech.done"));
            }
        }
        private static string L(string key) => RuntimeText.Get(key);

        private void OnGUI()
        {
            if(host==null||!host.DiagnosticProfilingEnabled) { DrawGui();return; }
            if(DiagnosticGuiFrame!=Time.frameCount)
            {
                DiagnosticGuiFrame=Time.frameCount;DiagnosticGuiMs=0;
                guiSections=new GuiSectionEvidence { Frame=DiagnosticGuiFrame,Completed=true };
            }
            bool previousCompleted=guiSections.Completed, completed=false;
            bool previousRecording=recordGuiSections;
            bool previousThreadRecording=recordGuiThreadCycles;
            guiSections.Recorded=true;guiSections.Completed=false;guiSections.Calls++;
            if(Event.current.type==EventType.Repaint)guiSections.RepaintCalls++;
            recordGuiSections=true;
            recordGuiThreadCycles=DiagnosticGuiThreadCyclesEnabled;
            guiSections.ThreadCyclesRequested|=recordGuiThreadCycles;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            var threadStarted=BeginGuiThreadCycles(ref guiSections.TotalThreadCycles);
            try { DrawGui();completed=true; }
            finally
            {
                EndGuiThreadCycles(ref guiSections.TotalThreadCycles,threadStarted,completed);
                DiagnosticGuiMs+=GuiSectionElapsed(started);
                guiSections.Completed=previousCompleted&&completed;
                recordGuiSections=previousRecording;
                recordGuiThreadCycles=previousThreadRecording;
            }
        }

        private static double GuiSectionElapsed(long started) =>
            (System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency;

        private bool ReadGuiThreadCycles(out ulong cycles)
        {
            DiagnosticGuiThreadCycleReadAttempts++;
            bool available=WindowsCpuProbe.TrySampleThreadCycles(out cycles);
            if(available)DiagnosticGuiThreadCycleSuccessfulReads++;
            return available;
        }

        private GuiThreadCycleStart BeginGuiThreadCycles(ref GuiThreadCycleEvidence evidence)
        {
            if(!recordGuiThreadCycles)return default;
            evidence.Calls++;evidence.StartReadAttempts++;
            evidence.Available=false;evidence.Completed=false;
            bool available=ReadGuiThreadCycles(out var cycles);
            return new GuiThreadCycleStart { Requested=true,Available=available,Cycles=cycles };
        }

        private void EndGuiThreadCycles(ref GuiThreadCycleEvidence evidence,GuiThreadCycleStart start,bool completed)
        {
            if(!start.Requested)return;
            evidence.EndReadAttempts++;
            bool endAvailable=ReadGuiThreadCycles(out var end);
            AccumulateGuiThreadCyclePair(ref evidence,start.Available,start.Cycles,endAvailable,end,completed);
        }

        // Keep native sampling separate from aggregation so unavailable reads,
        // counter regression and overflow have one deterministic validity policy.
        private static void AccumulateGuiThreadCyclePair(ref GuiThreadCycleEvidence evidence,
            bool startAvailable,ulong start,bool endAvailable,ulong end,bool completed)
        {
            if(completed)evidence.CompletedCalls++;
            if(startAvailable&&endAvailable&&end>=start)
            {
                ulong delta=end-start;
                if(ulong.MaxValue-evidence.Cycles>=delta)
                {
                    evidence.Cycles+=delta;evidence.SuccessfulPairs++;
                }
            }
            evidence.Available=evidence.Calls>0&&evidence.SuccessfulPairs==evidence.Calls;
            evidence.Completed=evidence.Calls>0&&evidence.CompletedCalls==evidence.Calls;
        }

        private void DrawGui()
        {
            if (host == null || host.Observation == null) return;
            if (skinHeight != Screen.height)
            {
                labels.Clear(); button = null; skinHeight = Screen.height;
            }
            if (button == null)
            {
                button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(13 * Scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                button.normal.textColor = new Color(.8f,.94f,1);
                button.hover.textColor = Color.white;
            }
            bool profiling=recordGuiSections;
            long sectionStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            try { DrawHeader(); }
            finally { if(profiling)guiSections.HeaderMs+=GuiSectionElapsed(sectionStarted); }
            if (host.Mode == FrontendMode.MainMenu) { DrawMainMenu(); return; }
            if (host.Interaction.TechnologyOpen)
            {
                if (technology == null) technology = new TechnologyPanel();
                technology.Draw(host, Scale);
                HudIconAtlas.DrawTooltip(Scale);
                return;
            }
            sectionStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            try { CaptureGuiSelection(); }
            finally { if(profiling)guiSections.SelectionCaptureMs+=GuiSectionElapsed(sectionStarted); }
            sectionStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            var worldThreadStarted=BeginGuiThreadCycles(ref guiSections.WorldLabelsThreadCycles);
            bool worldCompleted=false;
            try { DrawWorldLabels();worldCompleted=true; }
            finally
            {
                EndGuiThreadCycles(ref guiSections.WorldLabelsThreadCycles,worldThreadStarted,worldCompleted);
                if(profiling)guiSections.WorldLabelsMs+=GuiSectionElapsed(sectionStarted);
            }
            sectionStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            var bottomThreadStarted=BeginGuiThreadCycles(ref guiSections.BottomThreadCycles);
            bool bottomCompleted=false;
            try { DrawBottom();bottomCompleted=true; }
            finally
            {
                EndGuiThreadCycles(ref guiSections.BottomThreadCycles,bottomThreadStarted,bottomCompleted);
                if(profiling)guiSections.BottomMs+=GuiSectionElapsed(sectionStarted);
            }
            if (host.Interaction.Dragging)
            {
                var rect = host.Interaction.DragRectangle();
                Fill(rect, new Color(.1f,.8f,1,.1f)); Border(rect, cyan);
            }
            if (host.Interaction.HasTargeting)
            {
                var r = new Rect(Screen.width * .27f, Screen.height - RtsInteraction.HudHeight - 42 * Scale, Screen.width * .46f, 30 * Scale);
                Fill(r, ink); Label(r, L(host.Interaction.TargetingPromptKey), 13, cyan, TextAnchor.MiddleCenter);
            }
            if (Time.unscaledTime < host.NoticeUntil && !string.IsNullOrEmpty(host.Notice))
            {
                var r = new Rect(22 * Scale, 85 * Scale, Screen.width * .58f, 36 * Scale);
                Fill(r, new Color(.02f,.06f,.09f,.88f));
                Fill(new Rect(r.x, r.y, 3, r.height), cyan);
                Label(new Rect(r.x+12,r.y,r.width-20,r.height), host.Notice, 13, Color.white);
            }
            if (host.Mode == FrontendMode.Paused) DrawPause();
            else if (host.Mode == FrontendMode.Results) DrawResults();
            HudIconAtlas.DrawTooltip(Scale);
        }

        private void DrawHeader()
        {
            float s = Scale;
            Fill(new Rect(0,0,Screen.width,62), ink);
            Fill(new Rect(0,60,Screen.width,2), new Color(.13f,.29f,.36f));
            Fill(new Rect(18,17,4,26), cyan);
            Label(new Rect(34,7,310*s,30), host.Rules.ProductTitle, 21, Color.white);
            Label(new Rect(35,34,320*s,17), mapCaption, 10, muted);
            if (host.Mode == FrontendMode.MainMenu) return;
            float statStart = Mathf.Max(300*s, Screen.width*.40f);
            var observation=host.Observation;
            if(cachedMinerals!=observation.Minerals) { cachedMinerals=observation.Minerals;mineralsCaption=cachedMinerals.ToString(); }
            if(cachedGas!=observation.Gas) { cachedGas=observation.Gas;gasCaption=cachedGas.ToString(); }
            if(cachedSupply!=observation.SupplyUsed||cachedSupplyCap!=observation.SupplyCap)
            { cachedSupply=observation.SupplyUsed;cachedSupplyCap=observation.SupplyCap;supplyCaption=cachedSupply+" / "+cachedSupplyCap; }
            Stat(statStart, "minerals", mineralsCaption, cyan);
            Stat(statStart+145*s, "gas", gasCaption, new Color(.4f,.91f,.59f));
            Stat(statStart+275*s, "supply", supplyCaption, new Color(1,.8f,.43f));
            Label(new Rect(Screen.width-205*s,10,95*s,42), Clock(host.Observation.Tick), 18, Color.white, TextAnchor.MiddleCenter);
            if (HudIconAtlas.Button(RtsInteraction.TechnologyButtonRect,HudIcon.Technology,L("tech.open"),s)) host.Interaction.ToggleTechnology();
            if (Button(new Rect(Screen.width-100*s,14,86*s,32), host.Mode==FrontendMode.Paused ? "▶" : "II")) host.TogglePause();
        }
        private void Stat(float x,string key,string value,Color color)
        {
            HudIcon icon=key=="minerals"?HudIcon.Minerals:key=="gas"?HudIcon.Gas:HudIcon.Supply;
            HudIconAtlas.Draw(new Rect(x,14*Scale,34*Scale,34*Scale),icon);
            Label(new Rect(x+41*Scale,8,104*Scale,17), L(key), 9, muted);
            Label(new Rect(x+41*Scale,25,104*Scale,30), value, 21, color);
        }
        private string Clock(int ticks)
        {
            int seconds = ticks / host.Rules.TickRate;
            if(cachedSeconds!=seconds) { cachedSeconds=seconds;clockCaption=(seconds/60).ToString("00")+":"+(seconds%60).ToString("00"); }
            return clockCaption;
        }
        private void CaptureGuiSelection()
        {
            // Published observations are detached read models. Validate the mutable
            // public selection exactly on every GUI event, including same-frame edits.
            var observation=host.Observation;
            var selection=host.Interaction.Selection;
            if(capturedSelectionValid&&object.ReferenceEquals(observation,capturedSelectionObservation)&&
                selection.Count==capturedSelection.Count)
            {
                int matched=0;
                while(matched<selection.Count&&selection[matched]==capturedSelection[matched])matched++;
                if(matched==selection.Count)return;
            }
            capturedSelectionValid=false;
            selectedIds.Clear();
            foreach(int id in selection) selectedIds.Add(id);
            selectedEntities.Clear();
            foreach(var entity in observation.Entities)
                if(selectedIds.Contains(entity.Id)) selectedEntities.Add(entity);
            capturedSelection.Clear();capturedSelection.AddRange(selection);
            capturedSelectionObservation=observation;capturedSelectionValid=true;
        }
        private void DrawBottom()
        {
            float s = Scale, h=RtsInteraction.HudHeight, y=Screen.height-h;
            Fill(new Rect(0,y,Screen.width,h), ink);
            Fill(new Rect(0,y,Screen.width,2), new Color(.18f,.36f,.44f));
            bool profiling=recordGuiSections;
            long sectionStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            try { DrawMinimap(); }
            finally { if(profiling)guiSections.MinimapMs+=GuiSectionElapsed(sectionStarted); }
            var map=RtsInteraction.MapRect;
            float left=map.xMax+22*s;
            float selectionWidth=Mathf.Clamp(Screen.width*.27f,270*s,450*s);
            var selected=selectedEntities;
            sectionStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            try
            {
                Fill(new Rect(left,y+14*s,selectionWidth,h-44*s),panel);
                Label(new Rect(left+14*s,y+19*s,selectionWidth-28*s,22*s),L("selection"),10,muted);
                if(selected.Count==0)
                {
                    Label(new Rect(left+14*s,y+47*s,selectionWidth-28*s,30*s),L("no.selection"),16,Color.white);
                    Label(new Rect(left+14*s,y+80*s,selectionWidth-28*s,93*s),L("no.selection.hint"),12,muted,TextAnchor.UpperLeft);
                }
                else DrawSelection(selected,new Rect(left+14*s,y+44*s,selectionWidth-28*s,h-78*s));
            }
            finally { if(profiling)guiSections.SelectionPanelMs+=GuiSectionElapsed(sectionStarted); }
            float commandsX=left+selectionWidth+18*s;
            sectionStarted=profiling?System.Diagnostics.Stopwatch.GetTimestamp():0;
            try
            {
                Label(new Rect(commandsX,y+16*s,Screen.width-commandsX-20*s,22*s),L(host.IsReplay?"replay.readonly":"orders"),10,muted);
                DrawCommands(selected,new Rect(commandsX,y+44*s,Screen.width-commandsX-18*s,h-77*s));
            }
            finally { if(profiling)guiSections.CommandsMs+=GuiSectionElapsed(sectionStarted); }
            Label(new Rect(left,y+h-24*s,Screen.width-left-18*s,20*s),L("controls"),10,muted);
            if(host.IsReplay)
            {
                Label(new Rect(20*s,71*s,350*s,28*s),L("replay.mode")+"  ×"+host.Speed,13,cyan);
                if(Button(new Rect(Screen.width-88*s,75*s,68*s,30*s),"×"+host.Speed)) host.SetSpeed(host.Speed>=8?1:host.Speed*2);
            }
        }
        private void DrawSelection(List<EntityState> selected,Rect area)
        {
            float s=Scale;
            var entity=selected[0];
            var definition=host.Definition(entity.DefinitionId);
            if(cachedSelectionCount!=selected.Count||cachedSelectionDefinition!=entity.DefinitionId)
            {
                cachedSelectionCount=selected.Count;cachedSelectionDefinition=entity.DefinitionId;
                selectionCaption=selected.Count>1?selected.Count+"  /  "+L("selection"):definition.DisplayName;
            }
            var portrait=new Rect(area.x,area.y,49*s,49*s);
            HudIconAtlas.Draw(portrait,HudIconAtlas.ForDefinition(entity.DefinitionId));
            HudIconAtlas.Border(portrait,new Color(.23f,.48f,.55f));
            float titleX=area.x+60*s, titleWidth=area.width-60*s;
            Label(new Rect(titleX,area.y,titleWidth,30*s),selectionCaption,18,Color.white);
            Color team=entity.Owner==1?new Color(1,.38f,.28f):cyan;
            var health=new Rect(titleX,area.y+36*s,titleWidth,7*s);
            Fill(health,new Color(.12f,.16f,.18f));
            Fill(new Rect(health.x,health.y,health.width*entity.Health/Mathf.Max(1,definition.MaxHealth),health.height),team);
            bool combatStats=entity.Owner==0&&selected.Count==1&&definition.Damage>0;
            int damage=combatStats?host.ObservedDamage(entity.DefinitionId):-1,armor=combatStats?host.ObservedArmor(entity.DefinitionId):-1;
            if(cachedHealth!=entity.Health||cachedMaxHealth!=definition.MaxHealth||cachedDamage!=damage||cachedArmor!=armor)
            {
                cachedHealth=entity.Health;cachedMaxHealth=definition.MaxHealth;cachedDamage=damage;cachedArmor=armor;
                selectionStats=entity.Health+" / "+definition.MaxHealth+" HP";
                if(combatStats)selectionStats+="    "+RuntimeText.Format("combat.stats",damage,armor);
            }
            Label(new Rect(area.x,area.y+48*s,area.width,23*s),selectionStats,12,muted);
            if(!entity.Completed)
            {
                float progress=1-entity.ConstructionRemaining/(float)Mathf.Max(1,entity.ConstructionTotal);
                Progress(new Rect(area.x,area.y+87*s,area.width,30*s),L("construction"),progress);
            }
            else if(entity.Research.Count>0)
            {
                var item=entity.Research[0];
                Progress(new Rect(area.x,area.y+87*s,area.width,30*s),host.Upgrade(item.DefinitionId).DisplayName,1-item.RemainingTicks/(float)Mathf.Max(1,item.TotalTicks));
            }
            else if(entity.Production.Count>0)
            {
                var item=entity.Production[0];
                Progress(new Rect(area.x,area.y+87*s,area.width,30*s),L("production")+"  "+entity.Production.Count,1-item.RemainingTicks/(float)Mathf.Max(1,item.TotalTicks));
            }
            else if(selected.Count>1&&!(definition.IsBuilding&&entity.RallyTargetId>0))
            {
                float size=23*s;
                for(int i=0;i<Mathf.Min(selected.Count,24);i++)
                {
                    var r=new Rect(area.x+(i%12)*(size+3),area.y+85*s+(i/12)*(size+3),size,size);
                    Fill(r,new Color(.07f,.2f,.25f)); Border(r,team);
                    HudIconAtlas.Draw(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),HudIconAtlas.ForDefinition(selected[i].DefinitionId));
                }
            }
            else if(entity.Cargo>0)
            {
                HudIconAtlas.Draw(new Rect(area.x,area.y+86*s,24*s,24*s),entity.GasCargo?HudIcon.Gas:HudIcon.Minerals);
                Label(new Rect(area.x+31*s,area.y+85*s,area.width-31*s,30*s),"+ "+entity.Cargo+"  "+L(entity.GasCargo?"gas":"minerals"),12,team);
            }
            else if(selected.Count==1&&entity.Owner==0&&!definition.IsBuilding&&entity.Orders.Count>0)
            {
                var order=entity.Orders[0].Kind;
                if(order==OrderKind.Patrol||order==OrderKind.Repair)
                {
                    HudIconAtlas.Draw(new Rect(area.x,area.y+87*s,22*s,22*s),order==OrderKind.Patrol?HudIcon.Patrol:HudIcon.Repair);
                    Label(new Rect(area.x+29*s,area.y+85*s,area.width-29*s,30*s),L(order==OrderKind.Patrol?"order.patrol":"order.repair"),12,cyan);
                }
                else if(order==OrderKind.Move)
                {
                    HudIconAtlas.Draw(new Rect(area.x,area.y+87*s,22*s,22*s),HudIcon.Move);
                    Label(new Rect(area.x+29*s,area.y+85*s,area.width-29*s,30*s),L("move"),12,cyan);
                }
            }
            if(entity.RallyTargetId>0&&definition.IsBuilding&&entity.Owner==0)
            {
                float rallyY=area.y+(entity.Production.Count>0?119:85)*s;
                HudIconAtlas.Draw(new Rect(area.x,rallyY,18*s,18*s),HudIcon.Rally);
                Label(new Rect(area.x+24*s,rallyY,area.width-24*s,18*s),L("rally.resource"),10,cyan);
            }
        }
        private void Progress(Rect rect,string text,float value)
        {
            Label(new Rect(rect.x,rect.y,rect.width,18*Scale),text,10,muted);
            var bar=new Rect(rect.x,rect.y+22*Scale,rect.width,5*Scale);
            Fill(bar,new Color(.12f,.16f,.18f)); Fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(value),bar.height),cyan);
        }
        private void DrawCommands(List<EntityState> selected,Rect rect)
        {
            bool own=false,worker=false,producer=false,stim=false,building=false,unfinished=false;
            foreach(var entity in selected)
            {
                if(entity.Owner!=0)continue;
                own=true;
                var definition=host.Definition(entity.DefinitionId);
                worker|=definition.IsWorker;building|=definition.IsBuilding;unfinished|=!entity.Completed;
                producer|=host.UiCommands.TrainingFor(entity.DefinitionId).Count>0;
                stim|=!definition.IsBuilding&&!definition.IsWorker&&definition.Damage>0;
            }
            var researcher = selected.Count == 1 && selected[0].Owner == 0 ? selected[0] : null;
            UpgradeDefinition[] upgrades=null;
            bool researchProducer=researcher!=null&&researchGroups.TryGetValue(researcher.DefinitionId,out upgrades);
            bool enabled=host.Mode==FrontendMode.Playing;
            commandActions.Clear();
            if(worker)
            {
                foreach(var definition in host.UiCommands.Buildings)AddBuild(definition.Id);
                AddAction(L("repair"),HudActionKind.Repair,true,HudIcon.Repair);
                AddAction(L("cancel"),HudActionKind.Cancel,true,HudIcon.Cancel);
            }
            else if(researchProducer||producer)
            {
                foreach(var upgrade in upgrades??System.Array.Empty<UpgradeDefinition>())
                {
                    string id = upgrade.Id;
                    bool done = host.Observation.CompletedUpgrades.Contains(id);
                    var availability = host.ResearchAvailability(researcher.Id, id);
                    AddAction(done?completedResearchCaptions[id]:researchCaptions[id],HudActionKind.Research,availability.Accepted,HudIconAtlas.ForUpgrade(upgrade),id,researcher.Id);
                }
                trainingShown.Clear();
                foreach(var entity in selected)
                    if(entity.Owner==0)
                        foreach(var definition in host.UiCommands.TrainingFor(entity.DefinitionId))
                            if(trainingShown.Add(definition.Id))
                                AddAction(trainCaptions[definition.Id],HudActionKind.Train,entity.Completed,HudIconAtlas.ForDefinition(definition.Id),definition.Id);
                AddAction(L("cancel"),HudActionKind.Cancel,producer||researcher.Research.Count>0||!researcher.Completed,HudIcon.Cancel);
                if(researchProducer)AddAction(L("tech.open"),HudActionKind.Technology,true,HudIcon.Technology);
            }
            else if(building)
            {
                AddAction(L("cancel"),HudActionKind.Cancel,unfinished,HudIcon.Cancel);
                AddAction(L("tech.open"),HudActionKind.Technology,true,HudIcon.Technology);
            }
            else
            {
                AddAction(L("attack"),HudActionKind.Attack,own,HudIcon.Attack);
                AddAction(L("patrol"),HudActionKind.Patrol,own,HudIcon.Patrol);
                AddAction(L("stop"),HudActionKind.Stop,own,HudIcon.Stop);
                AddAction(L("hold"),HudActionKind.Hold,own,HudIcon.Hold);
                AddAction(L("stim"),HudActionKind.Stim,stim,HudIcon.Stim);
            }
            int cols=3,rows=Mathf.Max(1,Mathf.CeilToInt(commandActions.Count/(float)cols));
            float gap=6*Scale,width=(rect.width-gap*(cols-1))/cols,height=Mathf.Min(58*Scale,(rect.height-gap*(rows-1))/rows);
            for(int i=0;i<commandActions.Count;i++)
            {
                var action=commandActions[i];
                GUI.enabled=enabled&&action.Active;
                if(HudIconAtlas.Button(new Rect(rect.x+(i%cols)*(width+gap),rect.y+(i/cols)*(height+gap),width,height),action.Icon,action.Label,Scale))InvokeAction(action);
            }
            GUI.enabled=true;
        }
        private void AddBuild(string id)=>AddAction(buildCaptions[id],HudActionKind.Build,true,HudIconAtlas.ForDefinition(id),id);
        private void AddAction(string label,HudActionKind kind,bool active,HudIcon icon,string id=null,int producer=0)
        {
            commandActions.Add(new HudAction{Label=label,Kind=kind,Active=active,Icon=icon,Id=id,Producer=producer});
        }
        private void InvokeAction(HudAction action)
        {
            switch(action.Kind)
            {
                case HudActionKind.Build:host.Interaction.BeginBuild(action.Id);break;
                case HudActionKind.Repair:host.Interaction.BeginRepair();break;
                case HudActionKind.Cancel:host.Interaction.Order(CommandKind.Cancel);break;
                case HudActionKind.Research:host.Interaction.Research(action.Producer,action.Id);break;
                case HudActionKind.Technology:host.Interaction.ToggleTechnology();break;
                case HudActionKind.Train:host.Interaction.Train(action.Id);break;
                case HudActionKind.Attack:host.Interaction.BeginAttack();break;
                case HudActionKind.Patrol:host.Interaction.BeginPatrol();break;
                case HudActionKind.Stop:host.Interaction.Order(CommandKind.Stop);break;
                case HudActionKind.Hold:host.Interaction.Order(CommandKind.Hold);break;
                case HudActionKind.Stim:host.Interaction.Order(CommandKind.Stim);break;
            }
        }
        private void DrawMinimap()
        {
            var observation=host.Observation;
            int width=host.Rules.MapWidth,height=host.Rules.MapHeight;
            if(minimap==null)
            {
                minimap=new Texture2D(width,height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
                miniPixels=new Color32[width*height];
            }
            if(mapTick!=observation.Tick)
            {
                mapTick=observation.Tick;
                for(int i=0;i<miniPixels.Length;i++) miniPixels[i]=observation.Visible[i]?new Color32(46,64,67,255):observation.Explored[i]?new Color32(24,34,40,255):new Color32(7,15,22,255);
                foreach(var b in host.Rules.Blocks)
                    for(int z=b.Z;z<b.Z+b.Height;z++) for(int x=b.X;x<b.X+b.Width;x++)
                        if(x>=0&&z>=0&&x<width&&z<height&&observation.Explored[z*width+x]) miniPixels[z*width+x]=new Color32(70,64,56,255);
                foreach(var e in observation.Entities)
                {
                    var color=e.Owner==0?new Color32(60,225,255,255):e.Owner==1?new Color32(255,80,60,255):new Color32(93,135,201,255);
                    int radius=host.Definition(e.DefinitionId).IsBuilding?1:0;
                    for(int dz=-radius;dz<=radius;dz++) for(int dx=-radius;dx<=radius;dx++)
                    {
                        int x=e.Position.X/1000+dx,z=e.Position.Z/1000+dz;
                        if(x>=0&&z>=0&&x<width&&z<height) miniPixels[z*width+x]=color;
                    }
                }
                minimap.SetPixels32(miniPixels); minimap.Apply(false,false);
            }
            var map=RtsInteraction.MapRect;
            GUI.DrawTexture(map,minimap,ScaleMode.StretchToFill);
            Border(map,new Color(.24f,.48f,.57f));
            var focus=host.Interaction.CameraFocus;
            float cx=map.x+focus.x/width*map.width,cz=map.y+(1-focus.z/height)*map.height;
            Border(new Rect(cx-16*Scale,cz-12*Scale,32*Scale,24*Scale),new Color(.8f,.95f,1,.7f));
        }
        private void DrawWorldLabels()
        {
            // This pass has no controls or layout IDs: projecting hundreds of
            // health bars during Layout/MouseMove cannot change input behavior.
            if(Event.current.type!=EventType.Repaint) return;
            var camera=host.GameCamera;
            // Screen dimensions belong to this GUI event; avoid a native height
            // query for every projected unit, refreshing on every Repaint.
            int screenHeight=Screen.height;
            float viewportBottom=screenHeight-RtsInteraction.HudHeight;
            healthBars.Begin(host.Observation.Entities.Count,screenHeight,new Rect(0,64,Screen.width,viewportBottom-64));
            foreach(var entity in host.Observation.Entities)
            {
                var def=host.Definition(entity.DefinitionId);
                if(def.IsResource||(!selectedIds.Contains(entity.Id)&&entity.Health==def.MaxHealth&&entity.Completed)) continue;
                var screen=camera.WorldToScreenPoint(BattleView.Position(entity.Position)+Vector3.up*(def.IsBuilding?3:1.7f));
                float y=screenHeight-screen.y;
                if(screen.z<0||y<64||y>viewportBottom) continue;
                float w=def.IsBuilding?62:32;
                var rect=new Rect(screen.x-w/2,y,w,4);
                healthBars.Add(rect,new Rect(rect.x+1,rect.y+1,(w-2)*entity.Health/Mathf.Max(1,def.MaxHealth),2),entity.Owner==1?new Color(1,.3f,.18f):new Color(.25f,.95f,.65f));
            }
            healthBars.Draw(host.DiagnosticProfilingEnabled);
        }
        private void DrawMainMenu()
        {
            float s=Scale;
            Fill(new Rect(0,62,Screen.width,Screen.height-62),new Color(.015f,.03f,.05f,.65f));
            float x=Screen.width*.09f,y=Screen.height*.22f;
            Label(new Rect(x,y,Screen.width*.7f,30*s),L("tagline"),12,cyan);
            Label(new Rect(x,y+37*s,Screen.width*.8f,90*s),host.Rules.ProductTitle,56,Color.white);
            Fill(new Rect(x,y+139*s,55*s,3*s),cyan);
            Label(new Rect(x,y+166*s,Screen.width*.7f,30*s),L("menu.subtitle"),13,muted);
            Label(new Rect(x,y+211*s,Screen.width*.75f,75*s),L("menu.description"),20,Color.white,TextAnchor.UpperLeft);
            if(Button(new Rect(x,y+315*s,330*s,56*s),L("menu.start"))) host.StartMatch();
            GUI.enabled=File.Exists(host.SavePath);
            if(Button(new Rect(x,y+385*s,330*s,42*s),L("menu.load"))) host.LoadMatch();
            GUI.enabled=true;
            Label(new Rect(x,Screen.height-62*s,Screen.width*.85f,26*s),L("prototype"),11,muted);
        }
        private void DrawPause()
        {
            float s=Scale; var r=new Rect(Screen.width/2-210*s,Screen.height/2-230*s,420*s,460*s);
            Fill(new Rect(0,62,Screen.width,Screen.height-62),new Color(0,.015f,.025f,.62f));
            Fill(r,ink); Border(r,new Color(.17f,.39f,.47f));
            Label(new Rect(r.x,r.y+20*s,r.width,45*s),L("pause"),28,Color.white,TextAnchor.MiddleCenter);
            string[] keys={"resume","save","load","replay","menu"};
            for(int i=0;i<keys.Length;i++)
            {
                GUI.enabled=!(host.IsReplay&&(i==1||i==3));
                if(Button(new Rect(r.x+35*s,r.y+85*s+i*62*s,r.width-70*s,48*s),L(keys[i])))
                {
                    if(i==0)host.TogglePause();else if(i==1)host.SaveMatch();else if(i==2)host.LoadMatch();else if(i==3)host.WatchReplay();else host.ReturnToMenu();
                }
            }
            GUI.enabled=true;
        }
        private void DrawResults()
        {
            float s=Scale; var r=new Rect(Screen.width/2-320*s,Screen.height/2-195*s,640*s,380*s);
            Fill(new Rect(0,62,Screen.width,Screen.height-62),new Color(0,.015f,.025f,.58f));
            Fill(r,ink); Border(r,cyan);
            string key=host.IsReplay?"replay.end":host.Observation.Winner==0?"victory":"defeat";
            Label(new Rect(r.x+20*s,r.y+30*s,r.width-40*s,65*s),L(key),42,host.Observation.Winner==0?cyan:new Color(1,.45f,.3f),TextAnchor.MiddleCenter);
            string bodyKey=host.IsReplay&&host.Observation.Phase!=MatchPhase.Finished?"replay.end.text":host.Observation.Winner==0?"victory.text":"defeat.text";
            Label(new Rect(r.x+35*s,r.y+112*s,r.width-70*s,65*s),L(bodyKey),16,Color.white,TextAnchor.MiddleCenter);
            Label(new Rect(r.x,r.y+187*s,r.width,30*s),Clock(host.Observation.Tick)+"   /   "+host.Rules.MapName,13,muted,TextAnchor.MiddleCenter);
            if(Button(new Rect(r.x+34*s,r.y+252*s,270*s,50*s),L("restart")))host.StartMatch();
            GUI.enabled=!host.IsReplay;
            if(Button(new Rect(r.x+336*s,r.y+252*s,270*s,50*s),L("replay")))host.WatchReplay();
            GUI.enabled=true;
            if(Button(new Rect(r.x+170*s,r.y+318*s,300*s,36*s),L("menu")))host.ReturnToMenu();
        }
        private bool Button(Rect rect,string text)
        {
            Fill(rect,new Color(.075f,.15f,.19f,.96f));
            Border(rect,new Color(.2f,.41f,.49f));
            return GUI.Button(rect,text,button);
        }
        private GUIStyle Style(int size)
        {
            int pixels=Mathf.RoundToInt(size*Scale);
            if(!labels.TryGetValue(pixels,out var style))
            {
                style=new GUIStyle(GUI.skin.label){fontSize=pixels,wordWrap=true,padding=new RectOffset(0,0,0,0),richText=false};
                labels.Add(pixels,style);
            }
            return style;
        }
        private void Label(Rect rect,string text,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var style=Style(size);style.normal.textColor=color;style.alignment=alignment;GUI.Label(rect,text,style);
        }
        private static void Fill(Rect rect,Color color)
        {
            Color previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;
        }
        private static void Border(Rect rect,Color color)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,1),color);Fill(new Rect(rect.x,rect.yMax-1,rect.width,1),color);
            Fill(new Rect(rect.x,rect.y,1,rect.height),color);Fill(new Rect(rect.xMax-1,rect.y,1,rect.height),color);
        }
        private void OnDestroy(){healthBars?.Dispose();if(minimap!=null)Destroy(minimap);}
    }
}
