using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Editor
{
    // Isolated economy/build-order fixture uses unchanged shipped content and public commands.
    // Enemy AI is intentionally disabled here; HeadlessSmoke separately checks live AI.
    public static class TechnologySmoke
    {
        public static void Run()
        {
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/Evidence/technology-smoke.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var evidence=new Evidence { Status="running", StartedUtc=DateTime.UtcNow.ToString("O") };
            Action write=()=>File.WriteAllText(path,JsonUtility.ToJson(evidence,true),new UTF8Encoding(false));
            write();
            try
            {
                var rules=JsonUtility.FromJson<RuleSet>(Resources.Load<TextAsset>("rts-rules").text);
                var match=new Match(rules,false,731);
                evidence.RuleVersion=rules.VersionId;
                Match restored=null;
                bool futureMarineQueued=false;
                var timer=Stopwatch.StartNew();
                var current=match.Observe(0);
                for(int i=0;i<40000;i++)
                {
                    Require(timer.Elapsed.TotalSeconds<90,"Technology build order exceeded 90 seconds.");
                    Require(current.Phase==MatchPhase.Running,"Fixture ended before technology completion.");
                    if(match.Tick%20==0) { Drive(match); if(restored!=null) Drive(restored); }
                    match.Step(); if(restored!=null) restored.Step();
                    current=match.Observe(0);
                    evidence.CompletedTicks=match.Tick;
                    var rejected=match.LastResults.FirstOrDefault(r=>!r.Accepted);
                    Require(rejected==null,"Build-order command rejected at "+match.Tick+": "+rejected?.Reason);
                    if(restored==null&&current.Entities.Any(e=>e.Research.Count>0)&&current.CompletedUpgrades.Count>0)
                    {
                        restored=Match.Restore(rules,JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(match.CaptureState())));
                        evidence.SnapshotTick=match.Tick;
                        evidence.Checks.Add("json-snapshot-with-completed-and-inflight-research");
                    }
                    if(restored!=null&&match.Tick%200==0)
                        Require(match.StateHash()==restored.StateHash(),"Technology continuation diverged at "+match.Tick);
                    if(match.Tick%1000==0)
                    {
                        evidence.LastEconomy="M="+current.Minerals+", G="+current.Gas;
                        evidence.LastBuildings=current.Entities.Where(e=>e.Owner==0&&match.Definition(e.DefinitionId).IsBuilding).Select(e=>e.DefinitionId+(e.Completed?"":"(building)")).ToArray();
                        evidence.CompletedUpgrades=current.CompletedUpgrades.ToArray();
                        write();
                    }
                    if(current.CompletedUpgrades.Count==rules.Upgrades.Length)
                    {
                        if(futureMarineQueued)
                        {
                            if(current.Entities.Count(e=>e.Owner==0&&e.DefinitionId=="marine")==3) break;
                            continue;
                        }
                        var marine=match.Definition("marine");
                        if(current.Minerals<marine.MineralCost||current.Gas<marine.GasCost) continue;
                        var barracks=match.Observe(0).Entities.First(e=>e.Owner==0&&e.DefinitionId=="barracks"&&e.Completed);
                        match.QueueCommand(match.MakeCommand(0,CommandKind.Train,new[]{barracks.Id},definitionId:"marine"));
                        restored.QueueCommand(restored.MakeCommand(0,CommandKind.Train,new[]{barracks.Id},definitionId:"marine"));
                        futureMarineQueued=true;
                    }
                }
                var observation=match.Observe(0);
                evidence.CompletedUpgrades=observation.CompletedUpgrades.ToArray();
                Require(evidence.CompletedUpgrades.Length==6,"All six shipped upgrades must complete within 40000 ticks.");
                Require(observation.Entities.Count(e=>e.Owner==0&&e.DefinitionId=="marine")==3,"New marine production after research failed.");
                Require(match.CombatDamage(0,"marine")==9&&match.CombatArmor(0,"marine")==3,"Owner-wide combat bonuses do not match three levels.");
                Require(match.CombatDamage(1,"marine")==6&&match.CombatArmor(1,"marine")==0,"Opponent inherited own upgrades.");
                Require(restored!=null&&match.StateHash()==restored.StateHash(),"Final technology JSON continuation diverged.");
                evidence.FinalHash=match.StateHash();
                var replay=Match.FromReplay(rules,JsonUtility.FromJson<ReplayData>(JsonUtility.ToJson(match.ExportReplay())));
                evidence.ReplayHash=replay.StateHash();
                Require(evidence.FinalHash==evidence.ReplayHash,"Technology accepted-command replay diverged.");
                evidence.CompletedTicks=match.Tick;
                evidence.AcceptedCommands=match.ExportReplay().Commands.Count;
                evidence.Checks.AddRange(new[]{"unchanged-runtime-catalog-real-mineral-and-gas-income","worker-build-refinery-depot-barracks-engineeringbay-armory","all-six-researches-and-new-unit-bonuses","restored-timeline-and-accepted-command-replay"});
                evidence.Status="passed";
            }
            catch(Exception exception) { evidence.Status="failed"; evidence.Error=exception.ToString(); throw; }
            finally { evidence.FinishedUtc=DateTime.UtcNow.ToString("O"); write(); }
        }

        static void Drive(Match match)
        {
            var o=match.Observe(0);
            var own=o.Entities.Where(e=>e.Owner==0).ToArray();
            var workers=own.Where(e=>e.DefinitionId=="worker").OrderBy(e=>e.Id).ToArray();
            var home=own.First(e=>e.DefinitionId=="command");
            var refinery=own.FirstOrDefault(e=>e.DefinitionId=="refinery"&&e.Completed);
            // Keep three workers on gas and recover idle builders back to mineral income.
            for(int i=0;i<workers.Length;i++)
            {
                var worker=workers[i];
                if(worker.Orders.Any(q=>q.Kind==OrderKind.Build)) continue;
                var existing=o.Entities.FirstOrDefault(e=>e.Id==worker.GatherTargetId&&e.DefinitionId=="mineral"&&e.ResourceRemaining>0);
                var resource=i<3&&refinery!=null?refinery:existing??o.Entities.Where(e=>e.DefinitionId=="mineral"&&e.ResourceRemaining>0).OrderBy(e=>Dist(e.Position,worker.Position)).FirstOrDefault();
                if(resource!=null&&(worker.Orders.Count==0||worker.GatherTargetId!=resource.Id))
                    match.QueueCommand(match.MakeCommand(0,CommandKind.Gather,new[]{worker.Id},targetId:resource.Id));
            }
            foreach(string id in new[]{"refinery","depot","barracks","engineeringbay","armory"})
            {
                if(own.Any(e=>e.DefinitionId==id)) continue;
                var definition=match.Definition(id);
                if(o.Minerals<definition.MineralCost||o.Gas<definition.GasCost||
                    !string.IsNullOrEmpty(definition.PrerequisiteId)&&!own.Any(e=>e.DefinitionId==definition.PrerequisiteId&&e.Completed)) return;
                var builder=workers.LastOrDefault(e=>!e.Orders.Any(q=>q.Kind==OrderKind.Build));
                if(builder==null) return;
                SimPoint? place=null;
                if(id=="refinery") place=o.Entities.Where(e=>e.DefinitionId=="geyser").Select(e=>(SimPoint?)e.Position).FirstOrDefault(p=>match.CanPlace(id,p.Value,0));
                else for(int radius=6000;radius<=14000&&!place.HasValue;radius+=2000)
                    for(int n=0;n<24;n++)
                    {
                        double angle=n*Math.PI/12;
                        var p=new SimPoint(home.Position.X+(int)(Math.Cos(angle)*radius),home.Position.Z+(int)(Math.Sin(angle)*radius));
                        if(match.CanPlace(id,p,0)) { place=p; break; }
                    }
                if(place.HasValue) match.QueueCommand(match.MakeCommand(0,CommandKind.Build,new[]{builder.Id},place.Value,definitionId:id));
                return;
            }
            foreach(var upgrade in match.Rules.Upgrades)
            {
                var station=own.FirstOrDefault(e=>match.ResearchAvailability(0,e.Id,upgrade.Id).Accepted);
                if(station==null) continue;
                match.QueueCommand(match.MakeCommand(0,CommandKind.Research,new[]{station.Id},definitionId:upgrade.Id));
                return;
            }
        }
        static long Dist(SimPoint a,SimPoint b) { long x=a.X-b.X,z=a.Z-b.Z; return x*x+z*z; }
        static void Require(bool valid,string message) { if(!valid) throw new InvalidOperationException(message); }
        [Serializable] sealed class Evidence
        {
            public string Status,StartedUtc,FinishedUtc,RuleVersion,FinalHash,ReplayHash,Error;
            public string LastEconomy;
            public int SnapshotTick,CompletedTicks,AcceptedCommands;
            public string[] CompletedUpgrades;
            public string[] LastBuildings;
            public List<string> Checks=new List<string>();
        }
    }
}
