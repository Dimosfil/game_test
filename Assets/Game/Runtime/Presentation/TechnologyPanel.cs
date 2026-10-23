using System;
using System.Collections.Generic;
using System.Linq;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Presentation
{
    // Presentation consumes the content graph and Match's authoritative research gate.
    public sealed class TechnologyPanel
    {
        Vector2 scroll;
        readonly Dictionary<string, Rect> nodes = new Dictionary<string, Rect>(StringComparer.Ordinal);
        readonly Color ready = new Color(.25f,.88f,1);
        readonly Color muted = new Color(.52f,.65f,.72f);
        GUIStyle title, text, small, centered;
        float styleScale = -1;

        public void Draw(GameHost host, float scale)
        {
            EnsureStyles(scale);
            float width = Mathf.Min(Screen.width - 32, 1260 * scale);
            var panel = new Rect((Screen.width-width)/2, 112*scale, width, Screen.height-136*scale);
            GUI.DrawTexture(new Rect(0,62,Screen.width,Screen.height-62), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.015f,.03f,.05f,.93f),0,0);
            HudIconAtlas.Draw(new Rect(panel.x,panel.y,32*scale,32*scale),HudIcon.Technology);
            GUI.Label(new Rect(panel.x+43*scale, panel.y, width-143*scale,34*scale), RuntimeText.Get("tech.title"), title);
            if (HudIconAtlas.Button(new Rect(panel.xMax-100*scale,panel.y,100*scale,30*scale),HudIcon.Cancel,RuntimeText.Get("tech.close"),scale)) host.Interaction.ToggleTechnology();
            GUI.Label(new Rect(panel.x,panel.y+37*scale,width,25*scale),RuntimeText.Get("tech.hint"),text);
            var viewport = new Rect(panel.x,panel.y+70*scale,width,panel.height-70*scale);
            var content = new Rect(0,0,width-22*scale,710*scale);
            scroll = GUI.BeginScrollView(viewport,scroll,content);
            var definitions = host.Rules.Definitions.Where(d=>!d.IsResource).ToArray();
            nodes.Clear();
            var columns = new Dictionary<int,int>();
            int maxDepth = definitions.Max(d=>Depth(d,host,0));
            float cellWidth = (content.width-20*scale)/(maxDepth+1), nodeWidth=Mathf.Min(175*scale,cellWidth-15*scale);
            foreach(var definition in definitions)
            {
                int depth=Depth(definition,host,0);
                columns.TryGetValue(depth,out int row);
                columns[depth]=row+1;
                nodes[definition.Id]=new Rect(10*scale+depth*cellWidth,10*scale+row*90*scale,nodeWidth,70*scale);
            }
            foreach(var definition in definitions)
            {
                string parent=Parent(definition);
                if(string.IsNullOrEmpty(parent)||!nodes.TryGetValue(parent,out var from)) continue;
                var to=nodes[definition.Id];
                Line(new Vector2(from.xMax,from.center.y),new Vector2(to.x,to.center.y),new Color(.22f,.39f,.46f));
            }
            foreach(var definition in definitions)
            {
                var r=nodes[definition.Id];
                bool owned=host.Observation.Entities.Any(e=>e.Owner==0&&e.Completed&&e.DefinitionId==definition.Id);
                GUI.color=owned?new Color(.12f,.35f,.4f):new Color(.07f,.11f,.15f);
                GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=Color.white;
                HudIconAtlas.Border(r,owned?new Color(.24f,.60f,.66f):new Color(.16f,.28f,.34f));
                GUI.color=owned?Color.white:new Color(.60f,.69f,.72f);
                HudIconAtlas.Draw(new Rect(r.x+5*scale,r.y+5*scale,37*scale,37*scale),HudIconAtlas.ForDefinition(definition.Id));
                GUI.color=Color.white;
                GUI.Label(new Rect(r.x+48*scale,r.y+4*scale,r.width-53*scale,39*scale),definition.DisplayName,small);
                var tint=owned?ready:muted;
                GUI.color=tint;
                GUI.Label(new Rect(r.x+4*scale,r.y+44*scale,r.width-8*scale,20*scale),owned?RuntimeText.Get("tech.ready"):RuntimeText.Cost(definition.MineralCost,definition.GasCost),centered);
                GUI.color=Color.white;
            }
            float researchY=Mathf.Max(220*scale,columns.Values.Max()*90*scale+20*scale);
            GUI.Label(new Rect(10*scale,researchY,content.width,28*scale),RuntimeText.Get("tech.upgrades"),text);
            var upgrades=host.Rules.Upgrades??new UpgradeDefinition[0];
            float half=(content.width-30*scale)/2;
            for(int i=0;i<upgrades.Length;i++)
            {
                var upgrade=upgrades[i];
                var r=new Rect(10*scale+(i%2)*(half+10*scale),researchY+36*scale+(i/2)*108*scale,half,99*scale);
                GUI.color=new Color(.06f,.1f,.14f); GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=Color.white;
                bool done=host.Observation.CompletedUpgrades.Contains(upgrade.Id);
                var producers=host.Observation.Entities.Where(e=>e.Owner==0&&e.DefinitionId==upgrade.ProducerId).ToArray();
                var busy=producers.FirstOrDefault(e=>e.Research.Any(q=>q.DefinitionId==upgrade.Id));
                var producer=producers.FirstOrDefault(e=>host.ResearchAvailability(e.Id,upgrade.Id).Accepted)??producers.FirstOrDefault();
                var gate=host.ResearchAvailability(producer?.Id??0,upgrade.Id);
                HudIconAtlas.Border(r,done||gate.Accepted?new Color(.22f,.49f,.55f):new Color(.14f,.25f,.31f));
                HudIconAtlas.Draw(new Rect(r.x+10*scale,r.y+7*scale,43*scale,43*scale),HudIconAtlas.ForUpgrade(upgrade));
                GUI.Label(new Rect(r.x+63*scale,r.y+5*scale,r.width-208*scale,28*scale),RuntimeText.Format(upgrade.DamageBonus>0?"tech.damage":"tech.armor",upgrade.DisplayName,upgrade.DamageBonus>0?upgrade.DamageBonus:upgrade.ArmorBonus),text);
                string requirement=host.Definition(upgrade.ProducerId).DisplayName;
                if(!string.IsNullOrEmpty(upgrade.PrerequisiteId)) requirement+=" → "+host.Definition(upgrade.PrerequisiteId).DisplayName;
                if(!string.IsNullOrEmpty(upgrade.PrerequisiteUpgradeId)) requirement+=" + "+host.Upgrade(upgrade.PrerequisiteUpgradeId).DisplayName;
                GUI.Label(new Rect(r.x+63*scale,r.y+35*scale,r.width-73*scale,29*scale),requirement,small);
                string status=done?RuntimeText.Get("tech.done"):busy!=null?RuntimeText.Format("tech.progress",Mathf.RoundToInt(100*(1-busy.Research[0].RemainingTicks/(float)busy.Research[0].TotalTicks))):gate.Accepted?RuntimeText.Get("tech.available"):RuntimeText.ResearchReason(gate.Reason,upgrade,id=>host.Definition(id).DisplayName);
                GUI.color=done||gate.Accepted?ready:muted;
                GUI.Label(new Rect(r.x+10*scale,r.y+67*scale,r.width-20*scale,26*scale),status,small); GUI.color=Color.white;
                GUI.enabled=host.Mode==FrontendMode.Playing&&!host.IsReplay&&gate.Accepted;
                if(HudIconAtlas.Button(new Rect(r.xMax-144*scale,r.y+4*scale,134*scale,27*scale),HudIconAtlas.ForUpgrade(upgrade),RuntimeText.Cost(upgrade.MineralCost,upgrade.GasCost)+" / "+RuntimeText.Format("duration.seconds",upgrade.ResearchTicks/host.Rules.TickRate),scale,upgrade.DisplayName))
                    host.Interaction.Research(producer.Id,upgrade.Id);
                GUI.enabled=true;
            }
            GUI.EndScrollView();
        }

        void EnsureStyles(float scale)
        {
            if (styleScale == scale) return;
            styleScale = scale;
            title = new GUIStyle(GUI.skin.label) { fontSize=Mathf.RoundToInt(22*scale), fontStyle=FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { fontSize=Mathf.RoundToInt(12*scale), wordWrap=true };
            small = new GUIStyle(text) { fontSize=Mathf.RoundToInt(11*scale), alignment=TextAnchor.MiddleLeft };
            centered = new GUIStyle(text) { alignment=TextAnchor.MiddleCenter };
        }

        static string Parent(EntityDefinition d)=>!string.IsNullOrEmpty(d.PrerequisiteId)?d.PrerequisiteId:d.ProducerId;
        static int Depth(EntityDefinition d,GameHost host,int guard)
        {
            string parent=Parent(d);
            return string.IsNullOrEmpty(parent)||guard>host.Rules.Definitions.Length?0:1+Depth(host.Definition(parent),host,guard+1);
        }
        static void Line(Vector2 a,Vector2 b,Color color)
        {
            var matrix=GUI.matrix;
            GUI.color=color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);
            GUI.DrawTexture(new Rect(a.x,a.y,Vector2.Distance(a,b),2),Texture2D.whiteTexture);
            GUI.matrix=matrix; GUI.color=Color.white;
        }
    }
}
